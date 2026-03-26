package com.qiplat.sweeteditor.selection;

import android.content.Context;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.animation.AlphaAnimation;
import android.widget.LinearLayout;
import android.widget.PopupWindow;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import java.util.List;

/**
 * Floating popup bar that displays selection menu items.
 */
public class SelectionMenuBar {

    public interface OnMenuItemClickListener {
        void onMenuItemClick(@NonNull SelectionMenuItem item);
    }

    private static final int CORNER_RADIUS_DP = 8;
    private static final int HORIZONTAL_PADDING_DP = 4;
    private static final int BUTTON_HORIZONTAL_PADDING_DP = 12;
    private static final int BAR_HEIGHT_DP = 36;
    private static final int FADE_DURATION_MS = 120;
    private static final int DIVIDER_WIDTH_DP = 1;

    private final Context context;
    private final PopupWindow popupWindow;
    private View contentView;

    private int bgColor;
    private int textColor;
    private int dividerColor;
    private int rippleColor;

    @Nullable private OnMenuItemClickListener listener;
    @Nullable private List<SelectionMenuItem> currentItems;

    public SelectionMenuBar(@NonNull Context context, int bgColor, int textColor, int dividerColor) {
        this.context = context;
        this.bgColor = bgColor;
        this.textColor = textColor;
        this.dividerColor = resolveDividerColor(bgColor, dividerColor);
        this.rippleColor = deriveOverlayColor(bgColor);

        contentView = new View(context);
        popupWindow = new PopupWindow(contentView,
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        popupWindow.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
        popupWindow.setClippingEnabled(true);
        popupWindow.setOutsideTouchable(false);
        popupWindow.setFocusable(false);
    }

    public void setOnMenuItemClickListener(@Nullable OnMenuItemClickListener listener) {
        this.listener = listener;
    }

    public void updateTheme(int bgColor, int textColor, int dividerColor) {
        this.bgColor = bgColor;
        this.textColor = textColor;
        this.dividerColor = resolveDividerColor(bgColor, dividerColor);
        this.rippleColor = deriveOverlayColor(bgColor);
        if (currentItems != null) {
            rebuildContent(currentItems);
        }
    }

    public void showAt(@NonNull View anchor, int x, int y, @NonNull List<SelectionMenuItem> items) {
        currentItems = items;
        rebuildContent(items);
        popupWindow.showAtLocation(anchor, Gravity.NO_GRAVITY, x, y);
        fadeIn();
    }

    public void updatePosition(int x, int y) {
        if (popupWindow.isShowing()) {
            popupWindow.update(x, y, -1, -1);
        }
    }

    public void dismiss() {
        if (popupWindow.isShowing()) {
            fadeOut(() -> {
                if (popupWindow.isShowing()) {
                    popupWindow.dismiss();
                }
            });
        }
    }

    public void dismissImmediate() {
        if (popupWindow.isShowing()) {
            popupWindow.dismiss();
        }
    }

    public boolean isShowing() {
        return popupWindow.isShowing();
    }

    public int getPopupWidth() {
        if (contentView != null) {
            contentView.measure(View.MeasureSpec.UNSPECIFIED, View.MeasureSpec.UNSPECIFIED);
            return contentView.getMeasuredWidth();
        }
        return 0;
    }

    public int getPopupHeight() {
        return dpToPx(BAR_HEIGHT_DP);
    }

    // Internal

    private void rebuildContent(@NonNull List<SelectionMenuItem> items) {
        View newContent = buildContentView(items);
        popupWindow.setContentView(newContent);
        contentView = newContent;
    }

    private View buildContentView(@NonNull List<SelectionMenuItem> items) {
        LinearLayout bar = new LinearLayout(context);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        bar.setPadding(dpToPx(HORIZONTAL_PADDING_DP), 0, dpToPx(HORIZONTAL_PADDING_DP), 0);
        bar.setMinimumHeight(dpToPx(BAR_HEIGHT_DP));

        GradientDrawable bg = new GradientDrawable();
        bg.setColor(bgColor);
        bg.setCornerRadius(dpToPx(CORNER_RADIUS_DP));
        bar.setBackground(bg);
        bar.setElevation(dpToPx(4));

        for (int i = 0; i < items.size(); i++) {
            SelectionMenuItem item = items.get(i);
            if (i > 0) {
                bar.addView(createDivider());
            }
            bar.addView(createButton(item));
        }
        return bar;
    }

    private TextView createButton(@NonNull SelectionMenuItem item) {
        TextView btn = new TextView(context);
        btn.setText(item.label);
        btn.setTextSize(12);
        btn.setGravity(Gravity.CENTER);
        btn.setPadding(dpToPx(BUTTON_HORIZONTAL_PADDING_DP), 0, dpToPx(BUTTON_HORIZONTAL_PADDING_DP), 0);
        btn.setMinHeight(dpToPx(BAR_HEIGHT_DP));

        if (item.enabled) {
            btn.setTextColor(textColor);
            GradientDrawable mask = new GradientDrawable();
            mask.setColor(Color.WHITE);
            mask.setCornerRadius(dpToPx(4));
            btn.setBackground(new RippleDrawable(
                    ColorStateList.valueOf(rippleColor), null, mask));
            btn.setOnClickListener(v -> {
                if (listener != null) {
                    listener.onMenuItemClick(item);
                }
            });
        } else {
            btn.setTextColor(Color.argb(80,
                    Color.red(textColor), Color.green(textColor), Color.blue(textColor)));
            btn.setClickable(false);
        }
        return btn;
    }

    private View createDivider() {
        View divider = new View(context);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                dpToPx(DIVIDER_WIDTH_DP), dpToPx(BAR_HEIGHT_DP - 12));
        lp.gravity = Gravity.CENTER_VERTICAL;
        divider.setLayoutParams(lp);
        divider.setBackgroundColor(dividerColor);
        return divider;
    }

    private void fadeIn() {
        AlphaAnimation anim = new AlphaAnimation(0f, 1f);
        anim.setDuration(FADE_DURATION_MS);
        contentView.startAnimation(anim);
    }

    private void fadeOut(Runnable onEnd) {
        AlphaAnimation anim = new AlphaAnimation(1f, 0f);
        anim.setDuration(FADE_DURATION_MS);
        anim.setFillAfter(true);
        anim.setAnimationListener(new android.view.animation.Animation.AnimationListener() {
            @Override public void onAnimationStart(android.view.animation.Animation a) {}
            @Override public void onAnimationRepeat(android.view.animation.Animation a) {}
            @Override public void onAnimationEnd(android.view.animation.Animation a) {
                contentView.post(onEnd);
            }
        });
        contentView.startAnimation(anim);
    }

    private static int deriveOverlayColor(int base) {
        float lum = Color.luminance(base);
        return lum > 0.5f ? Color.argb(30, 0, 0, 0) : Color.argb(40, 255, 255, 255);
    }

    private static int resolveDividerColor(int bgColor, int dividerColor) {
        return dividerColor != 0 ? dividerColor : deriveOverlayColor(bgColor);
    }

    private int dpToPx(int dp) {
        return (int) (dp * context.getResources().getDisplayMetrics().density + 0.5f);
    }
}
