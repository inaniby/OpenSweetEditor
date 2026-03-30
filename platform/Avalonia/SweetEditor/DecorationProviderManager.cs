using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace SweetEditor {
	public sealed class DecorationProviderManager {
		private readonly EditorControl editor;
		private readonly List<IDecorationProvider> providers = new();
		private readonly Dictionary<IDecorationProvider, ProviderState> states = new();
		private readonly DispatcherTimer debounceTimer;
		private readonly DispatcherTimer scrollRefreshTimer;

		private readonly List<TextChange> pendingTextChanges = new();
		private bool applyScheduled;
		private int generation;
		private int lastVisibleStartLine;
		private int lastVisibleEndLine = -1;
		private bool scrollRefreshScheduled;
		private bool pendingScrollRefresh;
		private long lastScrollRefreshTickMs;

		public DecorationProviderManager(EditorControl editor) {
			this.editor = editor;

			debounceTimer = new DispatcherTimer {
				Interval = TimeSpan.FromMilliseconds(50)
			};
			debounceTimer.Tick += (_, _) => {
				debounceTimer.Stop();
				DoRefresh();
			};

			scrollRefreshTimer = new DispatcherTimer {
				Interval = TimeSpan.FromMilliseconds(1)
			};
			scrollRefreshTimer.Tick += (_, _) => {
				scrollRefreshTimer.Stop();
				scrollRefreshScheduled = false;
				debounceTimer.Stop();
				DoRefresh();
				lastScrollRefreshTickMs = Environment.TickCount64;
				if (pendingScrollRefresh) {
					pendingScrollRefresh = false;
					ScheduleScrollRefresh();
				}
			};
		}

		public void AddProvider(IDecorationProvider provider) {
			if (provider == null) throw new ArgumentNullException(nameof(provider));
			if (providers.Contains(provider)) return;

			providers.Add(provider);
			states[provider] = new ProviderState();
			RequestRefresh();
		}

		public void RemoveProvider(IDecorationProvider provider) {
			if (provider == null) throw new ArgumentNullException(nameof(provider));

			providers.Remove(provider);
			if (states.TryGetValue(provider, out var state) && state.ActiveReceiver != null) {
				state.ActiveReceiver.Cancel();
			}
			states.Remove(provider);
			ScheduleApply();
		}

		public void RegisterProvider(IDecorationProvider provider) => AddProvider(provider);
		public void UnregisterProvider(IDecorationProvider provider) => RemoveProvider(provider);

		public void RequestRefresh() => ScheduleRefresh(0, null);
		public void OnDocumentLoaded() => ScheduleRefresh(0, null);
		public void OnScrollChanged() => ScheduleScrollRefresh();
		public void OnTextChanged(IReadOnlyList<TextChange>? changes) => ScheduleRefresh(50, changes);

		private void ScheduleRefresh(int delayMs, IReadOnlyList<TextChange>? changes) {
			if (changes != null && changes.Count > 0) {
				pendingTextChanges.AddRange(changes);
			}

			if (scrollRefreshScheduled) {
				scrollRefreshTimer.Stop();
				scrollRefreshScheduled = false;
			}
			pendingScrollRefresh = false;

			debounceTimer.Stop();
			debounceTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, delayMs == 0 ? 1 : delayMs));
			debounceTimer.Start();
		}

		private void ScheduleScrollRefresh() {
			long now = Environment.TickCount64;
			long elapsed = now - lastScrollRefreshTickMs;
			int minInterval = GetScrollRefreshMinIntervalMs();
			int delay = elapsed >= minInterval ? 1 : (int)Math.Max(1, minInterval - elapsed);

			if (scrollRefreshScheduled) {
				pendingScrollRefresh = true;
				return;
			}

			scrollRefreshScheduled = true;
			scrollRefreshTimer.Stop();
			scrollRefreshTimer.Interval = TimeSpan.FromMilliseconds(delay);
			scrollRefreshTimer.Start();
		}

		private void DoRefresh() {
			generation++;
			int currentGeneration = generation;

			var visible = editor.GetVisibleLineRange();
			lastVisibleStartLine = visible.start;
			lastVisibleEndLine = visible.end;

			var changes = new List<TextChange>(pendingTextChanges).AsReadOnly();
			pendingTextChanges.Clear();

			int total = editor.GetLineCount();
			int contextStart = visible.start;
			int contextEnd = visible.end;
			if (total > 0 && visible.end >= visible.start) {
				int overscanLines = CalculateOverscanLines(visible.start, visible.end);
				contextStart = Math.Max(0, visible.start - overscanLines);
				contextEnd = Math.Min(total - 1, visible.end + overscanLines);
			}

			var context = new DecorationContext(
				contextStart,
				contextEnd,
				total,
				changes,
				editor.GetLanguageConfiguration(),
				editor.Metadata);

			foreach (var provider in providers) {
				if (!states.TryGetValue(provider, out var state)) {
					state = new ProviderState();
					states[provider] = state;
				}
				state.ActiveReceiver?.Cancel();
				var receiver = new ManagedReceiver(this, provider, currentGeneration);
				state.ActiveReceiver = receiver;
				try {
					provider.ProvideDecorations(context, receiver);
				} catch (Exception ex) {
					Console.Error.WriteLine($"Decoration provider error: {ex.Message}");
				}
			}
		}

		private void ScheduleApply() {
			if (applyScheduled) return;
			applyScheduled = true;
			Dispatcher.UIThread.Post(ApplyMerged);
		}

		private void ApplyMerged() {
			applyScheduled = false;

			var syntaxSpans = new Dictionary<int, List<DecorationResult.SpanItem>>();
			var semanticSpans = new Dictionary<int, List<DecorationResult.SpanItem>>();
			var inlayHints = new Dictionary<int, List<DecorationResult.InlayHintItem>>();
			var diagnostics = new Dictionary<int, List<DecorationResult.DiagnosticItem>>();
			List<DecorationResult.IndentGuideItem>? indentGuides = null;
			List<DecorationResult.BracketGuideItem>? bracketGuides = null;
			List<DecorationResult.FlowGuideItem>? flowGuides = null;
			List<DecorationResult.SeparatorGuideItem>? separatorGuides = null;
			var foldRegions = new List<DecorationResult.FoldRegionItem>();
			var gutterIcons = new Dictionary<int, List<int>>();
			var phantomTexts = new Dictionary<int, List<DecorationResult.PhantomTextItem>>();
			DecorationApplyMode syntaxMode = DecorationApplyMode.MERGE;
			DecorationApplyMode semanticMode = DecorationApplyMode.MERGE;
			DecorationApplyMode inlayMode = DecorationApplyMode.MERGE;
			DecorationApplyMode diagnosticMode = DecorationApplyMode.MERGE;
			DecorationApplyMode indentMode = DecorationApplyMode.MERGE;
			DecorationApplyMode bracketMode = DecorationApplyMode.MERGE;
			DecorationApplyMode flowMode = DecorationApplyMode.MERGE;
			DecorationApplyMode separatorMode = DecorationApplyMode.MERGE;
			DecorationApplyMode foldMode = DecorationApplyMode.MERGE;
			DecorationApplyMode gutterMode = DecorationApplyMode.MERGE;
			DecorationApplyMode phantomMode = DecorationApplyMode.MERGE;

			foreach (var provider in providers) {
				if (!states.TryGetValue(provider, out var state) || state.Snapshot == null) continue;
				var result = state.Snapshot;

				syntaxMode = MergeMode(syntaxMode, result.SyntaxSpansMode);
				if (result.SyntaxSpans != null) AppendMap(syntaxSpans, result.SyntaxSpans);

				semanticMode = MergeMode(semanticMode, result.SemanticSpansMode);
				if (result.SemanticSpans != null) AppendMap(semanticSpans, result.SemanticSpans);

				inlayMode = MergeMode(inlayMode, result.InlayHintsMode);
				if (result.InlayHints != null) AppendMap(inlayHints, result.InlayHints);

				diagnosticMode = MergeMode(diagnosticMode, result.DiagnosticsMode);
				if (result.Diagnostics != null) AppendMap(diagnostics, result.Diagnostics);

				gutterMode = MergeMode(gutterMode, result.GutterIconsMode);
				if (result.GutterIcons != null) AppendMap(gutterIcons, result.GutterIcons);

				phantomMode = MergeMode(phantomMode, result.PhantomTextsMode);
				if (result.PhantomTexts != null) AppendMap(phantomTexts, result.PhantomTexts);

				indentMode = MergeMode(indentMode, result.IndentGuidesMode);
				if (result.IndentGuides != null) indentGuides = new List<DecorationResult.IndentGuideItem>(result.IndentGuides);

				bracketMode = MergeMode(bracketMode, result.BracketGuidesMode);
				if (result.BracketGuides != null) bracketGuides = new List<DecorationResult.BracketGuideItem>(result.BracketGuides);

				flowMode = MergeMode(flowMode, result.FlowGuidesMode);
				if (result.FlowGuides != null) flowGuides = new List<DecorationResult.FlowGuideItem>(result.FlowGuides);

				separatorMode = MergeMode(separatorMode, result.SeparatorGuidesMode);
				if (result.SeparatorGuides != null) separatorGuides = new List<DecorationResult.SeparatorGuideItem>(result.SeparatorGuides);

				foldMode = MergeMode(foldMode, result.FoldRegionsMode);
				if (result.FoldRegions != null) foldRegions.AddRange(result.FoldRegions);
			}

			ApplySpanMode(SpanLayer.SYNTAX, syntaxMode);
			ApplySpanMode(SpanLayer.SEMANTIC, semanticMode);
			ApplySpans(syntaxSpans, SpanLayer.SYNTAX);
			ApplySpans(semanticSpans, SpanLayer.SEMANTIC);

			ApplyInlayMode(inlayMode);
			foreach (var (line, items) in inlayHints) {
				var hints = new List<InlayHint>(items.Count);
				foreach (var item in items) {
					switch (item.Type) {
						case DecorationResult.InlayHintType.Text when item.Text != null:
							hints.Add(InlayHint.TextHint(item.Column, item.Text));
							break;
						case DecorationResult.InlayHintType.Icon:
							hints.Add(InlayHint.IconHint(item.Column, item.IntValue));
							break;
						case DecorationResult.InlayHintType.Color:
							hints.Add(InlayHint.ColorHint(item.Column, item.IntValue));
							break;
					}
				}
				editor.SetLineInlayHints(line, hints);
			}

			ApplyDiagnosticMode(diagnosticMode);
			foreach (var (line, items) in diagnostics) {
				var diagItems = new List<DiagnosticItem>(items.Count);
				for (int i = 0; i < items.Count; i++) {
					diagItems.Add(new DiagnosticItem(items[i].Column, items[i].Length, items[i].Severity, items[i].Color));
				}
				editor.SetLineDiagnostics(line, diagItems);
			}

			if (indentMode == DecorationApplyMode.REPLACE_ALL || indentMode == DecorationApplyMode.REPLACE_RANGE) {
				if (indentGuides != null) {
					var guides = new List<IndentGuide>(indentGuides.Count);
					foreach (var item in indentGuides) guides.Add(new IndentGuide(item.Start, item.End));
					editor.SetIndentGuides(guides);
				} else {
					editor.SetIndentGuides(new List<IndentGuide>());
				}
			} else if (indentGuides != null) {
				var guides = new List<IndentGuide>(indentGuides.Count);
				foreach (var item in indentGuides) guides.Add(new IndentGuide(item.Start, item.End));
				editor.SetIndentGuides(guides);
			}

			if (bracketMode == DecorationApplyMode.REPLACE_ALL || bracketMode == DecorationApplyMode.REPLACE_RANGE) {
				if (bracketGuides != null) {
					var guides = new List<BracketGuide>(bracketGuides.Count);
					foreach (var item in bracketGuides) guides.Add(new BracketGuide(item.Parent, item.End, item.Children));
					editor.SetBracketGuides(guides);
				} else {
					editor.SetBracketGuides(new List<BracketGuide>());
				}
			} else if (bracketGuides != null) {
				var guides = new List<BracketGuide>(bracketGuides.Count);
				foreach (var item in bracketGuides) guides.Add(new BracketGuide(item.Parent, item.End, item.Children));
				editor.SetBracketGuides(guides);
			}

			if (flowMode == DecorationApplyMode.REPLACE_ALL || flowMode == DecorationApplyMode.REPLACE_RANGE) {
				if (flowGuides != null) {
					var guides = new List<FlowGuide>(flowGuides.Count);
					foreach (var item in flowGuides) guides.Add(new FlowGuide(item.Start, item.End));
					editor.SetFlowGuides(guides);
				} else {
					editor.SetFlowGuides(new List<FlowGuide>());
				}
			} else if (flowGuides != null) {
				var guides = new List<FlowGuide>(flowGuides.Count);
				foreach (var item in flowGuides) guides.Add(new FlowGuide(item.Start, item.End));
				editor.SetFlowGuides(guides);
			}

			if (separatorMode == DecorationApplyMode.REPLACE_ALL || separatorMode == DecorationApplyMode.REPLACE_RANGE) {
				if (separatorGuides != null) {
					var guides = new List<SeparatorGuide>(separatorGuides.Count);
					foreach (var item in separatorGuides) {
						guides.Add(new SeparatorGuide(item.Line, item.Style, item.Count, item.TextEndColumn));
					}
					editor.SetSeparatorGuides(guides);
				} else {
					editor.SetSeparatorGuides(new List<SeparatorGuide>());
				}
			} else if (separatorGuides != null) {
				var guides = new List<SeparatorGuide>(separatorGuides.Count);
				foreach (var item in separatorGuides) {
					guides.Add(new SeparatorGuide(item.Line, item.Style, item.Count, item.TextEndColumn));
				}
				editor.SetSeparatorGuides(guides);
			}

			if (foldMode == DecorationApplyMode.REPLACE_ALL || foldMode == DecorationApplyMode.REPLACE_RANGE) {
				var regions = new List<FoldRegion>(foldRegions.Count);
				for (int i = 0; i < foldRegions.Count; i++) {
					var r = foldRegions[i];
					regions.Add(new FoldRegion(r.StartLine, r.EndLine));
				}
				editor.SetFoldRegions(regions);
			} else if (foldRegions.Count > 0) {
				var regions = new List<FoldRegion>(foldRegions.Count);
				for (int i = 0; i < foldRegions.Count; i++) {
					var r = foldRegions[i];
					regions.Add(new FoldRegion(r.StartLine, r.EndLine));
				}
				editor.SetFoldRegions(regions);
			}

			ApplyGutterMode(gutterMode);
			foreach (var (line, icons) in gutterIcons) {
				var iconList = new List<GutterIcon>(icons.Count);
				foreach (var icon in icons) iconList.Add(new GutterIcon(icon));
				editor.SetLineGutterIcons(line, iconList);
			}

			ApplyPhantomMode(phantomMode);
			foreach (var (line, items) in phantomTexts) {
				var phantomList = new List<PhantomText>(items.Count);
				foreach (var item in items) phantomList.Add(new PhantomText(item.Column, item.Text));
				editor.SetLinePhantomTexts(line, phantomList);
			}

			editor.Flush();
		}

		private void ApplySpanMode(SpanLayer layer, DecorationApplyMode mode) {
			if (mode == DecorationApplyMode.REPLACE_ALL) {
				editor.ClearHighlightsLayer((byte)layer);
			} else if (mode == DecorationApplyMode.REPLACE_RANGE) {
				ClearSpanRange(layer, lastVisibleStartLine, lastVisibleEndLine);
			}
		}

		private void ApplyInlayMode(DecorationApplyMode mode) {
			if (mode == DecorationApplyMode.REPLACE_ALL) {
				editor.ClearInlayHints();
			} else if (mode == DecorationApplyMode.REPLACE_RANGE) {
				ClearInlayRange(lastVisibleStartLine, lastVisibleEndLine);
			}
		}

		private void ApplyDiagnosticMode(DecorationApplyMode mode) {
			if (mode == DecorationApplyMode.REPLACE_ALL) {
				editor.ClearDiagnostics();
			} else if (mode == DecorationApplyMode.REPLACE_RANGE) {
				ClearDiagnosticRange(lastVisibleStartLine, lastVisibleEndLine);
			}
		}

		private void ApplyGutterMode(DecorationApplyMode mode) {
			if (mode == DecorationApplyMode.REPLACE_ALL) {
				editor.ClearGutterIcons();
			} else if (mode == DecorationApplyMode.REPLACE_RANGE) {
				ClearGutterRange(lastVisibleStartLine, lastVisibleEndLine);
			}
		}

		private void ApplyPhantomMode(DecorationApplyMode mode) {
			if (mode == DecorationApplyMode.REPLACE_ALL) {
				editor.ClearPhantomTexts();
			} else if (mode == DecorationApplyMode.REPLACE_RANGE) {
				ClearPhantomRange(lastVisibleStartLine, lastVisibleEndLine);
			}
		}

		private void ClearSpanRange(SpanLayer layer, int startLine, int endLine) {
			var empty = BuildEmptyRangeMap<StyleSpan>(startLine, endLine);
			if (empty.Count == 0) return;
			editor.SetBatchLineSpans((int)layer, empty);
		}

		private void ClearInlayRange(int startLine, int endLine) {
			var empty = BuildEmptyRangeMap<InlayHint>(startLine, endLine);
			if (empty.Count == 0) return;
			editor.SetBatchLineInlayHints(empty);
		}

		private void ClearDiagnosticRange(int startLine, int endLine) {
			var empty = BuildEmptyRangeMap<DiagnosticItem>(startLine, endLine);
			if (empty.Count == 0) return;
			editor.SetBatchLineDiagnostics(empty);
		}

		private void ClearGutterRange(int startLine, int endLine) {
			var empty = BuildEmptyRangeMap<GutterIcon>(startLine, endLine);
			if (empty.Count == 0) return;
			editor.SetBatchLineGutterIcons(empty);
		}

		private void ClearPhantomRange(int startLine, int endLine) {
			var empty = BuildEmptyRangeMap<PhantomText>(startLine, endLine);
			if (empty.Count == 0) return;
			editor.SetBatchLinePhantomTexts(empty);
		}

		private static Dictionary<int, IList<T>> BuildEmptyRangeMap<T>(int startLine, int endLine) {
			var outMap = new Dictionary<int, IList<T>>();
			if (endLine < startLine) return outMap;

			IList<T> empty = Array.Empty<T>();
			for (int line = startLine; line <= endLine; line++) {
				outMap[line] = empty;
			}
			return outMap;
		}

		private static DecorationApplyMode MergeMode(DecorationApplyMode current, DecorationApplyMode next) {
			return ModePriority(next) > ModePriority(current) ? next : current;
		}

		private static int ModePriority(DecorationApplyMode mode) {
			return mode switch {
				DecorationApplyMode.MERGE => 0,
				DecorationApplyMode.REPLACE_RANGE => 1,
				DecorationApplyMode.REPLACE_ALL => 2,
				_ => 0
			};
		}

		private int GetScrollRefreshMinIntervalMs() {
			return Math.Max(0, editor.Settings.GetDecorationScrollRefreshMinIntervalMs());
		}

		private int CalculateOverscanLines(int visibleStart, int visibleEnd) {
			int viewportLineCount = visibleEnd >= visibleStart ? (visibleEnd - visibleStart + 1) : 0;
			if (viewportLineCount <= 0) return 0;

			float multiplier = Math.Max(0f, editor.Settings.GetDecorationOverscanViewportMultiplier());
			return Math.Max(0, (int)Math.Ceiling(viewportLineCount * multiplier));
		}

		private void ApplySpans(Dictionary<int, List<DecorationResult.SpanItem>> map, SpanLayer layer) {
			foreach (var (line, spans) in map) {
				var styleSpans = new List<StyleSpan>(spans.Count);
				for (int i = 0; i < spans.Count; i++) {
					styleSpans.Add(new StyleSpan(spans[i].Column, spans[i].Length, spans[i].StyleId));
				}
				editor.SetLineSpans(line, (int)layer, styleSpans);
			}
		}

		private static void AppendMap<T>(Dictionary<int, List<T>> target, Dictionary<int, List<T>>? source) {
			if (source == null) return;
			foreach (var (line, list) in source) {
				if (!target.TryGetValue(line, out var dst)) {
					dst = new List<T>();
					target[line] = dst;
				}
				if (list != null) dst.AddRange(list);
			}
		}

		private void OnReceiverAccept(IDecorationProvider provider, int receiverGeneration, DecorationResult result) {
			if (receiverGeneration != generation) return;

			if (!states.TryGetValue(provider, out var state)) {
				state = new ProviderState();
				states[provider] = state;
			}
			if (state.Snapshot == null) {
				state.Snapshot = new DecorationResult();
			}

			MergePatch(state.Snapshot, result);
			ScheduleApply();
		}

		private static void MergePatch(DecorationResult target, DecorationResult patch) {
			if (patch.SyntaxSpans != null) {
				target.SyntaxSpans = patch.SyntaxSpans;
				target.SyntaxSpansMode = patch.SyntaxSpansMode;
			} else if (patch.SyntaxSpansMode != DecorationApplyMode.MERGE) {
				target.SyntaxSpans = null;
				target.SyntaxSpansMode = patch.SyntaxSpansMode;
			}

			if (patch.SemanticSpans != null) {
				target.SemanticSpans = patch.SemanticSpans;
				target.SemanticSpansMode = patch.SemanticSpansMode;
			} else if (patch.SemanticSpansMode != DecorationApplyMode.MERGE) {
				target.SemanticSpans = null;
				target.SemanticSpansMode = patch.SemanticSpansMode;
			}

			if (patch.InlayHints != null) {
				target.InlayHints = patch.InlayHints;
				target.InlayHintsMode = patch.InlayHintsMode;
			} else if (patch.InlayHintsMode != DecorationApplyMode.MERGE) {
				target.InlayHints = null;
				target.InlayHintsMode = patch.InlayHintsMode;
			}

			if (patch.Diagnostics != null) {
				target.Diagnostics = patch.Diagnostics;
				target.DiagnosticsMode = patch.DiagnosticsMode;
			} else if (patch.DiagnosticsMode != DecorationApplyMode.MERGE) {
				target.Diagnostics = null;
				target.DiagnosticsMode = patch.DiagnosticsMode;
			}

			if (patch.IndentGuides != null) {
				target.IndentGuides = patch.IndentGuides;
				target.IndentGuidesMode = patch.IndentGuidesMode;
			} else if (patch.IndentGuidesMode != DecorationApplyMode.MERGE) {
				target.IndentGuides = null;
				target.IndentGuidesMode = patch.IndentGuidesMode;
			}

			if (patch.BracketGuides != null) {
				target.BracketGuides = patch.BracketGuides;
				target.BracketGuidesMode = patch.BracketGuidesMode;
			} else if (patch.BracketGuidesMode != DecorationApplyMode.MERGE) {
				target.BracketGuides = null;
				target.BracketGuidesMode = patch.BracketGuidesMode;
			}

			if (patch.FlowGuides != null) {
				target.FlowGuides = patch.FlowGuides;
				target.FlowGuidesMode = patch.FlowGuidesMode;
			} else if (patch.FlowGuidesMode != DecorationApplyMode.MERGE) {
				target.FlowGuides = null;
				target.FlowGuidesMode = patch.FlowGuidesMode;
			}

			if (patch.SeparatorGuides != null) {
				target.SeparatorGuides = patch.SeparatorGuides;
				target.SeparatorGuidesMode = patch.SeparatorGuidesMode;
			} else if (patch.SeparatorGuidesMode != DecorationApplyMode.MERGE) {
				target.SeparatorGuides = null;
				target.SeparatorGuidesMode = patch.SeparatorGuidesMode;
			}

			if (patch.FoldRegions != null) {
				target.FoldRegions = patch.FoldRegions;
				target.FoldRegionsMode = patch.FoldRegionsMode;
			} else if (patch.FoldRegionsMode != DecorationApplyMode.MERGE) {
				target.FoldRegions = null;
				target.FoldRegionsMode = patch.FoldRegionsMode;
			}

			if (patch.GutterIcons != null) {
				target.GutterIcons = patch.GutterIcons;
				target.GutterIconsMode = patch.GutterIconsMode;
			} else if (patch.GutterIconsMode != DecorationApplyMode.MERGE) {
				target.GutterIcons = null;
				target.GutterIconsMode = patch.GutterIconsMode;
			}

			if (patch.PhantomTexts != null) {
				target.PhantomTexts = patch.PhantomTexts;
				target.PhantomTextsMode = patch.PhantomTextsMode;
			} else if (patch.PhantomTextsMode != DecorationApplyMode.MERGE) {
				target.PhantomTexts = null;
				target.PhantomTextsMode = patch.PhantomTextsMode;
			}
		}

		private sealed class ProviderState {
			public DecorationResult? Snapshot;
			public ManagedReceiver? ActiveReceiver;
		}

		private sealed class ManagedReceiver : IDecorationReceiver {
			private readonly DecorationProviderManager manager;
			private readonly IDecorationProvider provider;
			private readonly int receiverGeneration;
			private bool cancelled;

			public ManagedReceiver(DecorationProviderManager manager, IDecorationProvider provider, int receiverGeneration) {
				this.manager = manager;
				this.provider = provider;
				this.receiverGeneration = receiverGeneration;
			}

			public bool Accept(DecorationResult result) {
				if (cancelled || receiverGeneration != manager.generation) {
					return false;
				}
				var snapshot = result.Clone();
				Dispatcher.UIThread.Post(() => {
					if (cancelled || receiverGeneration != manager.generation) {
						return;
					}
					manager.OnReceiverAccept(provider, receiverGeneration, snapshot);
				});
				return true;
			}

			public bool IsCancelled => cancelled || receiverGeneration != manager.generation;
			public void Cancel() => cancelled = true;
		}
	}
}
