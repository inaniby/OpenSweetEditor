package com.qiplat.sweeteditor.core.foundation;

import androidx.annotation.NonNull;

/**
 * Exact text change payload for incremental update flows.
 */
public final class TextChange {
    @NonNull
    public final TextRange range;
    @NonNull
    public final String text;

    public TextChange(@NonNull TextRange range, @NonNull String text) {
        this.range = range;
        this.text = text;
    }

    @NonNull
    @Override
    public String toString() {
        return "TextChange{range=" + range + ", text=" + text + '}';
    }
}
