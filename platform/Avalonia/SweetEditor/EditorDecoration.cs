using System;
using System.Collections.Generic;

namespace SweetEditor {
	[Flags]
	public enum DecorationType {
		SyntaxHighlight = 1 << 0,
		SemanticHighlight = 1 << 1,
		InlayHint = 1 << 2,
		Diagnostic = 1 << 3,
		FoldRegion = 1 << 4,
		IndentGuide = 1 << 5,
		BracketGuide = 1 << 6,
		FlowGuide = 1 << 7,
		SeparatorGuide = 1 << 8,
		GutterIcon = 1 << 9,
		PhantomText = 1 << 10,
	}

	public enum DecorationApplyMode {
		MERGE = 0,
		REPLACE_ALL = 1,
		REPLACE_RANGE = 2,
	}

	public sealed class DecorationContext {
		public int VisibleStartLine { get; }
		public int VisibleEndLine { get; }
		public int TotalLineCount { get; }
		public IReadOnlyList<TextChange> TextChanges { get; }
		public LanguageConfiguration? LanguageConfiguration { get; }
		public IEditorMetadata? EditorMetadata { get; }

		public DecorationContext(int visibleStartLine, int visibleEndLine, int totalLineCount,
								 IReadOnlyList<TextChange> textChanges, LanguageConfiguration? languageConfiguration = null,
								 IEditorMetadata? editorMetadata = null) {
			VisibleStartLine = visibleStartLine;
			VisibleEndLine = visibleEndLine;
			TotalLineCount = totalLineCount;
			TextChanges = textChanges;
			LanguageConfiguration = languageConfiguration;
			EditorMetadata = editorMetadata;
		}
	}

	public interface IDecorationReceiver {
		bool Accept(DecorationResult result);
		bool IsCancelled { get; }
	}

	public interface IDecorationProvider {
		DecorationType Capabilities { get; }
		void ProvideDecorations(DecorationContext context, IDecorationReceiver receiver);
	}

	public sealed class DecorationResult {
		public Dictionary<int, List<SpanItem>>? SyntaxSpans { get; set; }
		public Dictionary<int, List<SpanItem>>? SemanticSpans { get; set; }
		public Dictionary<int, List<InlayHintItem>>? InlayHints { get; set; }
		public Dictionary<int, List<DiagnosticItem>>? Diagnostics { get; set; }
		public List<IndentGuideItem>? IndentGuides { get; set; }
		public List<BracketGuideItem>? BracketGuides { get; set; }
		public List<FlowGuideItem>? FlowGuides { get; set; }
		public List<SeparatorGuideItem>? SeparatorGuides { get; set; }
		public List<FoldRegionItem>? FoldRegions { get; set; }
		public Dictionary<int, List<int>>? GutterIcons { get; set; }
		public Dictionary<int, List<PhantomTextItem>>? PhantomTexts { get; set; }

		public DecorationApplyMode SyntaxSpansMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode SemanticSpansMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode InlayHintsMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode DiagnosticsMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode IndentGuidesMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode BracketGuidesMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode FlowGuidesMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode SeparatorGuidesMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode FoldRegionsMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode GutterIconsMode { get; set; } = DecorationApplyMode.MERGE;
		public DecorationApplyMode PhantomTextsMode { get; set; } = DecorationApplyMode.MERGE;

		public DecorationResult Clone() {
			return new DecorationResult {
				SyntaxSpans = CopyMap(SyntaxSpans),
				SemanticSpans = CopyMap(SemanticSpans),
				InlayHints = CopyMap(InlayHints),
				Diagnostics = CopyMap(Diagnostics),
				IndentGuides = IndentGuides == null ? null : new List<IndentGuideItem>(IndentGuides),
				BracketGuides = BracketGuides == null ? null : new List<BracketGuideItem>(BracketGuides),
				FlowGuides = FlowGuides == null ? null : new List<FlowGuideItem>(FlowGuides),
				SeparatorGuides = SeparatorGuides == null ? null : new List<SeparatorGuideItem>(SeparatorGuides),
				FoldRegions = FoldRegions == null ? null : new List<FoldRegionItem>(FoldRegions),
				GutterIcons = CopyMap(GutterIcons),
				PhantomTexts = CopyMap(PhantomTexts),
				SyntaxSpansMode = SyntaxSpansMode,
				SemanticSpansMode = SemanticSpansMode,
				InlayHintsMode = InlayHintsMode,
				DiagnosticsMode = DiagnosticsMode,
				IndentGuidesMode = IndentGuidesMode,
				BracketGuidesMode = BracketGuidesMode,
				FlowGuidesMode = FlowGuidesMode,
				SeparatorGuidesMode = SeparatorGuidesMode,
				FoldRegionsMode = FoldRegionsMode,
				GutterIconsMode = GutterIconsMode,
				PhantomTextsMode = PhantomTextsMode,
			};
		}

		private static Dictionary<int, List<T>>? CopyMap<T>(Dictionary<int, List<T>>? source) {
			if (source == null) return null;
			var outMap = new Dictionary<int, List<T>>(source.Count);
			foreach (var kv in source) {
				outMap[kv.Key] = kv.Value == null ? new List<T>() : new List<T>(kv.Value);
			}
			return outMap;
		}

		public sealed record SpanItem(int Column, int Length, int StyleId);
		public sealed record DiagnosticItem(int Column, int Length, int Severity, int Color);
		public sealed record FoldRegionItem(int StartLine, int EndLine);
		public sealed record IndentGuideItem(TextPosition Start, TextPosition End);
		public sealed record BracketGuideItem(TextPosition Parent, TextPosition End, TextPosition[]? Children);
		public sealed record FlowGuideItem(TextPosition Start, TextPosition End);
		public sealed record SeparatorGuideItem(int Line, int Style, int Count, int TextEndColumn);
		public sealed record PhantomTextItem(int Column, string Text);

		public sealed record InlayHintItem(int Column, InlayHintType Type, string? Text, int IntValue) {
			public static InlayHintItem TextHint(int column, string text) => new(column, InlayHintType.Text, text, 0);
			public static InlayHintItem IconHint(int column, int iconId) => new(column, InlayHintType.Icon, null, iconId);
			public static InlayHintItem ColorHint(int column, int color) => new(column, InlayHintType.Color, null, color);
		}

		public enum InlayHintType {
			Text = 0,
			Icon = 1,
			Color = 2,
		}
	}
}
