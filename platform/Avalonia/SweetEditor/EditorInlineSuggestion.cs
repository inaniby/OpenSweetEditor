using System;
using System.Collections.Generic;

namespace SweetEditor {
	/// <summary>
	/// Immutable inline suggestion model.
	/// </summary>
	public sealed class InlineSuggestion {
		public int Line { get; }
		public int Column { get; }
		public string Text { get; }

		public InlineSuggestion(int line, int column, string text) {
			Line = line < 0 ? 0 : line;
			Column = column < 0 ? 0 : column;
			Text = text ?? string.Empty;
		}
	}

	/// <summary>
	/// Host-visible inline suggestion callback contract.
	/// </summary>
	public interface IInlineSuggestionListener {
		void OnSuggestionAccepted(InlineSuggestion suggestion);
		void OnSuggestionDismissed(InlineSuggestion suggestion);
	}

	internal sealed class InlineSuggestionDecorationProvider : IDecorationProvider {
		private readonly Func<InlineSuggestion?> getSuggestion;

		public InlineSuggestionDecorationProvider(Func<InlineSuggestion?> getSuggestion) {
			this.getSuggestion = getSuggestion;
		}

		public DecorationType Capabilities => DecorationType.PhantomText;

		public void ProvideDecorations(DecorationContext context, IDecorationReceiver receiver) {
			if (receiver.IsCancelled) {
				return;
			}

			var suggestion = getSuggestion();
			var result = new DecorationResult {
				PhantomTextsMode = DecorationApplyMode.REPLACE_ALL,
			};

			if (suggestion != null && !string.IsNullOrEmpty(suggestion.Text)) {
				result.PhantomTexts = new Dictionary<int, List<PhantomText>> {
					[suggestion.Line] = new List<PhantomText> {
						new(suggestion.Column, suggestion.Text),
					},
				};
			}

			receiver.Accept(result);
		}
	}
}
