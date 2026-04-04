part of '../editor_core.dart';

/// 2D coordinate.
class PointF {
  const PointF({this.x = 0, this.y = 0});

  final double x;
  final double y;
}

/// Visual run type.
enum VisualRunType {
  text(0),
  whitespace(1),
  newline(2),
  inlayHint(3),
  phantomText(4),
  foldPlaceholder(5),
  tab(6);

  const VisualRunType(this.value);
  final int value;

  static VisualRunType fromValue(int value) => VisualRunType.values.firstWhere(
    (e) => e.value == value,
    orElse: () => text,
  );
}

/// Fold state.
enum FoldState {
  none(0),
  expanded(1),
  collapsed(2);

  const FoldState(this.value);
  final int value;

  static FoldState fromValue(int value) =>
      FoldState.values.firstWhere((e) => e.value == value, orElse: () => none);
}

/// Guide direction.
enum GuideDirection {
  horizontal(0),
  vertical(1);

  const GuideDirection(this.value);
  final int value;

  static GuideDirection fromValue(int value) => GuideDirection.values
      .firstWhere((e) => e.value == value, orElse: () => vertical);
}

/// Guide type.
enum GuideType {
  indent(0),
  bracket(1),
  flow(2),
  separator(3);

  const GuideType(this.value);
  final int value;

  static GuideType fromValue(int value) => GuideType.values.firstWhere(
    (e) => e.value == value,
    orElse: () => indent,
  );
}

/// Guide style.
enum GuideStyle {
  solid(0),
  dashed(1),
  double_(2);

  const GuideStyle(this.value);
  final int value;

  static GuideStyle fromValue(int value) => GuideStyle.values.firstWhere(
    (e) => e.value == value,
    orElse: () => solid,
  );
}

/// Layout metrics snapshot produced by the core.
class LayoutMetrics {
  const LayoutMetrics({
    this.fontHeight = 0,
    this.fontAscent = 0,
    this.lineSpacingAdd = 0,
    this.lineSpacingMult = 1,
    this.lineNumberMargin = 0,
    this.lineNumberWidth = 0,
    this.maxGutterIcons = 0,
    this.inlayHintPadding = 0,
    this.inlayHintMargin = 0,
    this.foldArrowMode = FoldArrowMode.always,
    this.hasFoldRegions = false,
  });

  static const LayoutMetrics empty = LayoutMetrics();

  final double fontHeight;
  final double fontAscent;
  final double lineSpacingAdd;
  final double lineSpacingMult;
  final double lineNumberMargin;
  final double lineNumberWidth;
  final int maxGutterIcons;
  final double inlayHintPadding;
  final double inlayHintMargin;
  final FoldArrowMode foldArrowMode;
  final bool hasFoldRegions;
}

/// A single visual run (text segment or decoration) within a visual line.
class VisualRun {
  const VisualRun({
    required this.type,
    required this.x,
    required this.y,
    this.text = '',
    this.style = const TextStyle(),
    this.iconId = 0,
    this.colorValue = 0,
    this.width = 0,
    this.padding = 0,
    this.margin = 0,
  });

  final VisualRunType type;
  final double x;
  final double y;
  final String text;
  final TextStyle style;
  final int iconId;
  final int colorValue;
  final double width;
  final double padding;
  final double margin;
}

/// A visual line (one wrap-row of a logical line).
class VisualLine {
  const VisualLine({
    required this.logicalLine,
    this.wrapIndex = 0,
    this.lineNumberPosition = const PointF(),
    this.runs = const <VisualRun>[],
    this.isPhantomLine = false,
    this.foldState = FoldState.none,
  });

  final int logicalLine;
  final int wrapIndex;
  final PointF lineNumberPosition;
  final List<VisualRun> runs;
  final bool isPhantomLine;
  final FoldState foldState;
}

/// Gutter icon render item.
class GutterIconRenderItem {
  const GutterIconRenderItem({
    required this.logicalLine,
    required this.iconId,
    required this.origin,
    required this.width,
    required this.height,
  });

  final int logicalLine;
  final int iconId;
  final PointF origin;
  final double width;
  final double height;
}

/// Fold marker render item.
class FoldMarkerRenderItem {
  const FoldMarkerRenderItem({
    required this.logicalLine,
    required this.foldState,
    required this.origin,
    required this.width,
    required this.height,
  });

  final int logicalLine;
  final FoldState foldState;
  final PointF origin;
  final double width;
  final double height;
}

/// Cursor state.
class Cursor {
  const Cursor({
    this.textPosition = const TextPosition(0, 0),
    this.position = const PointF(),
    this.height = 0,
    this.visible = false,
    this.showDragger = false,
  });

  final TextPosition textPosition;
  final PointF position;
  final double height;
  final bool visible;
  final bool showDragger;
}

/// Selection rectangle.
class SelectionRect {
  const SelectionRect({
    required this.origin,
    required this.width,
    required this.height,
  });

  final PointF origin;
  final double width;
  final double height;
}

/// Selection handle.
class SelectionHandle {
  const SelectionHandle({
    this.position = const PointF(),
    this.height = 0,
    this.visible = false,
  });

  final PointF position;
  final double height;
  final bool visible;
}

/// Composition (IME) decoration.
class CompositionDecoration {
  const CompositionDecoration({
    this.active = false,
    this.origin = const PointF(),
    this.width = 0,
    this.height = 0,
  });

  final bool active;
  final PointF origin;
  final double width;
  final double height;
}

/// Guide segment for rendering.
class GuideSegment {
  const GuideSegment({
    required this.direction,
    required this.type,
    required this.style,
    required this.start,
    required this.end,
    this.arrowEnd = false,
  });

  final GuideDirection direction;
  final GuideType type;
  final GuideStyle style;
  final PointF start;
  final PointF end;
  final bool arrowEnd;
}

/// Diagnostic decoration for rendering.
class DiagnosticDecoration {
  const DiagnosticDecoration({
    required this.origin,
    required this.width,
    required this.height,
    required this.severity,
    required this.color,
  });

  final PointF origin;
  final double width;
  final double height;
  final int severity;
  final int color;
}

/// Linked editing rect.
class LinkedEditingRect {
  const LinkedEditingRect({
    required this.origin,
    required this.width,
    required this.height,
    this.isActive = false,
  });

  final PointF origin;
  final double width;
  final double height;
  final bool isActive;
}

/// Bracket highlight rect.
class BracketHighlightRect {
  const BracketHighlightRect({
    required this.origin,
    required this.width,
    required this.height,
  });

  final PointF origin;
  final double width;
  final double height;
}

/// Scrollbar rect.
class ScrollbarRect {
  const ScrollbarRect({
    this.origin = const PointF(),
    this.width = 0,
    this.height = 0,
  });

  final PointF origin;
  final double width;
  final double height;
}

/// Scrollbar model.
class ScrollbarModel {
  const ScrollbarModel({
    this.visible = false,
    this.alpha = 0,
    this.thumbActive = false,
    this.track = const ScrollbarRect(),
    this.thumb = const ScrollbarRect(),
  });

  final bool visible;
  final double alpha;
  final bool thumbActive;
  final ScrollbarRect track;
  final ScrollbarRect thumb;
}

/// Cursor rect (for floating panel positioning).
class CursorRect {
  const CursorRect({this.x = 0, this.y = 0, this.height = 0});

  final double x;
  final double y;
  final double height;
}

/// Scroll metrics.
class ScrollMetrics {
  const ScrollMetrics({
    this.scale = 1,
    this.scrollX = 0,
    this.scrollY = 0,
    this.maxScrollX = 0,
    this.maxScrollY = 0,
    this.contentWidth = 0,
    this.contentHeight = 0,
    this.viewportWidth = 0,
    this.viewportHeight = 0,
    this.textAreaX = 0,
    this.textAreaWidth = 0,
    this.canScrollX = false,
    this.canScrollY = false,
  });

  static const ScrollMetrics empty = ScrollMetrics();

  final double scale;
  final double scrollX;
  final double scrollY;
  final double maxScrollX;
  final double maxScrollY;
  final double contentWidth;
  final double contentHeight;
  final double viewportWidth;
  final double viewportHeight;
  final double textAreaX;
  final double textAreaWidth;
  final bool canScrollX;
  final bool canScrollY;
}

/// Full editor render model (parsed from binary payload).
class EditorRenderModel {
  const EditorRenderModel({
    this.splitX = 0,
    this.splitLineVisible = false,
    this.scrollX = 0,
    this.scrollY = 0,
    this.viewportWidth = 0,
    this.viewportHeight = 0,
    this.currentLine = const PointF(),
    this.currentLineRenderMode = 0,
    this.visualLines = const <VisualLine>[],
    this.gutterIcons = const <GutterIconRenderItem>[],
    this.foldMarkers = const <FoldMarkerRenderItem>[],
    this.cursor = const Cursor(),
    this.selectionRects = const <SelectionRect>[],
    this.selectionStartHandle = const SelectionHandle(),
    this.selectionEndHandle = const SelectionHandle(),
    this.compositionDecoration = const CompositionDecoration(),
    this.guideSegments = const <GuideSegment>[],
    this.diagnosticDecorations = const <DiagnosticDecoration>[],
    this.maxGutterIcons = 0,
    this.linkedEditingRects = const <LinkedEditingRect>[],
    this.bracketHighlightRects = const <BracketHighlightRect>[],
    this.verticalScrollbar = const ScrollbarModel(),
    this.horizontalScrollbar = const ScrollbarModel(),
    this.gutterSticky = false,
    this.gutterVisible = true,
  });

  static const EditorRenderModel empty = EditorRenderModel();

  final double splitX;
  final bool splitLineVisible;
  final double scrollX;
  final double scrollY;
  final double viewportWidth;
  final double viewportHeight;
  final PointF currentLine;
  final int currentLineRenderMode;
  final List<VisualLine> visualLines;
  final List<GutterIconRenderItem> gutterIcons;
  final List<FoldMarkerRenderItem> foldMarkers;
  final Cursor cursor;
  final List<SelectionRect> selectionRects;
  final SelectionHandle selectionStartHandle;
  final SelectionHandle selectionEndHandle;
  final CompositionDecoration compositionDecoration;
  final List<GuideSegment> guideSegments;
  final List<DiagnosticDecoration> diagnosticDecorations;
  final int maxGutterIcons;
  final List<LinkedEditingRect> linkedEditingRects;
  final List<BracketHighlightRect> bracketHighlightRects;
  final ScrollbarModel verticalScrollbar;
  final ScrollbarModel horizontalScrollbar;
  final bool gutterSticky;
  final bool gutterVisible;
}
