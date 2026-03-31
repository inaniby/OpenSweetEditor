using System;
using System.Collections.Generic;

namespace SweetEditor {
	public interface INewLineActionProvider {
		NewLineAction? ProvideNewLineAction(NewLineContext context);
	}

	public sealed class NewLineContext {
		public int LineNumber { get; }
		public int Column { get; }
		public string LineText { get; }
		public LanguageConfiguration? LanguageConfig { get; }
		public IEditorMetadata? EditorMetadata { get; }

		public NewLineContext(
			int lineNumber,
			int column,
			string lineText,
			LanguageConfiguration? languageConfig,
			IEditorMetadata? editorMetadata) {
			LineNumber = lineNumber;
			Column = column;
			LineText = lineText;
			LanguageConfig = languageConfig;
			EditorMetadata = editorMetadata;
		}
	}

	public sealed class NewLineAction {
		public string Text { get; }

		public NewLineAction(string text) {
			Text = text;
		}
	}

	internal sealed class NewLineActionProviderManager {
		private readonly List<INewLineActionProvider> providers = new();
		private readonly EditorControl editor;

		public NewLineActionProviderManager(EditorControl editor) {
			this.editor = editor;
		}

		public void AddProvider(INewLineActionProvider provider) {
			if (provider == null) throw new ArgumentNullException(nameof(provider));
			if (!providers.Contains(provider)) {
				providers.Add(provider);
			}
		}

		public void RemoveProvider(INewLineActionProvider provider) {
			if (provider == null) throw new ArgumentNullException(nameof(provider));
			providers.Remove(provider);
		}

		public void RegisterProvider(INewLineActionProvider provider) => AddProvider(provider);
		public void UnregisterProvider(INewLineActionProvider provider) => RemoveProvider(provider);

		public NewLineAction? ProvideNewLineAction() {
			if (providers.Count == 0) {
				return null;
			}

			var context = CreateContext();
			foreach (var provider in providers) {
				try {
					var action = provider.ProvideNewLineAction(context);
					if (action != null) {
						return action;
					}
				} catch (Exception ex) {
					Console.Error.WriteLine($"NewLineAction provider error: {ex.Message}");
				}
			}
			return null;
		}

		private NewLineContext CreateContext() {
			var cursorPosition = editor.GetCursorPosition();
			var lineText = cursorPosition.Line >= 0 ? editor.GetLineText(cursorPosition.Line) : string.Empty;
			return new NewLineContext(
				cursorPosition.Line,
				cursorPosition.Column,
				lineText,
				editor.GetLanguageConfiguration(),
				editor.Metadata);
		}
	}
}
