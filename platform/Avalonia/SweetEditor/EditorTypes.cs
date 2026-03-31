using System;
using System.Collections.Generic;

namespace SweetEditor {
	public enum FoldArrowMode : int { AUTO, ALWAYS, HIDDEN }
	public enum WrapMode : int {
		NONE = 0,
		CHAR_BREAK = 1,
		WORD_BREAK = 2,
		CHARACTER = CHAR_BREAK
	}
	public enum CurrentLineRenderMode : int { BACKGROUND, BORDER, NONE }
	public enum AutoIndentMode : int {
		NONE = 0,
		KEEP_INDENT = 1,
		TABS = KEEP_INDENT,
		SPACES = KEEP_INDENT
	}
	public enum ScrollBehavior {
		TOP = 0,
		CENTER = 1,
		BOTTOM = 2,
		NEAREST = TOP
	}
	public enum ScrollbarMode {
		ALWAYS = 0,
		TRANSIENT = 1,
		NEVER = 2,
		AUTO = TRANSIENT
	}
	public enum ScrollbarTrackTapMode {
		JUMP = 0,
		DISABLED = 1,
		PAGE_UP_DOWN = JUMP,
		JUMP_TO_CURSOR = DISABLED
	}
	public enum SpanLayer { SYNTAX = 0, SEMANTIC = 1 }
	public enum GestureType {
		UNDEFINED = 0,
		TAP = 1,
		DOUBLE_TAP = 2,
		LONG_PRESS = 3,
		SCALE = 4,
		SCROLL = 5,
		FAST_SCROLL = 6,
		DRAG_SELECT = 7,
		CONTEXT_MENU = 8
	}
	public enum HitTargetType {
		NONE = 0,
		INLAY_HINT_TEXT = 1,
		INLAY_HINT_ICON = 2,
		GUTTER_ICON = 3,
		FOLD_PLACEHOLDER = 4,
		FOLD_GUTTER = 5,
		INLAY_HINT_COLOR = 6,
		TEXT = NONE,
		FOLD_MARKER = FOLD_GUTTER,
		SELECTION_HANDLE = NONE
	}
	public enum VisualRunType {
		TEXT = 0,
		WHITESPACE = 1,
		NEWLINE = 2,
		INLAY_HINT = 3,
		PHANTOM_TEXT = 4,
		FOLD_PLACEHOLDER = 5,
		TAB = 6,
		ICON = 7,
	}

	public enum FoldState {
		NONE = 0,
		EXPANDED = 1,
		FOLDED = 2,
		PARENT = 3,
	}
	public enum GuideDirection { VERTICAL, HORIZONTAL }
	public enum GuideType { INDENT, BRACKET, FLOW, SEPARATOR }
	public enum GuideStyle { SOLID, DASHED, DOTTED }
	public enum TextChangeAction { Key, Paste, Undo, Redo, Delete, Insert, Replace, Composition, Other }

	public struct TextPosition {
		public int Line;
		public int Column;
		public override string ToString() => $"({Line}, {Column})";
	}

	public struct TextRange {
		public TextPosition Start;
		public TextPosition End;
		public TextRange(TextPosition start, TextPosition end) {
			Start = start;
			End = end;
		}
		public override string ToString() => $"{Start}-{End}";
	}

	public struct PointF {
		public float X;
		public float Y;
		public PointF(float x, float y) { X = x; Y = y; }
		public override string ToString() => $"({X}, {Y})";
	}

	public struct CursorRect {
		public float X;
		public float Y;
		public float Height;

		public override string ToString() => $"CursorRect(X={X}, Y={Y}, Height={Height})";
	}

	public struct TextStyle {
		public int Color;
		public int BackgroundColor;
		public int FontStyle;

		public TextStyle(int color, int fontStyle) {
			Color = color;
			BackgroundColor = 0;
			FontStyle = fontStyle;
		}

		public TextStyle(int color, int backgroundColor, int fontStyle) {
			Color = color;
			BackgroundColor = backgroundColor;
			FontStyle = fontStyle;
		}
	}

	public struct StyleSpan {
		public int Column;
		public int Length;
		public int StyleId;
		public StyleSpan(int column, int length, int styleId) {
			Column = column;
			Length = length;
			StyleId = styleId;
		}
	}

	public struct InlayHint {
		public int Column;
		public InlayType Type;
		public string? Text;
		public int IntValue;
		public InlayHint(int column, InlayType type, string? text, int intValue) {
			Column = column;
			Type = type;
			Text = text;
			IntValue = intValue;
		}
		public static InlayHint TextHint(int column, string text) => new(column, InlayType.Text, text, 0);
		public static InlayHint IconHint(int column, int iconId) => new(column, InlayType.Icon, null, iconId);
		public static InlayHint ColorHint(int column, int color) => new(column, InlayType.Color, null, color);
	}

	public enum InlayType { Text, Icon, Color }

	public struct PhantomText {
		public int Column;
		public string? Text;
		public PhantomText(int column, string? text) {
			Column = column;
			Text = text;
		}
	}

	public struct GutterIcon {
		public int IconId;
		public GutterIcon(int iconId) { IconId = iconId; }
	}

	public struct DiagnosticItem {
		public int Column;
		public int Length;
		public int Severity;
		public int Color;
		public DiagnosticItem(int column, int length, int severity, int color) {
			Column = column;
			Length = length;
			Severity = severity;
			Color = color;
		}
	}

	public struct FoldRegion {
		public int StartLine;
		public int EndLine;
		public FoldRegion(int startLine, int endLine) {
			StartLine = startLine;
			EndLine = endLine;
		}
	}

	public struct IndentGuide {
		public TextPosition Start;
		public TextPosition End;
		public IndentGuide(TextPosition start, TextPosition end) {
			Start = start;
			End = end;
		}
	}

	public struct BracketGuide {
		public TextPosition Parent;
		public TextPosition End;
		public TextPosition[]? Children;
		public BracketGuide(TextPosition parent, TextPosition end, TextPosition[]? children) {
			Parent = parent;
			End = end;
			Children = children;
		}
	}

	public struct FlowGuide {
		public TextPosition Start;
		public TextPosition End;
		public FlowGuide(TextPosition start, TextPosition end) {
			Start = start;
			End = end;
		}
	}

	public struct SeparatorGuide {
		public int Line;
		public int Style;
		public int Count;
		public int TextEndColumn;
		public SeparatorGuide(int line, int style, int count, int textEndColumn) {
			Line = line;
			Style = style;
			Count = count;
			TextEndColumn = textEndColumn;
		}
	}

	public struct EditorOptions {
		public float TouchSlop = 8.0f;
		public long DoubleTapTimeout = 300;
		public long LongPressMs = 500;
		public float FlingFriction = 0.8f;
		public float FlingMinVelocity = 50.0f;
		public float FlingMaxVelocity = 15000.0f;
		public ulong MaxUndoStackSize = 100;

		public EditorOptions() {
		}

		public EditorOptions(float touchSlop, long doubleTapTimeout, long longPressMs, float flingFriction, float flingMinVelocity, float flingMaxVelocity, ulong maxUndoStackSize) {
			TouchSlop = touchSlop;
			DoubleTapTimeout = doubleTapTimeout;
			LongPressMs = longPressMs;
			FlingFriction = flingFriction;
			FlingMinVelocity = flingMinVelocity;
			FlingMaxVelocity = flingMaxVelocity;
			MaxUndoStackSize = maxUndoStackSize;
		}
	}

	public struct VisualRun {
		public VisualRunType Type;
		public float X;
		public float Y;
		public string? Text;
		public TextStyle Style;
		public int IconId;
		public int ColorValue;
		public float Width;
		public float Padding;
		public float Margin;

		public VisualRun() {
		}

		public VisualRun(VisualRunType type, float x, float y, string text, TextStyle style, int iconId, int colorValue, float width, float padding, float margin) {
			Type = type;
			X = x;
			Y = y;
			Text = text;
			Style = style;
			IconId = iconId;
			ColorValue = colorValue;
			Width = width;
			Padding = padding;
			Margin = margin;
		}
	}

	public struct VisualLine {
		public int LogicalLine;
		public int WrapIndex;
		public PointF LineNumberPosition;
		public List<VisualRun>? Runs;
		public bool IsPhantomLine;
		public FoldState FoldState;

		public VisualLine() {
		}

		public VisualLine(int logicalLine, int wrapIndex, PointF lineNumberPosition, List<VisualRun> runs, bool isPhantomLine, FoldState foldState) {
			LogicalLine = logicalLine;
			WrapIndex = wrapIndex;
			LineNumberPosition = lineNumberPosition;
			Runs = runs;
			IsPhantomLine = isPhantomLine;
			FoldState = foldState;
		}
	}

	public struct GutterIconRenderItem {
		public int LogicalLine;
		public int IconId;
		public PointF Origin;
		public float Width;
		public float Height;

		public GutterIconRenderItem() {
		}

		public GutterIconRenderItem(int logicalLine, int iconId, PointF origin, float width, float height) {
			LogicalLine = logicalLine;
			IconId = iconId;
			Origin = origin;
			Width = width;
			Height = height;
		}
	}

	public struct FoldMarkerRenderItem {
		public int LogicalLine;
		public FoldState FoldState;
		public PointF Origin;
		public float Width;
		public float Height;

		public FoldMarkerRenderItem() {
		}

		public FoldMarkerRenderItem(int logicalLine, FoldState foldState, PointF origin, float width, float height) {
			LogicalLine = logicalLine;
			FoldState = foldState;
			Origin = origin;
			Width = width;
			Height = height;
		}
	}

	public struct Cursor {
		public TextPosition TextPosition;
		public PointF Position;
		public float Height;
		public bool Visible;
		public bool ShowDragger;

		public Cursor() {
		}

		public Cursor(TextPosition textPosition, PointF position, float height, bool visible, bool showDragger) {
			TextPosition = textPosition;
			Position = position;
			Height = height;
			Visible = visible;
			ShowDragger = showDragger;
		}
	}

	public struct SelectionRect {
		public PointF Origin;
		public float Width;
		public float Height;

		public SelectionRect() {
		}

		public SelectionRect(PointF origin, float width, float height) {
			Origin = origin;
			Width = width;
			Height = height;
		}
	}

	public struct SelectionHandle {
		public PointF Position;
		public float Height;
		public bool Visible;

		public SelectionHandle() {
		}

		public SelectionHandle(PointF position, float height, bool visible) {
			Position = position;
			Height = height;
			Visible = visible;
		}
	}

	public struct CompositionDecoration {
		public bool Active;
		public PointF Origin;
		public float Width;
		public float Height;

		public CompositionDecoration() {
		}

		public CompositionDecoration(bool active, PointF origin, float width, float height) {
			Active = active;
			Origin = origin;
			Width = width;
			Height = height;
		}
	}

	public struct GuideSegment {
		public GuideDirection Direction;
		public GuideType Type;
		public GuideStyle Style;
		public PointF Start;
		public PointF End;
		public bool ArrowEnd;

		public GuideSegment() {
		}

		public GuideSegment(GuideDirection direction, GuideType type, GuideStyle style, PointF start, PointF end, bool arrowEnd) {
			Direction = direction;
			Type = type;
			Style = style;
			Start = start;
			End = end;
			ArrowEnd = arrowEnd;
		}
	}

	public struct DiagnosticDecoration {
		public PointF Origin;
		public float Width;
		public float Height;
		public int Severity;
		public int Color;

		public DiagnosticDecoration() {
		}

		public DiagnosticDecoration(PointF origin, float width, float height, int severity, int color) {
			Origin = origin;
			Width = width;
			Height = height;
			Severity = severity;
			Color = color;
		}
	}

	public struct LinkedEditingRect {
		public PointF Origin;
		public float Width;
		public float Height;
		public bool IsActive;

		public LinkedEditingRect() {
		}

		public LinkedEditingRect(PointF origin, float width, float height, bool isActive) {
			Origin = origin;
			Width = width;
			Height = height;
			IsActive = isActive;
		}
	}

	public struct BracketHighlightRect {
		public PointF Origin;
		public float Width;
		public float Height;

		public BracketHighlightRect() {
		}

		public BracketHighlightRect(PointF origin, float width, float height) {
			Origin = origin;
			Width = width;
			Height = height;
		}
	}

	public struct ScrollbarRect {
		public PointF Origin;
		public float Width;
		public float Height;

		public ScrollbarRect() {
		}

		public ScrollbarRect(PointF origin, float width, float height) {
			Origin = origin;
			Width = width;
			Height = height;
		}
	}

	public struct ScrollbarModel {
		public bool Visible;
		public float Alpha;
		public bool ThumbActive;
		public ScrollbarRect Track;
		public ScrollbarRect Thumb;

		public ScrollbarModel() {
		}

		public ScrollbarModel(bool visible, float alpha, bool thumbActive, ScrollbarRect track, ScrollbarRect thumb) {
			Visible = visible;
			Alpha = alpha;
			ThumbActive = thumbActive;
			Track = track;
			Thumb = thumb;
		}
	}

	public struct EditorRenderModel {
		public float SplitX;
		public bool SplitLineVisible;
		public float ScrollX;
		public float ScrollY;
		public float ViewportWidth;
		public float ViewportHeight;
		public PointF CurrentLine;
		public CurrentLineRenderMode CurrentLineRenderMode;
		public List<VisualLine> VisualLines;
		public List<GutterIconRenderItem> GutterIcons;
		public List<FoldMarkerRenderItem> FoldMarkers;
		public Cursor Cursor;
		public List<SelectionRect> SelectionRects;
		public SelectionHandle SelectionStartHandle;
		public SelectionHandle SelectionEndHandle;
		public CompositionDecoration CompositionDecoration;
		public List<GuideSegment> GuideSegments;
		public List<DiagnosticDecoration> DiagnosticDecorations;
		public int MaxGutterIcons;
		public List<LinkedEditingRect> LinkedEditingRects;
		public List<BracketHighlightRect> BracketHighlightRects;
		public ScrollbarModel VerticalScrollbar;
		public ScrollbarModel HorizontalScrollbar;
		public bool GutterSticky;
		public bool GutterVisible;
	}

	public struct TextChange {
		public TextRange Range;
		public string NewText;
	}

	public struct TextEditResult {
		public List<TextChange> Changes;
		public static TextEditResult Empty => new TextEditResult { Changes = new List<TextChange>() };
	}

	public struct KeyEventResult {
		public bool Handled;
		public bool ContentChanged;
		public bool CursorChanged;
		public bool SelectionChanged;
		public TextEditResult? EditResult;
	}

	public struct GestureResult {
		public GestureType Type;
		public PointF TapPoint;
		public TextPosition CursorPosition;
		public bool HasSelection;
		public TextRange Selection;
		public float ViewScrollX;
		public float ViewScrollY;
		public float ViewScale;
		public HitTarget HitTarget;
		public bool NeedsEdgeScroll;
		public bool NeedsFling;
		public bool NeedsAnimation;
	}

	public struct HitTarget {
		public HitTargetType Type;
		public int Line;
		public int Column;
		public int IconId;
		public int ColorValue;
	}

	public struct ScrollMetrics {
		public float Scale;
		public float ScrollX;
		public float ScrollY;
		public float MaxScrollX;
		public float MaxScrollY;
		public float ContentWidth;
		public float ContentHeight;
		public float ViewportWidth;
		public float ViewportHeight;
		public float TextAreaX;
		public float TextAreaWidth;
		public int CanScrollXInt;
		public int CanScrollYInt;
	}

	public struct HandleConfig {
		public float StartLeft;
		public float StartTop;
		public float StartRight;
		public float StartBottom;
		public float EndLeft;
		public float EndTop;
		public float EndRight;
		public float EndBottom;
	}

	public struct ScrollbarConfig {
		public float Thickness;
		public float MinThumb;
		public float ThumbHitPadding;
		public ScrollbarMode Mode;
		public bool ThumbDraggable;
		public ScrollbarTrackTapMode TrackTapMode;
		public int FadeDelayMs;
		public int FadeDurationMs;
	}

	public class LinkedEditingGroup {
		public int Index;
		public string? DefaultText;
		public List<TextRange> Ranges = new();
	}

	public class LinkedEditingModel {
		public List<LinkedEditingGroup> Groups = new();
	}

	public interface IEditorMetadata { }

	public class LanguageConfiguration {
		public string LanguageId { get; set; } = string.Empty;
		public List<BracketPair>? Brackets { get; set; }
		public List<BracketPair>? AutoClosingPairs { get; set; }
		public string? LineComment { get; set; }
		public BlockComment? BlockCommentValue { get; set; }
		public int? TabSize { get; set; }
		public bool? InsertSpaces { get; set; }
	}

	public class BracketPair {
		public string Open { get; set; } = string.Empty;
		public string Close { get; set; } = string.Empty;
	}

	public class BlockComment {
		public string Open { get; set; } = string.Empty;
		public string Close { get; set; } = string.Empty;
	}

	public class Document {
		private readonly IntPtr handle;

		public IntPtr Handle => handle;

		public Document(string text) {
			handle = NativeMethods.CreateDocument(text);
			if (handle == IntPtr.Zero) {
				throw new InvalidOperationException("Failed to create document");
			}
		}

		public string GetLineText(int line) {
			IntPtr cstringPtr = NativeMethods.GetDocumentLineText(handle, (UIntPtr)line);
			if (cstringPtr == IntPtr.Zero) {
				return string.Empty;
			}
			string result = System.Runtime.InteropServices.Marshal.PtrToStringUni(cstringPtr) ?? string.Empty;
			NativeMethods.FreeUtf16String(cstringPtr);
			return result;
		}

		public int GetLineCount() {
			return (int)NativeMethods.GetDocumentLineCount(handle);
		}
	}

	public class EditorTheme {
		public const uint STYLE_KEYWORD = 1;
		public const uint STYLE_STRING = 2;
		public const uint STYLE_COMMENT = 3;
		public const uint STYLE_NUMBER = 4;
		public const uint STYLE_BUILTIN = 5;
		public const uint STYLE_TYPE = 6;
		public const uint STYLE_CLASS = 7;
		public const uint STYLE_FUNCTION = 8;
		public const uint STYLE_VARIABLE = 9;
		public const uint STYLE_PUNCTUATION = 10;
		public const uint STYLE_ANNOTATION = 11;
		public const uint STYLE_PREPROCESSOR = 12;
		public const uint STYLE_USER_BASE = 100;

		public uint BackgroundColor;
		public uint ForegroundColor;
		public uint LineNumberColor;
		public uint CurrentLineNumberColor;
		public uint SelectionBackgroundColor;
		public uint CurrentLineColor;
		public uint CursorColor;
		public uint GutterBackgroundColor;
		public uint GutterSplitLineColor;
		public uint GuideColor;
		public uint SeparatorColor;
		public uint SplitLineColor;
		public uint ScrollbarTrackColor;
		public uint ScrollbarThumbColor;
		public uint ScrollbarThumbActiveColor;
		public uint CompositionColor;
		public uint InlayHintBgColor;
		public uint InlayHintTextColor;
		public uint InlayHintIconColor;
		public uint PhantomTextColor;
		public uint FoldPlaceholderBgColor;
		public uint FoldPlaceholderTextColor;
		public uint DiagnosticErrorColor;
		public uint DiagnosticWarningColor;
		public uint DiagnosticInfoColor;
		public uint DiagnosticHintColor;
		public uint LinkedEditingActiveColor;
		public uint LinkedEditingInactiveColor;
		public uint BracketHighlightBorderColor;
		public uint BracketHighlightBgColor;
		public uint CompletionBgColor;
		public uint CompletionBorderColor;
		public uint CompletionSelectedBgColor;
		public uint CompletionLabelColor;
		public uint CompletionDetailColor;
		public Dictionary<uint, TextStyle> TextStyles { get; set; } = new();

		public static EditorTheme Dark() => new EditorTheme {
			BackgroundColor = 0xFF1B1E24,
			ForegroundColor = 0xFFD7DEE9,
			LineNumberColor = 0xFF7A828E,
			CurrentLineNumberColor = 0xFFD7DEE9,
			SelectionBackgroundColor = 0xFF454B59,
			CurrentLineColor = 0xFF2C313A,
			CursorColor = 0xFF528BFF,
			GutterBackgroundColor = 0xFF1B1E24,
			GutterSplitLineColor = 0xFF2C313A,
			GuideColor = 0xFF3E4451,
			SeparatorColor = 0xFF3E4451,
			SplitLineColor = 0xFF3E4451,
			ScrollbarTrackColor = 0x48FFFFFF,
			ScrollbarThumbColor = 0xAA858585,
			ScrollbarThumbActiveColor = 0xFFBBBBBB,
			CompositionColor = 0xFFD7DEE9,
			InlayHintBgColor = 0xFF2C313A,
			InlayHintTextColor = 0xFF7A828E,
			InlayHintIconColor = 0xFF7A828E,
			PhantomTextColor = 0xFF7A828E,
			FoldPlaceholderBgColor = 0xFF2C313A,
			FoldPlaceholderTextColor = 0xFF7A828E,
			DiagnosticErrorColor = 0xFFF44747,
			DiagnosticWarningColor = 0xFFFFCC6D,
			DiagnosticInfoColor = 0xFF75BEFF,
			DiagnosticHintColor = 0xFF75BEFF,
			LinkedEditingActiveColor = 0x80528BFF,
			LinkedEditingInactiveColor = 0x80528BFF,
			BracketHighlightBorderColor = 0xFF528BFF,
			BracketHighlightBgColor = 0x20528BFF,
			CompletionBgColor = 0xFF1B1E24,
			CompletionBorderColor = 0xFF3E4451,
			CompletionSelectedBgColor = 0xFF2C313A,
			CompletionLabelColor = 0xFFD7DEE9,
			CompletionDetailColor = 0xFF7A828E,
			TextStyles = new() {
				[STYLE_KEYWORD] = new TextStyle(unchecked((int)0xFF7AA2F7), 0, 1),
				[STYLE_STRING] = new TextStyle(unchecked((int)0xFF9ECE6A), 0, 0),
				[STYLE_COMMENT] = new TextStyle(unchecked((int)0xFF7A8294), 0, 2),
				[STYLE_NUMBER] = new TextStyle(unchecked((int)0xFFFF9E64), 0, 0),
				[STYLE_BUILTIN] = new TextStyle(unchecked((int)0xFF7DCFFF), 0, 0),
				[STYLE_TYPE] = new TextStyle(unchecked((int)0xFFBB9AF7), 0, 0),
				[STYLE_CLASS] = new TextStyle(unchecked((int)0xFFE0AF68), 0, 1),
				[STYLE_FUNCTION] = new TextStyle(unchecked((int)0xFF73DACA), 0, 0),
				[STYLE_VARIABLE] = new TextStyle(unchecked((int)0xFFD7DEE9), 0, 0),
				[STYLE_PUNCTUATION] = new TextStyle(unchecked((int)0xFFB0BED3), 0, 0),
				[STYLE_ANNOTATION] = new TextStyle(unchecked((int)0xFF2AC3DE), 0, 0),
				[STYLE_PREPROCESSOR] = new TextStyle(unchecked((int)0xFFF7768E), 0, 0),
			},
		};

		public static EditorTheme Light() => new EditorTheme {
			BackgroundColor = 0xFFFAFBFD,
			ForegroundColor = 0xFF1F2937,
			LineNumberColor = 0xFF9CA3AF,
			CurrentLineNumberColor = 0xFF1F2937,
			SelectionBackgroundColor = 0xFFBFDBFE,
			CurrentLineColor = 0xFFF3F4F6,
			CursorColor = 0xFF3B82F6,
			GutterBackgroundColor = 0xFFFAFBFD,
			GutterSplitLineColor = 0xFFE5E7EB,
			GuideColor = 0xFFE5E7EB,
			SeparatorColor = 0xFFE5E7EB,
			SplitLineColor = 0xFFE5E7EB,
			ScrollbarTrackColor = 0x48000000,
			ScrollbarThumbColor = 0xAA858585,
			ScrollbarThumbActiveColor = 0xFFBBBBBB,
			CompositionColor = 0xFF1F2937,
			InlayHintBgColor = 0xFFF3F4F6,
			InlayHintTextColor = 0xFF9CA3AF,
			InlayHintIconColor = 0xFF9CA3AF,
			PhantomTextColor = 0xFF9CA3AF,
			FoldPlaceholderBgColor = 0xFFF3F4F6,
			FoldPlaceholderTextColor = 0xFF9CA3AF,
			DiagnosticErrorColor = 0xFFDC2626,
			DiagnosticWarningColor = 0xFFD97706,
			DiagnosticInfoColor = 0xFF2563EB,
			DiagnosticHintColor = 0xFF2563EB,
			LinkedEditingActiveColor = 0x803B82F6,
			LinkedEditingInactiveColor = 0x803B82F6,
			BracketHighlightBorderColor = 0xFF3B82F6,
			BracketHighlightBgColor = 0x203B82F6,
			CompletionBgColor = 0xFFFFFFFF,
			CompletionBorderColor = 0xFFE5E7EB,
			CompletionSelectedBgColor = 0xFFF3F4F6,
			CompletionLabelColor = 0xFF1F2937,
			CompletionDetailColor = 0xFF9CA3AF,
			TextStyles = new() {
				[STYLE_KEYWORD] = new TextStyle(unchecked((int)0xFF3559D6), 0, 1),
				[STYLE_STRING] = new TextStyle(unchecked((int)0xFF0F7B6C), 0, 0),
				[STYLE_COMMENT] = new TextStyle(unchecked((int)0xFF7B8798), 0, 2),
				[STYLE_NUMBER] = new TextStyle(unchecked((int)0xFFB45309), 0, 0),
				[STYLE_BUILTIN] = new TextStyle(unchecked((int)0xFF006E7F), 0, 0),
				[STYLE_TYPE] = new TextStyle(unchecked((int)0xFF6D28D9), 0, 0),
				[STYLE_CLASS] = new TextStyle(unchecked((int)0xFF9A3412), 0, 1),
				[STYLE_FUNCTION] = new TextStyle(unchecked((int)0xFF0E7490), 0, 0),
				[STYLE_VARIABLE] = new TextStyle(unchecked((int)0xFF1F2937), 0, 0),
				[STYLE_PUNCTUATION] = new TextStyle(unchecked((int)0xFF6E82A0), 0, 0),
				[STYLE_ANNOTATION] = new TextStyle(unchecked((int)0xFF0F766E), 0, 0),
				[STYLE_PREPROCESSOR] = new TextStyle(unchecked((int)0xFFBE123C), 0, 0),
			},
		};

		public EditorTheme DefineTextStyle(uint styleId, TextStyle style) {
			TextStyles[styleId] = style;
			return this;
		}
	}

	public class GestureEvent {
		public EventType Type;
		public List<PointF> Points = new();
		public byte Modifiers;
		public float WheelDeltaX;
		public float WheelDeltaY;
		public float DirectScale;
	}

	public enum EventType {
		UNDEFINED = 0,
		TOUCH_DOWN = 1,
		TOUCH_POINTER_DOWN = 2,
		TOUCH_MOVE = 3,
		TOUCH_POINTER_UP = 4,
		TOUCH_UP = 5,
		TOUCH_CANCEL = 6,
		MOUSE_DOWN = 7,
		MOUSE_MOVE = 8,
		MOUSE_UP = 9,
		MOUSE_WHEEL = 10,
		MOUSE_RIGHT_DOWN = 11,
		DIRECT_SCALE = 12,
		DIRECT_SCROLL = 13,
	}
}
