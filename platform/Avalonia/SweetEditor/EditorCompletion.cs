using System;
using System.Collections.Generic;

namespace SweetEditor {
	public class CompletionItem {
		public class TextEdit {
			public TextRange Range { get; }
			public string NewText { get; }
			public TextEdit(TextRange range, string newText) { Range = range; NewText = newText; }
		}

		public const int KIND_KEYWORD = 0;
		public const int KIND_FUNCTION = 1;
		public const int KIND_VARIABLE = 2;
		public const int KIND_CLASS = 3;
		public const int KIND_INTERFACE = 4;
		public const int KIND_MODULE = 5;
		public const int KIND_PROPERTY = 6;
		public const int KIND_SNIPPET = 7;
		public const int KIND_TEXT = 8;

		public const int INSERT_TEXT_FORMAT_PLAIN_TEXT = 1;
		public const int INSERT_TEXT_FORMAT_SNIPPET = 2;

		public string Label { get; set; } = string.Empty;
		public string? Detail { get; set; }
		public string? InsertText { get; set; }
		public int InsertTextFormat { get; set; } = INSERT_TEXT_FORMAT_PLAIN_TEXT;
		public TextEdit? TextEditValue { get; set; }
		public string? FilterText { get; set; }
		public string? SortKey { get; set; }
		public int Kind { get; set; }

		public string MatchText => FilterText ?? Label;

		public override string ToString() => $"CompletionItem{{label='{Label}', kind={Kind}}}";
	}

	public enum CompletionTriggerKind { Invoked, Character, Retrigger }

	public class CompletionContext {
		public CompletionTriggerKind TriggerKind { get; }
		public string? TriggerCharacter { get; }
		public TextPosition CursorPosition { get; }
		public string LineText { get; }
		public TextRange? WordRange { get; }
		public LanguageConfiguration? LanguageConfiguration { get; }
		public IEditorMetadata? EditorMetadata { get; }

		public CompletionContext(CompletionTriggerKind triggerKind, string? triggerCharacter,
								 TextPosition cursorPosition, string lineText, TextRange? wordRange,
								 LanguageConfiguration? languageConfiguration = null,
								 IEditorMetadata? editorMetadata = null) {
			TriggerKind = triggerKind;
			TriggerCharacter = triggerCharacter;
			CursorPosition = cursorPosition;
			LineText = lineText;
			WordRange = wordRange;
			LanguageConfiguration = languageConfiguration;
			EditorMetadata = editorMetadata;
		}
	}

	public class CompletionResult {
		public List<CompletionItem> Items { get; }
		public bool IsIncomplete { get; }
		public CompletionResult(List<CompletionItem> items, bool isIncomplete = false) {
			Items = items;
			IsIncomplete = isIncomplete;
		}
	}

	public interface ICompletionReceiver {
		bool Accept(CompletionResult result);
		bool IsCancelled { get; }
	}

	public interface ICompletionProvider {
		bool IsTriggerCharacter(string ch);
		void ProvideCompletions(CompletionContext context, ICompletionReceiver receiver);
	}

	public interface INewLineActionProvider {
		NewLineAction? GetNewLineAction(NewLineActionContext context);
	}
}
