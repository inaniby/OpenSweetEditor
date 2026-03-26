package com.nanib.pydroid;

import android.content.Intent;
import android.graphics.Color;
import android.graphics.PorterDuff;
import android.graphics.Typeface;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.util.Log;
import android.view.View;
import android.view.KeyEvent;
import android.view.ViewGroup;
import android.view.Window;
import android.view.inputmethod.EditorInfo;
import android.view.WindowInsetsController;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.ImageButton;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.ContextCompat;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;

import com.google.android.material.bottomsheet.BottomSheetBehavior;

import com.nanib.pydroid.python.PythonPackageManager;
import com.nanib.pydroid.python.PythonRuntime;
import com.nanib.pydroid.widget.TerminalEditText;
import com.qiplat.sweeteditor.EditorSettings;
import com.qiplat.sweeteditor.EditorTheme;
import com.qiplat.sweeteditor.SweetEditor;
import com.qiplat.sweeteditor.copilot.InlineSuggestion;
import com.qiplat.sweeteditor.core.Document;
import com.qiplat.sweeteditor.core.foundation.CurrentLineRenderMode;
import com.qiplat.sweeteditor.core.foundation.FoldArrowMode;
import com.qiplat.sweeteditor.core.foundation.WrapMode;
import com.qiplat.sweeteditor.event.CursorChangedEvent;
import com.qiplat.sweeteditor.event.GutterIconClickEvent;
import com.qiplat.sweeteditor.event.InlayHintClickEvent;
import com.qiplat.sweeteditor.event.TextChangedEvent;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.File;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.io.StringWriter;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

public class MainActivity extends AppCompatActivity {
    private static final int STYLE_COLOR = EditorTheme.STYLE_PREPROCESSOR + 1;
    private static final String DEMO_FILES_ASSET_DIR = "files";
    private static final String DEFAULT_PYTHON_FILE_NAME = "example.py";
    private static final String FALLBACK_FILE_NAME = "example.java";

    private static final int DARK_BG = 0xFF1B1E24;
    private static final int DARK_FG = 0xFFD7DEE9;
    private static final int DARK_SECONDARY = 0xFF5E6778;
    private static final int LIGHT_BG = 0xFFFAFBFD;
    private static final int LIGHT_FG = 0xFF1F2937;
    private static final int LIGHT_SECONDARY = 0xFF8A94A6;

    private static final int OUTPUT_TRIM_LIMIT = 60_000;
    private static final int MAX_CONSOLE_HISTORY = 200;

    private static final String CONSOLE_REPL_ENTER = "python";
    private static final String CONSOLE_REPL_ENTER_ALT = "python -i";
    private static final String CONSOLE_REPL_EXIT = ":exit";
    private static final String CONSOLE_REPL_RESET = ":reset";
    private SweetEditor mEditor;
    private TextView mStatusBar;
    private View mRootContainer;
    private View mToolbarContainer;
    private ImageButton mBtnUndo;
    private ImageButton mBtnRedo;
    private ImageButton mBtnTheme;
    private ImageButton mBtnWrap;
    private ImageButton mBtnRunPython;
    private ImageButton mBtnPythonTools;
    private ImageButton mBtnToggleConsole;
    private TerminalEditText mConsoleTerminal;
    private View mPythonOutputContainer;
    @Nullable
    private BottomSheetBehavior<View> mConsoleBehavior;
    private int mConsoleInputStart;
    private int mConsolePeekHeightPx;
    private boolean mPythonTaskStartedInCommand;

    private boolean mConsoleReplMode;
    @NonNull
    private final List<String> mConsoleHistory = new ArrayList<>();
    private int mConsoleHistoryIndex = -1;
    @Nullable
    private String mConsoleHistoryDraft;
    @NonNull
    private final StringBuilder mReplPendingBlock = new StringBuilder();
    private TextView mConsoleTitle;
    private Spinner mFileSpinner;

    private boolean mIsDarkTheme = true;
    private WrapMode mWrapModePreset = WrapMode.NONE;
    private final List<String> mDemoFiles = new ArrayList<>();

    private DemoDecorationProvider mDemoProvider;
    private DemoCompletionProvider mDemoCompletionProvider;
    private final Handler mSuggestionHandler = new Handler(Looper.getMainLooper());
    private Runnable mPendingSuggestion;

    private final ExecutorService mPythonExecutor = Executors.newSingleThreadExecutor();
    private final Object mPythonTaskLock = new Object();
    @Nullable
    private Future<?> mPythonTask;

    private PythonPackageManager mPythonPackageManager;
    private PythonRuntime mPythonRuntime;
    @Nullable
    private PythonRuntime.PythonEnvironment mPythonEnvironment;

    private ActivityResultLauncher<Intent> mPythonToolsLauncher;

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setupImmersiveWindow();
        setContentView(R.layout.activity_main);

        bindViews();
        registerActivityLaunchers();
        setupPythonRuntime();

        applyToolbarInsets();

        EditorSettings settings = mEditor.getSettings();
        settings.setTypeface(Typeface.create(Typeface.MONOSPACE, Typeface.NORMAL));
        settings.setEditorTextSize(28f);
        settings.setFoldArrowMode(FoldArrowMode.AUTO);
        settings.setMaxGutterIcons(1);
        settings.setCurrentLineRenderMode(CurrentLineRenderMode.BORDER);
        registerColorStyleForCurrentTheme();
        mEditor.setVerticalFadingEdgeEnabled(false);
        mEditor.setHorizontalFadingEdgeEnabled(false);
        mEditor.setFadingEdgeLength(0);
        mEditor.setOverScrollMode(View.OVER_SCROLL_NEVER);

        try {
            DemoDecorationProvider.ensureSweetLineReady(this);
        } catch (IOException e) {
            throw new RuntimeException(e);
        }

        mDemoProvider = new DemoDecorationProvider(mEditor);
        mEditor.addDecorationProvider(mDemoProvider);

        mDemoCompletionProvider = new DemoCompletionProvider(mEditor);
        mDemoCompletionProvider.setPythonEnvironment(mPythonEnvironment);
        mEditor.addCompletionProvider(mDemoCompletionProvider);

        mEditor.setEditorIconProvider(iconId -> {
            if (iconId == DemoDecorationProvider.ICON_TYPE) {
                return ContextCompat.getDrawable(this, R.mipmap.ic_gutter_down);
            } else if (iconId == DemoDecorationProvider.ICON_AT) {
                return ContextCompat.getDrawable(this, R.mipmap.ic_gutter_at);
            }
            return null;
        });

        setupToolbar();
        setupFileSpinner();
        setupPythonActions();
        subscribeEditorEvents();
        applyAppTheme();
    }

    @Override
    protected void onResume() {
        super.onResume();
        reloadInstalledPythonEnvironment(false);
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        cancelPendingSuggestion();

        if (mDemoCompletionProvider != null) {
            mDemoCompletionProvider.shutdown();
        }

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

    private void bindViews() {
        mEditor = findViewById(R.id.editor);
        mStatusBar = findViewById(R.id.tv_status);
        mRootContainer = findViewById(R.id.root_container);
        mToolbarContainer = findViewById(R.id.toolbar_container);
        mBtnUndo = findViewById(R.id.btn_undo);
        mBtnRedo = findViewById(R.id.btn_redo);
        mBtnTheme = findViewById(R.id.btn_switch_theme);
        mBtnWrap = findViewById(R.id.btn_wrap_mode);
        mBtnRunPython = findViewById(R.id.btn_run_python);
        mBtnPythonTools = findViewById(R.id.btn_python_tools);
        mFileSpinner = findViewById(R.id.spn_files);
        mBtnToggleConsole = findViewById(R.id.btn_toggle_console);
        mPythonOutputContainer = findViewById(R.id.python_output_container);
        mConsoleTitle = findViewById(R.id.tv_console_title);
        mConsoleTerminal = findViewById(R.id.et_console_terminal);

        mConsolePeekHeightPx = dpToPx(220);
        mConsoleBehavior = BottomSheetBehavior.from(mPythonOutputContainer);
        mConsoleBehavior.setHideable(true);
        mConsoleBehavior.setSkipCollapsed(false);
        mConsoleBehavior.setFitToContents(false);
        mConsoleBehavior.setHalfExpandedRatio(0.6f);
        mConsoleBehavior.setPeekHeight(mConsolePeekHeightPx, false);
        mConsoleBehavior.setState(BottomSheetBehavior.STATE_HIDDEN);

        mConsoleTerminal.setHorizontallyScrolling(false);
        mConsoleTerminal.setText("");
        mConsoleInputStart = 0;
        appendConsolePromptLine();
    }

    private void registerActivityLaunchers() {
        mPythonToolsLauncher = registerForActivityResult(
                new ActivityResultContracts.StartActivityForResult(),
                result -> {
                    Intent data = result.getData();
                    if (data == null) {
                        return;
                    }
                    boolean changed = data.getBooleanExtra(PythonToolsActivity.EXTRA_ENV_CHANGED, false);
                    if (changed) {
                        reloadInstalledPythonEnvironment(true);
                    }
                }
        );
    }

    private void setupPythonRuntime() {
        mPythonPackageManager = new PythonPackageManager(this);
        mPythonRuntime = new PythonRuntime(this);
        reloadInstalledPythonEnvironment(true);
    }

    private void reloadInstalledPythonEnvironment(boolean updateStatus) {
        PythonPackageManager.InstallResult restored = mPythonPackageManager.restoreInstalledPackage();
        if (restored != null) {
            try {
                PythonRuntime.PythonEnvironment env = mPythonRuntime.createEnvironment(restored);
                mPythonEnvironment = env;
                if (mDemoCompletionProvider != null) {
                    mDemoCompletionProvider.setPythonEnvironment(env);
                    mDemoCompletionProvider.invalidatePythonIndexes();
                }
                if (updateStatus) {
                    updateStatus("Python runtime restored: " + restored.pythonVersion + " (" + restored.abi + ")");
                }
            } catch (Throwable t) {
                Log.e("Pydroid", "Failed to restore Python runtime", t);
                if (updateStatus) {
                    updateStatus("Python runtime restore failed");
                }
            }
            return;
        }

        mPythonEnvironment = null;
        if (mDemoCompletionProvider != null) {
            mDemoCompletionProvider.setPythonEnvironment(null);
            mDemoCompletionProvider.invalidatePythonIndexes();
        }
        if (updateStatus) {
            updateStatus("Python runtime not installed");
        }
    }

    private void setupImmersiveWindow() {
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        Window window = getWindow();
        window.setStatusBarColor(Color.TRANSPARENT);
        window.setNavigationBarColor(Color.TRANSPARENT);
    }

    private void applyToolbarInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(mToolbarContainer, (v, insets) -> {
            int top = insets.getInsets(WindowInsetsCompat.Type.statusBars()).top;
            v.setPadding(v.getPaddingLeft(), top + 6, v.getPaddingRight(), v.getPaddingBottom());
            return insets;
        });
    }

    private void applyAppTheme() {
        int bg = mIsDarkTheme ? DARK_BG : LIGHT_BG;
        int fg = mIsDarkTheme ? DARK_FG : LIGHT_FG;
        int secondary = mIsDarkTheme ? DARK_SECONDARY : LIGHT_SECONDARY;

        mRootContainer.setBackgroundColor(bg);
        mToolbarContainer.setBackgroundColor(bg);
        tintImageButton(mBtnUndo, fg);
        tintImageButton(mBtnRedo, fg);
        tintImageButton(mBtnTheme, fg);
        tintImageButton(mBtnWrap, fg);
        tintImageButton(mBtnRunPython, fg);
        tintImageButton(mBtnPythonTools, fg);
        tintImageButton(mBtnToggleConsole, fg);
        int panelBg = mIsDarkTheme ? 0xFF10151D : 0xFFFFFFFF;
        mPythonOutputContainer.setBackgroundColor(panelBg);
        mConsoleTerminal.setBackgroundColor(panelBg);
        mConsoleTerminal.setTextColor(fg);
        mConsoleTerminal.setHintTextColor(secondary);
        mConsoleTitle.setTextColor(secondary);

        mStatusBar.setBackgroundColor(bg);
        mStatusBar.setTextColor(secondary);

        updateStatusBarAppearance();
        updateSpinnerTheme(fg, bg);
    }

    private void tintImageButton(ImageButton btn, int color) {
        btn.setColorFilter(color, PorterDuff.Mode.SRC_IN);
    }

    private void updateStatusBarAppearance() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            WindowInsetsController ctrl = getWindow().getInsetsController();
            if (ctrl != null) {
                if (mIsDarkTheme) {
                    ctrl.setSystemBarsAppearance(0,
                            WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS);
                } else {
                    ctrl.setSystemBarsAppearance(
                            WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS,
                            WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS);
                }
            }
        } else {
            View decorView = getWindow().getDecorView();
            int flags = decorView.getSystemUiVisibility();
            if (mIsDarkTheme) {
                flags &= ~View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
            } else {
                flags |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
            }
            decorView.setSystemUiVisibility(flags);
        }
    }

    private void updateSpinnerTheme(int textColor, int bgColor) {
        ArrayAdapter<String> adapter = new ArrayAdapter<String>(this,
                android.R.layout.simple_spinner_item, mDemoFiles) {
            @NonNull
            @Override
            public View getView(int position, @Nullable View convertView, @NonNull ViewGroup parent) {
                TextView tv = (TextView) super.getView(position, convertView, parent);
                tv.setTextColor(textColor);
                tv.setTextSize(13f);
                return tv;
            }

            @Override
            public View getDropDownView(int position, @Nullable View convertView, @NonNull ViewGroup parent) {
                TextView tv = (TextView) super.getDropDownView(position, convertView, parent);
                tv.setTextColor(textColor);
                tv.setBackgroundColor(bgColor);
                tv.setPadding(24, 20, 24, 20);
                return tv;
            }
        };
        adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        int currentSelection = mFileSpinner.getSelectedItemPosition();
        mFileSpinner.setAdapter(adapter);
        if (currentSelection >= 0 && currentSelection < mDemoFiles.size()) {
            mFileSpinner.setSelection(currentSelection);
        }
    }

    private void subscribeEditorEvents() {
        mEditor.subscribe(TextChangedEvent.class, e -> {
            String rangeStr = e.changeRange != null
                    ? e.changeRange.start.line + ":" + e.changeRange.start.column
                    + "-" + e.changeRange.end.line + ":" + e.changeRange.end.column
                    : "null";
            String textPreview = e.text != null
                    ? (e.text.length() > 50 ? e.text.substring(0, 50) + "..." : e.text).replace("\n", "\\n")
                    : "null";
            Log.d("SweetEditor", "[TextChanged] action=" + e.action.name() + " range=" + rangeStr + " text=" + textPreview);
        });
        mEditor.subscribe(InlayHintClickEvent.class, e -> {
            if (e.isColor) {
                Toast.makeText(this, "Click color: " + String.format("0X%X", e.colorValue), Toast.LENGTH_SHORT).show();
            } else if (!e.isIcon) {
                Toast.makeText(this, "Click inlay hint: (" + e.line + "," + e.column + ")", Toast.LENGTH_SHORT).show();
            }
        });
        mEditor.subscribe(GutterIconClickEvent.class, e ->
                Toast.makeText(this, "Click icon at line: " + e.line, Toast.LENGTH_SHORT).show());

        mEditor.subscribe(CursorChangedEvent.class, this::scheduleSuggestionIfAtLineEnd);

        mEditor.setInlineSuggestionListener(new com.qiplat.sweeteditor.copilot.InlineSuggestionListener() {
            @Override
            public void onSuggestionAccepted(@NonNull InlineSuggestion suggestion) {
                updateStatus("Accepted suggestion at line " + suggestion.line);
            }

            @Override
            public void onSuggestionDismissed(@NonNull InlineSuggestion suggestion) {
                updateStatus("Dismissed suggestion at line " + suggestion.line);
            }
        });
    }

    private void scheduleSuggestionIfAtLineEnd(@NonNull CursorChangedEvent event) {
        if (mEditor.hasSelection()) {
            return;
        }
        cancelPendingSuggestion();
        Document doc = mEditor.getDocument();
        if (doc == null) {
            return;
        }

        int line = event.cursorPosition.line;
        int column = event.cursorPosition.column;
        String lineText = doc.getLineText(line);
        if (lineText == null) {
            return;
        }

        if (column != lineText.length()) {
            if (mEditor.isInlineSuggestionShowing()) {
                mEditor.dismissInlineSuggestion();
            }
            return;
        }

        String suggestionText = buildContextSuggestion(doc, line, lineText);
        if (suggestionText == null || suggestionText.isEmpty()) {
            if (mEditor.isInlineSuggestionShowing()) {
                mEditor.dismissInlineSuggestion();
            }
            return;
        }

        mPendingSuggestion = () -> {
            if (mEditor.isInlineSuggestionShowing()) {
                mEditor.dismissInlineSuggestion();
            }
            mEditor.showInlineSuggestion(new InlineSuggestion(line, column, suggestionText));
        };
        mSuggestionHandler.postDelayed(mPendingSuggestion, 650);
    }

    @Nullable
    private String buildContextSuggestion(@NonNull Document doc,
                                          int line,
                                          @NonNull String lineText) {
        String fileName = currentFileName();
        String lowerName = fileName.toLowerCase(Locale.ROOT);
        String trimmed = lineText.trim();
        if (trimmed.isEmpty()) {
            return null;
        }

        if (lowerName.endsWith(".py") || lowerName.endsWith(".pyw") || lowerName.endsWith(".pyi")) {
            return buildPythonSuggestion(doc, line, lineText, trimmed);
        }

        if (lowerName.endsWith(".java")) {
            return buildJavaSuggestion(trimmed);
        }

        if (lowerName.endsWith(".lua")) {
            if (trimmed.startsWith("function ")) {
                return "\n    -- TODO\nend";
            }
            if (trimmed.startsWith("if ") && trimmed.endsWith(" then")) {
                return "\n    -- TODO\nend";
            }
        }

        return null;
    }

    @Nullable
    private String buildPythonSuggestion(@NonNull Document doc,
                                         int line,
                                         @NonNull String lineText,
                                         @NonNull String trimmed) {
        if (trimmed.endsWith("httpx.")) {
            return "get(\"https://example.com\")";
        }
        if (trimmed.endsWith("httpx.get(")) {
            return "\"https://example.com\")";
        }
        if (trimmed.endsWith("sys.platform")) {
            return ".startswith(\"linux\")";
        }
        if (trimmed.endsWith("print(")) {
            return "\"\")";
        }
        if (trimmed.startsWith("def ") && trimmed.endsWith(":")) {
            return "\n    pass";
        }
        if (trimmed.startsWith("if __name__ == \"__main__\":")
                || trimmed.startsWith("if __name__ == '__main__':")) {
            return "\n    main()";
        }
        if ((trimmed.startsWith("if ") || trimmed.startsWith("for ") || trimmed.startsWith("while "))
                && trimmed.endsWith(":")) {
            return "\n    pass";
        }
        if (trimmed.startsWith("with ") && trimmed.endsWith(":")) {
            return "\n    pass";
        }
        if (lineText.endsWith(".") && hasRecentImport(doc, line, "json")) {
            String lhs = lineText.trim().toLowerCase(Locale.ROOT);
            if (lhs.endsWith("json.")) {
                return "loads(\"{}\")";
            }
        }
        return null;
    }

    @Nullable
    private String buildJavaSuggestion(@NonNull String trimmed) {
        if (trimmed.endsWith("System.out.")) {
            return "println();";
        }
        if (trimmed.startsWith("if (") && trimmed.endsWith(") {")) {
            return "\n    \n}";
        }
        if (trimmed.startsWith("for (") && trimmed.endsWith(") {")) {
            return "\n    \n}";
        }
        return null;
    }

    private boolean hasRecentImport(@NonNull Document doc, int line, @NonNull String module) {
        int start = Math.max(0, line - 40);
        for (int i = line; i >= start; i--) {
            String text = doc.getLineText(i);
            if (text == null) {
                continue;
            }
            String t = text.trim();
            if (t.startsWith("import ") || t.startsWith("from ")) {
                if (t.contains(module)) {
                    return true;
                }
            }
        }
        return false;
    }

    @NonNull
    private String currentFileName() {
        Object metadata = mEditor.getMetadata();
        if (metadata instanceof DemoFileMetadata) {
            return ((DemoFileMetadata) metadata).fileName;
        }
        return "";
    }

    private void cancelPendingSuggestion() {
        if (mPendingSuggestion != null) {
            mSuggestionHandler.removeCallbacks(mPendingSuggestion);
            mPendingSuggestion = null;
        }
    }

    private void setupFileSpinner() {
        mDemoFiles.clear();
        mDemoFiles.addAll(listDemoFiles());
        if (mDemoFiles.isEmpty()) {
            mDemoFiles.add(FALLBACK_FILE_NAME);
        }

        mFileSpinner.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
                if (position < 0 || position >= mDemoFiles.size()) {
                    return;
                }
                loadDemoFile(mDemoFiles.get(position));
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {
            }
        });

        int preferredIndex = mDemoFiles.indexOf(DEFAULT_PYTHON_FILE_NAME);
        if (preferredIndex >= 0) {
            mFileSpinner.setSelection(preferredIndex);
        } else if (!mDemoFiles.isEmpty()) {
            mFileSpinner.setSelection(0);
        }
    }

    private List<String> listDemoFiles() {
        List<String> files = new ArrayList<>();
        try {
            String[] entries = getAssets().list(DEMO_FILES_ASSET_DIR);
            if (entries == null) {
                return files;
            }
            for (String name : entries) {
                if (name == null || name.trim().isEmpty()) {
                    continue;
                }
                String assetPath = DEMO_FILES_ASSET_DIR + "/" + name;
                try (InputStream ignored = getAssets().open(assetPath)) {
                    files.add(name);
                } catch (IOException ignored) {
                }
            }
            Collections.sort(files, String.CASE_INSENSITIVE_ORDER);
        } catch (IOException e) {
            Log.e("SweetEditor", "Failed to list demo files", e);
        }
        return files;
    }

    private void loadDemoFile(@NonNull String fileName) {
        String assetPath = DEMO_FILES_ASSET_DIR + "/" + fileName;
        String code = loadAsset(assetPath);
        mEditor.loadDocument(new Document(code));
        mEditor.setMetadata(new DemoFileMetadata(fileName));
        mEditor.dismissInlineSuggestion();
        mEditor.post(mEditor::requestDecorationRefresh);
        updateStatus("Loaded: " + fileName);
    }

    @NonNull
    private String loadAsset(@NonNull String fileName) {
        StringBuilder sb = new StringBuilder();
        try (InputStream is = getAssets().open(fileName);
             BufferedReader reader = new BufferedReader(new InputStreamReader(is))) {
            String line;
            while ((line = reader.readLine()) != null) {
                if (sb.length() > 0) {
                    sb.append('\n');
                }
                sb.append(line);
            }
        } catch (IOException e) {
            Log.e("SweetEditor", "Failed to load asset: " + fileName, e);
        }
        return sb.toString();
    }

    private void setupToolbar() {
        mBtnUndo.setOnClickListener(v -> {
            if (mEditor.canUndo()) {
                mEditor.undo();
                updateStatus("Undo");
            } else {
                updateStatus("Nothing to undo");
            }
        });

        mBtnRedo.setOnClickListener(v -> {
            if (mEditor.canRedo()) {
                mEditor.redo();
                updateStatus("Redo");
            } else {
                updateStatus("Nothing to redo");
            }
        });

        mBtnTheme.setOnClickListener(v -> {
            mIsDarkTheme = !mIsDarkTheme;
            mEditor.applyTheme(mIsDarkTheme ? EditorTheme.dark() : EditorTheme.light());
            registerColorStyleForCurrentTheme();
            applyAppTheme();
            updateStatus(mIsDarkTheme ? "Dark theme" : "Light theme");
        });

        mBtnWrap.setOnClickListener(v -> cycleWrapMode());

        mBtnPythonTools.setOnClickListener(v -> {
            Intent intent = new Intent(this, PythonToolsActivity.class);
            mPythonToolsLauncher.launch(intent);
        });
    }

    private void setupPythonActions() {
        mBtnRunPython.setOnClickListener(v -> runPythonCodeFromEditor());
        mBtnRunPython.setOnLongClickListener(v -> {
            if (mPythonRuntime != null) {
                mPythonRuntime.requestInterrupt();
                updateStatus("Interrupt requested");
            }
            return true;
        });

        mBtnToggleConsole.setOnClickListener(v -> toggleConsoleVisibility());

        mConsoleTerminal.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_SEND
                    || actionId == EditorInfo.IME_ACTION_DONE
                    || actionId == EditorInfo.IME_NULL) {
                submitConsoleCommand();
                return true;
            }
            return false;
        });

        mConsoleTerminal.setOnClickListener(v -> mConsoleTerminal.ensureEditableCursor());
        mConsoleTerminal.setOnFocusChangeListener((v, hasFocus) -> {
            if (hasFocus) {
                mConsoleTerminal.ensureEditableCursor();
            }
        });

        mConsoleTerminal.setOnKeyListener((v, keyCode, event) -> {
            if (event.getAction() != KeyEvent.ACTION_DOWN) {
                return false;
            }
            if (keyCode == KeyEvent.KEYCODE_ENTER || keyCode == KeyEvent.KEYCODE_NUMPAD_ENTER) {
                submitConsoleCommand();
                return true;
            }
            if (keyCode == KeyEvent.KEYCODE_DPAD_UP) {
                return navigateConsoleHistory(-1);
            }
            if (keyCode == KeyEvent.KEYCODE_DPAD_DOWN) {
                return navigateConsoleHistory(1);
            }
            return false;
        });
    }

    private void runPythonCodeFromEditor() {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        Document doc = mEditor.getDocument();
        final String code = doc == null ? "" : doc.getText();
        if (code.trim().isEmpty()) {
            updateStatus("Editor is empty");
            return;
        }

        showConsole(false);

        startPythonTask("Running Python code...", () -> {
            PythonRuntime.PythonEnvironment env = requirePythonEnvironment();
            int exitCode = mPythonRuntime.runCode(env, code);
            PythonRuntime.OutputBuffers output = mPythonRuntime.readOutputBuffers(env);
            postTaskResult("Run Python", exitCode, output);
        });
    }


    private void toggleConsoleVisibility() {
        if (mConsoleBehavior == null) {
            return;
        }

        int state = mConsoleBehavior.getState();
        if (state == BottomSheetBehavior.STATE_HIDDEN) {
            showConsole(true);
            updateStatus("Console opened");
            return;
        }

        if (state == BottomSheetBehavior.STATE_EXPANDED) {
            mConsoleBehavior.setPeekHeight(mConsolePeekHeightPx, false);
            mConsoleBehavior.setState(BottomSheetBehavior.STATE_HIDDEN);
            updateStatus("Console hidden");
            return;
        }

        hideConsole();
    }

    private void showConsole(boolean focusInput) {
        if (mConsoleBehavior == null) {
            return;
        }

        mConsoleBehavior.setPeekHeight(mConsolePeekHeightPx, false);
        if (mConsoleBehavior.getState() == BottomSheetBehavior.STATE_HIDDEN) {
            mConsoleBehavior.setState(BottomSheetBehavior.STATE_COLLAPSED);
        }

        ensureConsolePromptReady();
        if (focusInput) {
            focusConsoleInput();
        }
    }

    private void hideConsole() {
        if (mConsoleBehavior != null) {
            mConsoleBehavior.setPeekHeight(mConsolePeekHeightPx, false);
            mConsoleBehavior.setState(BottomSheetBehavior.STATE_HIDDEN);
        }
        updateStatus("Console hidden");
    }

    private void focusConsoleInput() {
        mConsoleTerminal.requestFocus();
        mConsoleTerminal.post(() -> {
            Editable editable = mConsoleTerminal.getText();
            int len = editable == null ? 0 : editable.length();
            mConsoleTerminal.setSelection(len);
            mConsoleTerminal.ensureEditableCursor();
        });
    }

    private void submitConsoleCommand() {
        showConsole(true);

        String raw = getCurrentConsoleInput().replace("\r", "");
        if (raw.trim().isEmpty() && mReplPendingBlock.length() == 0) {
            appendOutputText("\n");
            appendConsolePromptLine();
            return;
        }

        appendOutputText("\n");
        recordConsoleHistory(raw);

        mPythonTaskStartedInCommand = false;
        executeConsoleCommand(raw);
        if (!mPythonTaskStartedInCommand) {
            appendConsolePromptLine();
        }
    }

    @NonNull
    private String currentConsolePromptText() {
        if (mReplPendingBlock.length() > 0) {
            return "...";
        }
        if (mConsoleReplMode) {
            return ">>>";
        }
        return "$";
    }

    private void updateConsolePrompt() {
        mConsoleTerminal.setInputStart(mConsoleInputStart);
    }

    private void ensureConsolePromptReady() {
        Editable editable = mConsoleTerminal.getText();
        if (editable == null || editable.length() == 0) {
            appendConsolePromptLine();
            return;
        }
        mConsoleTerminal.setInputStart(mConsoleInputStart);
        mConsoleTerminal.ensureEditableCursor();
    }

    private void appendConsolePromptLine() {
        Editable editable = mConsoleTerminal.getText();
        if (editable == null) {
            return;
        }

        int len = editable.length();
        if (len > 0 && editable.charAt(len - 1) != '\n') {
            editable.append('\n');
        }

        editable.append(currentConsolePromptText()).append(' ');
        mConsoleInputStart = editable.length();
        mConsoleTerminal.setInputStart(mConsoleInputStart);
        mConsoleTerminal.setSelection(editable.length());
        scrollOutputToBottom();
    }

    @NonNull
    private String getCurrentConsoleInput() {
        Editable editable = mConsoleTerminal.getText();
        if (editable == null) {
            return "";
        }

        int startPos = Math.max(0, Math.min(mConsoleInputStart, editable.length()));
        return editable.subSequence(startPos, editable.length()).toString();
    }

    private void replaceCurrentConsoleInput(@NonNull String value) {
        Editable editable = mConsoleTerminal.getText();
        if (editable == null) {
            return;
        }

        int startPos = Math.max(0, Math.min(mConsoleInputStart, editable.length()));
        editable.replace(startPos, editable.length(), value);
        mConsoleTerminal.setInputStart(mConsoleInputStart);
        mConsoleTerminal.setSelection(editable.length());
        scrollOutputToBottom();
    }

    private void clearConsoleBuffer() {
        mConsoleTerminal.setText("");
        mConsoleInputStart = 0;
        mConsoleTerminal.setInputStart(mConsoleInputStart);
    }

    private void executeConsoleCommand(@NonNull String rawCommand) {
        String trimmed = rawCommand.trim();

        if (trimmed.isEmpty()) {
            if (mConsoleReplMode && mReplPendingBlock.length() > 0) {
                executeReplInput("");
            }
            return;
        }

        if (CONSOLE_REPL_EXIT.equals(trimmed)) {
            mConsoleReplMode = false;
            mReplPendingBlock.setLength(0);
            appendConsoleRaw("Interactive mode disabled.\n");
            updateConsolePrompt();
            return;
        }

        if (CONSOLE_REPL_RESET.equals(trimmed)) {
            mReplPendingBlock.setLength(0);
            if (mPythonEnvironment == null) {
                appendConsoleRaw("Interactive context reset.\n");
                updateConsolePrompt();
                return;
            }

            startPythonTask("Resetting interactive context...", () -> {
                PythonRuntime.PythonEnvironment env = requirePythonEnvironment();
                int exitCode = mPythonRuntime.restart(env);
                PythonRuntime.OutputBuffers output = mPythonRuntime.readOutputBuffers(env);
                postTaskResult("Interactive context reset", exitCode, output);
            });
            return;
        }

        if ("clear".equalsIgnoreCase(trimmed) || "cls".equalsIgnoreCase(trimmed)) {
            clearConsoleBuffer();
            updateStatus("Console cleared");
            return;
        }

        if ("help".equalsIgnoreCase(trimmed)) {
            appendConsoleRaw(
                    "Commands:\n"
                            + "  python                  start interactive mode\n"
                            + "  python -i               start interactive mode\n"
                            + "  python -c \"code\"        run one-liner\n"
                            + "  python -m module [args] run module\n"
                            + "  python script.py [args] run script from work dir\n"
                            + "  python <code>           run inline code (compat mode)\n"
                            + "  pip <args>              run pip command\n"
                            + "  clear / cls             clear console output\n"
                            + "  :exit                   leave interactive mode\n"
                            + "  :reset                  clear interactive context\n");
            return;
        }

        if (trimmed.equals("pip") || trimmed.startsWith("pip ")) {
            String pipArgs = trimmed.equals("pip") ? "--help" : trimmed.substring(4).trim();
            executePipCommand(pipArgs);
            return;
        }

        if (CONSOLE_REPL_ENTER.equals(trimmed) || CONSOLE_REPL_ENTER_ALT.equals(trimmed)) {
            enterInteractiveMode();
            return;
        }

        if (mConsoleReplMode) {
            executeReplInput(rawCommand);
            return;
        }

        if (trimmed.equals("python") || trimmed.startsWith("python ")) {
            executePythonCommand(rawCommand);
            return;
        }

        executePythonSnippet(rawCommand, "python");
    }

    private void enterInteractiveMode() {
        mConsoleReplMode = true;
        mReplPendingBlock.setLength(0);
        appendConsoleRaw("Interactive mode enabled. Use :exit to leave, :reset to clear context.\n");
        updateConsolePrompt();
    }

    private void executePythonCommand(@NonNull String rawCommand) {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        List<String> tokens = parseCommandArgs(rawCommand);
        if (tokens.isEmpty()) {
            updateStatus("Empty command");
            return;
        }

        if (tokens.size() == 1) {
            enterInteractiveMode();
            return;
        }

        String firstArg = tokens.get(1);
        if ("-i".equals(firstArg)) {
            enterInteractiveMode();
            return;
        }

        if ("-V".equals(firstArg) || "--version".equals(firstArg)) {
            executePythonSnippet("import sys\nprint(sys.version)", "python --version");
            return;
        }

        if ("-c".equals(firstArg)) {
            if (tokens.size() < 3) {
                updateStatus("python -c requires code");
                return;
            }
            String code = joinArgs(tokens.subList(2, tokens.size()));
            executePythonSnippet(code, "python -c");
            return;
        }

        if ("-m".equals(firstArg)) {
            if (tokens.size() < 3) {
                updateStatus("python -m requires module name");
                return;
            }
            String module = tokens.get(2);
            List<String> moduleArgs = tokens.size() > 3
                    ? new ArrayList<>(tokens.subList(3, tokens.size()))
                    : new ArrayList<>();
            executePythonModule(module, moduleArgs);
            return;
        }

        if (firstArg.startsWith("-")) {
            updateStatus("Unsupported python option: " + firstArg);
            return;
        }

        String scriptPath = tokens.get(1);
        PythonRuntime.PythonEnvironment env = mPythonEnvironment;
        File scriptCandidate = new File(scriptPath);
        if (!scriptCandidate.isAbsolute()) {
            scriptCandidate = new File(env.workingDir, scriptPath);
        }
        boolean treatAsScript = scriptCandidate.isFile()
                || scriptPath.endsWith(".py")
                || scriptPath.contains("/");

        if (treatAsScript) {
            List<String> scriptArgs = tokens.size() > 2
                    ? new ArrayList<>(tokens.subList(2, tokens.size()))
                    : new ArrayList<>();
            executePythonScript(scriptPath, scriptArgs);
            return;
        }

        String snippet = rawCommand.substring(rawCommand.indexOf("python") + "python".length()).trim();
        executePythonSnippet(snippet, "python");
    }

    private void executePythonModule(@NonNull String module,
                                     @NonNull List<String> args) {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        final List<String> moduleArgs = new ArrayList<>(args);
        final String title = "python -m " + module
                + (moduleArgs.isEmpty() ? "" : " " + joinArgs(moduleArgs));

        startPythonTask(title + " ...", () -> {
            PythonRuntime.PythonEnvironment env = requirePythonEnvironment();
            int exitCode = mPythonRuntime.runModule(env, module, moduleArgs);
            PythonRuntime.OutputBuffers output = mPythonRuntime.readOutputBuffers(env);
            postTaskResult(title, exitCode, output);
        });
    }

    private void executePythonScript(@NonNull String scriptPath,
                                     @NonNull List<String> args) {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        final String rawPath = scriptPath;
        final List<String> scriptArgs = new ArrayList<>(args);
        final String title = "python " + rawPath
                + (scriptArgs.isEmpty() ? "" : " " + joinArgs(scriptArgs));

        startPythonTask(title + " ...", () -> {
            PythonRuntime.PythonEnvironment env = requirePythonEnvironment();
            File scriptFile = new File(rawPath);
            if (!scriptFile.isAbsolute()) {
                scriptFile = new File(env.workingDir, rawPath);
            }
            if (!scriptFile.isFile()) {
                throw new IllegalArgumentException("Script not found: " + scriptFile.getAbsolutePath());
            }

            int exitCode = mPythonRuntime.runScript(env, scriptFile, scriptArgs);
            PythonRuntime.OutputBuffers output = mPythonRuntime.readOutputBuffers(env);
            postTaskResult(title, exitCode, output);
        });
    }

    private void recordConsoleHistory(@NonNull String rawCommand) {
        String trimmed = rawCommand.trim();
        if (trimmed.isEmpty()) {
            return;
        }

        int lastIndex = mConsoleHistory.size() - 1;
        if (lastIndex < 0 || !mConsoleHistory.get(lastIndex).equals(rawCommand)) {
            mConsoleHistory.add(rawCommand);
            if (mConsoleHistory.size() > MAX_CONSOLE_HISTORY) {
                mConsoleHistory.remove(0);
            }
        }

        mConsoleHistoryIndex = -1;
        mConsoleHistoryDraft = null;
    }

    private boolean navigateConsoleHistory(int direction) {
        if (mConsoleHistory.isEmpty()) {
            return false;
        }

        if (direction < 0) {
            if (mConsoleHistoryIndex < 0) {
                mConsoleHistoryDraft = getCurrentConsoleInput();
                mConsoleHistoryIndex = mConsoleHistory.size() - 1;
            } else if (mConsoleHistoryIndex > 0) {
                mConsoleHistoryIndex--;
            }
        } else if (direction > 0) {
            if (mConsoleHistoryIndex < 0) {
                return false;
            }
            if (mConsoleHistoryIndex < mConsoleHistory.size() - 1) {
                mConsoleHistoryIndex++;
            } else {
                mConsoleHistoryIndex = -1;
                String draft = mConsoleHistoryDraft == null ? "" : mConsoleHistoryDraft;
                replaceCurrentConsoleInput(draft);
                return true;
            }
        } else {
            return false;
        }

        String value = mConsoleHistory.get(mConsoleHistoryIndex);
        replaceCurrentConsoleInput(value);
        return true;
    }

    private void executePipCommand(@NonNull String rawArgs) {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        List<String> parsedArgs = parseCommandArgs(rawArgs);
        if (parsedArgs.isEmpty()) {
            parsedArgs.add("--help");
        }

        final List<String> args = new ArrayList<>(parsedArgs);
        final String title = "pip " + joinArgs(args);

        startPythonTask(title + " ...", () -> {
            PythonRuntime.PythonEnvironment env = requirePythonEnvironment();
            File getPipScript = new File(getFilesDir(), "python/tools/get-pip.py");
            int ensureStatus = mPythonRuntime.ensurePip(env, getPipScript.isFile() ? getPipScript : null);
            int exitCode = ensureStatus;
            if (ensureStatus == 0) {
                exitCode = mPythonRuntime.runModule(env, "pip", args);
            }
            PythonRuntime.OutputBuffers output = mPythonRuntime.readOutputBuffers(env);
            postTaskResult(title, exitCode, output);
        });
    }

    private void executeReplInput(@NonNull String rawLine) {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        String line = rawLine.replace("\r", "");

        if (mReplPendingBlock.length() > 0) {
            if (line.trim().isEmpty()) {
                String snippet = mReplPendingBlock.toString();
                mReplPendingBlock.setLength(0);
                updateConsolePrompt();
                executeReplSnippet(snippet);
                return;
            }
            mReplPendingBlock.append(line).append('\n');
            updateConsolePrompt();
            return;
        }

        if (line.trim().endsWith(":")) {
            mReplPendingBlock.append(line).append('\n');
            appendConsoleRaw("...\n");
            updateConsolePrompt();
            return;
        }

        updateConsolePrompt();
        executeReplSnippet(line);
    }

    private void executeReplSnippet(@NonNull String snippet) {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        final String source = snippet;
        startPythonTask("python >>>", () -> {
            PythonRuntime.PythonEnvironment env = requirePythonEnvironment();
            int exitCode = mPythonRuntime.runReplSnippet(env, source);
            PythonRuntime.OutputBuffers output = mPythonRuntime.readOutputBuffers(env);
            postTaskResult("python >>>", exitCode, output);
        });
    }

    private void executePythonSnippet(@NonNull String code,
                                      @NonNull String title) {
        if (mPythonEnvironment == null) {
            updateStatus("Please install/import official Python package first");
            return;
        }

        final String snippet = code.trim();
        if (snippet.isEmpty()) {
            updateStatus("Empty command");
            return;
        }

        startPythonTask(title + " ...", () -> {
            PythonRuntime.PythonEnvironment env = requirePythonEnvironment();
            int exitCode = mPythonRuntime.runCode(env, snippet);
            PythonRuntime.OutputBuffers output = mPythonRuntime.readOutputBuffers(env);
            postTaskResult(title, exitCode, output);
        });
    }

    @NonNull
    private static List<String> parseCommandArgs(@NonNull String commandLine) {
        List<String> args = new ArrayList<>();
        StringBuilder current = new StringBuilder();
        boolean escaped = false;
        char quote = 0;

        for (int i = 0; i < commandLine.length(); i++) {
            char ch = commandLine.charAt(i);
            if (escaped) {
                current.append(ch);
                escaped = false;
                continue;
            }
            if (ch == '\\') {
                escaped = true;
                continue;
            }
            if (quote != 0) {
                if (ch == quote) {
                    quote = 0;
                } else {
                    current.append(ch);
                }
                continue;
            }
            if (ch == '\'' || ch == '"') {
                quote = ch;
                continue;
            }
            if (Character.isWhitespace(ch)) {
                if (current.length() > 0) {
                    args.add(current.toString());
                    current.setLength(0);
                }
                continue;
            }
            current.append(ch);
        }

        if (current.length() > 0) {
            args.add(current.toString());
        }
        return args;
    }

    @NonNull
    private static String joinArgs(@NonNull List<String> args) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < args.size(); i++) {
            if (i > 0) {
                sb.append(' ');
            }
            sb.append(args.get(i));
        }
        return sb.toString();
    }

    private void appendConsoleRaw(@NonNull String text) {
        appendOutputText(text);
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

            mPythonTaskStartedInCommand = true;
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
        showConsole(false);
        updateStatus("Task failed: " + (t.getMessage() == null ? t.getClass().getSimpleName() : t.getMessage()));
        appendOutputBlock("Task Error", 1, "", stackTrace(t));
        appendConsolePromptLine();
    }

    private void postTaskResult(@NonNull String title,
                                int exitCode,
                                @NonNull PythonRuntime.OutputBuffers output) {
        runOnUiThread(() -> {
            showConsole(false);
            appendOutputBlock(title, exitCode, output.stdout, output.stderr);
            updateStatus(title + " finished with exit code " + exitCode);
            appendConsolePromptLine();
        });
    }

    private void appendOutputBlock(@NonNull String title,
                                   int exitCode,
                                   @Nullable String stdout,
                                   @Nullable String stderr) {
        if (stdout != null && !stdout.isEmpty()) {
            appendOutputText(ensureTrailingNewline(stdout));
        }
        if (stderr != null && !stderr.isEmpty()) {
            appendOutputText(ensureTrailingNewline(stderr));
        }
    }

    @NonNull
    private static String ensureTrailingNewline(@NonNull String text) {
        return text.endsWith("\n") ? text : text + "\n";
    }

    private void appendOutputText(@NonNull String text) {
        Editable editable = mConsoleTerminal.getText();
        if (editable == null) {
            return;
        }

        editable.append(text);
        int overflow = editable.length() - OUTPUT_TRIM_LIMIT;
        if (overflow > 0) {
            editable.delete(0, overflow);
            mConsoleInputStart = Math.max(0, mConsoleInputStart - overflow);
        }

        mConsoleInputStart = Math.max(0, Math.min(mConsoleInputStart, editable.length()));
        mConsoleTerminal.setInputStart(mConsoleInputStart);
        mConsoleTerminal.setSelection(editable.length());
        scrollOutputToBottom();
    }

    private void scrollOutputToBottom() {
        if (mConsoleTerminal.getLayout() == null) {
            mConsoleTerminal.scrollTo(0, 0);
            return;
        }

        int scrollAmount = mConsoleTerminal.getLayout().getLineTop(mConsoleTerminal.getLineCount())
                - mConsoleTerminal.getHeight();
        if (scrollAmount > 0) {
            mConsoleTerminal.scrollTo(0, scrollAmount);
        } else {
            mConsoleTerminal.scrollTo(0, 0);
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

    private int dpToPx(int dp) {
        return Math.round(dp * getResources().getDisplayMetrics().density);
    }

    private void cycleWrapMode() {
        WrapMode[] wrapModes = WrapMode.values();
        mWrapModePreset = wrapModes[(mWrapModePreset.ordinal() + 1) % wrapModes.length];
        mEditor.getSettings().setWrapMode(mWrapModePreset);
        updateStatus("WrapMode: " + mWrapModePreset.name());
    }

    private void updateStatus(@NonNull String message) {
        mStatusBar.setText(message);
    }

    private void registerColorStyleForCurrentTheme() {
        int color = mIsDarkTheme ? 0xFFB5CEA8 : 0xFF098658;
        mEditor.registerTextStyle(STYLE_COLOR, color, 0);
    }
}
