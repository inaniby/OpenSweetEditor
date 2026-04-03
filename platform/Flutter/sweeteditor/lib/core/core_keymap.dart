part of '../editor_core.dart';

class KeyModifier {
  KeyModifier._();

  static const int none = 0;
  static const int shift = 1;
  static const int ctrl = 2;
  static const int alt = 4;
  static const int meta = 8;
}

class EditorCommand {
  EditorCommand._();

  static const int none = 0;
  static const int cursorLeft = 1;
  static const int cursorRight = 2;
  static const int cursorUp = 3;
  static const int cursorDown = 4;
  static const int cursorLineStart = 5;
  static const int cursorLineEnd = 6;
  static const int cursorPageUp = 7;
  static const int cursorPageDown = 8;
  static const int selectLeft = 9;
  static const int selectRight = 10;
  static const int selectUp = 11;
  static const int selectDown = 12;
  static const int selectLineStart = 13;
  static const int selectLineEnd = 14;
  static const int selectPageUp = 15;
  static const int selectPageDown = 16;
  static const int selectAll = 17;
  static const int backspace = 18;
  static const int deleteForward = 19;
  static const int insertTab = 20;
  static const int insertNewline = 21;
  static const int insertLineAbove = 22;
  static const int insertLineBelow = 23;
  static const int undo = 24;
  static const int redo = 25;
  static const int moveLineUp = 26;
  static const int moveLineDown = 27;
  static const int copyLineUp = 28;
  static const int copyLineDown = 29;
  static const int deleteLine = 30;
  static const int copy = 31;
  static const int paste = 32;
  static const int cut = 33;
  static const int triggerCompletion = 34;

  static const int builtInMax = triggerCompletion;

  static bool isBuiltIn(int command) => command > none && command <= builtInMax;

  static bool isPlatformHandled(int command) =>
      command == copy ||
      command == paste ||
      command == cut ||
      command == triggerCompletion;
}

class KeyChord {
  const KeyChord({
    this.modifiers = KeyModifier.none,
    this.keyCode = KeyCode.none,
  });

  final int modifiers;
  final KeyCode keyCode;

  bool get isEmpty => keyCode == KeyCode.none;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is KeyChord &&
          modifiers == other.modifiers &&
          keyCode == other.keyCode;

  @override
  int get hashCode => Object.hash(modifiers, keyCode);
}

class KeyBinding {
  const KeyBinding({
    required this.first,
    this.second = const KeyChord(),
    this.command = EditorCommand.none,
  });

  final KeyChord first;
  final KeyChord second;
  final int command;

  KeyBinding copyWith({KeyChord? first, KeyChord? second, int? command}) {
    return KeyBinding(
      first: first ?? this.first,
      second: second ?? this.second,
      command: command ?? this.command,
    );
  }
}

class KeyMap {
  KeyMap([Iterable<KeyBinding>? bindings]) {
    if (bindings != null) {
      for (final binding in bindings) {
        addBinding(binding);
      }
    }
  }

  final List<KeyBinding> _bindings = <KeyBinding>[];

  List<KeyBinding> get bindings => List<KeyBinding>.unmodifiable(_bindings);

  void addBinding(KeyBinding binding) {
    if (binding.first.isEmpty) return;
    _bindings.removeWhere(
      (existing) =>
          existing.first == binding.first && existing.second == binding.second,
    );
    _bindings.add(binding);
  }

  Uint8List toBytes() {
    final data = ByteData(4 + _bindings.length * 10);
    var offset = 0;
    data.setUint32(offset, _bindings.length, Endian.little);
    offset += 4;
    for (final binding in _bindings) {
      data.setUint8(offset, binding.first.modifiers);
      offset += 1;
      data.setUint16(offset, binding.first.keyCode.value, Endian.little);
      offset += 2;
      data.setUint8(offset, binding.second.modifiers);
      offset += 1;
      data.setUint16(offset, binding.second.keyCode.value, Endian.little);
      offset += 2;
      data.setUint32(offset, binding.command, Endian.little);
      offset += 4;
    }
    return data.buffer.asUint8List();
  }
}
