package com.qiplat.sweeteditor.copilot;

/**
 * Immutable data class representing an inline suggestion (Copilot).
 */
public final class InlineSuggestion {
    /** Target line number (0-based). */
    public final int line;
    /** Insertion column (0-based, UTF-16 offset). */
    public final int column;
    /** Suggestion text content. */
    public final String text;

    public InlineSuggestion(int line, int column, String text) {
        this.line = line;
        this.column = column;
        this.text = text;
    }
}
