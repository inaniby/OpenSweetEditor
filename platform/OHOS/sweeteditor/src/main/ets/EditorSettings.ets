import { AutoIndentMode, CurrentLineRenderMode, FoldArrowMode, WrapMode } from './core/EditorFoundation';
import { SweetEditor } from './SweetEditor';

export class EditorSettings {
  private editor: SweetEditor;
  private textSize: number = 28;
  private fontFamily: string = 'monospace';
  private scale: number = 1.0;
  private foldArrowMode: FoldArrowMode = FoldArrowMode.ALWAYS;
  private wrapMode: WrapMode = WrapMode.NONE;
  private lineSpacingAdd: number = 0;
  private lineSpacingMult: number = 1.0;
  private contentStartPadding: number = 0;
  private showSplitLine: boolean = true;
  private gutterSticky: boolean = true;
  private gutterVisible: boolean = true;
  private currentLineRenderMode: CurrentLineRenderMode = CurrentLineRenderMode.BACKGROUND;
  private autoIndentMode: AutoIndentMode = AutoIndentMode.NONE;
  private backspaceUnindent: boolean = true;
  private readOnly: boolean = false;
  private compositionEnabled: boolean = true;
  private maxGutterIcons: number = 0;
  private decorationScrollRefreshMinIntervalMs: number = 16;
  private decorationOverscanViewportMultiplier: number = 1.5;

  constructor(editor: SweetEditor) {
    this.editor = editor;
  }

  setEditorTextSize(size: number): void {
    this.textSize = size;
    this.editor.applyTextSize(size);
  }

  getEditorTextSize(): number {
    return this.textSize;
  }

  setFontFamily(fontFamily: string): void {
    this.fontFamily = fontFamily;
    this.editor.applyFontFamily(fontFamily);
  }

  getFontFamily(): string {
    return this.fontFamily;
  }

  setScale(scale: number): void {
    this.scale = scale;
    this.editor.applyScale(scale);
  }

  getScale(): number {
    return this.scale;
  }

  setFoldArrowMode(mode: FoldArrowMode): void {
    this.foldArrowMode = mode;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setFoldArrowMode(mode);
  }

  getFoldArrowMode(): FoldArrowMode {
    return this.foldArrowMode;
  }

  setWrapMode(mode: WrapMode): void {
    this.wrapMode = mode;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setWrapMode(mode);
    this.editor.flush();
  }

  getWrapMode(): WrapMode {
    return this.wrapMode;
  }

  setLineSpacing(add: number, mult: number): void {
    this.lineSpacingAdd = add;
    this.lineSpacingMult = mult;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setLineSpacing(add, mult);
    this.editor.flush();
  }

  getLineSpacingAdd(): number {
    return this.lineSpacingAdd;
  }

  getLineSpacingMult(): number {
    return this.lineSpacingMult;
  }

  setContentStartPadding(padding: number): void {
    this.contentStartPadding = Math.max(0, padding);
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setContentStartPadding(this.contentStartPadding);
    this.editor.flush();
  }

  getContentStartPadding(): number {
    return this.contentStartPadding;
  }

  setShowSplitLine(show: boolean): void {
    this.showSplitLine = show;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setShowSplitLine(show);
    this.editor.flush();
  }

  isShowSplitLine(): boolean {
    return this.showSplitLine;
  }

  setGutterSticky(sticky: boolean): void {
    this.gutterSticky = sticky;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setGutterSticky(sticky);
    this.editor.flush();
  }

  isGutterSticky(): boolean {
    return this.gutterSticky;
  }

  setGutterVisible(visible: boolean): void {
    this.gutterVisible = visible;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setGutterVisible(visible);
    this.editor.flush();
  }

  isGutterVisible(): boolean {
    return this.gutterVisible;
  }

  setCurrentLineRenderMode(mode: CurrentLineRenderMode): void {
    this.currentLineRenderMode = mode;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setCurrentLineRenderMode(mode);
    this.editor.flush();
  }

  getCurrentLineRenderMode(): CurrentLineRenderMode {
    return this.currentLineRenderMode;
  }

  setAutoIndentMode(mode: AutoIndentMode): void {
    this.autoIndentMode = mode;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setAutoIndentMode(mode);
  }

  getAutoIndentMode(): AutoIndentMode {
    return this.autoIndentMode;
  }

  setBackspaceUnindent(enabled: boolean): void {
    this.backspaceUnindent = enabled;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setBackspaceUnindent(enabled);
  }

  isBackspaceUnindent(): boolean {
    return this.backspaceUnindent;
  }

  setReadOnly(readOnly: boolean): void {
    this.readOnly = readOnly;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setReadOnly(readOnly);
  }

  isReadOnly(): boolean {
    return this.readOnly;
  }

  setCompositionEnabled(enabled: boolean): void {
    this.compositionEnabled = enabled;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setCompositionEnabled(enabled);
    this.editor.flush();
  }

  isCompositionEnabled(): boolean {
    return this.compositionEnabled;
  }

  setMaxGutterIcons(count: number): void {
    this.maxGutterIcons = count;
    const core = this.editor.getEditorCore();
    if (!core) {
      return;
    }
    core.setMaxGutterIcons(count);
  }

  getMaxGutterIcons(): number {
    return this.maxGutterIcons;
  }

  setDecorationScrollRefreshMinIntervalMs(intervalMs: number): void {
    this.decorationScrollRefreshMinIntervalMs = Math.max(0, intervalMs);
    this.editor.requestDecorationRefresh();
  }

  getDecorationScrollRefreshMinIntervalMs(): number {
    return this.decorationScrollRefreshMinIntervalMs;
  }

  setDecorationOverscanViewportMultiplier(multiplier: number): void {
    this.decorationOverscanViewportMultiplier = Math.max(0, multiplier);
    this.editor.requestDecorationRefresh();
  }

  getDecorationOverscanViewportMultiplier(): number {
    return this.decorationOverscanViewportMultiplier;
  }
}
