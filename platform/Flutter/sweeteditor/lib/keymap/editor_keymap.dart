import '../editor_core.dart' as core;

export '../editor_core.dart'
    show EditorCommand, KeyBinding, KeyChord, KeyMap, KeyModifier;

typedef EditorCommandHandler = bool Function();

enum KeyResolveStatus { matched, pending, noMatch }

class KeyResolveResult {
  const KeyResolveResult({required this.status, required this.command});

  final KeyResolveStatus status;
  final int command;
}

class EditorKeyMap extends core.KeyMap {
  EditorKeyMap({Iterable<core.KeyBinding>? bindings}) : super(bindings);

  final Map<int, EditorCommandHandler> _handlers =
      <int, EditorCommandHandler>{};
  final Map<core.KeyChord, Object> _entries = <core.KeyChord, Object>{};
  int _nextCustomCommandId = core.EditorCommand.builtInMax + 1;

  bool _pending = false;
  int _pendingTimeMs = 0;
  Map<core.KeyChord, int>? _pendingSubMap;

  static const int _pendingTimeoutMs = 2000;

  factory EditorKeyMap.defaultKeyMap() => EditorKeyMap.vscode();

  factory EditorKeyMap.vscode() {
    final keyMap = EditorKeyMap();

    void addBuiltIn(int modifiers, core.KeyCode keyCode, int command) {
      keyMap.addBinding(
        core.KeyBinding(
          first: core.KeyChord(modifiers: modifiers, keyCode: keyCode),
          command: command,
        ),
      );
    }

    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.left,
      core.EditorCommand.cursorLeft,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.right,
      core.EditorCommand.cursorRight,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.up,
      core.EditorCommand.cursorUp,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.down,
      core.EditorCommand.cursorDown,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.home,
      core.EditorCommand.cursorLineStart,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.end,
      core.EditorCommand.cursorLineEnd,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.pageUp,
      core.EditorCommand.cursorPageUp,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.pageDown,
      core.EditorCommand.cursorPageDown,
    );

    addBuiltIn(
      core.KeyModifier.shift,
      core.KeyCode.left,
      core.EditorCommand.selectLeft,
    );
    addBuiltIn(
      core.KeyModifier.shift,
      core.KeyCode.right,
      core.EditorCommand.selectRight,
    );
    addBuiltIn(
      core.KeyModifier.shift,
      core.KeyCode.up,
      core.EditorCommand.selectUp,
    );
    addBuiltIn(
      core.KeyModifier.shift,
      core.KeyCode.down,
      core.EditorCommand.selectDown,
    );
    addBuiltIn(
      core.KeyModifier.shift,
      core.KeyCode.home,
      core.EditorCommand.selectLineStart,
    );
    addBuiltIn(
      core.KeyModifier.shift,
      core.KeyCode.end,
      core.EditorCommand.selectLineEnd,
    );
    addBuiltIn(
      core.KeyModifier.shift,
      core.KeyCode.pageUp,
      core.EditorCommand.selectPageUp,
    );
    addBuiltIn(
      core.KeyModifier.shift,
      core.KeyCode.pageDown,
      core.EditorCommand.selectPageDown,
    );

    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.backspace,
      core.EditorCommand.backspace,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.deleteKey,
      core.EditorCommand.deleteForward,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.tab,
      core.EditorCommand.insertTab,
    );
    addBuiltIn(
      core.KeyModifier.none,
      core.KeyCode.enter,
      core.EditorCommand.insertNewline,
    );

    addBuiltIn(
      core.KeyModifier.ctrl,
      core.KeyCode.a,
      core.EditorCommand.selectAll,
    );
    addBuiltIn(
      core.KeyModifier.meta,
      core.KeyCode.a,
      core.EditorCommand.selectAll,
    );
    addBuiltIn(core.KeyModifier.ctrl, core.KeyCode.z, core.EditorCommand.undo);
    addBuiltIn(core.KeyModifier.meta, core.KeyCode.z, core.EditorCommand.undo);
    addBuiltIn(
      core.KeyModifier.ctrl | core.KeyModifier.shift,
      core.KeyCode.z,
      core.EditorCommand.redo,
    );
    addBuiltIn(
      core.KeyModifier.meta | core.KeyModifier.shift,
      core.KeyCode.z,
      core.EditorCommand.redo,
    );
    addBuiltIn(core.KeyModifier.ctrl, core.KeyCode.y, core.EditorCommand.redo);
    addBuiltIn(core.KeyModifier.meta, core.KeyCode.y, core.EditorCommand.redo);

    addBuiltIn(core.KeyModifier.ctrl, core.KeyCode.c, core.EditorCommand.copy);
    addBuiltIn(core.KeyModifier.meta, core.KeyCode.c, core.EditorCommand.copy);
    addBuiltIn(core.KeyModifier.ctrl, core.KeyCode.v, core.EditorCommand.paste);
    addBuiltIn(core.KeyModifier.meta, core.KeyCode.v, core.EditorCommand.paste);
    addBuiltIn(core.KeyModifier.ctrl, core.KeyCode.x, core.EditorCommand.cut);
    addBuiltIn(core.KeyModifier.meta, core.KeyCode.x, core.EditorCommand.cut);
    addBuiltIn(
      core.KeyModifier.ctrl,
      core.KeyCode.space,
      core.EditorCommand.triggerCompletion,
    );
    addBuiltIn(
      core.KeyModifier.meta,
      core.KeyCode.space,
      core.EditorCommand.triggerCompletion,
    );

    addBuiltIn(
      core.KeyModifier.ctrl,
      core.KeyCode.enter,
      core.EditorCommand.insertLineBelow,
    );
    addBuiltIn(
      core.KeyModifier.meta,
      core.KeyCode.enter,
      core.EditorCommand.insertLineBelow,
    );
    addBuiltIn(
      core.KeyModifier.ctrl | core.KeyModifier.shift,
      core.KeyCode.enter,
      core.EditorCommand.insertLineAbove,
    );
    addBuiltIn(
      core.KeyModifier.meta | core.KeyModifier.shift,
      core.KeyCode.enter,
      core.EditorCommand.insertLineAbove,
    );

    addBuiltIn(
      core.KeyModifier.alt,
      core.KeyCode.up,
      core.EditorCommand.moveLineUp,
    );
    addBuiltIn(
      core.KeyModifier.alt,
      core.KeyCode.down,
      core.EditorCommand.moveLineDown,
    );
    addBuiltIn(
      core.KeyModifier.alt | core.KeyModifier.shift,
      core.KeyCode.up,
      core.EditorCommand.copyLineUp,
    );
    addBuiltIn(
      core.KeyModifier.alt | core.KeyModifier.shift,
      core.KeyCode.down,
      core.EditorCommand.copyLineDown,
    );

    addBuiltIn(
      core.KeyModifier.ctrl | core.KeyModifier.shift,
      core.KeyCode.k,
      core.EditorCommand.deleteLine,
    );
    addBuiltIn(
      core.KeyModifier.meta | core.KeyModifier.shift,
      core.KeyCode.k,
      core.EditorCommand.deleteLine,
    );

    return keyMap;
  }

  factory EditorKeyMap.jetbrains() => EditorKeyMap.vscode();

  factory EditorKeyMap.sublime() => EditorKeyMap.vscode();

  @override
  void addBinding(core.KeyBinding binding) {
    super.addBinding(binding);
    _registerBindingEntry(binding);
  }

  int registerCommand(core.KeyBinding binding, EditorCommandHandler handler) {
    var resolvedBinding = binding;
    var command = binding.command;
    if (command == core.EditorCommand.none) {
      command = _nextCustomCommandId++;
      resolvedBinding = binding.copyWith(command: command);
    } else if (command >= _nextCustomCommandId) {
      _nextCustomCommandId = command + 1;
    }

    addBinding(resolvedBinding);
    _handlers[command] = handler;
    return command;
  }

  KeyResolveResult resolve(core.KeyChord chord, {int? timeMs}) {
    final nowMs = timeMs ?? DateTime.now().millisecondsSinceEpoch;
    if (_pending) {
      final subMap = _pendingSubMap;
      final expired =
          subMap == null || (nowMs - _pendingTimeMs > _pendingTimeoutMs);
      if (expired) {
        _cancelPending();
      } else {
        final command = subMap[chord];
        _cancelPending();
        if (command != null) {
          return KeyResolveResult(
            status: KeyResolveStatus.matched,
            command: command,
          );
        }
        return const KeyResolveResult(
          status: KeyResolveStatus.noMatch,
          command: core.EditorCommand.none,
        );
      }
    }

    final entry = _entries[chord];
    if (entry == null) {
      return const KeyResolveResult(
        status: KeyResolveStatus.noMatch,
        command: core.EditorCommand.none,
      );
    }
    if (entry is int) {
      return KeyResolveResult(status: KeyResolveStatus.matched, command: entry);
    }
    if (entry is Map<core.KeyChord, int>) {
      _pending = true;
      _pendingTimeMs = nowMs;
      _pendingSubMap = entry;
      return const KeyResolveResult(
        status: KeyResolveStatus.pending,
        command: core.EditorCommand.none,
      );
    }
    return const KeyResolveResult(
      status: KeyResolveStatus.noMatch,
      command: core.EditorCommand.none,
    );
  }

  bool invokeHandler(int command) => _handlers[command]?.call() ?? false;

  void _registerBindingEntry(core.KeyBinding binding) {
    final first = binding.first;
    if (binding.second.isEmpty) {
      _entries[first] = binding.command;
      return;
    }

    final existing = _entries[first];
    if (existing is Map<core.KeyChord, int>) {
      existing[binding.second] = binding.command;
      return;
    }

    _entries[first] = <core.KeyChord, int>{binding.second: binding.command};
  }

  void _cancelPending() {
    _pending = false;
    _pendingTimeMs = 0;
    _pendingSubMap = null;
  }
}
