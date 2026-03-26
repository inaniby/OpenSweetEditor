package com.qiplat.sweeteditor.event;

import androidx.annotation.NonNull;

import com.qiplat.sweeteditor.selection.SelectionMenuItem;

/**
 * Published when a custom (non-builtin) selection menu item is clicked.
 * <p>
 * Builtin actions (cut/copy/paste/select_all) are handled internally
 * and do not fire this event.
 */
public final class SelectionMenuItemClickEvent extends EditorEvent {
    @NonNull public final SelectionMenuItem item;

    public SelectionMenuItemClickEvent(@NonNull SelectionMenuItem item) {
        this.item = item;
    }
}
