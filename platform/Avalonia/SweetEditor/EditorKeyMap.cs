using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace SweetEditor {
	/// <summary>
	/// Key code values that must stay aligned with the SweetEditor core contract.
	/// </summary>
	public enum KeyCode : ushort {
		NONE = 0,
		BACKSPACE = 8,
		TAB = 9,
		ENTER = 13,
		ESCAPE = 27,
		SPACE = 32,
		PAGE_UP = 33,
		PAGE_DOWN = 34,
		END = 35,
		HOME = 36,
		LEFT = 37,
		UP = 38,
		RIGHT = 39,
		DOWN = 40,
		DELETE_KEY = 46,
		A = 65,
		C = 67,
		D = 68,
		K = 75,
		V = 86,
		X = 88,
		Y = 89,
		Z = 90,
	}

	/// <summary>
	/// Modifier flags that must stay aligned with the SweetEditor core contract.
	/// </summary>
	[Flags]
	public enum KeyModifier : byte {
		NONE = 0,
		SHIFT = 1,
		CTRL = 2,
		ALT = 4,
		META = 8,
	}

	/// <summary>
	/// Built-in editor commands that must stay aligned with the SweetEditor core contract.
	/// </summary>
	public enum EditorCommand : int {
		NONE = 0,
		CURSOR_LEFT = 1,
		CURSOR_RIGHT = 2,
		CURSOR_UP = 3,
		CURSOR_DOWN = 4,
		CURSOR_LINE_START = 5,
		CURSOR_LINE_END = 6,
		CURSOR_PAGE_UP = 7,
		CURSOR_PAGE_DOWN = 8,
		SELECT_LEFT = 9,
		SELECT_RIGHT = 10,
		SELECT_UP = 11,
		SELECT_DOWN = 12,
		SELECT_LINE_START = 13,
		SELECT_LINE_END = 14,
		SELECT_PAGE_UP = 15,
		SELECT_PAGE_DOWN = 16,
		SELECT_ALL = 17,
		BACKSPACE = 18,
		DELETE_FORWARD = 19,
		INSERT_TAB = 20,
		INSERT_NEWLINE = 21,
		INSERT_LINE_ABOVE = 22,
		INSERT_LINE_BELOW = 23,
		UNDO = 24,
		REDO = 25,
		MOVE_LINE_UP = 26,
		MOVE_LINE_DOWN = 27,
		COPY_LINE_UP = 28,
		COPY_LINE_DOWN = 29,
		DELETE_LINE = 30,
		COPY = 31,
		PASTE = 32,
		CUT = 33,
		TRIGGER_COMPLETION = 34,
	}

	/// <summary>
	/// A single key press: modifiers + key code.
	/// </summary>
	public readonly struct KeyChord : IEquatable<KeyChord> {
		public static readonly KeyChord Empty = new(KeyModifier.NONE, KeyCode.NONE);

		public KeyChord(KeyModifier modifiers, KeyCode keyCode) {
			Modifiers = modifiers;
			KeyCode = keyCode;
		}

		public KeyModifier Modifiers { get; }

		public KeyCode KeyCode { get; }

		public bool IsEmpty => KeyCode == KeyCode.NONE;

		public bool Equals(KeyChord other) {
			return Modifiers == other.Modifiers && KeyCode == other.KeyCode;
		}

		public override bool Equals(object? obj) {
			return obj is KeyChord other && Equals(other);
		}

		public override int GetHashCode() {
			return HashCode.Combine((byte)Modifiers, (ushort)KeyCode);
		}

		public override string ToString() {
			if (IsEmpty) {
				return "<empty>";
			}
			return $"{Modifiers}+{KeyCode}";
		}

		public static bool operator ==(KeyChord left, KeyChord right) => left.Equals(right);

		public static bool operator !=(KeyChord left, KeyChord right) => !left.Equals(right);

		internal static bool TryFromAvalonia(Key key, KeyModifiers modifiers, out KeyChord chord) {
			KeyCode keyCode = ToKeyCode(key);
			if (keyCode == KeyCode.NONE) {
				chord = Empty;
				return false;
			}

			chord = new KeyChord(ToKeyModifier(modifiers), keyCode);
			return true;
		}

		internal static KeyModifier ToKeyModifier(KeyModifiers modifiers) {
			KeyModifier result = KeyModifier.NONE;
			if ((modifiers & KeyModifiers.Shift) != 0) {
				result |= KeyModifier.SHIFT;
			}
			if ((modifiers & KeyModifiers.Control) != 0) {
				result |= KeyModifier.CTRL;
			}
			if ((modifiers & KeyModifiers.Alt) != 0) {
				result |= KeyModifier.ALT;
			}
			if ((modifiers & KeyModifiers.Meta) != 0) {
				result |= KeyModifier.META;
			}
			return result;
		}

		internal static KeyCode ToKeyCode(Key key) {
			return key switch {
				Key.Back => KeyCode.BACKSPACE,
				Key.Tab => KeyCode.TAB,
				Key.Enter => KeyCode.ENTER,
				Key.Escape => KeyCode.ESCAPE,
				Key.Space => KeyCode.SPACE,
				Key.PageUp => KeyCode.PAGE_UP,
				Key.PageDown => KeyCode.PAGE_DOWN,
				Key.End => KeyCode.END,
				Key.Home => KeyCode.HOME,
				Key.Left => KeyCode.LEFT,
				Key.Up => KeyCode.UP,
				Key.Right => KeyCode.RIGHT,
				Key.Down => KeyCode.DOWN,
				Key.Delete => KeyCode.DELETE_KEY,
				Key.A => KeyCode.A,
				Key.C => KeyCode.C,
				Key.D => KeyCode.D,
				Key.K => KeyCode.K,
				Key.V => KeyCode.V,
				Key.X => KeyCode.X,
				Key.Y => KeyCode.Y,
				Key.Z => KeyCode.Z,
				_ => KeyCode.NONE,
			};
		}
	}

	/// <summary>
	/// A single-chord or double-chord binding mapped to a command id.
	/// </summary>
	public readonly struct KeyBinding : IEquatable<KeyBinding> {
		public KeyBinding(KeyChord first, EditorCommand command)
			: this(first, KeyChord.Empty, (int)command) {
		}

		public KeyBinding(KeyChord first, int commandId)
			: this(first, KeyChord.Empty, commandId) {
		}

		public KeyBinding(KeyChord first, KeyChord second, EditorCommand command)
			: this(first, second, (int)command) {
		}

		public KeyBinding(KeyChord first, KeyChord second, int commandId) {
			if (first.IsEmpty) {
				throw new ArgumentException("The first chord cannot be empty.", nameof(first));
			}
			if (commandId < 0) {
				throw new ArgumentOutOfRangeException(nameof(commandId));
			}

			First = first;
			Second = second;
			CommandId = commandId;
		}

		public KeyChord First { get; }

		public KeyChord Second { get; }

		public int Command => CommandId;

		internal int CommandId { get; }

		public bool IsChorded => !Second.IsEmpty;

		internal KeyBinding WithCommandId(int commandId) {
			return new KeyBinding(First, Second, commandId);
		}

		public bool Equals(KeyBinding other) {
			return First == other.First && Second == other.Second && CommandId == other.CommandId;
		}

		public override bool Equals(object? obj) {
			return obj is KeyBinding other && Equals(other);
		}

		public override int GetHashCode() {
			return HashCode.Combine(First, Second, CommandId);
		}

		public override string ToString() {
			return IsChorded
				? $"{First} , {Second} => {CommandId}"
				: $"{First} => {CommandId}";
		}

		public static bool operator ==(KeyBinding left, KeyBinding right) => left.Equals(right);

		public static bool operator !=(KeyBinding left, KeyBinding right) => !left.Equals(right);
	}

	/// <summary>
	/// Pure data mapping from key bindings to command ids.
	/// </summary>
	public class KeyMap {
		private readonly List<KeyBinding> bindings = new();

		public KeyMap() {
		}

		public KeyMap(IEnumerable<KeyBinding> bindings) {
			if (bindings == null) {
				return;
			}
			foreach (KeyBinding binding in bindings) {
				AddOrReplace(binding);
			}
		}

		public IReadOnlyList<KeyBinding> Bindings => bindings;

		public int Count => bindings.Count;

		public void Clear() {
			bindings.Clear();
		}

		public void Add(KeyBinding binding) {
			bindings.Add(binding);
		}

		public void AddOrReplace(KeyBinding binding) {
			for (int i = bindings.Count - 1; i >= 0; i--) {
				KeyBinding existing = bindings[i];
				if (existing.First == binding.First && existing.Second == binding.Second) {
					bindings.RemoveAt(i);
				}
			}
			bindings.Add(binding);
		}

		public bool Remove(KeyBinding binding) {
			return bindings.Remove(binding);
		}

		internal int RemoveByCommand(int commandId) {
			int removed = 0;
			for (int i = bindings.Count - 1; i >= 0; i--) {
				if (bindings[i].CommandId == commandId) {
					bindings.RemoveAt(i);
					removed++;
				}
			}
			return removed;
		}

		internal bool TryResolve(KeyChord first, out int commandId) {
			for (int i = 0; i < bindings.Count; i++) {
				KeyBinding binding = bindings[i];
				if (binding.First == first && binding.Second.IsEmpty) {
					commandId = binding.CommandId;
					return true;
				}
			}

			commandId = 0;
			return false;
		}

		internal bool TryResolve(KeyChord first, KeyChord second, out int commandId) {
			for (int i = 0; i < bindings.Count; i++) {
				KeyBinding binding = bindings[i];
				if (binding.First == first && binding.Second == second) {
					commandId = binding.CommandId;
					return true;
				}
			}

			commandId = 0;
			return false;
		}

		internal bool HasSecondChordPrefix(KeyChord first) {
			for (int i = 0; i < bindings.Count; i++) {
				KeyBinding binding = bindings[i];
				if (binding.First == first && !binding.Second.IsEmpty) {
					return true;
				}
			}
			return false;
		}

		internal KeyMap Clone() {
			return new KeyMap(bindings);
		}

		public static KeyMap DefaultKeyMap() => Vscode();


		public static KeyMap Vscode() {
			KeyMap map = new();

			static KeyChord Chord(KeyModifier modifiers, KeyCode keyCode) => new(modifiers, keyCode);

			// Navigation
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.LEFT), EditorCommand.CURSOR_LEFT));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.RIGHT), EditorCommand.CURSOR_RIGHT));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.UP), EditorCommand.CURSOR_UP));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.DOWN), EditorCommand.CURSOR_DOWN));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.HOME), EditorCommand.CURSOR_LINE_START));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.END), EditorCommand.CURSOR_LINE_END));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.PAGE_UP), EditorCommand.CURSOR_PAGE_UP));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.PAGE_DOWN), EditorCommand.CURSOR_PAGE_DOWN));

			// Selection navigation
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.SHIFT, KeyCode.LEFT), EditorCommand.SELECT_LEFT));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.SHIFT, KeyCode.RIGHT), EditorCommand.SELECT_RIGHT));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.SHIFT, KeyCode.UP), EditorCommand.SELECT_UP));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.SHIFT, KeyCode.DOWN), EditorCommand.SELECT_DOWN));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.SHIFT, KeyCode.HOME), EditorCommand.SELECT_LINE_START));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.SHIFT, KeyCode.END), EditorCommand.SELECT_LINE_END));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.SHIFT, KeyCode.PAGE_UP), EditorCommand.SELECT_PAGE_UP));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.SHIFT, KeyCode.PAGE_DOWN), EditorCommand.SELECT_PAGE_DOWN));

			// Text edit basics
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.BACKSPACE), EditorCommand.BACKSPACE));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.DELETE_KEY), EditorCommand.DELETE_FORWARD));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.TAB), EditorCommand.INSERT_TAB));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.NONE, KeyCode.ENTER), EditorCommand.INSERT_NEWLINE));

			// Common clipboard / selection / completion
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL, KeyCode.A), EditorCommand.SELECT_ALL));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL, KeyCode.C), EditorCommand.COPY));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL, KeyCode.V), EditorCommand.PASTE));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL, KeyCode.X), EditorCommand.CUT));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL, KeyCode.SPACE), EditorCommand.TRIGGER_COMPLETION));

			// macOS / Meta aliases
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META, KeyCode.A), EditorCommand.SELECT_ALL));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META, KeyCode.C), EditorCommand.COPY));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META, KeyCode.V), EditorCommand.PASTE));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META, KeyCode.X), EditorCommand.CUT));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META, KeyCode.SPACE), EditorCommand.TRIGGER_COMPLETION));

			// Undo / redo
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL, KeyCode.Z), EditorCommand.UNDO));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL, KeyCode.Y), EditorCommand.REDO));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL | KeyModifier.SHIFT, KeyCode.Z), EditorCommand.REDO));

			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META, KeyCode.Z), EditorCommand.UNDO));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META, KeyCode.Y), EditorCommand.REDO));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META | KeyModifier.SHIFT, KeyCode.Z), EditorCommand.REDO));

			// VS Code-like structural editing
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL, KeyCode.ENTER), EditorCommand.INSERT_LINE_BELOW));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL | KeyModifier.SHIFT, KeyCode.ENTER), EditorCommand.INSERT_LINE_ABOVE));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.ALT, KeyCode.UP), EditorCommand.MOVE_LINE_UP));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.ALT, KeyCode.DOWN), EditorCommand.MOVE_LINE_DOWN));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.ALT | KeyModifier.SHIFT, KeyCode.UP), EditorCommand.COPY_LINE_UP));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.ALT | KeyModifier.SHIFT, KeyCode.DOWN), EditorCommand.COPY_LINE_DOWN));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.CTRL | KeyModifier.SHIFT, KeyCode.K), EditorCommand.DELETE_LINE));

			// macOS / Meta aliases for structural editing
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META, KeyCode.ENTER), EditorCommand.INSERT_LINE_BELOW));
			map.AddOrReplace(new KeyBinding(Chord(KeyModifier.META | KeyModifier.SHIFT, KeyCode.ENTER), EditorCommand.INSERT_LINE_ABOVE));

			return map;
		}
	}

	internal enum KeyMapMatchKind {
		None = 0,
		AwaitingSecondChord = 1,
		Command = 2,
	}

	internal readonly struct KeyMapMatch {
		public static readonly KeyMapMatch None = new(KeyMapMatchKind.None, 0);

		public KeyMapMatch(KeyMapMatchKind kind, int commandId) {
			Kind = kind;
			CommandId = commandId;
		}

		public KeyMapMatchKind Kind { get; }

		public int CommandId { get; }

		public bool HasMatch => Kind != KeyMapMatchKind.None;

		public bool AwaitingSecondChord => Kind == KeyMapMatchKind.AwaitingSecondChord;

		public bool IsCommand => Kind == KeyMapMatchKind.Command;

		public static KeyMapMatch WaitSecondChord() => new(KeyMapMatchKind.AwaitingSecondChord, 0);

		public static KeyMapMatch Command(int commandId) => new(KeyMapMatchKind.Command, commandId);
	}

	internal enum EditorCommandRoute {
		Core = 0,
		Host = 1,
	}

	/// <summary>
	/// Widget-layer key map that can also register host-side command handlers.
	/// </summary>
	public sealed class EditorKeyMap : KeyMap {
		public delegate bool EditorCommandHandler(SweetEditorControl editor);

		public const int BUILT_IN_MAX = (int)EditorCommand.TRIGGER_COMPLETION;

		private readonly Dictionary<int, EditorCommandHandler> hostCommandHandlers = new();
		private int nextCustomCommandId = BUILT_IN_MAX + 1;

		public EditorKeyMap() {
		}

		public EditorKeyMap(IEnumerable<KeyBinding> bindings) : base(bindings) {
		}

		public static new EditorKeyMap DefaultKeyMap() => Vscode();

		public static new EditorKeyMap Vscode() => new(KeyMap.Vscode().Bindings);

		public int RegisterCommand(KeyBinding binding, EditorCommandHandler handler) {
			if (handler == null) {
				return (int)EditorCommand.NONE;
			}

			int commandId = binding.Command;
			if (commandId == (int)EditorCommand.NONE) {
				commandId = AllocateCustomCommandId();
				binding = binding.WithCommandId(commandId);
			}

			AddOrReplace(binding);
			hostCommandHandlers[commandId] = handler;
			return commandId;
		}

		internal new EditorKeyMap Clone() {
			EditorKeyMap clone = new(Bindings);
			foreach (KeyValuePair<int, EditorCommandHandler> kv in hostCommandHandlers) {
				clone.hostCommandHandlers[kv.Key] = kv.Value;
			}
			clone.nextCustomCommandId = nextCustomCommandId;
			return clone;
		}

		internal bool TryInvokeHostCommand(SweetEditorControl editor, int commandId) {
			if (editor == null) {
				return false;
			}
			return hostCommandHandlers.TryGetValue(commandId, out EditorCommandHandler? handler) && handler(editor);
		}

		internal static EditorCommandRoute GetCommandRoute(int commandId) {
			EditorCommand command = (EditorCommand)commandId;
			return command switch {
				EditorCommand.COPY => EditorCommandRoute.Host,
				EditorCommand.PASTE => EditorCommandRoute.Host,
				EditorCommand.CUT => EditorCommandRoute.Host,
				EditorCommand.TRIGGER_COMPLETION => EditorCommandRoute.Host,
				_ => EditorCommandRoute.Core,
			};
		}

		internal KeyMapMatch Match(KeyChord incoming, ref KeyChord pendingFirstChord) {
			if (incoming.IsEmpty) {
				return KeyMapMatch.None;
			}

			if (!pendingFirstChord.IsEmpty) {
				KeyChord first = pendingFirstChord;
				pendingFirstChord = KeyChord.Empty;

				if (TryResolve(first, incoming, out int chordedCommandId)) {
					return KeyMapMatch.Command(chordedCommandId);
				}
			}

			if (TryResolve(incoming, out int singleCommandId)) {
				return KeyMapMatch.Command(singleCommandId);
			}

			if (HasSecondChordPrefix(incoming)) {
				pendingFirstChord = incoming;
				return KeyMapMatch.WaitSecondChord();
			}

			return KeyMapMatch.None;
		}

		private int AllocateCustomCommandId() {
			while (hostCommandHandlers.ContainsKey(nextCustomCommandId)) {
				nextCustomCommandId++;
			}
			return nextCustomCommandId++;
		}
	}
}
