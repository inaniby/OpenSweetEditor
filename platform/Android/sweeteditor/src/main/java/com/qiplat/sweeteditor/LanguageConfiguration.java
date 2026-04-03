package com.qiplat.sweeteditor;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * Language configuration describing metadata such as brackets, comments, and indentation for a specific programming language.
 * When set to EditorCore, brackets will be automatically synced to Core layer's setBracketPairs.
 */
public class LanguageConfiguration {

    public static final int DEFAULT_TAB_SIZE = 4;

    /** Bracket pair */
    public static class BracketPair {
        @NonNull public final String open;
        @NonNull public final String close;

        public BracketPair(@NonNull String open, @NonNull String close) {
            this.open = open;
            this.close = close;
        }
    }

    @NonNull private final String languageId;
    @Nullable private final List<BracketPair> brackets;
    @Nullable private final List<BracketPair> autoClosingPairs;
    private final int tabSize;
    private final boolean insertSpaces;

    private LanguageConfiguration(Builder builder) {
        this.languageId = builder.languageId;
        this.brackets = builder.brackets == null ? null : Collections.unmodifiableList(new ArrayList<>(builder.brackets));
        this.autoClosingPairs = builder.autoClosingPairs == null ? null : Collections.unmodifiableList(new ArrayList<>(builder.autoClosingPairs));
        this.tabSize = builder.tabSize;
        this.insertSpaces = builder.insertSpaces;
    }

    @NonNull public String getLanguageId() { return languageId; }
    @Nullable public List<BracketPair> getBrackets() { return brackets; }
    @Nullable public List<BracketPair> getAutoClosingPairs() { return autoClosingPairs; }
    public int getTabSize() { return tabSize; }
    public boolean getInsertSpaces() { return insertSpaces; }

    public static class Builder {
        @NonNull private final String languageId;
        private List<BracketPair> brackets;
        private List<BracketPair> autoClosingPairs;
        private int tabSize = DEFAULT_TAB_SIZE;
        private boolean insertSpaces;

        public Builder(@NonNull String languageId) {
            this.languageId = languageId;
        }

        public Builder addBracket(@NonNull String open, @NonNull String close) {
            if (brackets == null) brackets = new ArrayList<>();
            brackets.add(new BracketPair(open, close));
            return this;
        }

        public Builder addAutoClosingPair(@NonNull String open, @NonNull String close) {
            if (autoClosingPairs == null) autoClosingPairs = new ArrayList<>();
            autoClosingPairs.add(new BracketPair(open, close));
            return this;
        }

        public Builder setTabSize(int tabSize) {
            this.tabSize = tabSize;
            return this;
        }

        public Builder setInsertSpaces(boolean insertSpaces) {
            this.insertSpaces = insertSpaces;
            return this;
        }

        public LanguageConfiguration build() {
            return new LanguageConfiguration(this);
        }
    }
}
