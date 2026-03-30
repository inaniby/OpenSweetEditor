using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace SweetEditor {
	public sealed class CompletionProviderManager {
		public event Action<IReadOnlyList<CompletionItem>>? ItemsUpdated;
		public event Action? Dismissed;

		private readonly List<ICompletionProvider> providers = new();
		private readonly Dictionary<ICompletionProvider, ManagedReceiver> activeReceivers = new();
		private readonly EditorControl editor;
		private readonly DispatcherTimer debounceTimer;

		private int generation;
		private readonly List<CompletionItem> mergedItems = new();
		private CompletionTriggerKind lastTriggerKind;
		private string? lastTriggerChar;

		public CompletionProviderManager(EditorControl editor) {
			this.editor = editor;
			debounceTimer = new DispatcherTimer {
				Interval = TimeSpan.FromMilliseconds(50)
			};
			debounceTimer.Tick += (_, _) => {
				debounceTimer.Stop();
				ExecuteRefresh(lastTriggerKind, lastTriggerChar);
			};
		}

		public void AddProvider(ICompletionProvider provider) {
			if (provider == null) throw new ArgumentNullException(nameof(provider));
			if (!providers.Contains(provider)) {
				providers.Add(provider);
			}
		}

		public void RemoveProvider(ICompletionProvider provider) {
			if (provider == null) throw new ArgumentNullException(nameof(provider));
			providers.Remove(provider);
			if (activeReceivers.TryGetValue(provider, out var receiver)) {
				receiver.Cancel();
				activeReceivers.Remove(provider);
			}
		}

		public void RegisterProvider(ICompletionProvider provider) => AddProvider(provider);
		public void UnregisterProvider(ICompletionProvider provider) => RemoveProvider(provider);

		public void TriggerCompletion(CompletionTriggerKind kind, string? triggerChar) {
			if (providers.Count == 0) return;
			lastTriggerKind = kind;
			lastTriggerChar = triggerChar;
			debounceTimer.Stop();
			int delay = kind == CompletionTriggerKind.Invoked ? 1 : 50;
			debounceTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, delay));
			debounceTimer.Start();
		}

		public void TriggerCompletion() => TriggerCompletion(CompletionTriggerKind.Invoked, null);

		public void Dismiss() {
			debounceTimer.Stop();
			generation++;
			CancelAllReceivers();
			mergedItems.Clear();
			Dismissed?.Invoke();
		}

		public bool IsTriggerCharacter(string ch) {
			foreach (var provider in providers) {
				if (provider.IsTriggerCharacter(ch)) {
					return true;
				}
			}
			return false;
		}

		public void ShowItems(List<CompletionItem> items) {
			debounceTimer.Stop();
			generation++;
			CancelAllReceivers();
			mergedItems.Clear();
			mergedItems.AddRange(items);
			ItemsUpdated?.Invoke(new List<CompletionItem>(mergedItems));
		}

		private void ExecuteRefresh(CompletionTriggerKind kind, string? triggerChar) {
			int currentGeneration = ++generation;
			CancelAllReceivers();
			mergedItems.Clear();

			var context = BuildContext(kind, triggerChar);
			if (context == null) {
				Dismiss();
				return;
			}

			foreach (var provider in providers) {
				var receiver = new ManagedReceiver(this, provider, currentGeneration);
				activeReceivers[provider] = receiver;
				try {
					provider.ProvideCompletions(context, receiver);
				} catch (Exception ex) {
					Console.Error.WriteLine($"Completion provider error: {ex.Message}");
				}
			}
		}

		private void CancelAllReceivers() {
			foreach (var receiver in activeReceivers.Values) {
				receiver.Cancel();
			}
			activeReceivers.Clear();
		}

		private CompletionContext? BuildContext(CompletionTriggerKind kind, string? triggerChar) {
			var cursor = editor.GetCursorPosition();
			string lineText = cursor.Line >= 0 ? editor.GetLineText(cursor.Line) : string.Empty;
			var wordRange = editor.GetWordRangeAtCursor();
			return new CompletionContext(
				kind,
				triggerChar,
				cursor,
				lineText,
				wordRange,
				editor.GetLanguageConfiguration(),
				editor.Metadata);
		}

		private void OnReceiverAccept(ICompletionProvider provider, CompletionResult result, int receiverGeneration) {
			if (receiverGeneration != generation) return;
			mergedItems.AddRange(result.Items);
			mergedItems.Sort((a, b) => string.Compare(a.SortKey ?? a.Label, b.SortKey ?? b.Label, StringComparison.Ordinal));
			if (mergedItems.Count == 0) {
				Dismissed?.Invoke();
			} else {
				ItemsUpdated?.Invoke(new List<CompletionItem>(mergedItems));
			}
		}

		private sealed class ManagedReceiver : ICompletionReceiver {
			private readonly CompletionProviderManager manager;
			private readonly ICompletionProvider provider;
			private readonly int receiverGeneration;
			private bool cancelled;

			public ManagedReceiver(CompletionProviderManager manager, ICompletionProvider provider, int receiverGeneration) {
				this.manager = manager;
				this.provider = provider;
				this.receiverGeneration = receiverGeneration;
			}

			public void Cancel() => cancelled = true;

			public bool Accept(CompletionResult result) {
				if (cancelled || receiverGeneration != manager.generation) {
					return false;
				}
				var snapshot = result;
				Dispatcher.UIThread.Post(() => {
					if (cancelled || receiverGeneration != manager.generation) {
						return;
					}
					manager.OnReceiverAccept(provider, snapshot, receiverGeneration);
				});
				return true;
			}

			public bool IsCancelled => cancelled || receiverGeneration != manager.generation;
		}
	}
}
