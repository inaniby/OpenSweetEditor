import { EditorEvent, EditorEventListener } from './EditorEvent';

/**
 * Generic editor event bus (platform layer event system).
 * Dispatches by event name, each event type has its own subscriber list.
 */
export class EditorEventBus {
  private listeners: Map<string, EditorEventListener<EditorEvent>[]> = new Map();

  subscribe<T extends EditorEvent>(eventName: string, listener: EditorEventListener<T>): void {
    let list = this.listeners.get(eventName);
    if (!list) {
      list = [];
      this.listeners.set(eventName, list);
    }
    if (!list.includes(listener as EditorEventListener<EditorEvent>)) {
      list.push(listener as EditorEventListener<EditorEvent>);
    }
  }

  unsubscribe<T extends EditorEvent>(eventName: string, listener: EditorEventListener<T>): void {
    const list = this.listeners.get(eventName);
    if (!list) {
      return;
    }
    const idx = list.indexOf(listener as EditorEventListener<EditorEvent>);
    if (idx >= 0) {
      list.splice(idx, 1);
    }
  }

  publish<T extends EditorEvent>(event: T): void {
    const list = this.listeners.get(event.eventName);
    if (!list || list.length === 0) {
      return;
    }
    for (const listener of list) {
      listener(event);
    }
  }

  clear(): void {
    this.listeners.clear();
  }
}
