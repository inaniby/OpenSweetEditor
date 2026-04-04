part of '../editor_core.dart';

/// Highlight layer.
enum SpanLayer {
  syntax(0),
  semantic(1);

  const SpanLayer(this.value);
  final int value;
}

/// Inlay hint type.
enum InlayType {
  text(0),
  icon(1),
  color(2);

  const InlayType(this.value);
  final int value;
}

/// Text style (color + background + font attributes).
class TextStyle {
  const TextStyle({
    this.color = 0,
    this.backgroundColor = 0,
    this.fontStyle = 0,
  });

  final int color;
  final int backgroundColor;
  final int fontStyle;
}

/// Style span for a line region.
class StyleSpan {
  const StyleSpan({
    required this.column,
    required this.length,
    required this.styleId,
  });

  final int column;
  final int length;
  final int styleId;
}

/// Inlay hint.
class InlayHint {
  const InlayHint({
    required this.type,
    required this.column,
    this.text,
    this.intValue = 0,
  });

  final InlayType type;
  final int column;
  final String? text;
  final int intValue;
}

/// Phantom text.
class PhantomText {
  const PhantomText({required this.column, required this.text});

  final int column;
  final String text;
}

/// Gutter icon.
class GutterIcon {
  const GutterIcon({required this.iconId});

  final int iconId;
}

/// Separator line style.
enum SeparatorStyle {
  single(0),
  double_(1);

  const SeparatorStyle(this.value);
  final int value;
}

/// Minimal diagnostic decoration model for a line.
class Diagnostic {
  const Diagnostic({
    required this.column,
    required this.length,
    required this.severity,
    required this.color,
  });

  final int column;
  final int length;
  final int severity;
  final int color;
}

/// Fold region.
class FoldRegion {
  const FoldRegion({required this.startLine, required this.endLine});

  final int startLine;
  final int endLine;
}

/// Indent guide.
class IndentGuide {
  const IndentGuide({required this.start, required this.end});

  final TextPosition start;
  final TextPosition end;
}

/// Bracket guide.
class BracketGuide {
  const BracketGuide({
    required this.parent,
    required this.end,
    this.children = const <TextPosition>[],
  });

  final TextPosition parent;
  final TextPosition end;
  final List<TextPosition> children;
}

/// Control-flow back-arrow guide.
class FlowGuide {
  const FlowGuide({required this.start, required this.end});

  final TextPosition start;
  final TextPosition end;
}

/// Horizontal separator guide.
class SeparatorGuide {
  const SeparatorGuide({
    required this.line,
    required this.style,
    required this.count,
    required this.textEndColumn,
  });

  final int line;
  final SeparatorStyle style;
  final int count;
  final int textEndColumn;
}
