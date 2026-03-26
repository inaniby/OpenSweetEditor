package com.nanib.pydroid.python;

import android.content.Context;
import android.os.Build;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import java.io.Closeable;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.atomic.AtomicBoolean;

public class PythonRuntime implements Closeable {
    private final Context mContext;
    private final AtomicBoolean mBridgeLoaded = new AtomicBoolean(false);
    private final Object mLifecycleLock = new Object();

    @Nullable
    private String mRuntimeKey;

    public PythonRuntime(@NonNull Context context) {
        mContext = context.getApplicationContext();
    }

    public static final class PythonEnvironment {
        @NonNull
        public final File prefixDir;
        @NonNull
        public final String pythonVersion;
        @NonNull
        public final String abi;
        @NonNull
        public final File workingDir;
        @NonNull
        public final File userSiteDir;
        @NonNull
        public final File tmpDir;
        @NonNull
        public final File stdoutFile;
        @NonNull
        public final File stderrFile;

        PythonEnvironment(@NonNull File prefixDir,
                          @NonNull String pythonVersion,
                          @NonNull String abi,
                          @NonNull File workingDir,
                          @NonNull File userSiteDir,
                          @NonNull File tmpDir,
                          @NonNull File stdoutFile,
                          @NonNull File stderrFile) {
            this.prefixDir = prefixDir;
            this.pythonVersion = pythonVersion;
            this.abi = abi;
            this.workingDir = workingDir;
            this.userSiteDir = userSiteDir;
            this.tmpDir = tmpDir;
            this.stdoutFile = stdoutFile;
            this.stderrFile = stderrFile;
        }

        @NonNull
        public File getPrefixLibDir() {
            return new File(prefixDir, "lib");
        }

        @NonNull
        public File getStdlibDir() {
            return new File(prefixDir, "lib/python" + pythonVersion);
        }

        @NonNull
        public File findLibPython() {
            File[] files = getPrefixLibDir().listFiles();
            if (files == null || files.length == 0) {
                throw new IllegalStateException("No shared libraries found under prefix/lib");
            }

            List<File> candidates = new ArrayList<>();
            for (File file : files) {
                String name = file.getName();
                if (!file.isFile()) {
                    continue;
                }
                if (!name.startsWith("libpython3")) {
                    continue;
                }
                if (name.endsWith(".so") || name.contains(".so.")) {
                    candidates.add(file);
                }
            }

            if (candidates.isEmpty()) {
                throw new IllegalStateException("Could not find libpython3*.so");
            }

            candidates.sort(Comparator.comparing(File::getName, String.CASE_INSENSITIVE_ORDER));
            return candidates.get(0);
        }
    }

    public static final class OutputBuffers {
        @NonNull
        public final String stdout;
        @NonNull
        public final String stderr;

        OutputBuffers(@NonNull String stdout, @NonNull String stderr) {
            this.stdout = stdout;
            this.stderr = stderr;
        }
    }

    @NonNull
    public PythonEnvironment createEnvironment(@NonNull PythonPackageManager.InstallResult install) {
        boolean supported = false;
        for (String abi : Build.SUPPORTED_ABIS) {
            if (install.abi.equals(abi)) {
                supported = true;
                break;
            }
        }
        if (!supported) {
            throw new IllegalStateException(
                    "Runtime ABI " + install.abi + " is incompatible with device ABIs "
                            + Arrays.toString(Build.SUPPORTED_ABIS));
        }

        File workDir = ensureDir(new File(mContext.getFilesDir(), "python/work"));
        File userSite = ensureDir(new File(mContext.getFilesDir(), "python/site-packages"));
        File tmp = ensureDir(new File(mContext.getCacheDir(), "python-tmp"));

        File stdout = new File(tmp, "stdout.log");
        File stderr = new File(tmp, "stderr.log");
        writeText(stdout, "");
        writeText(stderr, "");

        return new PythonEnvironment(
                install.prefixDir,
                install.pythonVersion,
                install.abi,
                workDir,
                userSite,
                tmp,
                stdout,
                stderr
        );
    }

    public int runScript(@NonNull PythonEnvironment environment,
                         @NonNull File scriptFile,
                         @Nullable List<String> args) {
        ensureRuntimeStarted(environment);
        clearOutputBuffers(environment);
        String[] safeArgs = args == null ? new String[0] : args.toArray(new String[0]);
        return PythonBridge.runFile(scriptFile.getAbsolutePath(), safeArgs);
    }

    public int runCode(@NonNull PythonEnvironment environment,
                       @NonNull String code) {
        File script = new File(environment.workingDir, "inline_run.py");
        writeText(script, code);
        return runScript(environment, script, Collections.emptyList());
    }

    public int runModule(@NonNull PythonEnvironment environment,
                         @NonNull String module,
                         @Nullable List<String> args) {
        ensureRuntimeStarted(environment);
        clearOutputBuffers(environment);
        String[] safeArgs = args == null ? new String[0] : args.toArray(new String[0]);
        return PythonBridge.runModule(module, safeArgs);
    }

    public int runReplSnippet(@NonNull PythonEnvironment environment,
                              @NonNull String snippet) {
        ensureRuntimeStarted(environment);
        clearOutputBuffers(environment);
        return PythonBridge.runReplSnippet(snippet);
    }

    public int ensurePip(@NonNull PythonEnvironment environment,
                         @Nullable File getPipScript) {
        int pipStatus = runModule(environment, "pip", Collections.singletonList("--version"));
        if (pipStatus == 0) {
            return 0;
        }

        restart(environment);
        pipStatus = runModule(environment, "pip", Collections.singletonList("--version"));
        if (pipStatus == 0) {
            return 0;
        }

        if (getPipScript == null || !getPipScript.isFile()) {
            throw new IllegalStateException(
                    "This Android Python build may not include ensurepip; import get-pip.py first");
        }

        List<String> bootstrapArgs = new ArrayList<>();
        bootstrapArgs.add("--no-warn-script-location");
        bootstrapArgs.add("--target");
        bootstrapArgs.add(environment.userSiteDir.getAbsolutePath());

        int bootstrapStatus = runScript(environment, getPipScript, bootstrapArgs);
        if (bootstrapStatus != 0) {
            return bootstrapStatus;
        }

        restart(environment);
        return runModule(environment, "pip", Collections.singletonList("--version"));
    }

    public int pipInstall(@NonNull PythonEnvironment environment,
                          @NonNull String requirement,
                          @Nullable File getPipScript,
                          @Nullable String indexUrl) {
        int ensureStatus = ensurePip(environment, getPipScript);
        if (ensureStatus != 0) {
            return ensureStatus;
        }

        List<String> args = new ArrayList<>();
        args.add("install");
        args.add("--target");
        args.add(environment.userSiteDir.getAbsolutePath());
        args.add("--upgrade");
        args.add("--prefer-binary");
        args.add("--only-binary");
        args.add(":all:");
        args.add(requirement);

        if (indexUrl != null && !indexUrl.trim().isEmpty()) {
            args.add("--index-url");
            args.add(indexUrl.trim());
        }

        return runModule(environment, "pip", args);
    }

    public int requestInterrupt() {
        if (!mBridgeLoaded.get()) {
            return 0;
        }
        return PythonBridge.requestInterrupt();
    }

    public void clearOutputBuffers(@NonNull PythonEnvironment environment) {
        writeText(environment.stdoutFile, "");
        writeText(environment.stderrFile, "");
    }

    @NonNull
    public OutputBuffers readOutputBuffers(@NonNull PythonEnvironment environment) {
        String stdout = readText(environment.stdoutFile);
        String stderr = readText(environment.stderrFile);
        return new OutputBuffers(stdout, stderr);
    }

    public int restart(@NonNull PythonEnvironment environment) {
        synchronized (mLifecycleLock) {
            mRuntimeKey = null;
            if (mBridgeLoaded.get()) {
                PythonBridge.stopRuntime();
            }
        }
        ensureRuntimeStarted(environment);
        return 0;
    }

    @Override
    public void close() {
        synchronized (mLifecycleLock) {
            mRuntimeKey = null;
            if (mBridgeLoaded.get()) {
                PythonBridge.stopRuntime();
            }
        }
    }

    private void ensureRuntimeStarted(@NonNull PythonEnvironment environment) {
        RuntimeLibraryLoader.loadAll(environment);
        loadBridgeOnce();

        String newRuntimeKey = buildRuntimeKey(environment);
        synchronized (mLifecycleLock) {
            if (newRuntimeKey.equals(mRuntimeKey) && PythonBridge.isRuntimeStarted()) {
                return;
            }

            if (mRuntimeKey != null || PythonBridge.isRuntimeStarted()) {
                PythonBridge.stopRuntime();
            }

            String[] envKeys = new String[]{
                    "TMPDIR",
                    "PYTHONPYCACHEPREFIX",
                    "PYDROID_STDOUT_PATH",
                    "PYDROID_STDERR_PATH"
            };
            String[] envValues = new String[]{
                    environment.tmpDir.getAbsolutePath(),
                    new File(environment.tmpDir, "pycache").getAbsolutePath(),
                    environment.stdoutFile.getAbsolutePath(),
                    environment.stderrFile.getAbsolutePath()
            };

            int startStatus = PythonBridge.startRuntime(
                    environment.findLibPython().getAbsolutePath(),
                    environment.prefixDir.getAbsolutePath(),
                    environment.workingDir.getAbsolutePath(),
                    environment.userSiteDir.getAbsolutePath(),
                    envKeys,
                    envValues
            );
            if (startStatus != 0) {
                throw new IllegalStateException("Failed to start Python runtime");
            }

            mRuntimeKey = newRuntimeKey;
        }
    }

    @NonNull
    private String buildRuntimeKey(@NonNull PythonEnvironment environment) {
        return environment.findLibPython().getAbsolutePath() + "\n"
                + environment.prefixDir.getAbsolutePath() + "\n"
                + environment.workingDir.getAbsolutePath() + "\n"
                + environment.userSiteDir.getAbsolutePath() + "\n"
                + environment.tmpDir.getAbsolutePath() + "\n"
                + environment.stdoutFile.getAbsolutePath() + "\n"
                + environment.stderrFile.getAbsolutePath();
    }

    private void loadBridgeOnce() {
        if (mBridgeLoaded.compareAndSet(false, true)) {
            System.loadLibrary("pybridge");
        }
    }

    @NonNull
    private static File ensureDir(@NonNull File dir) {
        if (!dir.exists() && !dir.mkdirs()) {
            throw new IllegalStateException("Failed to create directory: " + dir);
        }
        return dir;
    }

    private static void writeText(@NonNull File file, @NonNull String content) {
        File parent = file.getParentFile();
        if (parent != null && !parent.exists() && !parent.mkdirs()) {
            throw new IllegalStateException("Failed to create directory: " + parent);
        }
        try (OutputStreamWriter writer = new OutputStreamWriter(
                new FileOutputStream(file, false), StandardCharsets.UTF_8)) {
            writer.write(content);
            writer.flush();
        } catch (IOException e) {
            throw new IllegalStateException("Failed to write file: " + file, e);
        }
    }

    @NonNull
    private static String readText(@NonNull File file) {
        if (!file.exists()) {
            return "";
        }
        try (InputStreamReader reader = new InputStreamReader(
                new FileInputStream(file), StandardCharsets.UTF_8)) {
            StringBuilder sb = new StringBuilder();
            char[] buffer = new char[4096];
            int read;
            while ((read = reader.read(buffer)) != -1) {
                sb.append(buffer, 0, read);
            }
            return sb.toString();
        } catch (IOException e) {
            return "";
        }
    }

    private static final class RuntimeLibraryLoader {
        private static final Set<String> LOADED = new LinkedHashSet<>();

        static synchronized void loadAll(@NonNull PythonEnvironment environment) {
            File[] files = environment.getPrefixLibDir().listFiles();
            if (files == null || files.length == 0) {
                throw new IllegalStateException("No shared libraries found under " + environment.getPrefixLibDir());
            }

            List<File> libs = new ArrayList<>();
            for (File file : files) {
                String name = file.getName();
                if (!file.isFile()) {
                    continue;
                }
                if (name.endsWith(".so") || name.contains(".so.")) {
                    libs.add(file);
                }
            }

            File libPython = environment.findLibPython();
            List<File> prePython = new ArrayList<>();
            List<File> postPython = new ArrayList<>();
            for (File lib : libs) {
                if (libPython.getAbsolutePath().equals(lib.getAbsolutePath())) {
                    continue;
                }
                if (lib.getName().contains("python")) {
                    postPython.add(lib);
                } else {
                    prePython.add(lib);
                }
            }

            Comparator<File> byName = Comparator.comparing(File::getName, String.CASE_INSENSITIVE_ORDER);
            prePython.sort(byName);
            postPython.sort(byName);

            for (File file : prePython) {
                load(file);
            }
            load(libPython);
            for (File file : postPython) {
                load(file);
            }
        }

        private static void load(@NonNull File file) {
            String path = file.getAbsolutePath();
            if (!LOADED.add(path)) {
                return;
            }
            System.load(path);
        }
    }
}
