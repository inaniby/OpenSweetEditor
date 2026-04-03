part of '../editor_core.dart';

class SweetEditorException implements Exception {
  SweetEditorException(this.message);

  final String message;

  @override
  String toString() => 'SweetEditorException: $message';
}

/// Gesture event types.
class EventType {
  EventType._();

  static const int undefined = 0;
  static const int touchDown = 1;
  static const int touchPointerDown = 2;
  static const int touchMove = 3;
  static const int touchPointerUp = 4;
  static const int touchUp = 5;
  static const int touchCancel = 6;
  static const int mouseDown = 7;
  static const int mouseMove = 8;
  static const int mouseUp = 9;
  static const int mouseWheel = 10;
  static const int mouseRightDown = 11;
  static const int directScale = 12;
  static const int directScroll = 13;
}

/// Modifier key flags.
class Modifier {
  Modifier._();

  static const int none = 0;
  static const int shift = 1;
  static const int ctrl = 2;
  static const int alt = 4;
  static const int meta = 8;
}

/// Gesture type (result type, not input event type).
enum GestureType {
  undefined(0),
  tap(1),
  doubleTap(2),
  longPress(3),
  scale(4),
  scroll(5),
  fastScroll(6),
  dragSelect(7),
  contextMenu(8);

  const GestureType(this.value);
  final int value;

  static GestureType fromValue(int value) => GestureType.values.firstWhere(
    (e) => e.value == value,
    orElse: () => undefined,
  );
}

/// Hit target type.
enum HitTargetType {
  none(0),
  inlayHintText(1),
  inlayHintIcon(2),
  gutterIcon(3),
  foldPlaceholder(4),
  foldGutter(5),
  inlayHintColor(6);

  const HitTargetType(this.value);
  final int value;

  static HitTargetType fromValue(int value) => HitTargetType.values.firstWhere(
    (e) => e.value == value,
    orElse: () => none,
  );
}

/// Scrollbar mode.
enum ScrollbarMode {
  always(0),
  transient(1),
  never(2);

  const ScrollbarMode(this.value);
  final int value;
}

/// Scrollbar track tap mode.
enum ScrollbarTrackTapMode {
  jump(0),
  disabled(1);

  const ScrollbarTrackTapMode(this.value);
  final int value;
}

/// Key code definitions matching the C++ enum.
enum KeyCode {
  none(0),
  backspace(8),
  tab(9),
  enter(13),
  escape(27),
  deleteKey(46),
  left(37),
  up(38),
  right(39),
  down(40),
  home(36),
  end(35),
  pageUp(33),
  pageDown(34),
  a(65),
  c(67),
  v(86),
  x(88),
  z(90),
  y(89),
  k(75);

  const KeyCode(this.value);
  final int value;
}

/// A single text change from an edit operation.
class TextChange {
  const TextChange(this.range, this.newText);

  final TextRange range;
  final String newText;
}

/// Result of a text edit operation.
class TextEditResult {
  const TextEditResult({
    required this.changed,
    this.changes = const <TextChange>[],
  });

  static const TextEditResult empty = TextEditResult(changed: false);

  final bool changed;
  final List<TextChange> changes;
}

/// Hit target from a gesture.
class HitTarget {
  const HitTarget({
    this.type = HitTargetType.none,
    this.line = 0,
    this.column = 0,
    this.iconId = 0,
    this.colorValue = 0,
  });

  final HitTargetType type;
  final int line;
  final int column;
  final int iconId;
  final int colorValue;
}

/// Result of a gesture event.
class GestureResult {
  const GestureResult({
    this.type = GestureType.undefined,
    this.tapPoint = const PointF(),
    this.cursorPosition = const TextPosition(0, 0),
    this.hasSelection = false,
    this.selection = const TextRange(TextPosition(0, 0), TextPosition(0, 0)),
    this.viewScrollX = 0,
    this.viewScrollY = 0,
    this.viewScale = 1,
    this.hitTarget = const HitTarget(),
    this.needsEdgeScroll = false,
    this.needsFling = false,
    this.needsAnimation = false,
    this.isHandleDrag = false,
  });

  static const GestureResult empty = GestureResult();

  final GestureType type;
  final PointF tapPoint;
  final TextPosition cursorPosition;
  final bool hasSelection;
  final TextRange selection;
  final double viewScrollX;
  final double viewScrollY;
  final double viewScale;
  final HitTarget hitTarget;
  final bool needsEdgeScroll;
  final bool needsFling;
  final bool needsAnimation;
  final bool isHandleDrag;
}

/// Result of a keyboard event.
class KeyEventResult {
  const KeyEventResult({
    this.handled = false,
    this.contentChanged = false,
    this.cursorChanged = false,
    this.selectionChanged = false,
    this.editResult,
  });

  static const KeyEventResult empty = KeyEventResult();

  final bool handled;
  final bool contentChanged;
  final bool cursorChanged;
  final bool selectionChanged;
  final TextEditResult? editResult;
}

/// Editor options passed to create_editor as binary payload.
class EditorOptions {
  const EditorOptions({
    this.touchSlop = 10,
    this.doubleTapTimeout = 300,
    this.longPressMs = 500,
    this.flingFriction = 3.5,
    this.flingMinVelocity = 50,
    this.flingMaxVelocity = 8000,
    this.maxUndoStackSize = 512,
  });

  final double touchSlop;
  final int doubleTapTimeout;
  final int longPressMs;
  final double flingFriction;
  final double flingMinVelocity;
  final double flingMaxVelocity;
  final int maxUndoStackSize;

  /// Serialize to LE binary payload matching C API EditorOptions layout.
  Uint8List toBytes() {
    final data = ByteData(4 + 8 + 8 + 4 + 4 + 4 + 8);
    var offset = 0;
    data.setFloat32(offset, touchSlop, Endian.little);
    offset += 4;
    data.setInt64(offset, doubleTapTimeout, Endian.little);
    offset += 8;
    data.setInt64(offset, longPressMs, Endian.little);
    offset += 8;
    data.setFloat32(offset, flingFriction, Endian.little);
    offset += 4;
    data.setFloat32(offset, flingMinVelocity, Endian.little);
    offset += 4;
    data.setFloat32(offset, flingMaxVelocity, Endian.little);
    offset += 4;
    data.setUint64(offset, maxUndoStackSize, Endian.little);
    return data.buffer.asUint8List();
  }
}

/// Handle hit-test configuration.
class HandleConfig {
  const HandleConfig({
    this.startLeft = -32.1,
    this.startTop = -8.0,
    this.startRight = 8.0,
    this.startBottom = 32.1,
    this.endLeft = -8.0,
    this.endTop = -8.0,
    this.endRight = 32.1,
    this.endBottom = 32.1,
  });

  final double startLeft;
  final double startTop;
  final double startRight;
  final double startBottom;
  final double endLeft;
  final double endTop;
  final double endRight;
  final double endBottom;
}

/// Scrollbar configuration.
class ScrollbarConfig {
  const ScrollbarConfig({
    this.thickness = 10.0,
    this.minThumb = 24.0,
    this.thumbHitPadding = 0.0,
    this.mode = ScrollbarMode.always,
    this.thumbDraggable = true,
    this.trackTapMode = ScrollbarTrackTapMode.jump,
    this.fadeDelayMs = 1500,
    this.fadeDurationMs = 300,
  });

  final double thickness;
  final double minThumb;
  final double thumbHitPadding;
  final ScrollbarMode mode;
  final bool thumbDraggable;
  final ScrollbarTrackTapMode trackTapMode;
  final int fadeDelayMs;
  final int fadeDurationMs;
}

/// Gesture event input.
class GestureEvent {
  const GestureEvent({
    required this.type,
    required this.points,
    this.modifiers = Modifier.none,
    this.wheelDeltaX = 0,
    this.wheelDeltaY = 0,
    this.directScale = 1,
  });

  final int type;
  final List<PointF> points;
  final int modifiers;
  final double wheelDeltaX;
  final double wheelDeltaY;
  final double directScale;
}

// ---------------------------------------------------------------------------
// EditorCore — high-level wrapper for the native editor engine
// ---------------------------------------------------------------------------

class EditorCore {
  /// Create an EditorCore with the given text measurer callbacks and options.
  EditorCore({
    required bindings.text_measurer_t measurer,
    EditorOptions options = const EditorOptions(),
  }) : _handle = _createEditor(measurer, options) {
    if (_handle == 0) {
      throw SweetEditorException('Failed to create EditorCore');
    }
  }

  static int _createEditor(
    bindings.text_measurer_t measurer,
    EditorOptions options,
  ) {
    return using((arena) {
      final bytes = options.toBytes();
      final optionsPtr = arena.allocate<ffi.Uint8>(bytes.length);
      optionsPtr.asTypedList(bytes.length).setAll(0, bytes);
      return bindings.create_editor(measurer, optionsPtr, bytes.length);
    });
  }

  final int _handle;
  bool _closed = false;

  int get handle => _handle;

  // ---------------------------------------------------------------------------
  // Document & Viewport
  // ---------------------------------------------------------------------------

  void setDocument(Document document) {
    _ensureOpen();
    document._ensureOpen();
    bindings.set_editor_document(_handle, document._handle);
  }

  void setViewport(int width, int height) {
    _ensureOpen();
    bindings.set_editor_viewport(_handle, width, height);
  }

  void onFontMetricsChanged() {
    _ensureOpen();
    bindings.editor_on_font_metrics_changed(_handle);
  }

  // ---------------------------------------------------------------------------
  // Configuration
  // ---------------------------------------------------------------------------

  void setFoldArrowMode(FoldArrowMode mode) {
    _ensureOpen();
    bindings.editor_set_fold_arrow_mode(_handle, mode.value);
  }

  void setWrapMode(WrapMode mode) {
    _ensureOpen();
    bindings.editor_set_wrap_mode(_handle, mode.value);
  }

  void setTabSize(int tabSize) {
    _ensureOpen();
    bindings.editor_set_tab_size(_handle, tabSize);
  }

  void setScale(double scale) {
    _ensureOpen();
    bindings.editor_set_scale(_handle, scale);
  }

  void setLineSpacing({double add = 0, double mult = 1.0}) {
    _ensureOpen();
    bindings.editor_set_line_spacing(_handle, add, mult);
  }

  void setContentStartPadding(double padding) {
    _ensureOpen();
    bindings.editor_set_content_start_padding(_handle, padding);
  }

  void setShowSplitLine(bool show) {
    _ensureOpen();
    bindings.editor_set_show_split_line(_handle, show ? 1 : 0);
  }

  void setCurrentLineRenderMode(CurrentLineRenderMode mode) {
    _ensureOpen();
    bindings.editor_set_current_line_render_mode(_handle, mode.value);
  }

  void setGutterSticky(bool sticky) {
    _ensureOpen();
    bindings.editor_set_gutter_sticky(_handle, sticky ? 1 : 0);
  }

  void setGutterVisible(bool visible) {
    _ensureOpen();
    bindings.editor_set_gutter_visible(_handle, visible ? 1 : 0);
  }

  void setReadOnly(bool readOnly) {
    _ensureOpen();
    bindings.editor_set_read_only(_handle, readOnly ? 1 : 0);
  }

  bool get isReadOnly {
    _ensureOpen();
    return bindings.editor_is_read_only(_handle) != 0;
  }

  void setAutoIndentMode(AutoIndentMode mode) {
    _ensureOpen();
    bindings.editor_set_auto_indent_mode(_handle, mode.value);
  }

  AutoIndentMode get autoIndentMode {
    _ensureOpen();
    return AutoIndentMode.values.firstWhere(
      (m) => m.value == bindings.editor_get_auto_indent_mode(_handle),
      orElse: () => AutoIndentMode.none,
    );
  }

  void setBackspaceUnindent(bool enabled) {
    _ensureOpen();
    bindings.editor_set_backspace_unindent(_handle, enabled ? 1 : 0);
  }

  void setHandleConfig(HandleConfig config) {
    _ensureOpen();
    bindings.editor_set_handle_config(
      _handle,
      config.startLeft,
      config.startTop,
      config.startRight,
      config.startBottom,
      config.endLeft,
      config.endTop,
      config.endRight,
      config.endBottom,
    );
  }

  void setScrollbarConfig(ScrollbarConfig config) {
    _ensureOpen();
    bindings.editor_set_scrollbar_config(
      _handle,
      config.thickness,
      config.minThumb,
      config.thumbHitPadding,
      config.mode.value,
      config.thumbDraggable ? 1 : 0,
      config.trackTapMode.value,
      config.fadeDelayMs,
      config.fadeDurationMs,
    );
  }

  // ---------------------------------------------------------------------------
  // Rendering
  // ---------------------------------------------------------------------------

  /// Build render model. Returns parsed [EditorRenderModel].
  EditorRenderModel buildRenderModel() {
    _ensureOpen();
    return _callAndParse(
      EditorRenderModel.empty,
      (outSize) => bindings.build_editor_render_model(_handle, outSize),
      ProtocolDecoder.decodeRenderModel,
    );
  }

  /// Build render model and return raw bytes (for custom parsing).
  Uint8List? buildRenderModelRaw() {
    _ensureOpen();
    return using((arena) {
      final outSize = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final ptr = bindings.build_editor_render_model(_handle, outSize);
      if (ptr == ffi.nullptr) return null;
      final size = outSize.value;
      final bytes = Uint8List.fromList(ptr.asTypedList(size));
      bindings.free_binary_data(ptr.address);
      return bytes;
    });
  }

  // ---------------------------------------------------------------------------
  // Gesture Events
  // ---------------------------------------------------------------------------

  GestureResult handleGestureEvent(GestureEvent event) {
    _ensureOpen();
    return using((arena) {
      final flatPoints = <double>[];
      for (final p in event.points) {
        flatPoints.add(p.x);
        flatPoints.add(p.y);
      }
      final pointsPtr = arena.allocate<ffi.Float>(
        flatPoints.length * ffi.sizeOf<ffi.Float>(),
      );
      for (var i = 0; i < flatPoints.length; i++) {
        (pointsPtr + i).value = flatPoints[i];
      }
      final outSize = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final ptr = bindings.handle_editor_gesture_event_ex(
        _handle,
        event.type,
        event.points.length,
        pointsPtr,
        event.modifiers,
        event.wheelDeltaX,
        event.wheelDeltaY,
        event.directScale,
        outSize,
      );
      if (ptr == ffi.nullptr) return GestureResult.empty;
      final size = outSize.value;
      try {
        return ProtocolDecoder.decodeGestureResult(ptr, size);
      } finally {
        bindings.free_binary_data(ptr.address);
      }
    });
  }

  GestureResult tickEdgeScroll() {
    _ensureOpen();
    return _callAndParse(
      GestureResult.empty,
      (outSize) => bindings.editor_tick_edge_scroll(_handle, outSize),
      ProtocolDecoder.decodeGestureResult,
    );
  }

  GestureResult tickFling() {
    _ensureOpen();
    return _callAndParse(
      GestureResult.empty,
      (outSize) => bindings.editor_tick_fling(_handle, outSize),
      ProtocolDecoder.decodeGestureResult,
    );
  }

  GestureResult tickAnimations() {
    _ensureOpen();
    return _callAndParse(
      GestureResult.empty,
      (outSize) => bindings.editor_tick_animations(_handle, outSize),
      ProtocolDecoder.decodeGestureResult,
    );
  }

  // ---------------------------------------------------------------------------
  // Key Events
  // ---------------------------------------------------------------------------

  KeyEventResult handleKeyEvent(
    KeyCode keyCode, {
    String? text,
    int modifiers = 0,
  }) {
    _ensureOpen();
    return using((arena) {
      final textPtr = text != null
          ? _toNativeUtf8(text, arena)
          : ffi.nullptr.cast<ffi.Char>();
      final outSize = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final ptr = bindings.handle_editor_key_event(
        _handle,
        keyCode.value,
        textPtr,
        modifiers,
        outSize,
      );
      if (ptr == ffi.nullptr) return KeyEventResult.empty;
      final size = outSize.value;
      try {
        return ProtocolDecoder.decodeKeyEventResult(ptr, size);
      } finally {
        bindings.free_binary_data(ptr.address);
      }
    });
  }

  // ---------------------------------------------------------------------------
  // Editing
  // ---------------------------------------------------------------------------

  TextEditResult insertText(String text) {
    _ensureOpen();
    return using((arena) {
      final textPtr = _toNativeUtf8(text, arena);
      return _callAndParse(
        TextEditResult.empty,
        (outSize) => bindings.editor_insert_text(_handle, textPtr, outSize),
        ProtocolDecoder.decodeTextEditResult,
      );
    });
  }

  TextEditResult replaceText(
    int startLine,
    int startColumn,
    int endLine,
    int endColumn,
    String text,
  ) {
    _ensureOpen();
    return using((arena) {
      final textPtr = _toNativeUtf8(text, arena);
      return _callAndParse(
        TextEditResult.empty,
        (outSize) => bindings.editor_replace_text(
          _handle,
          startLine,
          startColumn,
          endLine,
          endColumn,
          textPtr,
          outSize,
        ),
        ProtocolDecoder.decodeTextEditResult,
      );
    });
  }

  TextEditResult deleteText(
    int startLine,
    int startColumn,
    int endLine,
    int endColumn,
  ) {
    _ensureOpen();
    return _callAndParse(
      TextEditResult.empty,
      (outSize) => bindings.editor_delete_text(
        _handle,
        startLine,
        startColumn,
        endLine,
        endColumn,
        outSize,
      ),
      ProtocolDecoder.decodeTextEditResult,
    );
  }

  TextEditResult backspace() =>
      _simpleEdit((s) => bindings.editor_backspace(_handle, s));
  TextEditResult deleteForward() =>
      _simpleEdit((s) => bindings.editor_delete_forward(_handle, s));
  TextEditResult moveLineUp() =>
      _simpleEdit((s) => bindings.editor_move_line_up(_handle, s));
  TextEditResult moveLineDown() =>
      _simpleEdit((s) => bindings.editor_move_line_down(_handle, s));
  TextEditResult copyLineUp() =>
      _simpleEdit((s) => bindings.editor_copy_line_up(_handle, s));
  TextEditResult copyLineDown() =>
      _simpleEdit((s) => bindings.editor_copy_line_down(_handle, s));
  TextEditResult deleteLine() =>
      _simpleEdit((s) => bindings.editor_delete_line(_handle, s));
  TextEditResult insertLineAbove() =>
      _simpleEdit((s) => bindings.editor_insert_line_above(_handle, s));
  TextEditResult insertLineBelow() =>
      _simpleEdit((s) => bindings.editor_insert_line_below(_handle, s));
  TextEditResult undo() => _simpleEdit((s) => bindings.editor_undo(_handle, s));
  TextEditResult redo() => _simpleEdit((s) => bindings.editor_redo(_handle, s));

  TextEditResult _simpleEdit(
    ffi.Pointer<ffi.Uint8> Function(ffi.Pointer<ffi.Size>) fn,
  ) {
    _ensureOpen();
    return _callAndParse(
      TextEditResult.empty,
      fn,
      ProtocolDecoder.decodeTextEditResult,
    );
  }

  bool get canUndo {
    _ensureOpen();
    return bindings.editor_can_undo(_handle) != 0;
  }

  bool get canRedo {
    _ensureOpen();
    return bindings.editor_can_redo(_handle) != 0;
  }

  // ---------------------------------------------------------------------------
  // Cursor & Selection
  // ---------------------------------------------------------------------------

  void setCursorPosition(int line, int column) {
    _ensureOpen();
    bindings.editor_set_cursor_position(_handle, line, column);
  }

  TextPosition getCursorPosition() {
    _ensureOpen();
    return using((arena) {
      final outLine = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final outColumn = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      bindings.editor_get_cursor_position(_handle, outLine, outColumn);
      return TextPosition(outLine.value, outColumn.value);
    });
  }

  void selectAll() {
    _ensureOpen();
    bindings.editor_select_all(_handle);
  }

  void setSelection(
    int startLine,
    int startColumn,
    int endLine,
    int endColumn,
  ) {
    _ensureOpen();
    bindings.editor_set_selection(
      _handle,
      startLine,
      startColumn,
      endLine,
      endColumn,
    );
  }

  TextRange? getSelection() {
    _ensureOpen();
    return using((arena) {
      final sl = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final sc = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final el = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final ec = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      if (bindings.editor_get_selection(_handle, sl, sc, el, ec) == 0) {
        return null;
      }
      return TextRange(
        TextPosition(sl.value, sc.value),
        TextPosition(el.value, ec.value),
      );
    });
  }

  String getSelectedText() {
    _ensureOpen();
    return _readNativeUtf8(bindings.editor_get_selected_text(_handle));
  }

  TextRange getWordRangeAtCursor() {
    _ensureOpen();
    return using((arena) {
      final sl = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final sc = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final el = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      final ec = arena.allocate<ffi.Size>(ffi.sizeOf<ffi.Size>());
      bindings.editor_get_word_range_at_cursor(_handle, sl, sc, el, ec);
      return TextRange(
        TextPosition(sl.value, sc.value),
        TextPosition(el.value, ec.value),
      );
    });
  }

  String getWordAtCursor() {
    _ensureOpen();
    return _readNativeUtf8(bindings.editor_get_word_at_cursor(_handle));
  }

  void moveCursorLeft({bool extendSelection = false}) {
    _ensureOpen();
    bindings.editor_move_cursor_left(_handle, extendSelection ? 1 : 0);
  }

  void moveCursorRight({bool extendSelection = false}) {
    _ensureOpen();
    bindings.editor_move_cursor_right(_handle, extendSelection ? 1 : 0);
  }

  void moveCursorUp({bool extendSelection = false}) {
    _ensureOpen();
    bindings.editor_move_cursor_up(_handle, extendSelection ? 1 : 0);
  }

  void moveCursorDown({bool extendSelection = false}) {
    _ensureOpen();
    bindings.editor_move_cursor_down(_handle, extendSelection ? 1 : 0);
  }

  void moveCursorToLineStart({bool extendSelection = false}) {
    _ensureOpen();
    bindings.editor_move_cursor_to_line_start(_handle, extendSelection ? 1 : 0);
  }

  void moveCursorToLineEnd({bool extendSelection = false}) {
    _ensureOpen();
    bindings.editor_move_cursor_to_line_end(_handle, extendSelection ? 1 : 0);
  }

  // ---------------------------------------------------------------------------
  // IME Composition
  // ---------------------------------------------------------------------------

  void compositionStart() {
    _ensureOpen();
    bindings.editor_composition_start(_handle);
  }

  void compositionUpdate(String text) {
    _ensureOpen();
    using((arena) {
      bindings.editor_composition_update(_handle, _toNativeUtf8(text, arena));
    });
  }

  TextEditResult compositionEnd({String? committedText}) {
    _ensureOpen();
    return using((arena) {
      final textPtr = committedText != null
          ? _toNativeUtf8(committedText, arena)
          : ffi.nullptr.cast<ffi.Char>();
      return _callAndParse(
        TextEditResult.empty,
        (outSize) => bindings.editor_composition_end(_handle, textPtr, outSize),
        ProtocolDecoder.decodeTextEditResult,
      );
    });
  }

  void compositionCancel() {
    _ensureOpen();
    bindings.editor_composition_cancel(_handle);
  }

  bool get isComposing {
    _ensureOpen();
    return bindings.editor_is_composing(_handle) != 0;
  }

  void setCompositionEnabled(bool enabled) {
    _ensureOpen();
    bindings.editor_set_composition_enabled(_handle, enabled ? 1 : 0);
  }

  bool get isCompositionEnabled {
    _ensureOpen();
    return bindings.editor_is_composition_enabled(_handle) != 0;
  }

  // ---------------------------------------------------------------------------
  // Navigation
  // ---------------------------------------------------------------------------

  void scrollToLine(
    int line, {
    ScrollBehavior behavior = ScrollBehavior.gotoCenter,
  }) {
    _ensureOpen();
    bindings.editor_scroll_to_line(_handle, line, behavior.value);
  }

  void gotoPosition(int line, int column) {
    _ensureOpen();
    bindings.editor_goto_position(_handle, line, column);
  }

  void ensureCursorVisible() {
    _ensureOpen();
    bindings.editor_ensure_cursor_visible(_handle);
  }

  void setScroll(double scrollX, double scrollY) {
    _ensureOpen();
    bindings.editor_set_scroll(_handle, scrollX, scrollY);
  }

  ScrollMetrics getScrollMetrics() {
    _ensureOpen();
    return _callAndParse(
      ScrollMetrics.empty,
      (outSize) => bindings.editor_get_scroll_metrics(_handle, outSize),
      ProtocolDecoder.decodeScrollMetrics,
    );
  }

  // ---------------------------------------------------------------------------
  // Styles & Decorations
  // ---------------------------------------------------------------------------

  void registerTextStyle(
    int styleId,
    int color, {
    int backgroundColor = 0,
    int fontStyle = 0,
  }) {
    _ensureOpen();
    bindings.editor_register_text_style(
      _handle,
      styleId,
      color,
      backgroundColor,
      fontStyle,
    );
  }

  void setLineSpans(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) => bindings.editor_set_line_spans(_handle, ptr, len),
    );
  }

  void setBatchLineSpans(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) => bindings.editor_set_batch_line_spans(_handle, ptr, len),
    );
  }

  void setBatchLineInlayHints(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) =>
          bindings.editor_set_batch_line_inlay_hints(_handle, ptr, len),
    );
  }

  void setBatchLinePhantomTexts(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) =>
          bindings.editor_set_batch_line_phantom_texts(_handle, ptr, len),
    );
  }

  void setBatchLineGutterIcons(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) =>
          bindings.editor_set_batch_line_gutter_icons(_handle, ptr, len),
    );
  }

  void setBatchLineDiagnostics(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) =>
          bindings.editor_set_batch_line_diagnostics(_handle, ptr, len),
    );
  }

  void setIndentGuides(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) => bindings.editor_set_indent_guides(_handle, ptr, len),
    );
  }

  void setBracketGuides(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) => bindings.editor_set_bracket_guides(_handle, ptr, len),
    );
  }

  void setFlowGuides(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) => bindings.editor_set_flow_guides(_handle, ptr, len),
    );
  }

  void setSeparatorGuides(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) => bindings.editor_set_separator_guides(_handle, ptr, len),
    );
  }

  void setMaxGutterIcons(int count) {
    _ensureOpen();
    bindings.editor_set_max_gutter_icons(_handle, count);
  }

  void registerBatchTextStyles(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) =>
          bindings.editor_register_batch_text_styles(_handle, ptr, len),
    );
  }

  void clearLineSpans(int line, SpanLayer layer) {
    _ensureOpen();
    bindings.editor_clear_line_spans(_handle, line, layer.value);
  }

  void clearHighlightsLayer(SpanLayer layer) {
    _ensureOpen();
    bindings.editor_clear_highlights_layer(_handle, layer.value);
  }

  void clearHighlights() {
    _ensureOpen();
    bindings.editor_clear_highlights(_handle);
  }

  void clearInlayHints() {
    _ensureOpen();
    bindings.editor_clear_inlay_hints(_handle);
  }

  void clearPhantomTexts() {
    _ensureOpen();
    bindings.editor_clear_phantom_texts(_handle);
  }

  void clearAllDecorations() {
    _ensureOpen();
    bindings.editor_clear_all_decorations(_handle);
  }

  void clearDiagnostics() {
    _ensureOpen();
    bindings.editor_clear_diagnostics(_handle);
  }

  void clearGutterIcons() {
    _ensureOpen();
    bindings.editor_clear_gutter_icons(_handle);
  }

  void clearGuides() {
    _ensureOpen();
    bindings.editor_clear_guides(_handle);
  }

  // ---------------------------------------------------------------------------
  // Folding
  // ---------------------------------------------------------------------------

  void setFoldRegions(Uint8List data) {
    _ensureOpen();
    _callWithBinaryData(
      data,
      (ptr, len) => bindings.editor_set_fold_regions(_handle, ptr, len),
    );
  }

  bool toggleFold(int line) {
    _ensureOpen();
    return bindings.editor_toggle_fold(_handle, line) != 0;
  }

  bool foldAt(int line) {
    _ensureOpen();
    return bindings.editor_fold_at(_handle, line) != 0;
  }

  bool unfoldAt(int line) {
    _ensureOpen();
    return bindings.editor_unfold_at(_handle, line) != 0;
  }

  void foldAll() {
    _ensureOpen();
    bindings.editor_fold_all(_handle);
  }

  void unfoldAll() {
    _ensureOpen();
    bindings.editor_unfold_all(_handle);
  }

  bool isLineVisible(int line) {
    _ensureOpen();
    return bindings.editor_is_line_visible(_handle, line) != 0;
  }

  // ---------------------------------------------------------------------------
  // Linked Editing
  // ---------------------------------------------------------------------------

  TextEditResult insertSnippet(String snippetTemplate) {
    _ensureOpen();
    return using((arena) {
      final templatePtr = _toNativeUtf8(snippetTemplate, arena);
      return _callAndParse(
        TextEditResult.empty,
        (outSize) =>
            bindings.editor_insert_snippet(_handle, templatePtr, outSize),
        ProtocolDecoder.decodeTextEditResult,
      );
    });
  }

  bool get isInLinkedEditing {
    _ensureOpen();
    return bindings.editor_is_in_linked_editing(_handle) != 0;
  }

  bool linkedEditingNext() {
    _ensureOpen();
    return bindings.editor_linked_editing_next(_handle) != 0;
  }

  bool linkedEditingPrev() {
    _ensureOpen();
    return bindings.editor_linked_editing_prev(_handle) != 0;
  }

  void cancelLinkedEditing() {
    _ensureOpen();
    bindings.editor_cancel_linked_editing(_handle);
  }

  // ---------------------------------------------------------------------------
  // Lifecycle
  // ---------------------------------------------------------------------------

  void close() {
    if (_closed) return;
    _closed = true;
    bindings.free_editor(_handle);
  }

  void dispose() => close();

  void _ensureOpen() {
    if (_closed) throw StateError('EditorCore is already closed');
  }
}

// ---------------------------------------------------------------------------
// Document — wrapper for the native document handle
// ---------------------------------------------------------------------------

class Document {
  /// Create a document from a Dart string.
  Document.fromString(String text)
    : _handle = using((arena) {
        final textPtr = _toNativeUtf16(text, arena);
        return bindings.create_document_from_utf16(textPtr);
      }) {
    if (_handle == 0) {
      throw SweetEditorException('Failed to create document from string');
    }
  }

  /// Create a document from a local file path.
  Document.fromFile(String path)
    : _handle = using((arena) {
        final pathPtr = _toNativeUtf8(path, arena);
        return bindings.create_document_from_file(pathPtr);
      }) {
    if (_handle == 0) {
      throw SweetEditorException('Failed to create document from file: $path');
    }
  }

  final int _handle;
  bool _closed = false;

  int get handle => _handle;

  /// Get document text as UTF8 string.
  String get text {
    _ensureOpen();
    final ptr = bindings.get_document_text(_handle);
    return _readNativeUtf8(ptr);
  }

  /// Get a single line's text (0-indexed).
  String getLineText(int line) {
    _ensureOpen();
    final ptr = bindings.get_document_line_text(_handle, line);
    return _readNativeUtf16(ptr);
  }

  /// Get total line count.
  int get lineCount {
    _ensureOpen();
    return bindings.get_document_line_count(_handle);
  }

  void close() {
    if (_closed) return;
    _closed = true;
    bindings.free_document(_handle);
  }

  void dispose() => close();

  void _ensureOpen() {
    if (_closed) throw StateError('Document is already closed');
  }
}
