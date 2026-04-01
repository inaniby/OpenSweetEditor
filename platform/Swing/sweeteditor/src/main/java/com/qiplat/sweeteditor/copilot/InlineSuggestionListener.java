package com.qiplat.sweeteditor.copilot;

/**
 * Callback interface for inline suggestion accept/dismiss events.
 */
public interface InlineSuggestionListener {
    /** Called when the user accepts the suggestion (Tab key or Accept button). */
    void onSuggestionAccepted(InlineSuggestion suggestion);
    /** Called when the user dismisses the suggestion (Esc key or Dismiss button). */
    void onSuggestionDismissed(InlineSuggestion suggestion);
}
