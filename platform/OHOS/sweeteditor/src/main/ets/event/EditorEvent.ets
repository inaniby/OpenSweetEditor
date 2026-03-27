import { TextPosition, TextRange } from '../core/EditorFoundation';
import { PointF } from '../core/EditorVisual';

/**
 * Base interface for editor events.
 * Each concrete event is an immutable data object that implements this interface.
 */
export interface EditorEvent {
  readonly eventName: string;
}

/**
 * Editor event listener callback type.
 */
export type EditorEventListener<T extends EditorEvent> = (event: T) => void;

export enum TextChangeAction {
  INSERT = 'insert',
  DELETE = 'delete',
  KEY = 'key',
  COMPOSITION = 'composition',
  UNDO = 'undo',
  REDO = 'redo'
}

export class TextChangedEvent implements EditorEvent {
  readonly eventName = 'TextChangedEvent';
  readonly action: TextChangeAction;
  readonly changeRange: TextRange | null;
  readonly text: string | null;

  constructor(action: TextChangeAction, changeRange: TextRange | null, text: string | null) {
    this.action = action;
    this.changeRange = changeRange;
    this.text = text;
  }
}

export class CursorChangedEvent implements EditorEvent {
  readonly eventName = 'CursorChangedEvent';
  readonly cursorPosition: TextPosition;

  constructor(cursorPosition: TextPosition) {
    this.cursorPosition = cursorPosition;
  }
}

export class SelectionChangedEvent implements EditorEvent {
  readonly eventName = 'SelectionChangedEvent';
  readonly hasSelection: boolean;
  readonly selection: TextRange | null;
  readonly cursorPosition: TextPosition;

  constructor(hasSelection: boolean, selection: TextRange | null, cursorPosition: TextPosition) {
    this.hasSelection = hasSelection;
    this.selection = selection;
    this.cursorPosition = cursorPosition;
  }
}

export class ScrollChangedEvent implements EditorEvent {
  readonly eventName = 'ScrollChangedEvent';
  readonly scrollX: number;
  readonly scrollY: number;

  constructor(scrollX: number, scrollY: number) {
    this.scrollX = scrollX;
    this.scrollY = scrollY;
  }
}

export class ScaleChangedEvent implements EditorEvent {
  readonly eventName = 'ScaleChangedEvent';
  readonly scale: number;

  constructor(scale: number) {
    this.scale = scale;
  }
}

export class LongPressEvent implements EditorEvent {
  readonly eventName = 'LongPressEvent';
  readonly cursorPosition: TextPosition;
  readonly screenPoint: PointF;

  constructor(cursorPosition: TextPosition, screenPoint: PointF) {
    this.cursorPosition = cursorPosition;
    this.screenPoint = screenPoint;
  }
}

export class DoubleTapEvent implements EditorEvent {
  readonly eventName = 'DoubleTapEvent';
  readonly cursorPosition: TextPosition;
  readonly hasSelection: boolean;
  readonly selection: TextRange | null;
  readonly screenPoint: PointF;

  constructor(cursorPosition: TextPosition, hasSelection: boolean,
    selection: TextRange | null, screenPoint: PointF) {
    this.cursorPosition = cursorPosition;
    this.hasSelection = hasSelection;
    this.selection = selection;
    this.screenPoint = screenPoint;
  }
}

export class ContextMenuEvent implements EditorEvent {
  readonly eventName = 'ContextMenuEvent';
  readonly cursorPosition: TextPosition;
  readonly screenPoint: PointF;

  constructor(cursorPosition: TextPosition, screenPoint: PointF) {
    this.cursorPosition = cursorPosition;
    this.screenPoint = screenPoint;
  }
}

export class GutterIconClickEvent implements EditorEvent {
  readonly eventName = 'GutterIconClickEvent';
  readonly line: number;
  readonly iconId: number;
  readonly screenPoint: PointF;

  constructor(line: number, iconId: number, screenPoint: PointF) {
    this.line = line;
    this.iconId = iconId;
    this.screenPoint = screenPoint;
  }
}

export class InlayHintClickEvent implements EditorEvent {
  readonly eventName = 'InlayHintClickEvent';
  readonly line: number;
  readonly column: number;
  readonly iconId: number;
  readonly isIcon: boolean;
  readonly isColor: boolean;
  readonly colorValue: number;
  readonly screenPoint: PointF;

  constructor(line: number, column: number, iconId: number, isIcon: boolean,
    screenPoint: PointF);

  constructor(line: number, column: number, colorValue: number,
    screenPoint: PointF);

  constructor(line: number, column: number, idOrColor: number,
    isIconOrPoint: boolean | PointF, screenPoint?: PointF) {
    this.line = line;
    this.column = column;
    if (typeof isIconOrPoint === 'boolean') {
      this.iconId = idOrColor;
      this.isIcon = isIconOrPoint;
      this.isColor = false;
      this.colorValue = 0;
      this.screenPoint = screenPoint!;
    } else {
      this.iconId = 0;
      this.isIcon = false;
      this.isColor = true;
      this.colorValue = idOrColor;
      this.screenPoint = isIconOrPoint;
    }
  }
}

export class FoldToggleEvent implements EditorEvent {
  readonly eventName = 'FoldToggleEvent';
  readonly line: number;
  readonly isGutter: boolean;
  readonly screenPoint: PointF;

  constructor(line: number, isGutter: boolean, screenPoint: PointF) {
    this.line = line;
    this.isGutter = isGutter;
    this.screenPoint = screenPoint;
  }
}

export class DocumentLoadedEvent implements EditorEvent {
  readonly eventName = 'DocumentLoadedEvent';
}

export class SelectionMenuItemClickEvent implements EditorEvent {
  readonly eventName = 'SelectionMenuItemClickEvent';
  readonly itemId: string;
  readonly itemLabel: string;

  constructor(itemId: string, itemLabel: string) {
    this.itemId = itemId;
    this.itemLabel = itemLabel;
  }
}
