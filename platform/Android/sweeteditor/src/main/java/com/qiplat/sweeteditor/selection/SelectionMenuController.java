package com.qiplat.sweeteditor.selection;

import android.graphics.PointF;
import android.os.Handler;
import android.os.Looper;
import android.view.MotionEvent;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.qiplat.sweeteditor.EditorTheme;
import com.qiplat.sweeteditor.SweetEditor;
import com.qiplat.sweeteditor.core.EditorCore;
import com.qiplat.sweeteditor.core.visual.SelectionHandle;
import com.qiplat.sweeteditor.event.EditorEventBus;
import com.qiplat.sweeteditor.event.SelectionMenuItemClickEvent;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * Controls the lifecycle of the selection context menu.
 * <p>
 * State machine:
 * <pre>
 *   HIDDEN --(DOUBLE_TAP + selection)--> VISIBLE
 *   VISIBLE --(handle drag / scroll / scale / tap)--> HIDDEN
 *   HIDDEN --(handle drag end + selection)--> VISIBLE
 * </pre>
 */
public class SelectionMenuController {

    private static final int SHOW_DELAY_MS = 100;
    private static final int MENU_OFFSET_Y_DP = 8;
    // Match Android handle visual geometry (teardrop rotated around tip).
    // Ensures popup placed below selection does not cover handles near top lines.
    private static final int MENU_HANDLE_CLEARANCE_DP = 32;

    private final SweetEditor editor;
    private final EditorEventBus eventBus;
    private final SelectionMenuBar menuBar;
    private final Handler handler = new Handler(Looper.getMainLooper());

    @Nullable private SelectionMenuItemProvider itemProvider;
    @Nullable private SelectionHandle startHandle;
    @Nullable private SelectionHandle endHandle;
    private boolean handleDragActive = false;
    private boolean hiddenByViewportGesture = false;
    private boolean pendingShow = false;

    public SelectionMenuController(@NonNull SweetEditor editor,
                                   @NonNull EditorEventBus eventBus,
                                   @NonNull EditorTheme theme) {
        this.editor = editor;
        this.eventBus = eventBus;
        this.menuBar = new SelectionMenuBar(editor.getContext(),
                theme.selectionMenuBgColor,
                theme.selectionMenuTextColor,
                theme.selectionMenuDividerColor);
        this.menuBar.setOnMenuItemClickListener(this::onItemClicked);
    }

    public void setItemProvider(@Nullable SelectionMenuItemProvider provider) {
        this.itemProvider = provider;
    }

    public void applyTheme(@NonNull EditorTheme theme) {
        menuBar.updateTheme(
                theme.selectionMenuBgColor,
                theme.selectionMenuTextColor,
                theme.selectionMenuDividerColor);
    }

    public void updateSelectionHandles(@Nullable SelectionHandle start, @Nullable SelectionHandle end) {
        this.startHandle = start;
        this.endHandle = end;
    }

    public void clearSelectionHandles() {
        this.startHandle = null;
        this.endHandle = null;
    }

    /**
     * Called from {@code SweetEditor.onTouchEvent} after gesture processing.
     * Drives the show/hide state machine.
     */
    public void onGestureResult(@NonNull EditorCore.GestureResult result, int actionMasked) {
        if (result.isHandleDrag) {
            if (!handleDragActive) {
                handleDragActive = true;
                hideImmediate();
            }
            return;
        }

        if (handleDragActive) {
            handleDragActive = false;
            if (result.hasSelection) {
                scheduleShow();
            }
        }

        switch (result.type) {
            case DOUBLE_TAP:
                hiddenByViewportGesture = false;
                if (result.hasSelection) {
                    scheduleShow();
                }
                break;

            case TAP:
                hiddenByViewportGesture = false;
                hideImmediate();
                break;

            case SCROLL:
            case FAST_SCROLL:
            case SCALE:
                hiddenByViewportGesture = true;
            case DRAG_SELECT:
                hideImmediate();
                break;

            default:
                break;
        }

        // Auto restore after viewport gesture (scroll/scale/fling) fully ends.
        boolean pointerReleased = actionMasked == MotionEvent.ACTION_UP
                || actionMasked == MotionEvent.ACTION_CANCEL
                || actionMasked == MotionEvent.ACTION_POINTER_UP;
        boolean animationTickEnded = actionMasked == -1 && !result.needsAnimation;
        boolean canRestoreNow = (pointerReleased && !result.needsAnimation) || animationTickEnded;
        if (hiddenByViewportGesture && canRestoreNow) {
            hiddenByViewportGesture = false;
            if (result.hasSelection) {
                scheduleShow();
            }
        }
    }

    /** Called when selectAll() is invoked programmatically. */
    public void onSelectAll() {
        scheduleShow();
    }

    /** Called when text content changes (insert/delete). */
    public void onTextChanged() {
        clearSelectionHandles();
        hide();
    }

    /** Dismiss immediately (e.g. on detach). */
    public void dismiss() {
        cancelPendingShow();
        menuBar.dismissImmediate();
    }

    public boolean isShowing() {
        return menuBar.isShowing();
    }

    private void scheduleShow() {
        cancelPendingShow();
        pendingShow = true;
        handler.postDelayed(this::doShow, SHOW_DELAY_MS);
    }

    private void cancelPendingShow() {
        pendingShow = false;
        handler.removeCallbacksAndMessages(null);
    }

    private void hide() {
        cancelPendingShow();
        if (menuBar.isShowing()) {
            menuBar.dismiss();
        }
    }

    private void hideImmediate() {
        cancelPendingShow();
        if (menuBar.isShowing()) {
            menuBar.dismissImmediate();
        }
    }

    private void doShow() {
        if (!pendingShow) return;
        pendingShow = false;

        if (!editor.hasSelection()) return;

        List<SelectionMenuItem> items = buildItems();
        if (items.isEmpty()) return;

        PointF pos = computeMenuPosition();
        if (menuBar.isShowing()) {
            menuBar.dismissImmediate();
        }
        menuBar.showAt(editor, (int) pos.x, (int) pos.y, items);
    }

    private List<SelectionMenuItem> buildItems() {
        if (itemProvider != null) {
            List<SelectionMenuItem> items = itemProvider.provideMenuItems(editor);
            return items != null ? items : Collections.emptyList();
        }
        return buildDefaultMenuItems();
    }

    private List<SelectionMenuItem> buildDefaultMenuItems() {
        boolean hasSelection = editor.hasSelection();
        List<SelectionMenuItem> items = new ArrayList<>(4);
        items.add(new SelectionMenuItem(SelectionMenuItem.ACTION_CUT, "Cut", hasSelection));
        items.add(new SelectionMenuItem(SelectionMenuItem.ACTION_COPY, "Copy", hasSelection));
        items.add(new SelectionMenuItem(SelectionMenuItem.ACTION_PASTE, "Paste"));
        items.add(new SelectionMenuItem(SelectionMenuItem.ACTION_SELECT_ALL, "Select All"));
        return items;
    }

    private void onItemClicked(@NonNull SelectionMenuItem item) {
        switch (item.id) {
            case SelectionMenuItem.ACTION_CUT:
                editor.cutToClipboard();
                hide();
                break;
            case SelectionMenuItem.ACTION_COPY:
                editor.copyToClipboard();
                hide();
                break;
            case SelectionMenuItem.ACTION_PASTE:
                editor.pasteFromClipboard();
                hide();
                break;
            case SelectionMenuItem.ACTION_SELECT_ALL:
                editor.selectAll();
                break;
            default:
                eventBus.publish(new SelectionMenuItemClickEvent(item));
                hide();
                break;
        }
    }

    private PointF computeMenuPosition() {
        int offsetY = dpToPx(MENU_OFFSET_Y_DP);
        int handleClearance = dpToPx(MENU_HANDLE_CLEARANCE_DP);

        float anchorX;
        float topY;
        float bottomY;
        SelectionHandle start = startHandle;
        SelectionHandle end = endHandle;
        if (start != null && start.visible && start.position != null) {
            float startX = start.position.x;
            float startY = start.position.y;
            float startBottom = startY + start.height;

            float endX = startX;
            float endY = startY;
            float endBottom = startBottom;
            if (end != null && end.visible && end.position != null) {
                endX = end.position.x;
                endY = end.position.y;
                endBottom = endY + end.height;
            }

            anchorX = (startX + endX) * 0.5f;
            topY = Math.min(startY, endY);
            bottomY = Math.max(startBottom, endBottom);
        } else {
            anchorX = editor.getWidth() * 0.5f;
            topY = 0f;
            bottomY = 0f;
        }

        int[] loc = new int[2];
        editor.getLocationOnScreen(loc);

        int menuWidth = Math.max(1, menuBar.getPopupWidth());
        int menuHeight = Math.max(1, menuBar.getPopupHeight());

        int minX = loc[0];
        int maxX = minX + Math.max(0, editor.getWidth() - menuWidth);
        int minY = loc[1];
        int maxY = minY + Math.max(0, editor.getHeight() - menuHeight);

        int screenX = loc[0] + Math.round(anchorX) - menuWidth / 2;
        screenX = clamp(screenX, minX, maxX);

        int aboveY = loc[1] + Math.round(topY) - menuHeight - offsetY;
        int belowY = loc[1] + Math.round(bottomY) + offsetY + handleClearance;
        int screenY = aboveY >= minY ? aboveY : belowY;
        screenY = clamp(screenY, minY, maxY);

        return new PointF(screenX, screenY);
    }

    private static int clamp(int value, int min, int max) {
        return Math.max(min, Math.min(max, value));
    }

    private int dpToPx(int dp) {
        return (int) (dp * editor.getContext().getResources().getDisplayMetrics().density + 0.5f);
    }
}
