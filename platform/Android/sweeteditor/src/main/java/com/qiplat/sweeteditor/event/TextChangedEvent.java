package com.qiplat.sweeteditor.event;

import androidx.annotation.NonNull;

import com.qiplat.sweeteditor.core.foundation.TextChange;

import java.util.Collections;
import java.util.List;

/**
 * Text content change event.
 */
public final class TextChangedEvent extends EditorEvent {
    @NonNull public final TextChangeAction action;
    @NonNull public final List<TextChange> changes;

    public TextChangedEvent(@NonNull TextChangeAction action, @NonNull List<TextChange> changes) {
        this.action = action;
        this.changes = Collections.unmodifiableList(changes);
    }
}
