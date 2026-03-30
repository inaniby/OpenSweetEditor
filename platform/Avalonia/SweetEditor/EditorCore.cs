using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SweetEditor {
	public sealed class EditorCore : IDisposable {
		private static bool exceptionHandlerInitialized;
		private IntPtr handle;
		private IntPtr documentHandle;
		private GCHandle textMeasurerHandle;
		private GCHandle inlayHintMeasurerHandle;
		private GCHandle iconMeasurerHandle;
		private GCHandle fontMetricsHandle;

		public IntPtr Handle => handle;
		public IntPtr DocumentHandle => documentHandle;

		public EditorCore(TextMeasurer measurer, EditorOptions options) {
			if (measurer.MeasureTextWidth == null) throw new ArgumentException("MeasureTextWidth must be set.", nameof(measurer));
			if (measurer.MeasureInlayHintWidth == null) throw new ArgumentException("MeasureInlayHintWidth must be set.", nameof(measurer));
			if (measurer.MeasureIconWidth == null) throw new ArgumentException("MeasureIconWidth must be set.", nameof(measurer));
			if (measurer.GetFontMetrics == null) throw new ArgumentException("GetFontMetrics must be set.", nameof(measurer));

			if (OperatingSystem.IsWindows() && !exceptionHandlerInitialized) {
				NativeMethods.InitUnhandledExceptionHandler();
				exceptionHandlerInitialized = true;
			}

			textMeasurerHandle = GCHandle.Alloc(measurer.MeasureTextWidth);
			inlayHintMeasurerHandle = GCHandle.Alloc(measurer.MeasureInlayHintWidth);
			iconMeasurerHandle = GCHandle.Alloc(measurer.MeasureIconWidth);
			fontMetricsHandle = GCHandle.Alloc(measurer.GetFontMetrics);

			byte[] optionsData = ProtocolEncoder.PackEditorOptions(options);
			handle = NativeMethods.CreateEditor(measurer, optionsData, (UIntPtr)optionsData.Length);
			if (handle == IntPtr.Zero) {
				throw new InvalidOperationException("Failed to create editor");
			}
		}

		public void SetDocument(IntPtr documentHandle) {
			this.documentHandle = documentHandle;
			NativeMethods.SetEditorDocument(handle, documentHandle);
		}

		public void SetViewport(int width, int height) {
			NativeMethods.SetViewport(handle, width, height);
		}

		public void OnFontMetricsChanged() {
			NativeMethods.OnFontMetricsChanged(handle);
		}

		public void SetFoldArrowMode(FoldArrowMode mode) {
			NativeMethods.SetFoldArrowMode(handle, (int)mode);
		}

		public void SetWrapMode(WrapMode mode) {
			NativeMethods.SetWrapMode(handle, (int)mode);
		}

		public void SetTabSize(int tabSize) {
			NativeMethods.SetTabSize(handle, tabSize);
		}

		public void SetScale(float scale) {
			NativeMethods.SetScale(handle, scale);
		}

		public void SetLineSpacing(float add, float mult) {
			NativeMethods.SetLineSpacing(handle, add, mult);
		}

		public void SetContentStartPadding(float padding) {
			NativeMethods.SetContentStartPadding(handle, padding);
		}

		public void SetShowSplitLine(bool show) {
			NativeMethods.SetShowSplitLine(handle, show ? 1 : 0);
		}

		public void SetGutterSticky(bool sticky) {
			NativeMethods.SetGutterSticky(handle, sticky ? 1 : 0);
		}

		public void SetGutterVisible(bool visible) {
			NativeMethods.SetGutterVisible(handle, visible ? 1 : 0);
		}

		public void SetCurrentLineRenderMode(CurrentLineRenderMode mode) {
			NativeMethods.SetCurrentLineRenderMode(handle, (int)mode);
		}

		public EditorRenderModel BuildRenderModel() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.BuildRenderModel(handle, out outSize);
			return ProtocolDecoder.ParseRenderModel(payloadPtr, outSize);
		}

		public GestureResult HandleGestureEvent(GestureEvent gesture) {
			if (gesture == null) throw new ArgumentNullException(nameof(gesture));
			int pointCount = gesture.Points.Count;
			float[] points = new float[pointCount * 2];
			for (int i = 0; i < pointCount; i++) {
				points[i * 2] = gesture.Points[i].X;
				points[i * 2 + 1] = gesture.Points[i].Y;
			}
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.HandleGestureEventEx(handle, (uint)gesture.Type, (uint)pointCount, points,
				gesture.Modifiers, gesture.WheelDeltaX, gesture.WheelDeltaY, gesture.DirectScale, out outSize);
			return ProtocolDecoder.ParseGestureResult(payloadPtr, outSize);
		}

		public GestureResult TickAnimations() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.TickAnimations(handle, out outSize);
			return ProtocolDecoder.ParseGestureResult(payloadPtr, outSize);
		}

		public KeyEventResult HandleKeyEvent(ushort keyCode, string? text, byte modifiers) {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.HandleKeyEvent(handle, keyCode, text, modifiers, out outSize);
			return ProtocolDecoder.ParseKeyEventResult(payloadPtr, outSize);
		}

		public TextEditResult InsertText(string text) {
			if (text == null) throw new ArgumentNullException(nameof(text));
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.InsertText(handle, text, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult ReplaceText(TextRange range, string text) {
			if (text == null) throw new ArgumentNullException(nameof(text));
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.ReplaceText(handle,
				range.Start.Line, range.Start.Column,
				range.End.Line, range.End.Column,
				text, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult DeleteText(TextRange range) {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.DeleteText(handle,
				range.Start.Line, range.Start.Column,
				range.End.Line, range.End.Column,
				out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult Backspace() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.Backspace(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult DeleteForward() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.DeleteForward(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult MoveLineUp() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.MoveLineUp(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult MoveLineDown() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.MoveLineDown(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult CopyLineUp() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.CopyLineUp(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult CopyLineDown() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.CopyLineDown(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult DeleteLine() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.DeleteLine(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult InsertLineAbove() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.InsertLineAbove(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult InsertLineBelow() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.InsertLineBelow(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public string GetSelectedText() {
			IntPtr cstringPtr = NativeMethods.GetSelectedText(handle);
			if (cstringPtr == IntPtr.Zero) {
				return string.Empty;
			}
			string result = Marshal.PtrToStringUni(cstringPtr) ?? string.Empty;
			NativeMethods.FreeUtf16String(cstringPtr);
			return result;
		}

		public TextEditResult Undo() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.Undo(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public TextEditResult Redo() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.Redo(handle, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public bool CanUndo() {
			return NativeMethods.CanUndo(handle) != 0;
		}

		public bool CanRedo() {
			return NativeMethods.CanRedo(handle) != 0;
		}

		public void SetCursorPosition(TextPosition position) {
			NativeMethods.SetCursorPosition(handle, (nuint)position.Line, (nuint)position.Column);
		}

		public TextPosition GetCursorPosition() {
			nuint line = 0, column = 0;
			NativeMethods.GetCursorPosition(handle, ref line, ref column);
			return new TextPosition { Line = (int)line, Column = (int)column };
		}

		public TextRange GetWordRangeAtCursor() {
			nuint startLine = 0, startColumn = 0, endLine = 0, endColumn = 0;
			NativeMethods.GetWordRangeAtCursor(handle, ref startLine, ref startColumn, ref endLine, ref endColumn);
			return new TextRange {
				Start = new TextPosition { Line = (int)startLine, Column = (int)startColumn },
				End = new TextPosition { Line = (int)endLine, Column = (int)endColumn }
			};
		}

		public string GetWordAtCursor() {
			IntPtr cstringPtr = NativeMethods.GetWordAtCursor(handle);
			if (cstringPtr == IntPtr.Zero) {
				return string.Empty;
			}
			string result = Marshal.PtrToStringUni(cstringPtr) ?? string.Empty;
			NativeMethods.FreeUtf16String(cstringPtr);
			return result;
		}

		public void SetSelection(TextRange range) {
			NativeMethods.SetSelection(handle,
				range.Start.Line, range.Start.Column,
				range.End.Line, range.End.Column);
		}

		public TextRange GetSelection() {
			nuint startLine = 0, startColumn = 0, endLine = 0, endColumn = 0;
			int hasSelection = NativeMethods.GetSelection(handle, ref startLine, ref startColumn, ref endLine, ref endColumn);
			if (hasSelection == 0) {
				return new TextRange {
					Start = new TextPosition { Line = -1, Column = -1 },
					End = new TextPosition { Line = -1, Column = -1 }
				};
			}
			return new TextRange {
				Start = new TextPosition { Line = (int)startLine, Column = (int)startColumn },
				End = new TextPosition { Line = (int)endLine, Column = (int)endColumn }
			};
		}

		public void SelectAll() {
			NativeMethods.SelectAll(handle);
		}

		public void MoveCursorLeft(bool extendSelection) {
			NativeMethods.MoveCursorLeft(handle, extendSelection ? 1 : 0);
		}

		public void MoveCursorRight(bool extendSelection) {
			NativeMethods.MoveCursorRight(handle, extendSelection ? 1 : 0);
		}

		public void MoveCursorUp(bool extendSelection) {
			NativeMethods.MoveCursorUp(handle, extendSelection ? 1 : 0);
		}

		public void MoveCursorDown(bool extendSelection) {
			NativeMethods.MoveCursorDown(handle, extendSelection ? 1 : 0);
		}

		public void MoveCursorToLineStart(bool extendSelection) {
			NativeMethods.MoveCursorToLineStart(handle, extendSelection ? 1 : 0);
		}

		public void MoveCursorToLineEnd(bool extendSelection) {
			NativeMethods.MoveCursorToLineEnd(handle, extendSelection ? 1 : 0);
		}

		public void CompositionStart() {
			NativeMethods.CompositionStart(handle);
		}

		public TextEditResult CompositionUpdate(string text) {
			if (text == null) throw new ArgumentNullException(nameof(text));
			NativeMethods.CompositionUpdate(handle, text);
			return TextEditResult.Empty;
		}

		public TextEditResult CompositionEnd(string text) {
			if (text == null) throw new ArgumentNullException(nameof(text));
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.CompositionEnd(handle, text, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public void CompositionCancel() {
			NativeMethods.CompositionCancel(handle);
		}

		public bool IsComposing() {
			return NativeMethods.IsComposing(handle) != 0;
		}

		public void SetReadOnly(bool readOnly) {
			NativeMethods.SetReadOnly(handle, readOnly ? 1 : 0);
		}

		public bool IsReadOnly() {
			return NativeMethods.IsReadOnly(handle) != 0;
		}

		public void SetAutoIndentMode(AutoIndentMode mode) {
			NativeMethods.SetAutoIndentMode(handle, (int)mode);
		}

		public AutoIndentMode GetAutoIndentMode() {
			return (AutoIndentMode)NativeMethods.GetAutoIndentMode(handle);
		}

		public void SetHandleConfig(HandleConfig config) {
			NativeMethods.SetHandleConfig(handle,
				config.StartLeft, config.StartTop, config.StartRight, config.StartBottom,
				config.EndLeft, config.EndTop, config.EndRight, config.EndBottom);
		}

		public void SetScrollbarConfig(ScrollbarConfig config) {
			NativeMethods.SetScrollbarConfig(handle,
				config.Thickness, config.MinThumb, config.ThumbHitPadding,
				(int)config.Mode, config.ThumbDraggable ? 1 : 0, (int)config.TrackTapMode,
				config.FadeDelayMs, config.FadeDurationMs);
		}

		public void GetPositionRect(TextPosition position, out float x, out float y, out float height) {
			x = 0;
			y = 0;
			height = 0;
			NativeMethods.GetPositionRect(handle, (nuint)position.Line, (nuint)position.Column, ref x, ref y, ref height);
		}

		public void GetCursorRect(out float x, out float y, out float height) {
			x = 0;
			y = 0;
			height = 0;
			NativeMethods.GetCursorRect(handle, ref x, ref y, ref height);
		}

		public void ScrollToLine(int line, ScrollBehavior behavior) {
			NativeMethods.ScrollToLine(handle, line, (byte)behavior);
		}

		public void GotoPosition(int line, int column) {
			NativeMethods.GotoPosition(handle, line, column);
		}

		public void SetScroll(float scrollX, float scrollY) {
			NativeMethods.SetScroll(handle, scrollX, scrollY);
		}

		public ScrollMetrics GetScrollMetrics() {
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.GetScrollMetrics(handle, out outSize);
			return ProtocolDecoder.ParseScrollMetrics(payloadPtr, outSize);
		}

		public void RegisterTextStyle(uint styleId, TextStyle style) {
			NativeMethods.registerTextStyle(handle, styleId, style.Color, style.BackgroundColor, style.FontStyle);
		}

		public void RegisterBatchTextStyles(IReadOnlyDictionary<uint, TextStyle> stylesById) {
			byte[] data = ProtocolEncoder.PackBatchTextStyles(stylesById);
			NativeMethods.registerBatchTextStyles(handle, data, (nuint)data.Length);
		}

		public void SetLineSpans(int line, int layer, IList<StyleSpan> spans) {
			byte[] data = ProtocolEncoder.PackLineSpans(line, layer, spans);
			NativeMethods.SetLineSpans(handle, data, (nuint)data.Length);
		}

		public void SetLineInlayHints(int line, IList<InlayHint> hints) {
			byte[] data = ProtocolEncoder.PackLineInlayHints(line, hints);
			NativeMethods.SetLineInlayHints(handle, data, (nuint)data.Length);
		}

		public void SetLinePhantomTexts(int line, IList<PhantomText> phantoms) {
			byte[] data = ProtocolEncoder.PackLinePhantomTexts(line, phantoms);
			NativeMethods.SetLinePhantomTexts(handle, data, (nuint)data.Length);
		}

		public void SetLineGutterIcons(int line, IList<GutterIcon> icons) {
			byte[] data = ProtocolEncoder.PackLineGutterIcons(line, icons);
			NativeMethods.SetLineGutterIcons(handle, data, (nuint)data.Length);
		}

		public void SetBatchLineSpans(int layer, Dictionary<int, IList<StyleSpan>> spansByLine) {
			byte[] data = ProtocolEncoder.PackBatchLineSpans(layer, spansByLine);
			NativeMethods.SetBatchLineSpans(handle, data, (nuint)data.Length);
		}

		public void SetBatchLineInlayHints(Dictionary<int, IList<InlayHint>> hintsByLine) {
			byte[] data = ProtocolEncoder.PackBatchLineInlayHints(hintsByLine);
			NativeMethods.SetBatchLineInlayHints(handle, data, (nuint)data.Length);
		}

		public void SetBatchLinePhantomTexts(Dictionary<int, IList<PhantomText>> phantomsByLine) {
			byte[] data = ProtocolEncoder.PackBatchLinePhantomTexts(phantomsByLine);
			NativeMethods.SetBatchLinePhantomTexts(handle, data, (nuint)data.Length);
		}

		public void SetBatchLineGutterIcons(Dictionary<int, IList<GutterIcon>> iconsByLine) {
			byte[] data = ProtocolEncoder.PackBatchLineGutterIcons(iconsByLine);
			NativeMethods.SetBatchLineGutterIcons(handle, data, (nuint)data.Length);
		}

		public void SetBatchLineDiagnostics(Dictionary<int, IList<DiagnosticItem>> diagsByLine) {
			byte[] data = ProtocolEncoder.PackBatchLineDiagnostics(diagsByLine);
			NativeMethods.SetBatchLineDiagnostics(handle, data, (nuint)data.Length);
		}

		public void ClearGutterIcons() {
			NativeMethods.ClearGutterIcons(handle);
		}

		public void SetMaxGutterIcons(uint count) {
			NativeMethods.SetMaxGutterIcons(handle, count);
		}

		public void SetLineDiagnostics(int line, IList<DiagnosticItem> items) {
			byte[] data = ProtocolEncoder.PackLineDiagnostics(line, items);
			NativeMethods.SetLineDiagnostics(handle, data, (nuint)data.Length);
		}

		public void ClearDiagnostics() {
			NativeMethods.ClearDiagnostics(handle);
		}

		public void SetIndentGuides(IList<IndentGuide> guides) {
			byte[] data = ProtocolEncoder.PackIndentGuides(guides);
			NativeMethods.SetIndentGuides(handle, data, (nuint)data.Length);
		}

		public void SetBracketGuides(IList<BracketGuide> guides) {
			byte[] data = ProtocolEncoder.PackBracketGuides(guides);
			NativeMethods.SetBracketGuides(handle, data, (nuint)data.Length);
		}

		public void SetFlowGuides(IList<FlowGuide> guides) {
			byte[] data = ProtocolEncoder.PackFlowGuides(guides);
			NativeMethods.SetFlowGuides(handle, data, (nuint)data.Length);
		}

		public void SetSeparatorGuides(IList<SeparatorGuide> guides) {
			byte[] data = ProtocolEncoder.PackSeparatorGuides(guides);
			NativeMethods.SetSeparatorGuides(handle, data, (nuint)data.Length);
		}

		public void ClearGuides() {
			NativeMethods.ClearGuides(handle);
		}

		public void SetFoldRegions(IList<FoldRegion> regions) {
			byte[] data = ProtocolEncoder.PackFoldRegions(regions);
			NativeMethods.SetFoldRegions(handle, data, (nuint)data.Length);
		}

		public bool ToggleFold(uint line) {
			return NativeMethods.ToggleFold(handle, line) != 0;
		}

		public bool FoldAt(uint line) {
			return NativeMethods.FoldAt(handle, line) != 0;
		}

		public bool UnfoldAt(uint line) {
			return NativeMethods.UnfoldAt(handle, line) != 0;
		}

		public void FoldAll() {
			NativeMethods.FoldAll(handle);
		}

		public void UnfoldAll() {
			NativeMethods.UnfoldAll(handle);
		}

		public bool IsLineVisible(uint line) {
			return NativeMethods.IsLineVisible(handle, line) != 0;
		}

		public void ClearHighlights() {
			NativeMethods.ClearHighlights(handle);
		}

		public void ClearHighlightsLayer(byte layer) {
			NativeMethods.ClearHighlightsLayer(handle, layer);
		}

		public void ClearInlayHints() {
			NativeMethods.ClearInlayHints(handle);
		}

		public void ClearPhantomTexts() {
			NativeMethods.ClearPhantomTexts(handle);
		}

		public void ClearAllDecorations() {
			NativeMethods.ClearAllDecorations(handle);
		}

		public void SetBracketPairs(int[] openChars, int[] closeChars) {
			NativeMethods.SetBracketPairs(handle, openChars, closeChars, (nuint)openChars.Length);
		}

		public void SetMatchedBrackets(TextPosition open, TextPosition close) {
			NativeMethods.SetMatchedBrackets(handle, (nuint)open.Line, (nuint)open.Column, (nuint)close.Line, (nuint)close.Column);
		}

		public void ClearMatchedBrackets() {
			NativeMethods.ClearMatchedBrackets(handle);
		}

		public TextEditResult InsertSnippet(string snippetTemplate) {
			if (snippetTemplate == null) throw new ArgumentNullException(nameof(snippetTemplate));
			UIntPtr outSize;
			IntPtr payloadPtr = NativeMethods.InsertSnippet(handle, snippetTemplate, out outSize);
			return ProtocolDecoder.ParseTextEditResult(payloadPtr, outSize);
		}

		public void StartLinkedEditing(LinkedEditingModel model) {
			byte[] data = ProtocolEncoder.PackLinkedEditingPayload(model);
			NativeMethods.StartLinkedEditing(handle, data, (nuint)data.Length);
		}

		public bool IsInLinkedEditing() {
			return NativeMethods.IsInLinkedEditing(handle) != 0;
		}

		public bool LinkedEditingNext() {
			return NativeMethods.LinkedEditingNext(handle) != 0;
		}

		public bool LinkedEditingPrev() {
			return NativeMethods.LinkedEditingPrev(handle) != 0;
		}

		public void CancelLinkedEditing() {
			NativeMethods.CancelLinkedEditing(handle);
		}

		public int GetLineCount() {
			return (int)NativeMethods.GetDocumentLineCount(documentHandle);
		}

		public string GetLineText(int line) {
			IntPtr cstringPtr = NativeMethods.GetDocumentLineText(documentHandle, (UIntPtr)line);
			if (cstringPtr == IntPtr.Zero) {
				return string.Empty;
			}
			string result = Marshal.PtrToStringUni(cstringPtr) ?? string.Empty;
			NativeMethods.FreeUtf16String(cstringPtr);
			return result;
		}

		public void Dispose() {
			if (handle != IntPtr.Zero) {
				NativeMethods.FreeEditor(handle);
				handle = IntPtr.Zero;
			}
			if (textMeasurerHandle.IsAllocated) {
				textMeasurerHandle.Free();
			}
			if (inlayHintMeasurerHandle.IsAllocated) {
				inlayHintMeasurerHandle.Free();
			}
			if (iconMeasurerHandle.IsAllocated) {
				iconMeasurerHandle.Free();
			}
			if (fontMetricsHandle.IsAllocated) {
				fontMetricsHandle.Free();
			}
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		public delegate float MeasureTextWidthDelegate([MarshalAs(UnmanagedType.LPWStr)] string text, int fontStyle);
		[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		public delegate float MeasureInlayHintWidthDelegate([MarshalAs(UnmanagedType.LPWStr)] string text);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
			public delegate float MeasureIconWidthDelegate(int iconId);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public delegate void GetFontMetricsDelegate(IntPtr arrPtr, UIntPtr length);

		[StructLayout(LayoutKind.Sequential)]
		public struct TextMeasurer {
			public MeasureTextWidthDelegate MeasureTextWidth;
			public MeasureInlayHintWidthDelegate MeasureInlayHintWidth;
			public MeasureIconWidthDelegate MeasureIconWidth;
			public GetFontMetricsDelegate GetFontMetrics;
		}
	}
}
