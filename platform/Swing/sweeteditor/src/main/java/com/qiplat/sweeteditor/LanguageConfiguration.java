package com.qiplat.sweeteditor;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * Language configuration, describing brackets, comments, indentation and other meta-information for a specific programming language.
 * When set to EditorCore, brackets will be automatically synced to the Core layer's setBracketPairs.
 */
public class LanguageConfiguration {

    public static final int DEFAULT_TAB_SIZE = 4;

    /** Bracket pairs */
    public static class BracketPair {
        public final String open;
        public final String close;

        public BracketPair(String open, String close) {
            this.open = open;
            this.close = close;
        }
    }

    /** Block comment */
    public static class BlockComment {
        public final String open;
        public final String close;

        public BlockComment(String open, String close) {
            this.open = open;
            this.close = close;
        }
    }

    private final String languageId;
    private final List<BracketPair> brackets;
    private final List<BracketPair> autoClosingPairs;
    private final String lineComment;
    private final BlockComment blockComment;
    private final Integer tabSize;
    private final Boolean insertSpaces;

    private LanguageConfiguration(Builder builder) {
        this.languageId = builder.languageId;
        this.brackets = builder.brackets == null ? null : Collections.unmodifiableList(new ArrayList<>(builder.brackets));
        this.autoClosingPairs = builder.autoClosingPairs == null ? null : Collections.unmodifiableList(new ArrayList<>(builder.autoClosingPairs));
        this.lineComment = builder.lineComment;
        this.blockComment = builder.blockComment;
        this.tabSize = builder.tabSize;
        this.insertSpaces = builder.insertSpaces;
    }

    public String getLanguageId() { return languageId; }
    public List<BracketPair> getBrackets() { return brackets; }
    public List<BracketPair> getAutoClosingPairs() { return autoClosingPairs; }
    public String getLineComment() { return lineComment; }
    public BlockComment getBlockComment() { return blockComment; }
    public Integer getTabSize() { return tabSize; }
    public Boolean getInsertSpaces() { return insertSpaces; }

    public static class Builder {
        private final String languageId;
        private List<BracketPair> brackets;
        private List<BracketPair> autoClosingPairs;
        private String lineComment;
        private BlockComment blockComment;
        private Integer tabSize = DEFAULT_TAB_SIZE;
        private Boolean insertSpaces;

        public Builder(String languageId) {
            this.languageId = languageId;
        }

        public Builder addBracket(String open, String close) {
            if (brackets == null) brackets = new ArrayList<>();
            brackets.add(new BracketPair(open, close));
            return this;
        }

        public Builder addAutoClosingPair(String open, String close) {
            if (autoClosingPairs == null) autoClosingPairs = new ArrayList<>();
            autoClosingPairs.add(new BracketPair(open, close));
            return this;
        }

        public Builder setLineComment(String lineComment) {
            this.lineComment = lineComment;
            return this;
        }

        public Builder setBlockComment(BlockComment blockComment) {
            this.blockComment = blockComment;
            return this;
        }

        public Builder setTabSize(Integer tabSize) {
            this.tabSize = tabSize;
            return this;
        }

        public Builder setInsertSpaces(Boolean insertSpaces) {
            this.insertSpaces = insertSpaces;
            return this;
        }

        public LanguageConfiguration build() {
            return new LanguageConfiguration(this);
        }
    }
}
