using System;
using System.Collections.Generic;

namespace SweetEditor {
	public sealed class NewLineActionProviderManager {
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
					var action = provider.GetNewLineAction(context);
					if (action != null) {
						return action;
					}
				} catch (Exception ex) {
					Console.Error.WriteLine($"NewLineAction provider error: {ex.Message}");
				}
			}
			return null;
		}

		private NewLineActionContext CreateContext() {
			var cursorPosition = editor.GetCursorPosition();
			var lineText = cursorPosition.Line >= 0 ? editor.GetLineText(cursorPosition.Line) : string.Empty;
			
			return new NewLineActionContext(
				cursorPosition,
				lineText,
				editor.GetLanguageConfiguration(),
				editor.Metadata
			);
		}
	}

	public class NewLineActionContext {
		public TextPosition CursorPosition { get; }
		public string LineText { get; }
		public LanguageConfiguration? LanguageConfiguration { get; }
		public IEditorMetadata? EditorMetadata { get; }

		public NewLineActionContext(TextPosition cursorPosition, string lineText, 
									LanguageConfiguration? languageConfiguration, 
									IEditorMetadata? editorMetadata) {
			CursorPosition = cursorPosition;
			LineText = lineText;
			LanguageConfiguration = languageConfiguration;
			EditorMetadata = editorMetadata;
		}
	}

	public class NewLineAction {
		public string Text { get; set; } = string.Empty;
		public int? CursorPosition { get; set; }

		public NewLineAction(string text, int? cursorPosition = null) {
			Text = text;
			CursorPosition = cursorPosition;
		}
	}
}
