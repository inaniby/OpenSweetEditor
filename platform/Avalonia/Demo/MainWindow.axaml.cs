using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using SweetEditor;
using SweetLine;
using EditorTextPosition = SweetEditor.TextPosition;
using EditorTextRange = SweetEditor.TextRange;
using SweetEditorDocument = SweetEditor.Document;
using SweetLineTextPosition = SweetLine.TextPosition;
using SweetLineTextRange = SweetLine.TextRange;

namespace Demo {
	public partial class MainWindow : Window {
		private const int StyleColor = (int)EditorTheme.STYLE_USER_BASE + 1;
		private const int IconClass = 1;
		private const int PerfHistoryCapacity = 90;
		private const string DefaultFileName = "example.cpp";
		private const string FallbackSampleCode =
			"// SweetEditor Demo\n" +
			"int main() {\n" +
			"    return 0;\n" +
			"}\n";

		private readonly EditorControl editor;
		private readonly ComboBox fileComboBox;
		private readonly TextBlock statusTextBlock;
		private readonly Border perfOverlay;
		private readonly TextBlock perfTextBlock;
		private readonly PerfGraphControl perfGraph;
		private readonly Border completionOverlay;
		private readonly TextBlock completionTextBlock;
		private readonly ContextMenu selectionContextMenu = new();
		private readonly List<string> demoFiles = new();
		private readonly DispatcherTimer perfTimer = new();
		private readonly Stopwatch perfClock = Stopwatch.StartNew();
		private readonly Process currentProcess = Process.GetCurrentProcess();
		private readonly List<double> fpsHistory = new(PerfHistoryCapacity);
		private readonly List<double> renderHistory = new(PerfHistoryCapacity);
		private readonly List<double> cpuHistory = new(PerfHistoryCapacity);

		private bool isDarkTheme = true;
		private WrapMode wrapModePreset = WrapMode.NONE;
		private bool suppressFileSelection;
		private bool decorationsLoaded = true;
		private string? currentFilePath;
		private long renderFrameCount;
		private double lastRenderMs;
		private int lastVisualLineCount;
		private int lastGutterIconCount;
		private int lastFoldMarkerCount;
		private int lastCompletionItemCount;
		private int completionUpdatesSinceSample;
		private int completionDismissSinceSample;
		private int contextMenuOpenSinceSample;
		private int selectionActionSinceSample;
		private int phantomActionSinceSample;
		private int snippetActionSinceSample;
		private int linkedEditActionSinceSample;
		private int foldActionSinceSample;
		private int textChangesSinceSample;
		private int scrollEventsSinceSample;
		private TimeSpan lastCpuSample;
		private long lastSampleTimestamp;
		private long lastAllocatedBytesSample;

		private DemoDecorationProvider? demoProvider;
		private DemoCompletionProvider? demoCompletionProvider;

		public MainWindow() {
			InitializeComponent();
			editor = this.FindControl<EditorControl>("Editor")
				?? throw new InvalidOperationException("Editor control was not found.");
			fileComboBox = this.FindControl<ComboBox>("FileComboBox")
				?? throw new InvalidOperationException("File combo box was not found.");
			statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock")
				?? throw new InvalidOperationException("Status text block was not found.");
			perfOverlay = this.FindControl<Border>("PerfOverlay")
				?? throw new InvalidOperationException("Perf overlay was not found.");
			perfTextBlock = this.FindControl<TextBlock>("PerfTextBlock")
				?? throw new InvalidOperationException("Perf text block was not found.");
			perfGraph = this.FindControl<PerfGraphControl>("PerfGraph")
				?? throw new InvalidOperationException("Perf graph was not found.");
			completionOverlay = this.FindControl<Border>("CompletionOverlay")
				?? throw new InvalidOperationException("Completion overlay was not found.");
			completionTextBlock = this.FindControl<TextBlock>("CompletionTextBlock")
				?? throw new InvalidOperationException("Completion text block was not found.");

			InitializeEditor();
			InitializePerformanceMonitor();
		}

		private void InitializeComponent() {
			AvaloniaXamlLoader.Load(this);
		}

		private void InitializeEditor() {
			editor.ApplyTheme(EditorTheme.Dark());
			editor.Settings.SetCurrentLineRenderMode(CurrentLineRenderMode.BACKGROUND);
			editor.Settings.SetFoldArrowMode(FoldArrowMode.AUTO);
			editor.Settings.SetMaxGutterIcons(1);
			editor.Settings.SetGutterVisible(true);
			editor.Settings.SetGutterSticky(true);
			editor.Settings.SetTabSize(4);
			editor.Settings.SetWrapMode(wrapModePreset);
			editor.SetEditorIconProvider(new DemoIconProvider());
			RegisterColorStyleForCurrentTheme();
			ConfigureDebugTracingIfEnabled();

			string? sweetLineWarning = null;
			try {
				DemoDecorationProvider.EnsureSweetLineReady(ResolveSyntaxFiles());
			} catch (Exception ex) {
				sweetLineWarning = ex.GetBaseException().Message;
			}

			demoProvider = new DemoDecorationProvider();
			editor.AddDecorationProvider(demoProvider);
			decorationsLoaded = true;

			demoCompletionProvider = new DemoCompletionProvider();
			editor.AddCompletionProvider(demoCompletionProvider);
			editor.TextChanged += OnEditorTextChangedForHighlightSync;
			editor.CompletionItemsUpdated += OnCompletionItemsUpdated;
			editor.CompletionDismissed += OnCompletionDismissed;
			editor.EditorContextMenu += OnEditorContextMenu;
			editor.InlayHintClick += (_, args) => UpdateStatus($"Inlay hint clicked at {args.Line}:{args.Column}");
			editor.GutterIconClick += (_, args) => UpdateStatus($"Gutter icon clicked line={args.Line} icon={args.IconId}");
			editor.FoldToggle += (_, args) => UpdateStatus($"Fold toggled at line {args.Line + 1}");
			BuildSelectionContextMenu();
			completionOverlay.IsVisible = true;
			completionTextBlock.Text = "No completion items yet.";

			SetupFileSpinner();
			Dispatcher.UIThread.Post(() => editor.Focus());
			UpdateStatus(string.IsNullOrWhiteSpace(sweetLineWarning)
				? "Ready"
				: $"Ready (SweetLine unavailable: {sweetLineWarning})");
		}

		private void InitializePerformanceMonitor() {
			lastCpuSample = currentProcess.TotalProcessorTime;
			lastSampleTimestamp = perfClock.ElapsedMilliseconds;
			lastAllocatedBytesSample = GC.GetTotalAllocatedBytes(false);
			perfTimer.Interval = TimeSpan.FromMilliseconds(350);
			perfTimer.Tick += (_, _) => RefreshPerformanceOverlay();
			perfTimer.Start();

			editor.RenderStatsUpdated += (_, args) => {
				renderFrameCount++;
				lastRenderMs = args.RenderMs;
				lastVisualLineCount = args.VisualLineCount;
				lastGutterIconCount = args.GutterIconCount;
				lastFoldMarkerCount = args.FoldMarkerCount;
			};

			editor.TextChanged += (_, args) => {
				textChangesSinceSample += Math.Max(1, args.Changes?.Count ?? 0);
			};

			editor.ScrollChanged += (_, _) => {
				scrollEventsSinceSample++;
			};

			RefreshPerformanceOverlay();
		}

		private void RefreshPerformanceOverlay() {
			long nowMs = perfClock.ElapsedMilliseconds;
			double elapsedSec = Math.Max(0.001, (nowMs - lastSampleTimestamp) / 1000.0);
			ScrollMetrics scroll = editor.GetScrollMetrics();
			int lineCount = editor.GetLineCount();
			EditorTextPosition cursor = editor.GetCursorPosition();
			string selectedText = editor.GetSelectedText();
			int selectedLength = selectedText.Length;

			TimeSpan cpuNow = currentProcess.TotalProcessorTime;
			double cpuMs = (cpuNow - lastCpuSample).TotalMilliseconds;
			double cpuPercent = cpuMs / (elapsedSec * 1000.0 * Math.Max(1, Environment.ProcessorCount)) * 100.0;
			double fps = renderFrameCount / elapsedSec;
			double textRate = textChangesSinceSample / elapsedSec;
			double scrollRate = scrollEventsSinceSample / elapsedSec;
			double memMb = currentProcess.WorkingSet64 / (1024.0 * 1024.0);
			double managedMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
			long allocatedNow = GC.GetTotalAllocatedBytes(false);
			double allocMb = (allocatedNow - lastAllocatedBytesSample) / (1024.0 * 1024.0);
			int threadCount = currentProcess.Threads.Count;
			double compRate = completionUpdatesSinceSample / elapsedSec;
			double menuRate = contextMenuOpenSinceSample / elapsedSec;
			double selectionRate = selectionActionSinceSample / elapsedSec;

			perfTextBlock.Text =
				$"SL      {(DemoDecorationProvider.IsEngineReady ? "ON " : "OFF")}\n" +
				$"FPS     {fps,6:F1}\n" +
				$"Render  {lastRenderMs,6:F2} ms\n" +
				$"CPU     {cpuPercent,6:F1}%\n" +
				$"Memory  {memMb,6:F1} MB\n" +
				$"Managed {managedMb,6:F1} MB\n" +
				$"Alloc   {allocMb,6:F2} MB/t\n" +
				$"Thread  {threadCount,6}\n" +
				$"Lines   {lineCount,6}\n" +
				$"Visual  {lastVisualLineCount,6}\n" +
				$"Gutter  {lastGutterIconCount,6}\n" +
				$"Fold    {lastFoldMarkerCount,6}\n" +
				$"Cursor  {cursor.Line + 1,4}:{cursor.Column + 1,-4}\n" +
				$"SelLen  {selectedLength,6}\n" +
				$"Comp    {lastCompletionItemCount,6} ({compRate,4:F1}/s)\n" +
				$"CDisp/s {completionDismissSinceSample / elapsedSec,6:F1}\n" +
				$"Menu/s  {menuRate,6:F1}\n" +
				$"SelOp/s {selectionRate,6:F1}\n" +
				$"Phantom {phantomActionSinceSample,6}\n" +
				$"Snippet {snippetActionSinceSample,6}\n" +
				$"Linked  {linkedEditActionSinceSample,6}\n" +
				$"FoldOp  {foldActionSinceSample,6}\n" +
				$"Scroll  {scroll.ScrollX,5:F0},{scroll.ScrollY,5:F0}\n" +
				$"Input/s T{textRate,4:F1} S{scrollRate,4:F1}";

			PushMetricSample(fpsHistory, fps);
			PushMetricSample(renderHistory, lastRenderMs);
			PushMetricSample(cpuHistory, cpuPercent);
			perfGraph.UpdateSeries(fpsHistory, renderHistory, cpuHistory);

			renderFrameCount = 0;
			textChangesSinceSample = 0;
			scrollEventsSinceSample = 0;
			completionUpdatesSinceSample = 0;
			completionDismissSinceSample = 0;
			contextMenuOpenSinceSample = 0;
			selectionActionSinceSample = 0;
			phantomActionSinceSample = 0;
			snippetActionSinceSample = 0;
			linkedEditActionSinceSample = 0;
			foldActionSinceSample = 0;
			lastCpuSample = cpuNow;
			lastSampleTimestamp = nowMs;
			lastAllocatedBytesSample = allocatedNow;
			perfOverlay.IsVisible = true;
		}

		private void ConfigureDebugTracingIfEnabled() {
			string? traceEnv = Environment.GetEnvironmentVariable("SWEETEDITOR_DEMO_TRACE_INPUT");
			if (!string.Equals(traceEnv, "1", StringComparison.Ordinal) &&
				!string.Equals(traceEnv, "true", StringComparison.OrdinalIgnoreCase)) {
				return;
			}

			editor.TextChanged += (_, args) => {
				if (args.Changes == null || args.Changes.Count == 0) {
					Console.WriteLine("[DEMO_INPUT] TextChanged changes=0");
					return;
				}
				TextChange first = args.Changes[0];
				string preview = (first.NewText ?? string.Empty)
					.Replace('\n', ' ')
					.Replace('\r', ' ');
				if (preview.Length > 32) {
					preview = preview[..32];
				}
				Console.WriteLine($"[DEMO_INPUT] TextChanged action={args.Action} changes={args.Changes.Count} firstText=\"{preview}\"");
			};

			editor.CursorChanged += (_, args) => {
				Console.WriteLine($"[DEMO_INPUT] Cursor line={args.CursorPosition.Line} column={args.CursorPosition.Column}");
			};

			editor.ScrollChanged += (_, args) => {
				Console.WriteLine($"[DEMO_INPUT] Scroll x={args.ScrollX:F2} y={args.ScrollY:F2}");
			};
		}

		private void SetupFileSpinner() {
			demoFiles.Clear();
			demoFiles.AddRange(ListDemoFiles());
			string? bundledDefaultFile = ResolveBundledDefaultFilePath();
			if (!string.IsNullOrEmpty(bundledDefaultFile) &&
				!demoFiles.Any(path => string.Equals(Path.GetFileName(path), DefaultFileName, StringComparison.OrdinalIgnoreCase))) {
				demoFiles.Insert(0, bundledDefaultFile);
			}

			suppressFileSelection = true;
			fileComboBox.ItemsSource = demoFiles
				.Select(Path.GetFileName)
				.ToList();
			suppressFileSelection = false;

			if (demoFiles.Count == 0) {
				LoadDemoText(DefaultFileName, LoadFallbackSampleCode(), null);
				return;
			}

			int preferredIndex = demoFiles.FindIndex(path =>
				string.Equals(Path.GetFileName(path), DefaultFileName, StringComparison.OrdinalIgnoreCase));
			if (preferredIndex < 0) {
				preferredIndex = 0;
			}

			suppressFileSelection = true;
			fileComboBox.SelectedIndex = preferredIndex;
			suppressFileSelection = false;
			LoadDemoFile(demoFiles[preferredIndex]);
		}

		private void LoadDemoFile(string filePath) {
			try {
				string text = File.ReadAllText(filePath);
				LoadDemoText(Path.GetFileName(filePath), text, filePath);
			} catch {
				LoadDemoText(Path.GetFileName(filePath), LoadFallbackSampleCode(), null);
			}
		}

		private void LoadDemoText(string fileName, string text, string? sourcePath) {
			string normalizedText = NormalizeNewlines(text);
			currentFilePath = sourcePath;
			demoProvider?.SetDocumentSource(fileName, normalizedText);
			editor.LoadDocument(new SweetEditorDocument(normalizedText));
			editor.SetMetadata(new DemoFileMetadata(fileName));
			ApplyInitialDemoView(fileName, normalizedText);
			if (decorationsLoaded) {
				editor.RequestDecorationRefresh();
				Dispatcher.UIThread.Post(() => editor.RequestDecorationRefresh());
			}
			UpdateStatus($"Loaded: {fileName}");
		}

		private void RegisterColorStyleForCurrentTheme() {
			int color = isDarkTheme ? unchecked((int)0xFFB5CEA8) : unchecked((int)0xFF098658);
			editor.EditorCoreInternal.RegisterTextStyle((uint)StyleColor, new TextStyle(color, 0, 0));
		}

		private static string NormalizeNewlines(string text) {
			return text.Replace("\r\n", "\n").Replace('\r', '\n');
		}

		private static List<string> ListDemoFiles() {
			string? resRoot = ResolveDemoResRoot();
			if (string.IsNullOrEmpty(resRoot)) {
				return new List<string>();
			}
			string filesDir = Path.Combine(resRoot, "files");
			if (!Directory.Exists(filesDir)) {
				return new List<string>();
			}
			return Directory
				.EnumerateFiles(filesDir, "*", SearchOption.TopDirectoryOnly)
				.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static List<string> ResolveSyntaxFiles() {
			string? resRoot = ResolveDemoResRoot();
			if (string.IsNullOrEmpty(resRoot)) {
				return new List<string>();
			}
			string syntaxDir = Path.Combine(resRoot, "syntaxes");
			if (!Directory.Exists(syntaxDir)) {
				return new List<string>();
			}
			return Directory
				.EnumerateFiles(syntaxDir, "*.json", SearchOption.AllDirectories)
				.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static string? ResolveDemoResRoot() {
			string? envPath = Environment.GetEnvironmentVariable("SWEETEDITOR_DEMO_RES_DIR");
			if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath)) {
				return Path.GetFullPath(envPath);
			}

			var starts = new List<string>();
			try {
				starts.Add(AppContext.BaseDirectory);
			} catch {
			}
			try {
				starts.Add(Directory.GetCurrentDirectory());
			} catch {
			}

			foreach (string start in starts) {
				DirectoryInfo? dir = new DirectoryInfo(start);
				while (dir != null) {
					string candidate1 = Path.Combine(dir.FullName, "_res");
					if (Directory.Exists(candidate1)) {
						return candidate1;
					}
					string candidate2 = Path.Combine(dir.FullName, "platform", "_res");
					if (Directory.Exists(candidate2)) {
						return candidate2;
					}
					dir = dir.Parent;
				}
			}

			return null;
		}

		private static string LoadFallbackSampleCode() {
			string[] candidates = {
				Path.Combine(AppContext.BaseDirectory, "Assets", DefaultFileName),
				Path.Combine(Directory.GetCurrentDirectory(), "platform", "Avalonia", "Demo", "Assets", DefaultFileName),
				Path.Combine(Directory.GetCurrentDirectory(), "Demo", "Assets", DefaultFileName),
			};

			for (int i = 0; i < candidates.Length; i++) {
				string candidate = candidates[i];
				try {
					if (File.Exists(candidate)) {
						return NormalizeNewlines(File.ReadAllText(candidate));
					}
				} catch {
				}
			}

			return FallbackSampleCode;
		}

		private static string? ResolveBundledDefaultFilePath() {
			string[] candidates = {
				Path.Combine(AppContext.BaseDirectory, "Assets", DefaultFileName),
				Path.Combine(Directory.GetCurrentDirectory(), "platform", "Avalonia", "Demo", "Assets", DefaultFileName),
				Path.Combine(Directory.GetCurrentDirectory(), "Demo", "Assets", DefaultFileName),
			};

			for (int i = 0; i < candidates.Length; i++) {
				string candidate = candidates[i];
				try {
					if (File.Exists(candidate)) {
						return Path.GetFullPath(candidate);
					}
				} catch {
				}
			}

			return null;
		}

		private void ApplyInitialDemoView(string fileName, string text) {
			if (!string.Equals(fileName, DefaultFileName, StringComparison.OrdinalIgnoreCase)) {
				return;
			}

			int targetLine = FindLineContaining(text, "switch (line[i]) {");
			if (targetLine < 0) {
				targetLine = FindLineContaining(text, "void log(Level level, const std::string& msg) {");
			}
			if (targetLine < 0) {
				return;
			}

			Dispatcher.UIThread.Post(() => {
				editor.SetCursorPosition(new EditorTextPosition {
					Line = targetLine,
					Column = 0
				});
			}, DispatcherPriority.Background);
		}

		private static int FindLineContaining(string text, string value) {
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value)) {
				return -1;
			}

			string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			for (int i = 0; i < lines.Length; i++) {
				if (lines[i].Contains(value, StringComparison.Ordinal)) {
					return i;
				}
			}
			return -1;
		}

		private void UpdateStatus(string message) {
			statusTextBlock.Text = message;
		}

		private string BuildEditorTextSnapshot() {
			int lineCount = editor.GetLineCount();
			if (lineCount <= 0) {
				return string.Empty;
			}

			var builder = new StringBuilder(lineCount * 32);
			for (int line = 0; line < lineCount; line++) {
				if (line > 0) {
					builder.Append('\n');
				}
				builder.Append(editor.GetLineText(line));
			}
			return builder.ToString();
		}

		private string ResolveActiveFileName() {
			if (!string.IsNullOrWhiteSpace(currentFilePath)) {
				return Path.GetFileName(currentFilePath);
			}
			if (editor.GetMetadata<DemoFileMetadata>() is DemoFileMetadata metadata &&
				!string.IsNullOrWhiteSpace(metadata.FileName)) {
				return metadata.FileName;
			}
			return DefaultFileName;
		}

		private static void PushMetricSample(List<double> history, double value) {
			history.Add(value);
			if (history.Count > PerfHistoryCapacity) {
				history.RemoveAt(0);
			}
		}

		private void BuildSelectionContextMenu() {
			var copy = new MenuItem { Header = "Copy Selection" };
			copy.Click += async (_, _) => await CopySelectionAsync();
			var cut = new MenuItem { Header = "Cut Selection" };
			cut.Click += async (_, _) => await CutSelectionAsync();
			var paste = new MenuItem { Header = "Paste" };
			paste.Click += async (_, _) => await PasteAsync();
			var selectWord = new MenuItem { Header = "Select Word" };
			selectWord.Click += (_, _) => SelectWordAtCursor();
			var selectAll = new MenuItem { Header = "Select All" };
			selectAll.Click += (_, _) => {
				editor.SelectAll();
				selectionActionSinceSample++;
				UpdateStatus("Selected all");
			};
			var upper = new MenuItem { Header = "Uppercase Selection" };
			upper.Click += (_, _) => UppercaseSelection();
			var wrapComment = new MenuItem { Header = "Wrap /* ... */" };
			wrapComment.Click += (_, _) => WrapSelectionWithComment();

			selectionContextMenu.Items.Clear();
			selectionContextMenu.Items.Add(copy);
			selectionContextMenu.Items.Add(cut);
			selectionContextMenu.Items.Add(paste);
			selectionContextMenu.Items.Add(new Separator());
			selectionContextMenu.Items.Add(selectWord);
			selectionContextMenu.Items.Add(selectAll);
			selectionContextMenu.Items.Add(new Separator());
			selectionContextMenu.Items.Add(upper);
			selectionContextMenu.Items.Add(wrapComment);
		}

		private void ShowSelectionContextMenu() {
			UpdateSelectionMenuState();
			selectionContextMenu.Open(editor);
			contextMenuOpenSinceSample++;
		}

		private void UpdateSelectionMenuState() {
			bool hasSelection = !string.IsNullOrEmpty(editor.GetSelectedText());
			for (int i = 0; i < selectionContextMenu.Items.Count; i++) {
				if (selectionContextMenu.Items[i] is not MenuItem item) {
					continue;
				}
				string header = item.Header?.ToString() ?? string.Empty;
				if (header.Contains("Selection", StringComparison.Ordinal)) {
					item.IsEnabled = hasSelection;
				}
				if (header.Contains("Wrap /*", StringComparison.Ordinal)) {
					item.IsEnabled = hasSelection;
				}
			}
		}

		private async Task CopySelectionAsync() {
			string text = editor.GetSelectedText();
			if (string.IsNullOrEmpty(text)) {
				UpdateStatus("Copy skipped: empty selection");
				return;
			}
			IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
			if (clipboard == null) {
				UpdateStatus("Clipboard unavailable");
				return;
			}
			await clipboard.SetTextAsync(text);
			selectionActionSinceSample++;
			UpdateStatus($"Copied {text.Length} chars");
		}

		private async Task CutSelectionAsync() {
			string text = editor.GetSelectedText();
			if (string.IsNullOrEmpty(text)) {
				UpdateStatus("Cut skipped: empty selection");
				return;
			}
			EditorTextRange range = editor.GetSelection();
			IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
			if (clipboard != null) {
				await clipboard.SetTextAsync(text);
			}
			editor.ReplaceText(range, string.Empty);
			selectionActionSinceSample++;
			UpdateStatus($"Cut {text.Length} chars");
		}

		private async Task PasteAsync() {
			IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
			if (clipboard == null) {
				UpdateStatus("Clipboard unavailable");
				return;
			}
			string? text = await clipboard.GetTextAsync();
			if (string.IsNullOrEmpty(text)) {
				UpdateStatus("Paste skipped: clipboard empty");
				return;
			}
			string selected = editor.GetSelectedText();
			if (string.IsNullOrEmpty(selected)) {
				editor.InsertText(text);
			} else {
				editor.ReplaceText(editor.GetSelection(), text);
			}
			selectionActionSinceSample++;
			UpdateStatus($"Pasted {text.Length} chars");
		}

		private void SelectWordAtCursor() {
			EditorTextRange range = editor.GetWordRangeAtCursor();
			if (range.End.Line == range.Start.Line && range.End.Column == range.Start.Column) {
				UpdateStatus("Select word: none");
				return;
			}
			editor.SetSelection(range);
			selectionActionSinceSample++;
			UpdateStatus($"Selected word at {range.Start.Line}:{range.Start.Column}");
		}

		private void UppercaseSelection() {
			string selectedText = editor.GetSelectedText();
			if (string.IsNullOrEmpty(selectedText)) {
				UpdateStatus("Uppercase skipped: empty selection");
				return;
			}
			editor.ReplaceText(editor.GetSelection(), selectedText.ToUpperInvariant());
			selectionActionSinceSample++;
			UpdateStatus("Selection uppercased");
		}

		private void WrapSelectionWithComment() {
			string selectedText = editor.GetSelectedText();
			if (string.IsNullOrEmpty(selectedText)) {
				UpdateStatus("Wrap skipped: empty selection");
				return;
			}
			editor.ReplaceText(editor.GetSelection(), $"/* {selectedText} */");
			selectionActionSinceSample++;
			UpdateStatus("Selection wrapped with comment");
		}

		private void UpdateCompletionPreview(IReadOnlyList<CompletionItem> items) {
			if (items.Count == 0) {
				completionTextBlock.Text = "No completion items.";
				return;
			}
			var preview = new StringBuilder();
			int count = Math.Min(6, items.Count);
			for (int i = 0; i < count; i++) {
				CompletionItem item = items[i];
				preview.Append("• ").Append(item.Label);
				if (!string.IsNullOrWhiteSpace(item.Detail)) {
					preview.Append("  ").Append(item.Detail);
				}
				if (i < count - 1) {
					preview.Append('\n');
				}
			}
			completionTextBlock.Text = preview.ToString();
		}

		private LinkedEditingModel? BuildLinkedEditingModelForCurrentWord() {
			EditorTextRange initialRange = editor.GetSelection();
			string selectedText = editor.GetSelectedText();
			string word = selectedText;
			EditorTextRange range = initialRange;

			if (string.IsNullOrWhiteSpace(word) || word.Contains('\n')) {
				word = editor.GetWordAtCursor();
				range = editor.GetWordRangeAtCursor();
			}

			if (string.IsNullOrWhiteSpace(word) ||
				(range.Start.Line == range.End.Line && range.Start.Column == range.End.Column)) {
				return null;
			}

			List<EditorTextRange> ranges = FindWordRanges(word, 8);
			if (ranges.Count < 2) {
				return null;
			}

			editor.SetSelection(ranges[0]);
			editor.SetCursorPosition(ranges[0].End);

			var group = new LinkedEditingGroup {
				Index = 0,
				DefaultText = word,
				Ranges = ranges
			};
			return new LinkedEditingModel {
				Groups = new List<LinkedEditingGroup> { group }
			};
		}

		private List<EditorTextRange> FindWordRanges(string word, int maxCount) {
			var ranges = new List<EditorTextRange>();
			if (string.IsNullOrEmpty(word) || maxCount <= 0) {
				return ranges;
			}

			int lineCount = editor.GetLineCount();
			for (int line = 0; line < lineCount && ranges.Count < maxCount; line++) {
				string lineText = editor.GetLineText(line);
				int searchIndex = 0;
				while (searchIndex <= lineText.Length - word.Length && ranges.Count < maxCount) {
					int found = lineText.IndexOf(word, searchIndex, StringComparison.Ordinal);
					if (found < 0) {
						break;
					}
					if (IsWordBoundary(lineText, found - 1) &&
						IsWordBoundary(lineText, found + word.Length)) {
						ranges.Add(new EditorTextRange(
							new EditorTextPosition { Line = line, Column = found },
							new EditorTextPosition { Line = line, Column = found + word.Length }));
					}
					searchIndex = found + word.Length;
				}
			}
			return ranges;
		}

		private static bool IsWordBoundary(string text, int index) {
			if (index < 0 || index >= text.Length) {
				return true;
			}
			char ch = text[index];
			return !(char.IsLetterOrDigit(ch) || ch == '_');
		}

		private void OnFileSelectionChanged(object? sender, SelectionChangedEventArgs e) {
			if (suppressFileSelection) {
				return;
			}
			int index = fileComboBox.SelectedIndex;
			if (index < 0 || index >= demoFiles.Count) {
				return;
			}
			LoadDemoFile(demoFiles[index]);
		}

		private void OnUndo(object? sender, RoutedEventArgs e) {
			if (editor.CanUndo()) {
				editor.Undo();
				UpdateStatus("Undo");
			} else {
				UpdateStatus("Nothing to undo");
			}
		}

		private void OnRedo(object? sender, RoutedEventArgs e) {
			if (editor.CanRedo()) {
				editor.Redo();
				UpdateStatus("Redo");
			} else {
				UpdateStatus("Nothing to redo");
			}
		}

		private void OnSelectAll(object? sender, RoutedEventArgs e) {
			editor.SelectAll();
			selectionActionSinceSample++;
			UpdateStatus("Selected all");
		}

		private void OnGetSelection(object? sender, RoutedEventArgs e) {
			string selectedText = editor.GetSelectedText();
			if (string.IsNullOrEmpty(selectedText)) {
				UpdateStatus("Selection: (empty)");
				return;
			}
			selectionActionSinceSample++;

			EditorTextRange selection = editor.GetSelection();
			string preview = selectedText
				.Replace('\n', ' ')
				.Replace('\r', ' ');
			if (preview.Length > 48) {
				preview = preview[..48] + "...";
			}
			UpdateStatus($"Selection {selection.Start.Line}:{selection.Start.Column}-{selection.End.Line}:{selection.End.Column} \"{preview}\"");
		}

		private void OnLoadDecorations(object? sender, RoutedEventArgs e) {
			if (demoProvider == null) {
				UpdateStatus("No decoration provider");
				return;
			}

			demoProvider.SetDocumentSource(ResolveActiveFileName(), BuildEditorTextSnapshot());

			if (!decorationsLoaded) {
				editor.AddDecorationProvider(demoProvider);
				decorationsLoaded = true;
			}
			editor.RequestDecorationRefresh();
			Dispatcher.UIThread.Post(() => editor.RequestDecorationRefresh());
			UpdateStatus("Applied all decorations");
		}

		private void OnClearDecorations(object? sender, RoutedEventArgs e) {
			if (demoProvider != null && decorationsLoaded) {
				editor.RemoveDecorationProvider(demoProvider);
				decorationsLoaded = false;
			}
			editor.ClearAllDecorations();
			editor.Flush();
			UpdateStatus("Cleared decorations");
		}

		private void OnToggleTheme(object? sender, RoutedEventArgs e) {
			isDarkTheme = !isDarkTheme;
			editor.ApplyTheme(isDarkTheme ? EditorTheme.Dark() : EditorTheme.Light());
			RegisterColorStyleForCurrentTheme();
			editor.RequestDecorationRefresh();
			UpdateStatus(isDarkTheme ? "Switched to dark theme" : "Switched to light theme");
		}

		private void OnCycleWrapMode(object? sender, RoutedEventArgs e) {
			WrapMode[] wrapModes = Enum.GetValues<WrapMode>();
			wrapModePreset = wrapModes[((int)wrapModePreset + 1) % wrapModes.Length];
			editor.Settings.SetWrapMode(wrapModePreset);
			UpdateStatus($"WrapMode: {wrapModePreset}");
		}

		private void OnReloadCurrentFile(object? sender, RoutedEventArgs e) {
			if (!string.IsNullOrWhiteSpace(currentFilePath) && File.Exists(currentFilePath)) {
				LoadDemoFile(currentFilePath);
				return;
			}
			LoadDemoText(DefaultFileName, LoadFallbackSampleCode(), null);
		}

		private void OnTriggerCompletion(object? sender, RoutedEventArgs e) {
			editor.TriggerCompletion();
			UpdateStatus("Completion requested");
		}

		private void OnAddPhantom(object? sender, RoutedEventArgs e) {
			EditorTextPosition cursor = editor.GetCursorPosition();
			string phantomText = " /* manual phantom demo */";
			if (demoProvider != null) {
				demoProvider.SetManualPhantom(cursor.Line, cursor.Column, phantomText);
				if (decorationsLoaded) {
					editor.RequestDecorationRefresh();
				} else {
					editor.SetLinePhantomTexts(cursor.Line, new List<PhantomText> { new(cursor.Column, phantomText) });
					editor.Flush();
				}
			}
			phantomActionSinceSample++;
			UpdateStatus($"Added phantom at {cursor.Line + 1}:{cursor.Column + 1}");
		}

		private void OnClearPhantom(object? sender, RoutedEventArgs e) {
			demoProvider?.ClearManualPhantoms();
			editor.ClearPhantomTexts();
			if (decorationsLoaded) {
				editor.RequestDecorationRefresh();
			} else {
				editor.Flush();
			}
			phantomActionSinceSample++;
			UpdateStatus("Cleared manual phantom text");
		}

		private void OnOpenSelectionMenu(object? sender, RoutedEventArgs e) {
			ShowSelectionContextMenu();
			UpdateStatus("Selection menu opened");
		}

		private void OnInsertSnippet(object? sender, RoutedEventArgs e) {
			editor.InsertSnippet("if (${1:condition}) {\n\t$0\n}");
			snippetActionSinceSample++;
			UpdateStatus("Inserted snippet demo");
		}

		private void OnStartLinkedEdit(object? sender, RoutedEventArgs e) {
			LinkedEditingModel? model = BuildLinkedEditingModelForCurrentWord();
			if (model == null) {
				UpdateStatus("Linked edit skipped: need 2+ matching words");
				return;
			}
			editor.StartLinkedEditing(model);
			linkedEditActionSinceSample++;
			UpdateStatus("Linked editing started");
		}

		private void OnFoldAll(object? sender, RoutedEventArgs e) {
			editor.FoldAll();
			foldActionSinceSample++;
			UpdateStatus("Folded all regions");
		}

		private void OnUnfoldAll(object? sender, RoutedEventArgs e) {
			editor.UnfoldAll();
			foldActionSinceSample++;
			UpdateStatus("Unfolded all regions");
		}

		private void OnCompletionItemsUpdated(object? sender, EditorControl.CompletionItemsEventArgs e) {
			lastCompletionItemCount = e.Items.Count;
			completionUpdatesSinceSample++;
			UpdateCompletionPreview(e.Items);
		}

		private void OnCompletionDismissed(object? sender, EventArgs e) {
			completionDismissSinceSample++;
			completionTextBlock.Text = "Completion dismissed.";
		}

		private void OnEditorContextMenu(object? sender, EditorControl.EditorContextMenuEventArgs e) {
			ShowSelectionContextMenu();
			UpdateStatus($"Context menu at {e.Position.X:F0},{e.Position.Y:F0}");
		}

		private void OnEditorTextChangedForHighlightSync(object? sender, EditorControl.TextChangedEventArgs e) {
			if (!decorationsLoaded || demoProvider == null) {
				return;
			}

			demoProvider.SetDocumentSource(ResolveActiveFileName(), BuildEditorTextSnapshot());
		}

		protected override void OnClosed(EventArgs e) {
			perfTimer.Stop();
			editor.TextChanged -= OnEditorTextChangedForHighlightSync;
			editor.CompletionItemsUpdated -= OnCompletionItemsUpdated;
			editor.CompletionDismissed -= OnCompletionDismissed;
			editor.EditorContextMenu -= OnEditorContextMenu;
			base.OnClosed(e);
		}

		private sealed class DemoCompletionProvider : ICompletionProvider {
			private static readonly HashSet<string> TriggerChars = [".", ":"];

			public bool IsTriggerCharacter(string ch) => TriggerChars.Contains(ch);

			public void ProvideCompletions(CompletionContext context, ICompletionReceiver receiver) {
				if (context.TriggerKind == CompletionTriggerKind.Character && context.TriggerCharacter == ".") {
					var items = new List<CompletionItem> {
						new() { Label = "length", Detail = "size_t", Kind = CompletionItem.KIND_PROPERTY, InsertText = "length()", SortKey = "a_length" },
						new() { Label = "push_back", Detail = "void push_back(T)", Kind = CompletionItem.KIND_FUNCTION, InsertText = "push_back()", SortKey = "b_push_back" },
						new() { Label = "begin", Detail = "iterator", Kind = CompletionItem.KIND_FUNCTION, InsertText = "begin()", SortKey = "c_begin" },
						new() { Label = "end", Detail = "iterator", Kind = CompletionItem.KIND_FUNCTION, InsertText = "end()", SortKey = "d_end" },
						new() { Label = "size", Detail = "size_t", Kind = CompletionItem.KIND_FUNCTION, InsertText = "size()", SortKey = "e_size" }
					};
					receiver.Accept(new CompletionResult(items));
					return;
				}

				Task.Run(async () => {
					await Task.Delay(200);
					if (receiver.IsCancelled) {
						return;
					}

					var items = new List<CompletionItem> {
						new() { Label = "std::string", Detail = "class", Kind = CompletionItem.KIND_CLASS, InsertText = "std::string", SortKey = "a_string" },
						new() { Label = "std::vector", Detail = "template class", Kind = CompletionItem.KIND_CLASS, InsertText = "std::vector<>", SortKey = "b_vector" },
						new() { Label = "std::cout", Detail = "ostream", Kind = CompletionItem.KIND_VARIABLE, InsertText = "std::cout", SortKey = "c_cout" },
						new() { Label = "if", Detail = "snippet", Kind = CompletionItem.KIND_SNIPPET, InsertText = "if (${1:condition}) {\n\t$0\n}", InsertTextFormat = CompletionItem.INSERT_TEXT_FORMAT_SNIPPET, SortKey = "d_if" },
						new() { Label = "for", Detail = "snippet", Kind = CompletionItem.KIND_SNIPPET, InsertText = "for (int ${1:i} = 0; ${1:i} < ${2:n}; ++${1:i}) {\n\t$0\n}", InsertTextFormat = CompletionItem.INSERT_TEXT_FORMAT_SNIPPET, SortKey = "e_for" },
						new() { Label = "class", Detail = "snippet - class definition", Kind = CompletionItem.KIND_SNIPPET, InsertText = "class ${1:ClassName} {\npublic:\n\t${1:ClassName}() {$2}\n\t~${1:ClassName}() {$3}\n$0\n};", InsertTextFormat = CompletionItem.INSERT_TEXT_FORMAT_SNIPPET, SortKey = "f_class" },
						new() { Label = "return", Detail = "keyword", Kind = CompletionItem.KIND_KEYWORD, InsertText = "return ", SortKey = "g_return" }
					};
					receiver.Accept(new CompletionResult(items));
				});
			}
		}

		private sealed class DemoDecorationProvider : IDecorationProvider {
			private const string DefaultAnalysisFileName = "example.cpp";
			private const int MaxDynamicDiagnostics = 8;
			private const string PhantomMemberStub =
				"\n    void debugTrace(const std::string& tag) {\n        log(DEBUG, tag);\n    }";
			private const string PhantomInlineHint = " /* demo phantom */";

			private static HighlightEngine? highlightEngine;
			public static bool IsEngineReady => highlightEngine != null;

			private readonly object stateLock = new();
			private DocumentAnalyzer? documentAnalyzer;
			private DocumentHighlight? cacheHighlight;
			private string sourceFileName = DefaultAnalysisFileName;
			private string sourceText = string.Empty;
			private string analyzedFileName = DefaultAnalysisFileName;
			private readonly Dictionary<int, List<DecorationResult.PhantomTextItem>> manualPhantoms = new();

			public DecorationType Capabilities =>
				DecorationType.SyntaxHighlight |
				DecorationType.IndentGuide |
				DecorationType.FoldRegion |
				DecorationType.SeparatorGuide |
				DecorationType.GutterIcon |
				DecorationType.InlayHint |
				DecorationType.PhantomText |
				DecorationType.Diagnostic;

			public static bool EnsureSweetLineReady(IReadOnlyList<string> syntaxFiles) {
				if (highlightEngine != null) {
					return true;
				}
				if (syntaxFiles == null || syntaxFiles.Count == 0) {
					throw new InvalidOperationException("No syntax files configured");
				}

				var engine = new HighlightEngine(new HighlightConfig(false, false));
				RegisterStyleMap(engine);

				foreach (string syntaxFile in syntaxFiles) {
					string syntaxJson = File.ReadAllText(syntaxFile);
					try {
						engine.CompileSyntaxFromJson(syntaxJson);
					} catch (SyntaxCompileError ex) {
						throw new InvalidOperationException($"Failed to compile syntax file: {syntaxFile}", ex);
					}
				}

				highlightEngine = engine;
				return true;
			}

			public void SetDocumentSource(string fileName, string text) {
				lock (stateLock) {
					sourceFileName = string.IsNullOrWhiteSpace(fileName) ? DefaultAnalysisFileName : fileName;
					sourceText = text ?? string.Empty;
					documentAnalyzer = null;
					cacheHighlight = null;
					analyzedFileName = sourceFileName;
				}
			}

			public void SetManualPhantom(int line, int column, string text) {
				if (line < 0 || column < 0 || string.IsNullOrEmpty(text)) {
					return;
				}
				lock (stateLock) {
					if (!manualPhantoms.TryGetValue(line, out List<DecorationResult.PhantomTextItem>? list)) {
						list = new List<DecorationResult.PhantomTextItem>();
						manualPhantoms[line] = list;
					}
					list.Clear();
					list.Add(new DecorationResult.PhantomTextItem(column, text));
				}
			}

			public void ClearManualPhantoms() {
				lock (stateLock) {
					manualPhantoms.Clear();
				}
			}

			public void ProvideDecorations(DecorationContext context, IDecorationReceiver receiver) {
				var diagnostics = new Dictionary<int, List<DecorationResult.DiagnosticItem>>();

				DecorationResult sweetLineResult = BuildSweetLineDecorationResult(context, diagnostics);
				receiver.Accept(sweetLineResult);

				Task.Run(async () => {
					await Task.Delay(500);
					if (receiver.IsCancelled) {
						return;
					}

					receiver.Accept(new DecorationResult {
						Diagnostics = diagnostics,
						DiagnosticsMode = DecorationApplyMode.REPLACE_ALL
					});
				});
			}

			private DecorationResult BuildSweetLineDecorationResult(
				DecorationContext context,
				Dictionary<int, List<DecorationResult.DiagnosticItem>> dynamicDiagnostics) {
				var dynamicPhantoms = new Dictionary<int, List<DecorationResult.PhantomTextItem>>();
				var syntaxSpans = new Dictionary<int, List<DecorationResult.SpanItem>>();
				var inlayHints = new Dictionary<int, List<DecorationResult.InlayHintItem>>();
				var gutterIcons = new Dictionary<int, List<int>>();
				var indentGuides = new List<DecorationResult.IndentGuideItem>();
				var foldRegions = new List<DecorationResult.FoldRegionItem>();
				var separatorGuides = new List<DecorationResult.SeparatorGuideItem>();
				var seenColorHints = new HashSet<string>();
				var phantomLines = new HashSet<int>();
				var seenDiagnostics = new HashSet<string>();
				int diagnosticCount = 0;
				TokenRangeInfo? firstKeywordRange = null;

				DocumentAnalyzer? analyzerSnapshot;
				DocumentHighlight? highlightSnapshot;
				string textSnapshot;
				Dictionary<int, List<DecorationResult.PhantomTextItem>> manualPhantomsSnapshot;

				lock (stateLock) {
					if (highlightEngine == null) {
						return new DecorationResult {
							PhantomTexts = dynamicPhantoms,
							PhantomTextsMode = DecorationApplyMode.REPLACE_ALL
						};
					}

					string currentFileName = ResolveCurrentFileName(context);
					if (!string.Equals(currentFileName, sourceFileName, StringComparison.Ordinal)) {
						sourceFileName = currentFileName;
					}

					if (cacheHighlight == null || documentAnalyzer == null || !string.Equals(currentFileName, analyzedFileName, StringComparison.Ordinal)) {
						using var sweetDoc = new SweetLine.Document(BuildAnalysisUri(currentFileName), sourceText);
						documentAnalyzer = highlightEngine.LoadDocument(sweetDoc);
						cacheHighlight = documentAnalyzer?.Analyze();
						analyzedFileName = currentFileName;
					} else if (context.TextChanges.Count > 0 && documentAnalyzer != null) {
						foreach (TextChange change in context.TextChanges) {
							string newText = change.NewText ?? string.Empty;
							cacheHighlight = documentAnalyzer.AnalyzeIncremental(ConvertAsSLTextRange(change.Range), newText);
							sourceText = ApplyTextChange(sourceText, change.Range, newText);
						}
					}

					analyzerSnapshot = documentAnalyzer;
					highlightSnapshot = cacheHighlight;
					textSnapshot = sourceText;
					manualPhantomsSnapshot = CopyMap(manualPhantoms);
				}

				if (highlightSnapshot?.Lines == null || highlightSnapshot.Lines.Count == 0) {
					return new DecorationResult {
						PhantomTexts = dynamicPhantoms,
						PhantomTextsMode = DecorationApplyMode.REPLACE_ALL,
						SyntaxSpans = syntaxSpans,
						SyntaxSpansMode = DecorationApplyMode.MERGE,
						InlayHints = inlayHints,
						InlayHintsMode = DecorationApplyMode.REPLACE_RANGE,
						IndentGuides = indentGuides,
						IndentGuidesMode = DecorationApplyMode.REPLACE_ALL,
						FoldRegions = foldRegions,
						FoldRegionsMode = DecorationApplyMode.REPLACE_ALL,
						SeparatorGuides = separatorGuides,
						SeparatorGuidesMode = DecorationApplyMode.REPLACE_ALL,
						GutterIcons = gutterIcons,
						GutterIconsMode = DecorationApplyMode.REPLACE_ALL
					};
				}

				List<string> textLines = SplitLines(textSnapshot);
				int renderStartLine = Math.Max(0, context.VisibleStartLine);
				int maxLine = Math.Min(context.VisibleEndLine, highlightSnapshot.Lines.Count - 1);
				for (int i = renderStartLine; i <= maxLine; i++) {
					LineHighlight lineHighlight = highlightSnapshot.Lines[i];
					if (lineHighlight?.Spans == null) {
						continue;
					}
					foreach (TokenSpan token in lineHighlight.Spans) {
						AppendStyleSpan(syntaxSpans, token);
						AppendColorInlayHint(inlayHints, seenColorHints, textLines, token);
						AppendTextInlayHint(inlayHints, textLines, token);
						AppendSeparator(separatorGuides, textLines, token);
						AppendGutterIcons(gutterIcons, textLines, token);
						firstKeywordRange = AppendDynamicDemoDecorations(
							dynamicPhantoms,
							phantomLines,
							dynamicDiagnostics,
							seenDiagnostics,
							ref diagnosticCount,
							firstKeywordRange,
							textLines,
							token);
					}
				}
				AppendDiagnosticFallbackIfNeeded(dynamicDiagnostics, seenDiagnostics, ref diagnosticCount, firstKeywordRange);
				MergePhantoms(dynamicPhantoms, manualPhantomsSnapshot);

				if (analyzerSnapshot != null && (context.TotalLineCount < 0 || context.TotalLineCount < 2048)) {
					IndentGuideResult guideResult = analyzerSnapshot.AnalyzeIndentGuides();
					if (guideResult?.GuideLines != null) {
						var seenFolds = new HashSet<string>();
						foreach (IndentGuideLine guide in guideResult.GuideLines) {
							if (guide == null || guide.EndLine < guide.StartLine) {
								continue;
							}

							int column = Math.Max(guide.Column, 0);
							indentGuides.Add(new DecorationResult.IndentGuideItem(
								new EditorTextPosition { Line = guide.StartLine, Column = column },
								new EditorTextPosition { Line = guide.EndLine, Column = column }));

							if (guide.EndLine <= guide.StartLine) {
								continue;
							}

							string key = $"{guide.StartLine}:{guide.EndLine}";
							if (seenFolds.Add(key)) {
								foldRegions.Add(new DecorationResult.FoldRegionItem(guide.StartLine, guide.EndLine));
							}
						}
					}
				}

				return new DecorationResult {
					PhantomTexts = dynamicPhantoms,
					PhantomTextsMode = DecorationApplyMode.REPLACE_ALL,
					SyntaxSpans = syntaxSpans,
					SyntaxSpansMode = DecorationApplyMode.MERGE,
					InlayHints = inlayHints,
					InlayHintsMode = DecorationApplyMode.REPLACE_RANGE,
					IndentGuides = indentGuides,
					IndentGuidesMode = DecorationApplyMode.REPLACE_ALL,
					FoldRegions = foldRegions,
					FoldRegionsMode = DecorationApplyMode.REPLACE_ALL,
					SeparatorGuides = separatorGuides,
					SeparatorGuidesMode = DecorationApplyMode.REPLACE_ALL,
					GutterIcons = gutterIcons,
					GutterIconsMode = DecorationApplyMode.REPLACE_ALL
				};
			}

			private static void RegisterStyleMap(HighlightEngine engine) {
				engine.RegisterStyleName("keyword", (int)EditorTheme.STYLE_KEYWORD);
				engine.RegisterStyleName("type", (int)EditorTheme.STYLE_TYPE);
				engine.RegisterStyleName("string", (int)EditorTheme.STYLE_STRING);
				engine.RegisterStyleName("comment", (int)EditorTheme.STYLE_COMMENT);
				engine.RegisterStyleName("preprocessor", (int)EditorTheme.STYLE_PREPROCESSOR);
				engine.RegisterStyleName("macro", (int)EditorTheme.STYLE_PREPROCESSOR);
				engine.RegisterStyleName("method", (int)EditorTheme.STYLE_FUNCTION);
				engine.RegisterStyleName("function", (int)EditorTheme.STYLE_FUNCTION);
				engine.RegisterStyleName("variable", (int)EditorTheme.STYLE_VARIABLE);
				engine.RegisterStyleName("field", (int)EditorTheme.STYLE_VARIABLE);
				engine.RegisterStyleName("number", (int)EditorTheme.STYLE_NUMBER);
				engine.RegisterStyleName("class", (int)EditorTheme.STYLE_CLASS);
				engine.RegisterStyleName("color", StyleColor);
				engine.RegisterStyleName("builtin", (int)EditorTheme.STYLE_BUILTIN);
				engine.RegisterStyleName("annotation", (int)EditorTheme.STYLE_ANNOTATION);
			}

			private static string ResolveCurrentFileName(DecorationContext context) {
				if (context.EditorMetadata is DemoFileMetadata fileMetadata &&
					!string.IsNullOrWhiteSpace(fileMetadata.FileName)) {
					return fileMetadata.FileName;
				}
				return DefaultAnalysisFileName;
			}

			private static string BuildAnalysisUri(string fileName) {
				return $"file:///{fileName}";
			}

			private static void AppendStyleSpan(Dictionary<int, List<DecorationResult.SpanItem>> syntaxSpans, TokenSpan token) {
				if (token.StyleId <= 0) {
					return;
				}
				TokenRangeInfo? range = ExtractSingleLineTokenRange(token);
				if (range == null) {
					return;
				}
				GetOrCreate(syntaxSpans, range.Line)
					.Add(new DecorationResult.SpanItem(range.StartColumn, range.Length, token.StyleId));
			}

			private static void AppendColorInlayHint(Dictionary<int, List<DecorationResult.InlayHintItem>> inlayHints,
				HashSet<string> seenHints,
				List<string> textLines,
				TokenSpan token) {
				TokenRangeInfo? range = ExtractSingleLineTokenRange(token);
				if (range == null) {
					return;
				}
				string literal = GetTokenLiteral(textLines, range);
				int? color = ParseColorLiteral(literal);
				if (color == null) {
					return;
				}
				string key = $"{range.Line}:{range.StartColumn}:{literal}";
				if (!seenHints.Add(key)) {
					return;
				}
				GetOrCreate(inlayHints, range.Line)
					.Add(DecorationResult.InlayHintItem.ColorHint(range.StartColumn, color.Value));
			}

			private static void AppendTextInlayHint(Dictionary<int, List<DecorationResult.InlayHintItem>> inlayHints,
				List<string> textLines,
				TokenSpan token) {
				if (token.StyleId != (int)EditorTheme.STYLE_KEYWORD) {
					return;
				}
				TokenRangeInfo? range = ExtractSingleLineTokenRange(token);
				if (range == null) {
					return;
				}
				string literal = GetTokenLiteral(textLines, range);
				List<DecorationResult.InlayHintItem> lineHints = GetOrCreate(inlayHints, range.Line);
				if (literal == "const") {
					lineHints.Add(DecorationResult.InlayHintItem.TextHint(range.EndColumn + 1, "immutable"));
				} else if (literal == "return") {
					lineHints.Add(DecorationResult.InlayHintItem.TextHint(range.EndColumn + 1, "value: "));
				} else if (literal == "case") {
					lineHints.Add(DecorationResult.InlayHintItem.TextHint(range.EndColumn + 1, "condition: "));
				}
			}

			private static void AppendSeparator(List<DecorationResult.SeparatorGuideItem> separatorGuides,
				List<string> textLines,
				TokenSpan token) {
				if (token.StyleId != (int)EditorTheme.STYLE_COMMENT) {
					return;
				}
				TokenRangeInfo? range = ExtractSingleLineTokenRange(token);
				if (range == null) {
					return;
				}
				string? lineText = GetLineText(textLines, range.Line);
				if (lineText == null || range.EndColumn > lineText.Length) {
					return;
				}
				int count = -1;
				bool isDouble = false;
				for (int i = 0; i < lineText.Length; i++) {
					char ch = lineText[i];
					if (count < 0) {
						if (ch == '/') {
							continue;
						}
						if (ch == '=') {
							count = 1;
							isDouble = true;
						} else if (ch == '-') {
							count = 1;
							isDouble = false;
						}
					} else if (isDouble && ch == '=') {
						count++;
					} else if (!isDouble && ch == '-') {
						count++;
					} else {
						break;
					}
				}
				if (count > 0) {
					separatorGuides.Add(new DecorationResult.SeparatorGuideItem(
						range.Line,
						isDouble ? 1 : 0,
						count,
						lineText.Length));
				}
			}

			private static void AppendGutterIcons(Dictionary<int, List<int>> gutterIcons,
				List<string> textLines,
				TokenSpan token) {
				if (token.StyleId != (int)EditorTheme.STYLE_KEYWORD) {
					return;
				}
				TokenRangeInfo? range = ExtractSingleLineTokenRange(token);
				if (range == null) {
					return;
				}
				string literal = GetTokenLiteral(textLines, range);
				if (literal == "class" || literal == "struct") {
					GetOrCreate(gutterIcons, range.Line).Add(IconClass);
				}
			}

			private static TokenRangeInfo? AppendDynamicDemoDecorations(
				Dictionary<int, List<DecorationResult.PhantomTextItem>> phantoms,
				HashSet<int> phantomLines,
				Dictionary<int, List<DecorationResult.DiagnosticItem>> diagnostics,
				HashSet<string> seenDiagnostics,
				ref int diagnosticCount,
				TokenRangeInfo? firstKeywordRange,
				List<string> textLines,
				TokenSpan token) {
				TokenRangeInfo? range = ExtractSingleLineTokenRange(token);
				if (range == null) {
					return firstKeywordRange;
				}
				string literal = GetTokenLiteral(textLines, range);
				if (string.IsNullOrEmpty(literal)) {
					return firstKeywordRange;
				}

				if (token.StyleId == (int)EditorTheme.STYLE_KEYWORD) {
					firstKeywordRange ??= range;
					if (phantomLines.Count == 0 && (literal == "class" || literal == "struct")) {
						GetOrCreate(phantoms, range.Line)
							.Add(new DecorationResult.PhantomTextItem(range.EndColumn, PhantomMemberStub));
						phantomLines.Add(range.Line);
					} else if (phantomLines.Count == 0 && literal == "return") {
						GetOrCreate(phantoms, range.Line)
							.Add(new DecorationResult.PhantomTextItem(range.EndColumn, PhantomInlineHint));
						phantomLines.Add(range.Line);
					}
					return firstKeywordRange;
				}

				if (token.StyleId == (int)EditorTheme.STYLE_COMMENT) {
					int fixmeIndex = literal.IndexOf("FIXME", StringComparison.OrdinalIgnoreCase);
					if (fixmeIndex >= 0) {
						AppendDiagnostic(diagnostics, seenDiagnostics, ref diagnosticCount,
							range.Line, range.StartColumn + fixmeIndex, 5, 0, 0);
					}
					int todoIndex = literal.IndexOf("TODO", StringComparison.OrdinalIgnoreCase);
					if (todoIndex >= 0) {
						AppendDiagnostic(diagnostics, seenDiagnostics, ref diagnosticCount,
							range.Line, range.StartColumn + todoIndex, 4, 1, 0);
					}
					return firstKeywordRange;
				}

				int? literalColor = ParseColorLiteral(literal);
				if (literalColor.HasValue) {
					AppendDiagnostic(diagnostics, seenDiagnostics, ref diagnosticCount,
						range.Line, range.StartColumn, range.Length, 2, literalColor.Value);
					return firstKeywordRange;
				}

				if (token.StyleId == (int)EditorTheme.STYLE_ANNOTATION) {
					AppendDiagnostic(diagnostics, seenDiagnostics, ref diagnosticCount,
						range.Line, range.StartColumn, range.Length, 3, 0);
				}
				return firstKeywordRange;
			}

			private static void AppendDiagnostic(
				Dictionary<int, List<DecorationResult.DiagnosticItem>> diagnostics,
				HashSet<string> seenDiagnostics,
				ref int diagnosticCount,
				int line,
				int column,
				int length,
				int severity,
				int color) {
				if (diagnosticCount >= MaxDynamicDiagnostics) {
					return;
				}
				if (line < 0 || column < 0 || length <= 0) {
					return;
				}
				string key = $"{line}:{column}:{length}:{severity}:{color}";
				if (!seenDiagnostics.Add(key)) {
					return;
				}
				GetOrCreate(diagnostics, line).Add(new DecorationResult.DiagnosticItem(column, length, severity, color));
				diagnosticCount++;
			}

			private static void AppendDiagnosticFallbackIfNeeded(
				Dictionary<int, List<DecorationResult.DiagnosticItem>> diagnostics,
				HashSet<string> seenDiagnostics,
				ref int diagnosticCount,
				TokenRangeInfo? firstKeywordRange) {
				if (diagnosticCount > 0 || firstKeywordRange == null) {
					return;
				}
				AppendDiagnostic(
					diagnostics,
					seenDiagnostics,
					ref diagnosticCount,
					firstKeywordRange.Line,
					firstKeywordRange.StartColumn,
					firstKeywordRange.Length,
					3,
					0);
			}

			private static int? ParseColorLiteral(string literal) {
				if (!literal.StartsWith("0X", StringComparison.Ordinal)) {
					return null;
				}
				try {
					return unchecked((int)Convert.ToUInt32(literal[2..], 16));
				} catch {
					return null;
				}
			}

			private static string GetTokenLiteral(List<string> textLines, TokenRangeInfo range) {
				string? lineText = GetLineText(textLines, range.Line);
				if (lineText == null || range.EndColumn > lineText.Length) {
					return string.Empty;
				}
				return lineText.Substring(range.StartColumn, range.Length);
			}

			private static string? GetLineText(List<string> textLines, int line) {
				if (line < 0 || line >= textLines.Count) {
					return null;
				}
				return textLines[line];
			}

			private static List<string> SplitLines(string text) {
				var lines = new List<string>();
				int start = 0;
				for (int i = 0; i < text.Length; i++) {
					if (text[i] == '\n') {
						string line = text.Substring(start, i - start);
						if (line.EndsWith("\r", StringComparison.Ordinal)) {
							line = line[..^1];
						}
						lines.Add(line);
						start = i + 1;
					}
				}
				string tail = text[start..];
				if (tail.EndsWith("\r", StringComparison.Ordinal)) {
					tail = tail[..^1];
				}
				lines.Add(tail);
				return lines;
			}

			private static TokenRangeInfo? ExtractSingleLineTokenRange(TokenSpan token) {
				int startLine = token.Range.Start.Line;
				int endLine = token.Range.End.Line;
				int startColumn = token.Range.Start.Column;
				int endColumn = token.Range.End.Column;
				if (startLine < 0 || startLine != endLine || startColumn < 0 || endColumn <= startColumn) {
					return null;
				}
				return new TokenRangeInfo(startLine, startColumn, endColumn);
			}

			private static string ApplyTextChange(string originalText, EditorTextRange range, string newText) {
				int startOffset = LineColumnToOffset(originalText, range.Start.Line, range.Start.Column);
				int endOffset = LineColumnToOffset(originalText, range.End.Line, range.End.Column);
				if (startOffset > endOffset) {
					(startOffset, endOffset) = (endOffset, startOffset);
				}
				var builder = new StringBuilder(Math.Max(0, originalText.Length - (endOffset - startOffset)) + newText.Length);
				builder.Append(originalText, 0, startOffset);
				builder.Append(newText);
				builder.Append(originalText, endOffset, originalText.Length - endOffset);
				return builder.ToString();
			}

			private static int LineColumnToOffset(string text, int targetLine, int targetColumn) {
				int line = 0;
				int index = 0;

				while (index < text.Length && line < Math.Max(0, targetLine)) {
					char ch = text[index++];
					if (ch == '\n') {
						line++;
					}
				}

				int column = 0;
				while (index < text.Length && column < Math.Max(0, targetColumn)) {
					char ch = text[index];
					if (ch == '\n') {
						break;
					}
					index++;
					column++;
				}
				return index;
			}

			private static SweetLineTextRange ConvertAsSLTextRange(EditorTextRange range) {
				return new SweetLineTextRange(
					new SweetLineTextPosition(range.Start.Line, range.Start.Column, 0),
					new SweetLineTextPosition(range.End.Line, range.End.Column, 0));
			}

			private static Dictionary<int, List<DecorationResult.PhantomTextItem>> CopyMap(
				Dictionary<int, List<DecorationResult.PhantomTextItem>> source) {
				var copy = new Dictionary<int, List<DecorationResult.PhantomTextItem>>(source.Count);
				foreach (var pair in source) {
					copy[pair.Key] = new List<DecorationResult.PhantomTextItem>(pair.Value);
				}
				return copy;
			}

			private static void MergePhantoms(
				Dictionary<int, List<DecorationResult.PhantomTextItem>> target,
				Dictionary<int, List<DecorationResult.PhantomTextItem>> patch) {
				foreach (var pair in patch) {
					if (!target.TryGetValue(pair.Key, out List<DecorationResult.PhantomTextItem>? list)) {
						list = new List<DecorationResult.PhantomTextItem>();
						target[pair.Key] = list;
					}
					list.AddRange(pair.Value);
				}
			}

			private static List<T> GetOrCreate<T>(Dictionary<int, List<T>> map, int line) {
				if (!map.TryGetValue(line, out List<T>? list)) {
					list = new List<T>();
					map[line] = list;
				}
				return list;
			}

			private sealed class TokenRangeInfo {
				public int Line { get; }
				public int StartColumn { get; }
				public int EndColumn { get; }
				public int Length => EndColumn - StartColumn;

				public TokenRangeInfo(int line, int startColumn, int endColumn) {
					Line = line;
					StartColumn = startColumn;
					EndColumn = endColumn;
				}
			}
		}

		private sealed class DemoFileMetadata : IEditorMetadata {
			public string FileName { get; }

			public DemoFileMetadata(string fileName) {
				FileName = fileName;
			}
		}

		private sealed class DemoIconProvider : EditorIconProvider {
			private readonly Dictionary<int, IImage> iconCache = new();

			public IImage? GetIconImage(int iconId) {
				if (iconId <= 0) {
					return null;
				}
				if (iconCache.TryGetValue(iconId, out IImage? image)) {
					return image;
				}

				uint argb = iconId switch {
					IconClass => 0xFF5AA9FF,
					_ => 0xFF9AA5B5,
				};
				var fill = new SolidColorBrush(Color.FromUInt32(argb));
				var geometry = new EllipseGeometry(new Rect(1, 1, 10, 10));
				var drawing = new GeometryDrawing {
					Brush = fill,
					Geometry = geometry
				};
				image = new DrawingImage(drawing);
				iconCache[iconId] = image;
				return image;
			}
		}
	}
}
