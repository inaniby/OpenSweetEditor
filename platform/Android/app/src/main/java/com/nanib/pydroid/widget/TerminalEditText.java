package com.nanib.pydroid.widget;

import android.content.Context;
import android.text.Editable;
import android.util.AttributeSet;
import android.view.KeyEvent;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.widget.AppCompatEditText;

public class TerminalEditText extends AppCompatEditText {
    private int mInputStart = 0;
    private boolean mAdjustingSelection;

    public TerminalEditText(@NonNull Context context) {
        super(context);
    }

    public TerminalEditText(@NonNull Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
    }

    public TerminalEditText(@NonNull Context context,
                            @Nullable AttributeSet attrs,
                            int defStyleAttr) {
        super(context, attrs, defStyleAttr);
    }

    public void setInputStart(int inputStart) {
        Editable editable = getText();
        int max = editable == null ? 0 : editable.length();
        mInputStart = Math.max(0, Math.min(inputStart, max));
        ensureEditableCursor();
    }

    public int getInputStart() {
        return mInputStart;
    }

    public void ensureEditableCursor() {
        Editable editable = getText();
        if (editable == null) {
            return;
        }
        int len = editable.length();
        int target = Math.max(mInputStart, Math.min(getSelectionEnd(), len));
        if (target != getSelectionStart() || target != getSelectionEnd()) {
            setSelection(target);
        }
    }

    @Override
    protected void onSelectionChanged(int selStart, int selEnd) {
        if (mAdjustingSelection) {
            super.onSelectionChanged(selStart, selEnd);
            return;
        }

        Editable editable = getText();
        int len = editable == null ? 0 : editable.length();

        int fixedStart = Math.min(Math.max(0, selStart), len);
        int fixedEnd = Math.min(Math.max(0, selEnd), len);

        // Keep cursor in editable tail, but allow ranged selection for copy.
        if (fixedStart == fixedEnd) {
            int cursor = Math.max(mInputStart, fixedEnd);
            fixedStart = cursor;
            fixedEnd = cursor;
        }

        if (fixedStart != selStart || fixedEnd != selEnd) {
            mAdjustingSelection = true;
            setSelection(fixedStart, fixedEnd);
            mAdjustingSelection = false;
            return;
        }

        super.onSelectionChanged(selStart, selEnd);
    }

    @Override
    public boolean onKeyDown(int keyCode, @NonNull KeyEvent event) {
        if (keyCode == KeyEvent.KEYCODE_DEL || keyCode == KeyEvent.KEYCODE_FORWARD_DEL) {
            int selStart = getSelectionStart();
            int selEnd = getSelectionEnd();
            int start = Math.min(selStart, selEnd);
            int end = Math.max(selStart, selEnd);

            if (end <= mInputStart) {
                return true;
            }

            if (start < mInputStart) {
                setSelection(Math.max(mInputStart, end));
                return true;
            }
        }
        return super.onKeyDown(keyCode, event);
    }

    @Override
    public boolean onTextContextMenuItem(int id) {
        if (id == android.R.id.cut) {
            return false;
        }
        if (id == android.R.id.paste || id == android.R.id.pasteAsPlainText) {
            ensureEditableCursor();
        }
        return super.onTextContextMenuItem(id);
    }
}
