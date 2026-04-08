package com.qiplat.sweeteditor.core.foundation;

import androidx.annotation.NonNull;

/**
 * Exact text change payload for incremental update flows.
 */
public final class TextChange {
    @NonNull
    public final TextRange range;
    @NonNull
    public final String newText;

    public TextChange(@NonNull TextRange range, @NonNull String newText) {
        this.range = range;
        this.newText = newText;
    }

    @NonNull
    @Override
    public String toString() {
        return "TextChange{range=" + range + ", newText=" + newText + '}';
    }
}
