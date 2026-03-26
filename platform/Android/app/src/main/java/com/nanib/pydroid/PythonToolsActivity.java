package com.nanib.pydroid;

import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.text.method.ScrollingMovementMethod;
import android.util.Log;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.TextView;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;

import com.nanib.pydroid.python.PythonPackageManager;
import com.nanib.pydroid.python.PythonRuntime;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.PrintWriter;
import java.io.StringWriter;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

public class PythonToolsActivity extends AppCompatActivity {
    public static final String EXTRA_ENV_CHANGED = "extra_env_changed";

    private static final int DARK_BG = 0xFF1B1E24;
    private static final int DARK_FG = 0xFFD7DEE9;
    private static final int DARK_SECONDARY = 0xFF5E6778;
    private static final int OUTPUT_TRIM_LIMIT = 60_000;
    private static final String GET_PIP_FILE_NAME = "get-pip.py";

    private View mToolbarContainer;
    private View mPythonPanel;
    private ImageButton mBtnBack;
    private Button mBtnInstallTar;
    private Button mBtnImportDir;
    private Button mBtnImportGetPip;
    private Button mBtnPipInstall;
    private EditText mEtPipRequirement;
    private EditText mEtPipIndexUrl;
    private TextView mPythonOutput;
    private TextView mStatusBar;

    private final ExecutorService mPythonExecutor = Executors.newSingleThreadExecutor();
    private final Object mPythonTaskLock = new Object();
    @Nullable
    private Future<?> mPythonTask;

    private PythonPackageManager mPythonPackageManager;
    private PythonRuntime mPythonRuntime;
    @Nullable
    private PythonRuntime.PythonEnvironment mPythonEnvironment;
    @Nullable
    private File mGetPipScriptFile;
    private boolean mEnvironmentChanged;

    private ActivityResultLauncher<String[]> mPickTarLauncher;
    private ActivityResultLauncher<Uri> mPickTreeLauncher;
    private ActivityResultLauncher<String[]> mPickGetPipLauncher;

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        setContentView(R.layout.activity_python_tools);

        bindViews();
        registerPickers();
        setupPythonRuntime();
        applyToolbarInsets();
        setupActions();
        applyTheme();
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        synchronized (mPythonTaskLock) {
            if (mPythonTask != null && !mPythonTask.isDone() && mPythonRuntime != null) {
                try {
                    mPythonRuntime.requestInterrupt();
                } catch (Throwable ignored) {
                }
            }
        }
        mPythonExecutor.shutdownNow();
        if (mPythonRuntime != null) {
            mPythonRuntime.close();
        }
    }

    @Override
    public void finish() {
        Intent result = new Intent();
        result.putExtra(EXTRA_ENV_CHANGED, mEnvironmentChanged);
        setResult(RESULT_OK, result);
        super.finish();
    }

    private void bindViews() {
        mToolbarContainer = findViewById(R.id.toolbar_container);
        mPythonPanel = findViewById(R.id.python_panel);
        mBtnBack = findViewById(R.id.btn_back);
        mBtnInstallTar = findViewById(R.id.btn_install_tar);
        mBtnImportDir = findViewById(R.id.btn_import_dir);
        mBtnImportGetPip = findViewById(R.id.btn_import_get_pip);
        mBtnPipInstall = findViewById(R.id.btn_pip_install);
        mEtPipRequirement = findViewById(R.id.et_pip_requirement);
        mEtPipIndexUrl = findViewById(R.id.et_pip_index_url);
        mPythonOutput = findViewById(R.id.tv_python_output);
        mPythonOutput.setMovementMethod(new ScrollingMovementMethod());
        mStatusBar = findViewById(R.id.tv_status);
    }

    private void applyToolbarInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(mToolbarContainer, (v, insets) -> {
            int top = insets.getInsets(WindowInsetsCompat.Type.statusBars()).top;
            v.setPadding(v.getPaddingLeft(), top + 6, v.getPaddingRight(), v.getPaddingBottom());
            return insets;
        });
    }

    private void applyTheme() {
        mToolbarContainer.setBackgroundColor(DARK_BG);
        mPythonPanel.setBackgroundColor(0xFF202731);

        mBtnBack.setColorFilter(DARK_FG);
        mBtnInstallTar.setTextColor(DARK_FG);
        mBtnImportDir.setTextColor(DARK_FG);
        mBtnImportGetPip.setTextColor(DARK_FG);
        mBtnPipInstall.setTextColor(DARK_FG);

        int panelInputBg = 0xFF10151D;
        mPythonOutput.setBackgroundColor(panelInputBg);
        mPythonOutput.setTextColor(DARK_FG);

        mEtPipRequirement.setBackgroundColor(panelInputBg);
        mEtPipRequirement.setTextColor(DARK_FG);
        mEtPipRequirement.setHintTextColor(DARK_SECONDARY);

        mEtPipIndexUrl.setBackgroundColor(panelInputBg);
        mEtPipIndexUrl.setTextColor(DARK_FG);
        mEtPipIndexUrl.setHintTextColor(DARK_SECONDARY);

        mStatusBar.setBackgroundColor(DARK_BG);
        mStatusBar.setTextColor(DARK_SECONDARY);
    }

    private void setupActions() {
        mBtnBack.setOnClickListener(v -> finish());

        mBtnInstallTar.setOnClickListener(v ->
                mPickTarLauncher.launch(new String[]{
                        "application/gzip",
                        "application/x-gzip",
                        "application/x-tar",
                        "application/octet-stream",
                        "*/*"
                }));

        mBtnImportDir.setOnClickListener(v -> mPickTreeLauncher.launch(null));

        mBtnImportGetPip.setOnClickListener(v ->
                mPickGetPipLauncher.launch(new String[]{
                        "text/x-python",
                        "text/plain",
                        "application/octet-stream",
                        "*/*"
                }));

        mBtnPipInstall.setOnClickListener(v -> pipInstallFromInput());
    }

    private void registerPickers() {
        mPickTarLauncher = registerForActivityResult(new ActivityResultContracts.OpenDocument(), uri -> {
            if (uri == null) {
                return;
            }
            persistReadPermission(uri);
            installFromTarAsync(uri);
        });

        mPickTreeLauncher = registerForActivityResult(new ActivityResultContracts.OpenDocumentTree(), uri -> {
            if (uri == null) {
                return;
            }
            persistReadPermission(uri);
            importExtractedTreeAsync(uri);
        });

        mPickGetPipLauncher = registerForActivityResult(new ActivityResultContracts.OpenDocument(), uri -> {
            if (uri == null) {
                return;
            }
            persistReadPermission(uri);
            importGetPipAsync(uri);
        });
    }

    private void setupPythonRuntime() {
        mPythonPackageManager = new PythonPackageManager(this);
        mPythonRuntime = new PythonRuntime(this);

        mGetPipScriptFile = new File(getPythonToolsDir(), GET_PIP_FILE_NAME);
        if (!mGetPipScriptFile.isFile()) {
            mGetPipScriptFile = null;
        }

        PythonPackageManager.InstallResult restored = mPythonPackageManager.restoreInstalledPackage();
        if (restored != null) {
            try {
                mPythonEnvironment = mPythonRuntime.createEnvironment(restored);
                updateStatus("Python runtime restored: " + restored.pythonVersion + " (" + restored.abi + ")");
            } catch (Throwable t) {
                Log.e("Pydroid", "Failed to restore Python runtime", t);
                updateStatus("Python runtime restore failed");
            }
        }
    }

    private File getPythonToolsDir() {
        File toolsDir = new File(getFilesDir(), "python/tools");
        if (!toolsDir.exists() && !toolsDir.mkdirs()) {
            throw new IllegalStateException("Failed to create tools directory: " + toolsDir);
        }
        return toolsDir;
    }

    private void persistReadPermission(@NonNull Uri uri) {
        int flags = Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION;
        try {
            getContentResolver().takePersistableUriPermission(uri, flags);
        } catch (Throwable ignored) {
        }
    }

    private void installFromTarAsync(@NonNull Uri uri) {
        startPythonTask("Installing official .tar.gz package...", () -> {
            PythonPackageManager.InstallResult result = mPythonPackageManager.installFromOfficialTarGz(uri);
            PythonRuntime.PythonEnvironment env = mPythonRuntime.createEnvironment(result);
            runOnUiThread(() -> {
                mPythonEnvironment = env;
                mEnvironmentChanged = true;
                updateStatus("Python installed: " + result.pythonVersion + " (" + result.abi + ")");
                appendOutputBlock("Install from .tar.gz", 0,
                        "prefix=" + result.prefixDir.getAbsolutePath(), "");
            });
        });
    }

    private void importExtractedTreeAsync(@NonNull Uri treeUri) {
        startPythonTask("Importing extracted package...", () -> {
            PythonPackageManager.InstallResult result = mPythonPackageManager.installFromExtractedTree(treeUri);
            PythonRuntime.PythonEnvironment env = mPythonRuntime.createEnvironment(result);
            runOnUiThread(() -> {
                mPythonEnvironment = env;
                mEnvironmentChanged = true;
                updateStatus("Python imported: " + result.pythonVersion + " (" + result.abi + ")");
                appendOutputBlock("Import extracted dir", 0,
                        "prefix=" + result.prefixDir.getAbsolutePath(), "");
            });
        });
    }

    private void importGetPipAsync(@NonNull Uri uri) {
        startPythonTask("Importing get-pip.py...", () -> {
            File target = new File(getPythonToolsDir(), GET_PIP_FILE_NAME);
            try (InputStream in = getContentResolver().openInputStream(uri);
                 FileOutputStream out = new FileOutputStream(target, false)) {
                if (in == null) {
                    throw new IOException("Failed to open selected get-pip.py");
                }
                byte[] buffer = new byte[16 * 1024];
                int n;
                while ((n = in.read(buffer)) != -1) {
                    out.write(buffer, 0, n);
                }
                out.flush();
            }
            runOnUiThread(() -> {
                mGetPipScriptFile = target;
                updateStatus("Imported get-pip.py");
                appendOutputBlock("Import get-pip.py", 0, target.getAbsolutePath(), "");
            });
        });
    }

    private void pipInstallFromInput() {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        final String requirement = mEtPipRequirement.getText().toString().trim();
        if (requirement.isEmpty()) {
            updateStatus("Enter a package requirement first");
            return;
        }
        final String indexUrl = mEtPipIndexUrl.getText().toString().trim();

        startPythonTask("pip install " + requirement + " ...", () -> {
            PythonRuntime.PythonEnvironment env = requirePythonEnvironment();
            int exitCode = mPythonRuntime.pipInstall(
                    env,
                    requirement,
                    mGetPipScriptFile,
                    indexUrl.isEmpty() ? null : indexUrl
            );
            PythonRuntime.OutputBuffers output = mPythonRuntime.readOutputBuffers(env);
            postTaskResult("pip install " + requirement, exitCode, output);
            if (exitCode == 0) {
                mEnvironmentChanged = true;
            }
        });
    }

    @NonNull
    private PythonRuntime.PythonEnvironment requirePythonEnvironment() {
        PythonRuntime.PythonEnvironment env = mPythonEnvironment;
        if (env == null) {
            throw new IllegalStateException("Python runtime is not ready");
        }
        return env;
    }

    private interface ThrowingRunnable {
        void run() throws Exception;
    }

    private void startPythonTask(@NonNull String startStatus,
                                 @NonNull ThrowingRunnable runnable) {
        synchronized (mPythonTaskLock) {
            if (mPythonTask != null && !mPythonTask.isDone()) {
                updateStatus("Another Python task is still running");
                return;
            }

            updateStatus(startStatus);
            mPythonTask = mPythonExecutor.submit(() -> {
                try {
                    runnable.run();
                } catch (Throwable t) {
                    Log.e("Pydroid", "Python task failed", t);
                    runOnUiThread(() -> showTaskError(t));
                } finally {
                    synchronized (mPythonTaskLock) {
                        mPythonTask = null;
                    }
                }
            });
        }
    }

    private void showTaskError(@NonNull Throwable t) {
        updateStatus("Task failed: " + (t.getMessage() == null ? t.getClass().getSimpleName() : t.getMessage()));
        appendOutputBlock("Task Error", 1, "", stackTrace(t));
    }

    private void postTaskResult(@NonNull String title,
                                int exitCode,
                                @NonNull PythonRuntime.OutputBuffers output) {
        runOnUiThread(() -> {
            appendOutputBlock(title, exitCode, output.stdout, output.stderr);
            updateStatus(title + " finished with exit code " + exitCode);
        });
    }

    private void appendOutputBlock(@NonNull String title,
                                   int exitCode,
                                   @Nullable String stdout,
                                   @Nullable String stderr) {
        StringBuilder block = new StringBuilder();
        block.append("[")
                .append(new SimpleDateFormat("HH:mm:ss", Locale.US).format(new Date()))
                .append("] ")
                .append(title)
                .append(" (exit=")
                .append(exitCode)
                .append(")\n");

        if (stdout != null && !stdout.isEmpty()) {
            block.append("--- stdout ---\n").append(stdout.trim()).append('\n');
        }
        if (stderr != null && !stderr.isEmpty()) {
            block.append("--- stderr ---\n").append(stderr.trim()).append('\n');
        }
        if ((stdout == null || stdout.isEmpty()) && (stderr == null || stderr.isEmpty())) {
            block.append("(no output)\n");
        }
        block.append('\n');

        String merged = mPythonOutput.getText().toString() + block;
        if (merged.length() > OUTPUT_TRIM_LIMIT) {
            merged = merged.substring(merged.length() - OUTPUT_TRIM_LIMIT);
        }
        mPythonOutput.setText(merged);

        int scrollAmount = mPythonOutput.getLayout() == null
                ? 0
                : mPythonOutput.getLayout().getLineTop(mPythonOutput.getLineCount()) - mPythonOutput.getHeight();
        if (scrollAmount > 0) {
            mPythonOutput.scrollTo(0, scrollAmount);
        } else {
            mPythonOutput.scrollTo(0, 0);
        }
    }

    @NonNull
    private String stackTrace(@NonNull Throwable throwable) {
        StringWriter sw = new StringWriter();
        PrintWriter pw = new PrintWriter(sw);
        throwable.printStackTrace(pw);
        pw.flush();
        return sw.toString();
    }

    private void updateStatus(@NonNull String message) {
        mStatusBar.setText(message);
    }
}
