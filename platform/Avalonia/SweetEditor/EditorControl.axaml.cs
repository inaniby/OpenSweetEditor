using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace SweetEditor {
	public partial class EditorControl : Control {
		#region Events
		public event EventHandler<TextChangedEventArgs>? TextChanged;
		public event EventHandler<CursorChangedEventArgs>? CursorChanged;
		public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
		public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;
		public event EventHandler<ScaleChangedEventArgs>? ScaleChanged;
		public event EventHandler<LongPressEventArgs>? LongPress;
		public event EventHandler<DoubleTapEventArgs>? DoubleTap;
		public event EventHandler<EditorContextMenuEventArgs>? EditorContextMenu;
		public event EventHandler<InlayHintClickEventArgs>? InlayHintClick;
		public event EventHandler<GutterIconClickEventArgs>? GutterIconClick;
		public event EventHandler<FoldToggleEventArgs>? FoldToggle;
		public event EventHandler<CompletionItemsEventArgs>? CompletionItemsUpdated;
		public event EventHandler? CompletionDismissed;
		public event EventHandler<RenderStatsEventArgs>? RenderStatsUpdated;
		#endregion

		#region Constants
		public const int FONT_STYLE_NORMAL = 0;
		public const int FONT_STYLE_BOLD = 1;
		public const int FONT_STYLE_ITALIC = 1 << 1;
		public const int FONT_STYLE_STRIKETHROUGH = 1 << 2;
		#endregion

		private EditorTheme currentTheme = EditorTheme.Dark();
		private EditorRenderer renderer = null!;
		private EditorCore editorCore = null!;
		private EditorRenderModel? renderModel;
		private DecorationProviderManager? decorationProviderManager;
		private CompletionProviderManager? completionProviderManager;
		private NewLineActionProviderManager? newLineActionProviderManager;
		private LanguageConfiguration? languageConfiguration;
		public IEditorMetadata? Metadata { get; set; }
		private EditorSettings? settings;
		private Document? currentDocument;
		private bool animationActive = false;
		private DispatcherTimer? animationTimer;
		private int lastViewportWidth = -1;
		private int lastViewportHeight = -1;
		private bool renderModelDirty = true;
		private bool perfOverlayEnabled = true;
		private const float DefaultContentStartPaddingDp = 3.0f;
		private const float WheelDeltaScale = 40f;

		public EditorControl() {
			InitializeComponent();
			InitializeEditor();
		}

		private void InitializeComponent() {
			Focusable = true;
			ClipToBounds = true;
		}

		private void InitializeEditor() {
			renderer = new EditorRenderer(currentTheme);
			editorCore = new EditorCore(
				renderer.GetTextMeasurer(),
				new EditorOptions());
			settings = new EditorSettings(this);
			decorationProviderManager = new DecorationProviderManager(this);
			completionProviderManager = new CompletionProviderManager(this);
			newLineActionProviderManager = new NewLineActionProviderManager(this);
			completionProviderManager.ItemsUpdated += OnCompletionItemsUpdated;
			completionProviderManager.Dismissed += OnCompletionDismissed;
			
			SubscribeInputEvents();
			InitializeAnimationTimer();
			settings.SetContentStartPadding(DefaultContentStartPaddingDp);
		}

		private void SubscribeInputEvents() {
			PointerPressed += OnPointerPressed;
			PointerMoved += OnPointerMoved;
			PointerReleased += OnPointerReleased;
			PointerWheelChanged += OnPointerWheelChanged;
			KeyDown += OnKeyDown;
			KeyUp += OnKeyUp;
			TextInput += OnTextInput;
		}

		private void InitializeAnimationTimer() {
			animationTimer = new DispatcherTimer {
				Interval = TimeSpan.FromMilliseconds(16)
			};
			animationTimer.Tick += OnAnimationTick;
		}

		private void UpdateAnimationTimer(bool needsAnimation) {
			if (animationTimer == null) {
				return;
			}
			if (needsAnimation && !animationActive) {
				animationActive = true;
				animationTimer.Start();
			} else if (!needsAnimation && animationActive) {
				animationActive = false;
				animationTimer.Stop();
			}
		}

		#region Public API - Construction/Initialization/Lifecycle

		public void LoadDocument(Document document) {
			currentDocument = document;
			editorCore.SetDocument(document.Handle);
			decorationProviderManager?.OnDocumentLoaded();
			completionProviderManager?.Dismiss();
			Flush();
		}

		public EditorTheme GetTheme() => currentTheme;

		public void ApplyTheme(EditorTheme theme) {
			currentTheme = theme;
			renderer.ApplyTheme(theme);
			if (theme.TextStyles != null && theme.TextStyles.Count > 0) {
				editorCore.RegisterBatchTextStyles(theme.TextStyles);
			}
			Flush();
		}

		public EditorSettings Settings => settings ??= new EditorSettings(this);

		public EditorCore EditorCoreInternal => editorCore;

		public void SetEditorIconProvider(EditorIconProvider? provider) {
			renderer.SetEditorIconProvider(provider);
			Flush();
		}

		public void SetPerfOverlayEnabled(bool enabled) {
			perfOverlayEnabled = enabled;
		}

		public bool IsPerfOverlayEnabled() => perfOverlayEnabled;

		public void SyncPlatformScaleInternal(float scale) {
			renderer.SyncPlatformScale(scale);
			editorCore.OnFontMetricsChanged();
		}

		public void Flush() {
			renderModelDirty = true;
			InvalidateVisual();
		}

		private void EnsureRenderModelUpToDate() {
			if (!renderModelDirty) {
				return;
			}
			renderModel = editorCore.BuildRenderModel();
			renderModelDirty = false;
		}

		public void RequestDecorationRefresh() {
			decorationProviderManager?.RequestRefresh();
		}

		public void SetLanguageConfiguration(LanguageConfiguration? config) {
			languageConfiguration = config;
			if (config != null) {
				if (config.Brackets != null && config.Brackets.Count > 0) {
					var openChars = new int[config.Brackets.Count];
					var closeChars = new int[config.Brackets.Count];
					for (int i = 0; i < config.Brackets.Count; i++) {
						openChars[i] = string.IsNullOrEmpty(config.Brackets[i].Open) ? 0 : char.ConvertToUtf32(config.Brackets[i].Open, 0);
						closeChars[i] = string.IsNullOrEmpty(config.Brackets[i].Close) ? 0 : char.ConvertToUtf32(config.Brackets[i].Close, 0);
					}
					editorCore.SetBracketPairs(openChars, closeChars);
				}
				if (config.TabSize.HasValue && config.TabSize.Value > 0) {
					editorCore.SetTabSize(config.TabSize.Value);
				}
			}
		}

		public LanguageConfiguration? GetLanguageConfiguration() => languageConfiguration;
		public Document? GetDocument() => currentDocument;

		public void SetMetadata<T>(T? metadata) where T : class, IEditorMetadata {
			Metadata = metadata;
		}

		public T? GetMetadata<T>() where T : class, IEditorMetadata {
			return Metadata as T;
		}

		public (int start, int end) GetVisibleLineRange() {
			EnsureRenderModelUpToDate();
			if (!renderModel.HasValue || renderModel.Value.VisualLines == null || renderModel.Value.VisualLines.Count == 0) {
				return (0, -1);
			}

			int start = int.MaxValue;
			int end = int.MinValue;
			foreach (var line in renderModel.Value.VisualLines) {
				if (line.WrapIndex != 0 || line.IsPhantomLine) continue;
				if (line.LogicalLine < start) start = line.LogicalLine;
				if (line.LogicalLine > end) end = line.LogicalLine;
			}

			if (start == int.MaxValue) return (0, -1);
			return (start, end);
		}

		public void AddNewLineActionProvider(INewLineActionProvider provider) =>
			newLineActionProviderManager?.AddProvider(provider);
		public void RemoveNewLineActionProvider(INewLineActionProvider provider) =>
			newLineActionProviderManager?.RemoveProvider(provider);

		public CursorRect GetPositionRect(int line, int column) => editorCore.GetPositionRect(line, column);

		public CursorRect GetCursorRect() => editorCore.GetCursorRect();

		public void AddDecorationProvider(IDecorationProvider provider) =>
			decorationProviderManager?.AddProvider(provider);
		public void RemoveDecorationProvider(IDecorationProvider provider) =>
			decorationProviderManager?.RemoveProvider(provider);

		public void AddCompletionProvider(ICompletionProvider provider) =>
			completionProviderManager?.AddProvider(provider);
		public void RemoveCompletionProvider(ICompletionProvider provider) =>
			completionProviderManager?.RemoveProvider(provider);

		public void TriggerCompletion() =>
			completionProviderManager?.TriggerCompletion(CompletionTriggerKind.Invoked, null);
		public void DismissCompletion() => completionProviderManager?.Dismiss();
		public void ShowCompletionItems(List<CompletionItem> items) => completionProviderManager?.ShowItems(items);

		#endregion

		#region Public API - Text Editing

		public TextEditResult InsertText(string text) {
			var result = editorCore.InsertText(text);
			FireTextChanged(TextChangeAction.Insert, result);
			Flush();
			return result;
		}

		public TextEditResult ReplaceText(TextRange range, string text) {
			var result = editorCore.ReplaceText(range, text);
			FireTextChanged(TextChangeAction.Replace, result);
			Flush();
			return result;
		}

		public TextEditResult DeleteText(TextRange range) {
			var result = editorCore.DeleteText(range);
			FireTextChanged(TextChangeAction.Delete, result);
			Flush();
			return result;
		}

		public TextEditResult Backspace() {
			var result = editorCore.Backspace();
			FireTextChanged(TextChangeAction.Delete, result);
			Flush();
			return result;
		}

		public TextEditResult DeleteForward() {
			var result = editorCore.DeleteForward();
			FireTextChanged(TextChangeAction.Delete, result);
			Flush();
			return result;
		}

		public string GetSelectedText() => editorCore.GetSelectedText();

		public TextEditResult MoveLineUp() {
			var result = editorCore.MoveLineUp();
			FireTextChanged(TextChangeAction.Other, result);
			Flush();
			return result;
		}

		public TextEditResult MoveLineDown() {
			var result = editorCore.MoveLineDown();
			FireTextChanged(TextChangeAction.Other, result);
			Flush();
			return result;
		}

		public TextEditResult CopyLineUp() {
			var result = editorCore.CopyLineUp();
			FireTextChanged(TextChangeAction.Insert, result);
			Flush();
			return result;
		}

		public TextEditResult CopyLineDown() {
			var result = editorCore.CopyLineDown();
			FireTextChanged(TextChangeAction.Insert, result);
			Flush();
			return result;
		}

		public TextEditResult DeleteLine() {
			var result = editorCore.DeleteLine();
			FireTextChanged(TextChangeAction.Delete, result);
			Flush();
			return result;
		}

		public TextEditResult InsertLineAbove() {
			var result = editorCore.InsertLineAbove();
			FireTextChanged(TextChangeAction.Insert, result);
			Flush();
			return result;
		}

		public TextEditResult InsertLineBelow() {
			var result = editorCore.InsertLineBelow();
			FireTextChanged(TextChangeAction.Insert, result);
			Flush();
			return result;
		}

		public TextEditResult Undo() {
			var result = editorCore.Undo();
			FireTextChanged(TextChangeAction.Undo, result);
			Flush();
			return result;
		}

		public TextEditResult Redo() {
			var result = editorCore.Redo();
			FireTextChanged(TextChangeAction.Redo, result);
			Flush();
			return result;
		}

		public bool CanUndo() => editorCore.CanUndo();
		public bool CanRedo() => editorCore.CanRedo();

		#endregion

		#region Public API - Cursor/Selection

		public void SetCursorPosition(TextPosition position) {
			editorCore.SetCursorPosition(position);
			Flush();
		}

		public TextPosition GetCursorPosition() => editorCore.GetCursorPosition();

		public int GetLineCount() => editorCore.GetLineCount();

		public int GetTotalLineCount() => GetLineCount();

		public string GetLineText(int line) => editorCore.GetLineText(line);

		public TextRange GetWordRangeAtCursor() => editorCore.GetWordRangeAtCursor();

		public string GetWordAtCursor() => editorCore.GetWordAtCursor();

		public void SetSelection(TextRange range) {
			editorCore.SetSelection(range);
			Flush();
		}

		public void SetSelection(int startLine, int startColumn, int endLine, int endColumn) {
			SetSelection(new TextRange(
				new TextPosition { Line = startLine, Column = startColumn },
				new TextPosition { Line = endLine, Column = endColumn }));
		}

		public TextRange GetSelection() => editorCore.GetSelection();

		public void SelectAll() {
			editorCore.SelectAll();
			Flush();
		}

		public void MoveCursorLeft(bool extendSelection) {
			editorCore.MoveCursorLeft(extendSelection);
			Flush();
		}

		public void MoveCursorRight(bool extendSelection) {
			editorCore.MoveCursorRight(extendSelection);
			Flush();
		}

		public void MoveCursorUp(bool extendSelection) {
			editorCore.MoveCursorUp(extendSelection);
			Flush();
		}

		public void MoveCursorDown(bool extendSelection) {
			editorCore.MoveCursorDown(extendSelection);
			Flush();
		}

		public void MoveCursorToLineStart(bool extendSelection) {
			editorCore.MoveCursorToLineStart(extendSelection);
			Flush();
		}

		public void MoveCursorToLineEnd(bool extendSelection) {
			editorCore.MoveCursorToLineEnd(extendSelection);
			Flush();
		}

		#endregion

		#region Public API - Scrolling

		public void ScrollToLine(int line, ScrollBehavior behavior = ScrollBehavior.CENTER) {
			editorCore.ScrollToLine(line, behavior);
			var metrics = editorCore.GetScrollMetrics();
			ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(metrics.ScrollX, metrics.ScrollY));
			decorationProviderManager?.OnScrollChanged();
			Flush();
		}

		public void GotoPosition(int line, int column = 0) {
			editorCore.GotoPosition(line, column);
			var metrics = editorCore.GetScrollMetrics();
			ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(metrics.ScrollX, metrics.ScrollY));
			decorationProviderManager?.OnScrollChanged();
			Flush();
		}

		public void SetScroll(float scrollX, float scrollY) {
			editorCore.SetScroll(scrollX, scrollY);
			ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(scrollX, scrollY));
			decorationProviderManager?.OnScrollChanged();
			Flush();
		}

		public ScrollMetrics GetScrollMetrics() => editorCore.GetScrollMetrics();

		#endregion

		#region Public API - Decorations

		public void registerTextStyle(uint styleId, int color, int backgroundColor, int fontStyle) =>
			editorCore.registerTextStyle(styleId, color, backgroundColor, fontStyle);

		public void registerTextStyle(uint styleId, int color, int fontStyle) =>
			editorCore.registerTextStyle(styleId, color, fontStyle);

		public void registerBatchTextStyles(IReadOnlyDictionary<uint, TextStyle> stylesById) =>
			editorCore.registerBatchTextStyles(stylesById);

		public void SetLineSpans(int line, SpanLayer layer, IList<StyleSpan> spans) {
			editorCore.SetLineSpans(line, (int)layer, spans);
		}

		public void SetLineSpans(int line, int layer, IList<StyleSpan> spans) {
			editorCore.SetLineSpans(line, layer, spans);
		}

		public void SetLineSpans(int line, IList<StyleSpan> spans) {
			SetLineSpans(line, SpanLayer.SYNTAX, spans);
		}

		public void SetLineInlayHints(int line, IList<InlayHint> hints) {
			editorCore.SetLineInlayHints(line, hints);
		}

		public void SetLinePhantomTexts(int line, IList<PhantomText> phantoms) {
			editorCore.SetLinePhantomTexts(line, phantoms);
		}

		public void SetLineGutterIcons(int line, IList<GutterIcon> icons) {
			editorCore.SetLineGutterIcons(line, icons);
		}

		public void SetBatchLineSpans(SpanLayer layer, Dictionary<int, IList<StyleSpan>> spansByLine) {
			editorCore.SetBatchLineSpans((int)layer, spansByLine);
		}

		public void SetBatchLineSpans(int layer, Dictionary<int, IList<StyleSpan>> spansByLine) {
			editorCore.SetBatchLineSpans(layer, spansByLine);
		}

		public void SetBatchLineInlayHints(Dictionary<int, IList<InlayHint>> hintsByLine) {
			editorCore.SetBatchLineInlayHints(hintsByLine);
		}

		public void SetBatchLinePhantomTexts(Dictionary<int, IList<PhantomText>> phantomsByLine) {
			editorCore.SetBatchLinePhantomTexts(phantomsByLine);
		}

		public void SetBatchLineGutterIcons(Dictionary<int, IList<GutterIcon>> iconsByLine) {
			editorCore.SetBatchLineGutterIcons(iconsByLine);
		}

		public void SetBatchLineDiagnostics(Dictionary<int, IList<DiagnosticItem>> diagsByLine) {
			editorCore.SetBatchLineDiagnostics(diagsByLine);
		}

		public void ClearGutterIcons() {
			editorCore.ClearGutterIcons();
		}

		public void SetMaxGutterIcons(int count) {
			editorCore.SetMaxGutterIcons((uint)Math.Max(0, count));
		}

		public void SetMaxGutterIcons(uint count) {
			editorCore.SetMaxGutterIcons(count);
		}

		public void SetLineDiagnostics(int line, IList<DiagnosticItem> items) {
			editorCore.SetLineDiagnostics(line, items);
		}

		public void ClearDiagnostics() {
			editorCore.ClearDiagnostics();
		}

		public void SetIndentGuides(IList<IndentGuide> guides) {
			editorCore.SetIndentGuides(guides);
		}

		public void SetBracketGuides(IList<BracketGuide> guides) {
			editorCore.SetBracketGuides(guides);
		}

		public void SetFlowGuides(IList<FlowGuide> guides) {
			editorCore.SetFlowGuides(guides);
		}

		public void SetSeparatorGuides(IList<SeparatorGuide> guides) {
			editorCore.SetSeparatorGuides(guides);
		}

		public void ClearGuides() {
			editorCore.ClearGuides();
		}

		public void SetFoldRegions(IList<FoldRegion> regions) {
			editorCore.SetFoldRegions(regions);
		}

		public bool ToggleFold(int line) {
			bool result = editorCore.ToggleFold((uint)Math.Max(0, line));
			Flush();
			return result;
		}

		public bool ToggleFold(uint line) {
			bool result = editorCore.ToggleFold(line);
			Flush();
			return result;
		}

		public bool FoldAt(int line) {
			bool result = editorCore.FoldAt((uint)Math.Max(0, line));
			Flush();
			return result;
		}

		public bool FoldAt(uint line) {
			bool result = editorCore.FoldAt(line);
			Flush();
			return result;
		}

		public bool UnfoldAt(int line) {
			bool result = editorCore.UnfoldAt((uint)Math.Max(0, line));
			Flush();
			return result;
		}

		public bool UnfoldAt(uint line) {
			bool result = editorCore.UnfoldAt(line);
			Flush();
			return result;
		}

		public void FoldAll() {
			editorCore.FoldAll();
			Flush();
		}

		public void UnfoldAll() {
			editorCore.UnfoldAll();
			Flush();
		}

		public bool IsLineVisible(int line) => editorCore.IsLineVisible((uint)Math.Max(0, line));

		public bool IsLineVisible(uint line) => editorCore.IsLineVisible(line);

		public void ClearHighlights() {
			editorCore.ClearHighlights();
		}

		public void ClearHighlightsLayer(byte layer) {
			editorCore.ClearHighlightsLayer(layer);
		}

		public void ClearHighlights(SpanLayer layer) {
			editorCore.ClearHighlightsLayer((byte)layer);
		}

		public void ClearInlayHints() {
			editorCore.ClearInlayHints();
		}

		public void ClearPhantomTexts() {
			editorCore.ClearPhantomTexts();
		}

		public void ClearAllDecorations() {
			editorCore.ClearAllDecorations();
		}

		public void SetMatchedBrackets(TextPosition open, TextPosition close) {
			editorCore.SetMatchedBrackets(open, close);
			Flush();
		}

		public void ClearMatchedBrackets() {
			editorCore.ClearMatchedBrackets();
			Flush();
		}

		public TextEditResult InsertSnippet(string snippetTemplate) {
			var result = editorCore.InsertSnippet(snippetTemplate);
			FireTextChanged(TextChangeAction.Insert, result);
			Flush();
			return result;
		}

		public void StartLinkedEditing(LinkedEditingModel model) {
			editorCore.StartLinkedEditing(model);
			Flush();
		}

		public bool IsInLinkedEditing() => editorCore.IsInLinkedEditing();

		public bool LinkedEditingNext() {
			bool result = editorCore.LinkedEditingNext();
			Flush();
			return result;
		}

		public bool LinkedEditingPrev() {
			bool result = editorCore.LinkedEditingPrev();
			Flush();
			return result;
		}

		public void CancelLinkedEditing() {
			editorCore.CancelLinkedEditing();
			Flush();
		}

		#endregion

		#region Input Event Handlers

		private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
			Focus();
			var currentPoint = e.GetCurrentPoint(this);
			bool isLeftButton = currentPoint.Properties.IsLeftButtonPressed;
			bool isRightButton = currentPoint.Properties.IsRightButtonPressed;
			if (!isLeftButton && !isRightButton) {
				return;
			}
			var point = currentPoint.Position;
			var modifiers = GetCurrentModifiers(e.KeyModifiers);
			var eventType = isRightButton ? EventType.MOUSE_RIGHT_DOWN : EventType.MOUSE_DOWN;
			
			var gestureResult = editorCore.HandleGestureEvent(new GestureEvent {
				Type = eventType,
				Points = new List<PointF> { new PointF((float)point.X, (float)point.Y) },
				Modifiers = modifiers,
				WheelDeltaX = 0,
				WheelDeltaY = 0,
				DirectScale = 1
			});
			
			FireGestureEvents(gestureResult);
			e.Handled = true;
			Flush();
		}

		private void OnPointerMoved(object? sender, PointerEventArgs e) {
			var currentPoint = e.GetCurrentPoint(this);
			if (!currentPoint.Properties.IsLeftButtonPressed) {
				return;
			}
			var point = currentPoint.Position;
			var modifiers = GetCurrentModifiers(e.KeyModifiers);
			
			var gestureResult = editorCore.HandleGestureEvent(new GestureEvent {
				Type = EventType.MOUSE_MOVE,
				Points = new List<PointF> { new PointF((float)point.X, (float)point.Y) },
				Modifiers = modifiers,
				WheelDeltaX = 0,
				WheelDeltaY = 0,
				DirectScale = 1
			});
			
			FireGestureEvents(gestureResult);
			e.Handled = true;
			Flush();
		}

		private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) {
			if (e.InitialPressMouseButton != MouseButton.Left) {
				return;
			}
			var point = e.GetCurrentPoint(this).Position;
			var modifiers = GetCurrentModifiers(e.KeyModifiers);
			
			var gestureResult = editorCore.HandleGestureEvent(new GestureEvent {
				Type = EventType.MOUSE_UP,
				Points = new List<PointF> { new PointF((float)point.X, (float)point.Y) },
				Modifiers = modifiers,
				WheelDeltaX = 0,
				WheelDeltaY = 0,
				DirectScale = 1
			});
			
			FireGestureEvents(gestureResult);
			e.Handled = true;
			Flush();
		}

		private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e) {
			var point = e.GetCurrentPoint(this).Position;
			var modifiers = GetCurrentModifiers(e.KeyModifiers);
			float wheelDeltaX = -(float)e.Delta.X * WheelDeltaScale;
			float wheelDeltaY = -(float)e.Delta.Y * WheelDeltaScale;
			
			var gestureResult = editorCore.HandleGestureEvent(new GestureEvent {
				Type = EventType.MOUSE_WHEEL,
				Points = new List<PointF> { new PointF((float)point.X, (float)point.Y) },
				Modifiers = modifiers,
				WheelDeltaX = wheelDeltaX,
				WheelDeltaY = wheelDeltaY,
				DirectScale = 1
			});
			
			FireGestureEvents(gestureResult);
			e.Handled = true;
			Flush();
		}

		private void OnKeyDown(object? sender, KeyEventArgs e) {
			var modifiers = GetCurrentModifiers(e.KeyModifiers);
			bool ctrlOrMeta = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
			if (ctrlOrMeta && e.Key == Key.Space) {
				completionProviderManager?.TriggerCompletion(CompletionTriggerKind.Invoked, null);
				e.Handled = true;
				return;
			}

			if (editorCore.IsComposing() && e.Key != Key.Escape) {
				return;
			}

			if (e.Key == Key.Enter && newLineActionProviderManager != null) {
				var action = newLineActionProviderManager.ProvideNewLineAction();
				if (action != null) {
					var editResult = editorCore.InsertText(action.Text);
					FireTextChanged(TextChangeAction.Key, editResult);
					UpdateCompletionAfterContentMutation();
					e.Handled = true;
					Flush();
					return;
				}
			}

			var keyCode = MapKeyToKeyCode(e.Key);
			if (keyCode == 0 && ctrlOrMeta) {
				keyCode = MapControlShortcutKey(e.Key);
			}
			if (keyCode == 0) return;

			var result = editorCore.HandleKeyEvent(keyCode, null, modifiers);
			if (result.Handled) {
				e.Handled = true;
				FireKeyEventChanges(result, TextChangeAction.Key);
				if (result.ContentChanged) {
					UpdateCompletionAfterContentMutation();
				}
				Flush();
			}
		}

		private void OnKeyUp(object? sender, KeyEventArgs e) {
			// Native core key handling is key-down based; dispatching key-up duplicates actions.
		}

		private void OnTextInput(object? sender, TextInputEventArgs e) {
			if (!string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0])) {
				var result = editorCore.InsertText(e.Text);
				FireTextChanged(TextChangeAction.Key, result);

				if (!editorCore.IsInLinkedEditing() && completionProviderManager != null) {
					if (completionProviderManager.IsTriggerCharacter(e.Text)) {
						completionProviderManager.TriggerCompletion(CompletionTriggerKind.Character, e.Text);
					} else if (char.IsLetterOrDigit(e.Text[0]) || e.Text[0] == '_') {
						completionProviderManager.TriggerCompletion(CompletionTriggerKind.Invoked, null);
					} else {
						completionProviderManager.Dismiss();
					}
				}

				e.Handled = true;
				Flush();
			}
		}

		#endregion

		#region Helper Methods

		private byte GetCurrentModifiers(KeyModifiers modifiers) {
			byte result = 0;
			if ((modifiers & KeyModifiers.Shift) != 0) result |= 0x01;
			if ((modifiers & KeyModifiers.Control) != 0) result |= 0x02;
			if ((modifiers & KeyModifiers.Alt) != 0) result |= 0x04;
			if ((modifiers & KeyModifiers.Meta) != 0) result |= 0x08;
			return result;
		}

		private ushort MapKeyToKeyCode(Key key) {
			return key switch {
				Key.Enter => 13,
				Key.Tab => 9,
				Key.Back => 8,
				Key.Delete => 46,
				Key.Escape => 27,
				Key.Left => 37,
				Key.Right => 39,
				Key.Up => 38,
				Key.Down => 40,
				Key.Home => 36,
				Key.End => 35,
				Key.PageUp => 33,
				Key.PageDown => 34,
				Key.F1 => 112,
				Key.F2 => 113,
				Key.F3 => 114,
				Key.F4 => 115,
				Key.F5 => 116,
				Key.F6 => 117,
				Key.F7 => 118,
				Key.F8 => 119,
				Key.F9 => 120,
				Key.F10 => 121,
				Key.F11 => 122,
				Key.F12 => 123,
				_ => 0
			};
		}

		private static ushort MapControlShortcutKey(Key key) {
			return key switch {
				Key.A => (ushort)'A',
				Key.C => (ushort)'C',
				Key.V => (ushort)'V',
				Key.X => (ushort)'X',
				Key.Y => (ushort)'Y',
				Key.Z => (ushort)'Z',
				_ => 0,
			};
		}

		private void FireGestureEvents(GestureResult result) {
			switch (result.Type) {
				case GestureType.TAP:
					CursorChanged?.Invoke(this, new CursorChangedEventArgs(result.CursorPosition));
					completionProviderManager?.Dismiss();
					FireTapEvent(result);
					break;
				case GestureType.DOUBLE_TAP:
					DoubleTap?.Invoke(this, new DoubleTapEventArgs(result.TapPoint));
					CursorChanged?.Invoke(this, new CursorChangedEventArgs(result.CursorPosition));
					if (result.HasSelection) {
						SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(result.Selection));
					}
					break;
				case GestureType.LONG_PRESS:
					LongPress?.Invoke(this, new LongPressEventArgs(result.TapPoint));
					CursorChanged?.Invoke(this, new CursorChangedEventArgs(result.CursorPosition));
					break;
				case GestureType.SCROLL:
				case GestureType.FAST_SCROLL:
					ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(result.ViewScrollX, result.ViewScrollY));
					decorationProviderManager?.OnScrollChanged();
					completionProviderManager?.Dismiss();
					break;
				case GestureType.SCALE:
					SyncPlatformScaleInternal(result.ViewScale);
					ScaleChanged?.Invoke(this, new ScaleChangedEventArgs(result.ViewScale));
					break;
				case GestureType.DRAG_SELECT:
					SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(result.Selection));
					break;
				case GestureType.CONTEXT_MENU:
					EditorContextMenu?.Invoke(this, new EditorContextMenuEventArgs(result.TapPoint));
					break;
			}
			UpdateAnimationTimer(result.NeedsAnimation);
		}

		private void FireTapEvent(GestureResult result) {
			switch (result.HitTarget.Type) {
				case HitTargetType.INLAY_HINT_TEXT:
				case HitTargetType.INLAY_HINT_ICON:
				case HitTargetType.INLAY_HINT_COLOR:
					InlayHintClick?.Invoke(this, new InlayHintClickEventArgs(
						result.HitTarget.Line, result.HitTarget.Column));
					break;
				case HitTargetType.GUTTER_ICON:
					GutterIconClick?.Invoke(this, new GutterIconClickEventArgs(
						result.HitTarget.Line, result.HitTarget.IconId));
					break;
				case HitTargetType.FOLD_PLACEHOLDER:
				case HitTargetType.FOLD_GUTTER:
					FoldToggle?.Invoke(this, new FoldToggleEventArgs(result.HitTarget.Line));
					break;
			}
		}

		private void FireKeyEventChanges(KeyEventResult result, TextChangeAction action) {
			if (result.ContentChanged) {
				if (result.EditResult != null) {
					FireTextChanged(action, result.EditResult.Value);
				} else {
					decorationProviderManager?.OnTextChanged(null);
				}
			}
			if (result.CursorChanged) {
				CursorChanged?.Invoke(this, new CursorChangedEventArgs(GetCursorPosition()));
			}
			if (result.SelectionChanged) {
				SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(GetSelection()));
			}
		}

		private void UpdateCompletionAfterContentMutation() {
			if (completionProviderManager == null || editorCore.IsInLinkedEditing()) {
				return;
			}

			TextPosition cursor = editorCore.GetCursorPosition();
			string lineText = cursor.Line >= 0 ? editorCore.GetLineText(cursor.Line) : string.Empty;
			string? triggerCharacter = GetAdjacentCompletionTrigger(lineText, cursor.Column);
			if (triggerCharacter != null) {
				completionProviderManager.TriggerCompletion(CompletionTriggerKind.Retrigger, triggerCharacter);
				return;
			}

			if (HasAdjacentCompletionWordCharacter(lineText, cursor.Column)) {
				completionProviderManager.TriggerCompletion(CompletionTriggerKind.Retrigger, null);
				return;
			}

			completionProviderManager.Dismiss();
		}

		private string? GetAdjacentCompletionTrigger(string lineText, int column) {
			char? previous = GetCharAt(lineText, column - 1);
			if (previous.HasValue) {
				string candidate = previous.Value.ToString();
				if (completionProviderManager?.IsTriggerCharacter(candidate) == true) {
					return candidate;
				}
			}

			char? current = GetCharAt(lineText, column);
			if (current.HasValue) {
				string candidate = current.Value.ToString();
				if (completionProviderManager?.IsTriggerCharacter(candidate) == true) {
					return candidate;
				}
			}

			return null;
		}

		private static bool HasAdjacentCompletionWordCharacter(string lineText, int column) {
			char? previous = GetCharAt(lineText, column - 1);
			char? current = GetCharAt(lineText, column);
			return (previous.HasValue && IsCompletionWordCharacter(previous.Value)) ||
				(current.HasValue && IsCompletionWordCharacter(current.Value));
		}

		private static char? GetCharAt(string text, int index) {
			if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length) {
				return null;
			}
			return text[index];
		}

		private static bool IsCompletionWordCharacter(char ch) =>
			char.IsLetterOrDigit(ch) || ch == '_';

		private void FireTextChanged(TextChangeAction action, TextEditResult editResult) {
			if (editResult.Changes != null && editResult.Changes.Count > 0) {
				TextChanged?.Invoke(this, new TextChangedEventArgs(action, editResult.Changes));
				decorationProviderManager?.OnTextChanged(editResult.Changes);
			} else {
				decorationProviderManager?.OnTextChanged(null);
			}
		}

		private void OnCompletionItemsUpdated(IReadOnlyList<CompletionItem> items) {
			CompletionItemsUpdated?.Invoke(this, new CompletionItemsEventArgs(items));
		}

		private void OnCompletionDismissed() {
			CompletionDismissed?.Invoke(this, EventArgs.Empty);
		}

		private void OnAnimationTick(object? sender, EventArgs e) {
			if (!animationActive) return;
			var result = editorCore.TickAnimations();
			FireGestureEvents(result);
			Flush();
		}

		#endregion

		#region Rendering

		public override void Render(DrawingContext context) {
			base.Render(context);
			var size = Bounds.Size;
			int width = Math.Max(0, (int)size.Width);
			int height = Math.Max(0, (int)size.Height);
			if (width != lastViewportWidth || height != lastViewportHeight) {
				editorCore.SetViewport(width, height);
				lastViewportWidth = width;
				lastViewportHeight = height;
				renderModelDirty = true;
			}
			EnsureRenderModelUpToDate();
			long renderStart = Stopwatch.GetTimestamp();
			renderer.Render(context, renderModel, currentTheme, size);
			if (perfOverlayEnabled && RenderStatsUpdated != null) {
				double renderMs = (Stopwatch.GetTimestamp() - renderStart) * 1000.0 / Stopwatch.Frequency;
				EditorRenderModel model = renderModel.GetValueOrDefault();
				RenderStatsUpdated.Invoke(this, new RenderStatsEventArgs(
					renderMs,
					model.VisualLines?.Count ?? 0,
					model.GutterIcons?.Count ?? 0,
					model.FoldMarkers?.Count ?? 0,
					model.ScrollX,
					model.ScrollY));
			}
		}

		#endregion

		#region Event Args Classes

		public class TextChangedEventArgs : EventArgs {
			public TextChangeAction Action { get; }
			public IReadOnlyList<TextChange> Changes { get; }
			public TextChangedEventArgs(TextChangeAction action, IReadOnlyList<TextChange> changes) {
				Action = action;
				Changes = changes;
			}
		}

		public class CursorChangedEventArgs : EventArgs {
			public TextPosition CursorPosition { get; }
			public CursorChangedEventArgs(TextPosition cursorPosition) {
				CursorPosition = cursorPosition;
			}
		}

		public class SelectionChangedEventArgs : EventArgs {
			public TextRange Selection { get; }
			public SelectionChangedEventArgs(TextRange selection) {
				Selection = selection;
			}
		}

		public class ScrollChangedEventArgs : EventArgs {
			public float ScrollX { get; }
			public float ScrollY { get; }
			public ScrollChangedEventArgs(float scrollX, float scrollY) {
				ScrollX = scrollX;
				ScrollY = scrollY;
			}
		}

		public class ScaleChangedEventArgs : EventArgs {
			public float Scale { get; }
			public ScaleChangedEventArgs(float scale) {
				Scale = scale;
			}
		}

		public class LongPressEventArgs : EventArgs {
			public PointF Position { get; }
			public LongPressEventArgs(PointF position) {
				Position = position;
			}
		}

		public class DoubleTapEventArgs : EventArgs {
			public PointF Position { get; }
			public DoubleTapEventArgs(PointF position) {
				Position = position;
			}
		}

		public class EditorContextMenuEventArgs : EventArgs {
			public PointF Position { get; }
			public EditorContextMenuEventArgs(PointF position) {
				Position = position;
			}
		}

		public class InlayHintClickEventArgs : EventArgs {
			public int Line { get; }
			public int Column { get; }
			public InlayHintClickEventArgs(int line, int column) {
				Line = line;
				Column = column;
			}
		}

		public class GutterIconClickEventArgs : EventArgs {
			public int Line { get; }
			public int IconId { get; }
			public GutterIconClickEventArgs(int line, int iconId) {
				Line = line;
				IconId = iconId;
			}
		}

		public class FoldToggleEventArgs : EventArgs {
			public int Line { get; }
			public FoldToggleEventArgs(int line) {
				Line = line;
			}
		}

		public class CompletionItemsEventArgs : EventArgs {
			public IReadOnlyList<CompletionItem> Items { get; }
			public CompletionItemsEventArgs(IReadOnlyList<CompletionItem> items) {
				Items = items;
			}
		}

		public class RenderStatsEventArgs : EventArgs {
			public double RenderMs { get; }
			public int VisualLineCount { get; }
			public int GutterIconCount { get; }
			public int FoldMarkerCount { get; }
			public float ScrollX { get; }
			public float ScrollY { get; }

			public RenderStatsEventArgs(double renderMs, int visualLineCount, int gutterIconCount, int foldMarkerCount, float scrollX, float scrollY) {
				RenderMs = renderMs;
				VisualLineCount = visualLineCount;
				GutterIconCount = gutterIconCount;
				FoldMarkerCount = foldMarkerCount;
				ScrollX = scrollX;
				ScrollY = scrollY;
			}
		}

		#endregion
	}
}
