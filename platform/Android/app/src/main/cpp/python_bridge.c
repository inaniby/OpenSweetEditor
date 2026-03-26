#include <jni.h>
#include <dlfcn.h>
#include <errno.h>
#include <pthread.h>
#include <signal.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

typedef int py_gil_state_t;

typedef struct {
    void *handle;
    bool initialized;

    void (*Py_InitializeEx)(int);
    int (*Py_FinalizeEx)(void);
    int (*PyRun_SimpleStringFlags)(const char *, void *);

    void *(*PyImport_AddModule)(const char *);
    void *(*PyModule_GetDict)(void *);
    void *(*PyDict_GetItemString)(void *, const char *);
    long (*PyLong_AsLong)(void *);

    void *(*PyErr_Occurred)(void);
    void (*PyErr_Print)(void);
    void (*PyErr_Clear)(void);
    void (*PyErr_SetInterrupt)(void);
    void (*PyErr_SetInterruptEx)(int);

    py_gil_state_t (*PyGILState_Ensure)(void);
    void (*PyGILState_Release)(py_gil_state_t);
    int (*Py_IsInitialized)(void);
    void *(*PyEval_SaveThread)(void);
    void (*PyEval_RestoreThread)(void *);

    void *main_thread_state;
} PyApi;

typedef struct {
    char *data;
    size_t len;
    size_t cap;
} StringBuilder;

static PyApi g_api = {0};
static pthread_mutex_t g_runtime_mutex = PTHREAD_MUTEX_INITIALIZER;
static pthread_mutex_t g_run_mutex = PTHREAD_MUTEX_INITIALIZER;

static const char *BOOTSTRAP_SCRIPT =
"import contextlib\n"
"import io\n"
"import os\n"
"import runpy\n"
"import sys\n"
"import traceback\n"
"\n"
"_PYDROID_STDOUT_PATH = os.environ.get('PYDROID_STDOUT_PATH')\n"
"_PYDROID_STDERR_PATH = os.environ.get('PYDROID_STDERR_PATH')\n"
"\n"
"def _pydroid_write(path, text):\n"
"    if not path or not text:\n"
"        return\n"
"    with open(path, 'a', encoding='utf-8', errors='replace') as fp:\n"
"        fp.write(text)\n"
"\n"
"def _pydroid_normalize_exit(exc):\n"
"    code = exc.code\n"
"    if code is None:\n"
"        return 0\n"
"    if isinstance(code, int):\n"
"        return code\n"
"    print(code, file=sys.stderr)\n"
"    return 1\n"
"\n"
"def _pydroid_capture(callable_obj, *args):\n"
"    out_buf = io.StringIO()\n"
"    err_buf = io.StringIO()\n"
"    try:\n"
"        with contextlib.redirect_stdout(out_buf), contextlib.redirect_stderr(err_buf):\n"
"            return callable_obj(*args)\n"
"    finally:\n"
"        _pydroid_write(_PYDROID_STDOUT_PATH, out_buf.getvalue())\n"
"        _pydroid_write(_PYDROID_STDERR_PATH, err_buf.getvalue())\n"
"\n"
"def _pydroid_run_file(path, argv_tail):\n"
"    old_argv = sys.argv[:]\n"
"    try:\n"
"        sys.argv = [path] + list(argv_tail)\n"
"        glb = {'__name__': '__main__', '__file__': path, '__package__': None}\n"
"        with open(path, 'rb') as fp:\n"
"            source = fp.read()\n"
"        exec(compile(source, path, 'exec'), glb, glb)\n"
"        return 0\n"
"    except SystemExit as exc:\n"
"        return _pydroid_normalize_exit(exc)\n"
"    except BaseException:\n"
"        traceback.print_exc()\n"
"        return 1\n"
"    finally:\n"
"        sys.argv = old_argv\n"
"\n"
"def _pydroid_run_module(module, argv_tail):\n"
"    old_argv = sys.argv[:]\n"
"    try:\n"
"        sys.argv = [module] + list(argv_tail)\n"
"        runpy.run_module(module, run_name='__main__', alter_sys=True)\n"
"        return 0\n"
"    except SystemExit as exc:\n"
"        return _pydroid_normalize_exit(exc)\n"
"    except BaseException:\n"
"        traceback.print_exc()\n"
"        return 1\n"
"    finally:\n"
"        sys.argv = old_argv\n"
"\n"
"_pydroid_repl_ns = {\"__name__\": \"__main__\", \"__package__\": None}\n"
"\n"
"def _pydroid_run_repl(snippet, _unused_argv):\n"
"    try:\n"
"        src = \"\" if snippet is None else str(snippet)\n"
"        try:\n"
"            code = compile(src, \"<stdin>\", \"eval\")\n"
"        except SyntaxError:\n"
"            code = compile(src, \"<stdin>\", \"exec\")\n"
"            exec(code, _pydroid_repl_ns, _pydroid_repl_ns)\n"
"        else:\n"
"            value = eval(code, _pydroid_repl_ns, _pydroid_repl_ns)\n"
"            if value is not None:\n"
"                print(repr(value))\n"
"        return 0\n"
"    except SystemExit as exc:\n"
"        return _pydroid_normalize_exit(exc)\n"
"    except BaseException:\n"
"        traceback.print_exc()\n"
"        return 1\n";

static void throw_runtime_exception(JNIEnv *env, const char *message) {
    jclass ex = (*env)->FindClass(env, "java/lang/RuntimeException");
    if (ex != NULL) {
        (*env)->ThrowNew(env, ex, message);
    }
}

static char *dup_jstring(JNIEnv *env, jstring value) {
    if (value == NULL) return NULL;
    const char *utf = (*env)->GetStringUTFChars(env, value, NULL);
    if (utf == NULL) return NULL;
    char *copy = strdup(utf);
    (*env)->ReleaseStringUTFChars(env, value, utf);
    return copy;
}

static int set_env(JNIEnv *env, const char *key, const char *value) {
    if (key == NULL || value == NULL) return 0;
    if (setenv(key, value, 1) != 0) {
        char buf[256];
        snprintf(buf, sizeof(buf), "setenv(%s) failed: %s", key, strerror(errno));
        throw_runtime_exception(env, buf);
        return -1;
    }
    return 0;
}

static int resolve_symbol_required(JNIEnv *env, void **out, const char *name) {
    *out = dlsym(g_api.handle, name);
    if (*out != NULL) {
        return 0;
    }

    const char *error = dlerror();
    char buf[320];
    snprintf(buf, sizeof(buf), "Failed to resolve %s: %s", name, error ? error : "unknown error");
    throw_runtime_exception(env, buf);
    return -1;
}

static void *resolve_symbol_optional(const char *name) {
    dlerror();
    void *symbol = dlsym(g_api.handle, name);
    (void)dlerror();
    return symbol;
}

static void reset_api(void) {
    memset(&g_api, 0, sizeof(g_api));
}

static int load_python_api(JNIEnv *env) {
    if (resolve_symbol_required(env, (void **)&g_api.Py_InitializeEx, "Py_InitializeEx") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.Py_FinalizeEx, "Py_FinalizeEx") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyRun_SimpleStringFlags, "PyRun_SimpleStringFlags") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyImport_AddModule, "PyImport_AddModule") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyModule_GetDict, "PyModule_GetDict") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyDict_GetItemString, "PyDict_GetItemString") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyLong_AsLong, "PyLong_AsLong") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyErr_Occurred, "PyErr_Occurred") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyErr_Print, "PyErr_Print") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyErr_Clear, "PyErr_Clear") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyGILState_Ensure, "PyGILState_Ensure") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyGILState_Release, "PyGILState_Release") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.Py_IsInitialized, "Py_IsInitialized") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyEval_SaveThread, "PyEval_SaveThread") != 0) return -1;
    if (resolve_symbol_required(env, (void **)&g_api.PyEval_RestoreThread, "PyEval_RestoreThread") != 0) return -1;

    g_api.PyErr_SetInterruptEx = (void (*)(int))resolve_symbol_optional("PyErr_SetInterruptEx");
    g_api.PyErr_SetInterrupt = (void (*)(void))resolve_symbol_optional("PyErr_SetInterrupt");
    return 0;
}

static void print_and_clear_python_error(void) {
    if (g_api.PyErr_Occurred != NULL && g_api.PyErr_Occurred() != NULL) {
        g_api.PyErr_Print();
        g_api.PyErr_Clear();
    }
}

static int run_python_script(JNIEnv *env, const char *script) {
    int rc = g_api.PyRun_SimpleStringFlags(script, NULL);
    if (rc == 0) {
        return 0;
    }

    print_and_clear_python_error();
    throw_runtime_exception(env, "Python script execution failed");
    return -1;
}

static int sb_init(StringBuilder *sb, size_t initial_cap) {
    sb->len = 0;
    sb->cap = initial_cap;
    sb->data = (char *)malloc(initial_cap);
    if (sb->data == NULL) {
        return -1;
    }
    sb->data[0] = '\0';
    return 0;
}

static void sb_free(StringBuilder *sb) {
    free(sb->data);
    sb->data = NULL;
    sb->len = 0;
    sb->cap = 0;
}

static int sb_reserve(StringBuilder *sb, size_t extra) {
    size_t need = sb->len + extra + 1;
    if (need <= sb->cap) {
        return 0;
    }

    size_t new_cap = sb->cap;
    while (new_cap < need) {
        new_cap *= 2;
    }

    char *new_data = (char *)realloc(sb->data, new_cap);
    if (new_data == NULL) {
        return -1;
    }

    sb->data = new_data;
    sb->cap = new_cap;
    return 0;
}

static int sb_append_raw(StringBuilder *sb, const char *text) {
    size_t text_len = strlen(text);
    if (sb_reserve(sb, text_len) != 0) {
        return -1;
    }

    memcpy(sb->data + sb->len, text, text_len);
    sb->len += text_len;
    sb->data[sb->len] = '\0';
    return 0;
}

static int sb_append_char(StringBuilder *sb, char ch) {
    if (sb_reserve(sb, 1) != 0) {
        return -1;
    }

    sb->data[sb->len++] = ch;
    sb->data[sb->len] = '\0';
    return 0;
}

static int sb_append_py_quoted(StringBuilder *sb, const char *text) {
    if (sb_append_char(sb, '\'') != 0) return -1;

    for (const unsigned char *p = (const unsigned char *)text; *p != '\0'; p++) {
        unsigned char ch = *p;
        if (ch == '\\' || ch == '\'') {
            if (sb_append_char(sb, '\\') != 0) return -1;
            if (sb_append_char(sb, (char)ch) != 0) return -1;
            continue;
        }
        if (ch == '\n') {
            if (sb_append_raw(sb, "\\n") != 0) return -1;
            continue;
        }
        if (ch == '\r') {
            if (sb_append_raw(sb, "\\r") != 0) return -1;
            continue;
        }
        if (ch == '\t') {
            if (sb_append_raw(sb, "\\t") != 0) return -1;
            continue;
        }
        if (ch < 0x20) {
            char tmp[5];
            snprintf(tmp, sizeof(tmp), "\\x%02x", ch);
            if (sb_append_raw(sb, tmp) != 0) return -1;
            continue;
        }
        if (sb_append_char(sb, (char)ch) != 0) return -1;
    }

    if (sb_append_char(sb, '\'') != 0) return -1;
    return 0;
}

static int sb_append_argv_list(JNIEnv *env, StringBuilder *sb, jobjectArray jArgs) {
    if (sb_append_char(sb, '[') != 0) return -1;

    if (jArgs == NULL) {
        if (sb_append_char(sb, ']') != 0) return -1;
        return 0;
    }

    jsize argc = (*env)->GetArrayLength(env, jArgs);
    for (jsize i = 0; i < argc; i++) {
        if (i > 0 && sb_append_raw(sb, ", ") != 0) return -1;

        jstring jArg = (jstring)(*env)->GetObjectArrayElement(env, jArgs, i);
        char *arg = dup_jstring(env, jArg);
        (*env)->DeleteLocalRef(env, jArg);

        if (arg == NULL) {
            throw_runtime_exception(env, "Argument conversion failed");
            return -1;
        }

        int rc = sb_append_py_quoted(sb, arg);
        free(arg);
        if (rc != 0) {
            throw_runtime_exception(env, "Out of memory while building Python command");
            return -1;
        }
    }

    if (sb_append_char(sb, ']') != 0) return -1;
    return 0;
}

static int fetch_status_from_main(JNIEnv *env, int *out_status) {
    void *main_module = g_api.PyImport_AddModule("__main__");
    if (main_module == NULL) {
        print_and_clear_python_error();
        throw_runtime_exception(env, "Failed to access __main__ module");
        return -1;
    }

    void *dict = g_api.PyModule_GetDict(main_module);
    if (dict == NULL) {
        print_and_clear_python_error();
        throw_runtime_exception(env, "Failed to access __main__.__dict__");
        return -1;
    }

    void *status_obj = g_api.PyDict_GetItemString(dict, "_pydroid_status");
    if (status_obj == NULL) {
        print_and_clear_python_error();
        throw_runtime_exception(env, "_pydroid_status not found");
        return -1;
    }

    long status = g_api.PyLong_AsLong(status_obj);
    if (g_api.PyErr_Occurred() != NULL) {
        print_and_clear_python_error();
        throw_runtime_exception(env, "Failed to parse _pydroid_status");
        return -1;
    }

    *out_status = (int)status;
    return 0;
}

static int run_helper(
    JNIEnv *env,
    const char *helper_name,
    const char *first_arg,
    jobjectArray jArgs,
    int *out_status
) {
    StringBuilder sb;
    if (sb_init(&sb, 512) != 0) {
        throw_runtime_exception(env, "Out of memory while preparing Python command");
        return -1;
    }

    int failed = 0;
    if (sb_append_raw(&sb, "_pydroid_status = _pydroid_capture(") != 0) failed = 1;
    if (!failed && sb_append_raw(&sb, helper_name) != 0) failed = 1;
    if (!failed && sb_append_raw(&sb, ", ") != 0) failed = 1;
    if (!failed && sb_append_py_quoted(&sb, first_arg) != 0) failed = 1;
    if (!failed && sb_append_raw(&sb, ", ") != 0) failed = 1;
    if (!failed && sb_append_argv_list(env, &sb, jArgs) != 0) failed = 1;
    if (!failed && sb_append_raw(&sb, ")\n") != 0) failed = 1;

    if (failed) {
        sb_free(&sb);
        if (!(*env)->ExceptionCheck(env)) {
            throw_runtime_exception(env, "Out of memory while preparing Python command");
        }
        return -1;
    }

    if (run_python_script(env, sb.data) != 0) {
        sb_free(&sb);
        return -1;
    }
    sb_free(&sb);

    if (fetch_status_from_main(env, out_status) != 0) {
        return -1;
    }
    return 0;
}

static int set_extra_env(JNIEnv *env, jobjectArray keys, jobjectArray values) {
    if (keys == NULL && values == NULL) {
        return 0;
    }
    if (keys == NULL || values == NULL) {
        throw_runtime_exception(env, "extraEnvKeys and extraEnvValues must both be non-null");
        return -1;
    }

    jsize key_count = (*env)->GetArrayLength(env, keys);
    jsize value_count = (*env)->GetArrayLength(env, values);
    if (key_count != value_count) {
        throw_runtime_exception(env, "extraEnvKeys and extraEnvValues size mismatch");
        return -1;
    }

    for (jsize i = 0; i < key_count; i++) {
        jstring jKey = (jstring)(*env)->GetObjectArrayElement(env, keys, i);
        jstring jValue = (jstring)(*env)->GetObjectArrayElement(env, values, i);

        char *key = dup_jstring(env, jKey);
        char *value = dup_jstring(env, jValue);

        (*env)->DeleteLocalRef(env, jKey);
        (*env)->DeleteLocalRef(env, jValue);

        if (key == NULL || value == NULL) {
            free(key);
            free(value);
            throw_runtime_exception(env, "Environment conversion failed");
            return -1;
        }

        int rc = set_env(env, key, value);
        free(key);
        free(value);
        if (rc != 0) {
            return -1;
        }
    }

    return 0;
}

static void stop_runtime_internal(void) {
    if (g_api.Py_IsInitialized != NULL && g_api.Py_IsInitialized()) {
        if (g_api.main_thread_state != NULL && g_api.PyEval_RestoreThread != NULL) {
            g_api.PyEval_RestoreThread(g_api.main_thread_state);
            g_api.main_thread_state = NULL;
        }
        g_api.Py_FinalizeEx();
    }
    if (g_api.handle != NULL) {
        dlclose(g_api.handle);
    }
    reset_api();
}

JNIEXPORT jint JNICALL
Java_com_nanib_pydroid_python_PythonBridge_startRuntime(
    JNIEnv *env,
    jclass clazz,
    jstring jLibPythonPath,
    jstring jPythonHome,
    jstring jWorkingDir,
    jstring jPythonPath,
    jobjectArray jExtraEnvKeys,
    jobjectArray jExtraEnvValues
) {
    (void)clazz;

    pthread_mutex_lock(&g_runtime_mutex);

    if (g_api.initialized) {
        pthread_mutex_unlock(&g_runtime_mutex);
        return 0;
    }

    char *lib_python_path = dup_jstring(env, jLibPythonPath);
    char *python_home = dup_jstring(env, jPythonHome);
    char *working_dir = dup_jstring(env, jWorkingDir);
    char *python_path = dup_jstring(env, jPythonPath);

    if (lib_python_path == NULL || python_home == NULL || working_dir == NULL) {
        throw_runtime_exception(env, "Argument conversion failed");
        free(lib_python_path);
        free(python_home);
        free(working_dir);
        free(python_path);
        pthread_mutex_unlock(&g_runtime_mutex);
        return 1;
    }

    if (set_env(env, "PYTHONHOME", python_home) != 0) goto fail;
    if (python_path != NULL && python_path[0] != '\0') {
        if (set_env(env, "PYTHONPATH", python_path) != 0) goto fail;
    }
    if (set_extra_env(env, jExtraEnvKeys, jExtraEnvValues) != 0) goto fail;

    if (chdir(working_dir) != 0) {
        throw_runtime_exception(env, "Failed to chdir to working directory");
        goto fail;
    }

    g_api.handle = dlopen(lib_python_path, RTLD_NOW | RTLD_GLOBAL);
    if (g_api.handle == NULL) {
        throw_runtime_exception(env, dlerror());
        goto fail;
    }

    if (load_python_api(env) != 0) goto fail;

    g_api.Py_InitializeEx(0);
    if (!g_api.Py_IsInitialized()) {
        throw_runtime_exception(env, "Python did not initialize");
        goto fail;
    }

    {
        py_gil_state_t gil = g_api.PyGILState_Ensure();
        int bootstrap_status = run_python_script(env, BOOTSTRAP_SCRIPT);
        g_api.PyGILState_Release(gil);
        if (bootstrap_status != 0) goto fail;

        g_api.main_thread_state = g_api.PyEval_SaveThread();
        if (g_api.main_thread_state == NULL) {
            throw_runtime_exception(env, "PyEval_SaveThread failed");
            goto fail;
        }
    }

    g_api.initialized = true;
    free(lib_python_path);
    free(python_home);
    free(working_dir);
    free(python_path);
    pthread_mutex_unlock(&g_runtime_mutex);
    return 0;

fail:
    free(lib_python_path);
    free(python_home);
    free(working_dir);
    free(python_path);
    stop_runtime_internal();
    pthread_mutex_unlock(&g_runtime_mutex);
    return 1;
}

JNIEXPORT jint JNICALL
Java_com_nanib_pydroid_python_PythonBridge_stopRuntime(
    JNIEnv *env,
    jclass clazz
) {
    (void)env;
    (void)clazz;

    pthread_mutex_lock(&g_runtime_mutex);
    pthread_mutex_lock(&g_run_mutex);
    stop_runtime_internal();
    pthread_mutex_unlock(&g_run_mutex);
    pthread_mutex_unlock(&g_runtime_mutex);
    return 0;
}

JNIEXPORT jboolean JNICALL
Java_com_nanib_pydroid_python_PythonBridge_isRuntimeStarted(
    JNIEnv *env,
    jclass clazz
) {
    (void)env;
    (void)clazz;

    pthread_mutex_lock(&g_runtime_mutex);
    bool started = g_api.initialized;
    pthread_mutex_unlock(&g_runtime_mutex);
    return started ? JNI_TRUE : JNI_FALSE;
}

JNIEXPORT jint JNICALL
Java_com_nanib_pydroid_python_PythonBridge_requestInterrupt(
    JNIEnv *env,
    jclass clazz
) {
    (void)clazz;

    pthread_mutex_lock(&g_runtime_mutex);

    if (!g_api.initialized) {
        pthread_mutex_unlock(&g_runtime_mutex);
        return 0;
    }

    if (g_api.PyErr_SetInterruptEx != NULL) {
        g_api.PyErr_SetInterruptEx(SIGINT);
    } else if (g_api.PyErr_SetInterrupt != NULL) {
        g_api.PyErr_SetInterrupt();
    } else {
        throw_runtime_exception(env, "Interrupt API not available in this Python runtime");
        pthread_mutex_unlock(&g_runtime_mutex);
        return 1;
    }

    pthread_mutex_unlock(&g_runtime_mutex);
    return 0;
}

JNIEXPORT jint JNICALL
Java_com_nanib_pydroid_python_PythonBridge_runFile(
    JNIEnv *env,
    jclass clazz,
    jstring jScriptPath,
    jobjectArray jArgs
) {
    (void)clazz;

    pthread_mutex_lock(&g_runtime_mutex);
    if (!g_api.initialized) {
        throw_runtime_exception(env, "Python runtime is not started");
        pthread_mutex_unlock(&g_runtime_mutex);
        return 1;
    }
    pthread_mutex_lock(&g_run_mutex);
    pthread_mutex_unlock(&g_runtime_mutex);

    char *script_path = dup_jstring(env, jScriptPath);
    if (script_path == NULL) {
        throw_runtime_exception(env, "Argument conversion failed");
        pthread_mutex_unlock(&g_run_mutex);
        return 1;
    }

    py_gil_state_t gil = g_api.PyGILState_Ensure();
    int status = 1;
    int rc = run_helper(env, "_pydroid_run_file", script_path, jArgs, &status);
    g_api.PyGILState_Release(gil);

    free(script_path);
    pthread_mutex_unlock(&g_run_mutex);
    return rc == 0 ? status : 1;
}

JNIEXPORT jint JNICALL
Java_com_nanib_pydroid_python_PythonBridge_runModule(
    JNIEnv *env,
    jclass clazz,
    jstring jModule,
    jobjectArray jArgs
) {
    (void)clazz;

    pthread_mutex_lock(&g_runtime_mutex);
    if (!g_api.initialized) {
        throw_runtime_exception(env, "Python runtime is not started");
        pthread_mutex_unlock(&g_runtime_mutex);
        return 1;
    }
    pthread_mutex_lock(&g_run_mutex);
    pthread_mutex_unlock(&g_runtime_mutex);

    char *module = dup_jstring(env, jModule);
    if (module == NULL) {
        throw_runtime_exception(env, "Argument conversion failed");
        pthread_mutex_unlock(&g_run_mutex);
        return 1;
    }

    py_gil_state_t gil = g_api.PyGILState_Ensure();
    int status = 1;
    int rc = run_helper(env, "_pydroid_run_module", module, jArgs, &status);
    g_api.PyGILState_Release(gil);

    free(module);
    pthread_mutex_unlock(&g_run_mutex);
    return rc == 0 ? status : 1;
}

JNIEXPORT jint JNICALL
Java_com_nanib_pydroid_python_PythonBridge_runReplSnippet(
    JNIEnv *env,
    jclass clazz,
    jstring jSnippet
) {
    (void)clazz;

    pthread_mutex_lock(&g_runtime_mutex);
    if (!g_api.initialized) {
        throw_runtime_exception(env, "Python runtime is not started");
        pthread_mutex_unlock(&g_runtime_mutex);
        return 1;
    }
    pthread_mutex_lock(&g_run_mutex);
    pthread_mutex_unlock(&g_runtime_mutex);

    char *snippet = dup_jstring(env, jSnippet);
    if (snippet == NULL) {
        throw_runtime_exception(env, "Argument conversion failed");
        pthread_mutex_unlock(&g_run_mutex);
        return 1;
    }

    py_gil_state_t gil = g_api.PyGILState_Ensure();
    int status = 1;
    int rc = run_helper(env, "_pydroid_run_repl", snippet, NULL, &status);
    g_api.PyGILState_Release(gil);

    free(snippet);
    pthread_mutex_unlock(&g_run_mutex);
    return rc == 0 ? status : 1;
}
