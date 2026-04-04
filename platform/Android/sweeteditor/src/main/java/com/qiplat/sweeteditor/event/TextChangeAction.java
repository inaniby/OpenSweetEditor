package com.qiplat.sweeteditor.event;

/**
 * Coarse-grained semantic classification for a text change cycle.
 */
public enum TextChangeAction {
    INSERT,
    DELETE,
    UNDO,
    REDO,
    KEY,
    COMPOSITION
}
