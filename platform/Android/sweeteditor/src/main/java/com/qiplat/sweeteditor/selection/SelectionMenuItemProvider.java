package com.qiplat.sweeteditor.selection;

import androidx.annotation.NonNull;

import com.qiplat.sweeteditor.SweetEditor;

import java.util.List;

/**
 * Provider interface for building selection menu items.
 * <p>
 * Implement this to customize the selection context menu.
 * The provider is called each time the menu is about to show,
 * so items can be dynamic based on editor state.
 */
public interface SelectionMenuItemProvider {
    /**
     * Build the list of menu items to display.
     *
     * @param editor the editor instance
     * @return ordered list of menu items
     */
    @NonNull
    List<SelectionMenuItem> provideMenuItems(@NonNull SweetEditor editor);
}
