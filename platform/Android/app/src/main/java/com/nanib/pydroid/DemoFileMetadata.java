package com.nanib.pydroid;

import androidx.annotation.NonNull;

import com.qiplat.sweeteditor.EditorMetadata;

public final class DemoFileMetadata implements EditorMetadata {
    @NonNull
    public final String fileName;

    public DemoFileMetadata(@NonNull String fileName) {
        this.fileName = fileName;
    }
}
