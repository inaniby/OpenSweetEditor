package com.qiplat.sweeteditor;

import com.qiplat.sweeteditor.core.foundation.AutoIndentMode;
import com.qiplat.sweeteditor.core.foundation.FoldArrowMode;
import com.qiplat.sweeteditor.core.foundation.WrapMode;

/**
 * Centralized configuration for {@link SweetEditor}.
 * <p>
 * Obtain via {@link SweetEditor#getSettings()}. All setters take effect immediately.
 */
public class EditorSettings {

    private final SweetEditor mEditor;

    private float mScale = 1.0f;
    private FoldArrowMode mFoldArrowMode = FoldArrowMode.ALWAYS;
    private WrapMode mWrapMode = WrapMode.NONE;
    private float mLineSpacingAdd = 0f;
    private float mLineSpacingMult = 1.0f;
    private AutoIndentMode mAutoIndentMode = AutoIndentMode.NONE;
    private boolean mReadOnly = false;
    private int mMaxGutterIcons = 0;

    EditorSettings(SweetEditor editor) {
        mEditor = editor;
    }

    public void setScale(float scale) {
        mScale = scale;
        mEditor.getEditorCore().setScale(scale);
        mEditor.syncPlatformScale(scale);
        mEditor.flush();
    }

    public float getScale() {
        return mScale;
    }

    public void setFoldArrowMode(FoldArrowMode mode) {
        mFoldArrowMode = mode;
        mEditor.getEditorCore().setFoldArrowMode(mode.value);
    }

    public FoldArrowMode getFoldArrowMode() {
        return mFoldArrowMode;
    }

    public void setWrapMode(WrapMode mode) {
        mWrapMode = mode;
        mEditor.getEditorCore().setWrapMode(mode.value);
        mEditor.flush();
    }

    public WrapMode getWrapMode() {
        return mWrapMode;
    }

    public void setLineSpacing(float add, float mult) {
        mLineSpacingAdd = add;
        mLineSpacingMult = mult;
        mEditor.getEditorCore().setLineSpacing(add, mult);
        mEditor.flush();
    }

    public float getLineSpacingAdd() {
        return mLineSpacingAdd;
    }

    public float getLineSpacingMult() {
        return mLineSpacingMult;
    }

    public void setAutoIndentMode(AutoIndentMode mode) {
        mAutoIndentMode = mode;
        mEditor.getEditorCore().setAutoIndentMode(mode.value);
    }

    public AutoIndentMode getAutoIndentMode() {
        return mAutoIndentMode;
    }

    public void setReadOnly(boolean readOnly) {
        mReadOnly = readOnly;
        mEditor.getEditorCore().setReadOnly(readOnly);
    }

    public boolean isReadOnly() {
        return mReadOnly;
    }

    public void setMaxGutterIcons(int count) {
        mMaxGutterIcons = count;
        mEditor.getEditorCore().setMaxGutterIcons(count);
    }

    public int getMaxGutterIcons() {
        return mMaxGutterIcons;
    }
}
