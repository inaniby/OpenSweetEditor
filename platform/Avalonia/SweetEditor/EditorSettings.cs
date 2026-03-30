namespace SweetEditor {
	public class EditorSettings {
		private readonly EditorControl editor;
		private float scale = 1.0f;
		private FoldArrowMode foldArrowMode = FoldArrowMode.ALWAYS;
		private WrapMode wrapMode = WrapMode.NONE;
		private float lineSpacingAdd = 0f;
		private float lineSpacingMult = 1.0f;
		private float contentStartPadding = 0f;
		private bool showSplitLine = true;
		private bool gutterSticky = true;
		private bool gutterVisible = true;
		private CurrentLineRenderMode currentLineRenderMode = CurrentLineRenderMode.BACKGROUND;
		private AutoIndentMode autoIndentMode = AutoIndentMode.NONE;
		private bool readOnly = false;
		private int tabSize = 4;
		private int maxGutterIcons = 0;
		private int decorationScrollRefreshMinIntervalMs = 16;
		private float decorationOverscanViewportMultiplier = 1.5f;

		internal EditorSettings(EditorControl editor) {
			this.editor = editor;
		}

		public void SetScale(float scale) {
			this.scale = scale;
			editor.EditorCoreInternal.SetScale(scale);
			editor.SyncPlatformScaleInternal(scale);
			editor.Flush();
		}

		public float GetScale() => scale;

		public void SetFoldArrowMode(FoldArrowMode mode) {
			foldArrowMode = mode;
			editor.EditorCoreInternal.SetFoldArrowMode(mode);
		}

		public FoldArrowMode GetFoldArrowMode() => foldArrowMode;

		public void SetWrapMode(WrapMode mode) {
			wrapMode = mode;
			editor.EditorCoreInternal.SetWrapMode(mode);
			editor.Flush();
		}

		public WrapMode GetWrapMode() => wrapMode;

		public void SetLineSpacing(float add, float mult) {
			lineSpacingAdd = add;
			lineSpacingMult = mult;
			editor.EditorCoreInternal.SetLineSpacing(add, mult);
			editor.Flush();
		}

		public float GetLineSpacingAdd() => lineSpacingAdd;

		public float GetLineSpacingMult() => lineSpacingMult;

		public void SetContentStartPadding(float padding) {
			contentStartPadding = System.Math.Max(0f, padding);
			editor.EditorCoreInternal.SetContentStartPadding(contentStartPadding);
			editor.Flush();
		}

		public float GetContentStartPadding() => contentStartPadding;

		public void SetShowSplitLine(bool show) {
			showSplitLine = show;
			editor.EditorCoreInternal.SetShowSplitLine(show);
			editor.Flush();
		}

		public bool IsShowSplitLine() => showSplitLine;

		public void SetGutterSticky(bool sticky) {
			gutterSticky = sticky;
			editor.EditorCoreInternal.SetGutterSticky(sticky);
			editor.Flush();
		}

		public bool IsGutterSticky() => gutterSticky;

		public void SetGutterVisible(bool visible) {
			gutterVisible = visible;
			editor.EditorCoreInternal.SetGutterVisible(visible);
			editor.Flush();
		}

		public bool IsGutterVisible() => gutterVisible;

		public void SetCurrentLineRenderMode(CurrentLineRenderMode mode) {
			currentLineRenderMode = mode;
			editor.EditorCoreInternal.SetCurrentLineRenderMode(mode);
			editor.Flush();
		}

		public CurrentLineRenderMode GetCurrentLineRenderMode() => currentLineRenderMode;

		public void SetAutoIndentMode(AutoIndentMode mode) {
			autoIndentMode = mode;
			editor.EditorCoreInternal.SetAutoIndentMode(mode);
		}

		public AutoIndentMode GetAutoIndentMode() => autoIndentMode;

		public void SetReadOnly(bool readOnly) {
			this.readOnly = readOnly;
			editor.EditorCoreInternal.SetReadOnly(readOnly);
		}

		public bool IsReadOnly() => readOnly;

		public void SetTabSize(int value) {
			tabSize = System.Math.Max(1, value);
			editor.EditorCoreInternal.SetTabSize(tabSize);
			editor.Flush();
		}

		public int GetTabSize() => tabSize;

		public void SetMaxGutterIcons(int count) {
			maxGutterIcons = System.Math.Max(0, count);
			editor.EditorCoreInternal.SetMaxGutterIcons((uint)maxGutterIcons);
		}

		public int GetMaxGutterIcons() => maxGutterIcons;

		public void SetDecorationScrollRefreshMinIntervalMs(int intervalMs) {
			decorationScrollRefreshMinIntervalMs = System.Math.Max(0, intervalMs);
			editor.RequestDecorationRefresh();
		}

		public int GetDecorationScrollRefreshMinIntervalMs() => decorationScrollRefreshMinIntervalMs;

		public void SetDecorationOverscanViewportMultiplier(float multiplier) {
			decorationOverscanViewportMultiplier = System.Math.Max(0f, multiplier);
			editor.RequestDecorationRefresh();
		}

		public float GetDecorationOverscanViewportMultiplier() => decorationOverscanViewportMultiplier;
	}
}
