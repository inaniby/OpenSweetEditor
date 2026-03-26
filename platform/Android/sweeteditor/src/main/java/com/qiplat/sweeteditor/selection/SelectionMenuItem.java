package com.qiplat.sweeteditor.selection;

import androidx.annotation.NonNull;

/**
 * Data class representing a single item in the selection context menu.
 * <p>
 * Use predefined constants ({@link #ACTION_CUT}, {@link #ACTION_COPY}, etc.)
 * for standard actions, or create custom items with your own IDs.
 */
public class SelectionMenuItem {

    public static final String ACTION_CUT = "cut";
    public static final String ACTION_COPY = "copy";
    public static final String ACTION_PASTE = "paste";
    public static final String ACTION_SELECT_ALL = "select_all";

    /** Unique identifier for this menu item. */
    @NonNull public final String id;
    /** Display label shown on the button. */
    @NonNull public final String label;
    /** Whether this item is enabled (disabled items are grayed out). */
    public final boolean enabled;

    public SelectionMenuItem(@NonNull String id, @NonNull String label, boolean enabled) {
        this.id = id;
        this.label = label;
        this.enabled = enabled;
    }

    public SelectionMenuItem(@NonNull String id, @NonNull String label) {
        this(id, label, true);
    }
}
