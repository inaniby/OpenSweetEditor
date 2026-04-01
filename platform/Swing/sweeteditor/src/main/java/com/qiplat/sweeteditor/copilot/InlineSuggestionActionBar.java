package com.qiplat.sweeteditor.copilot;

import com.qiplat.sweeteditor.EditorTheme;

import javax.swing.*;
import java.awt.*;

/**
 * Floating action bar (JWindow) for Accept/Dismiss inline suggestion actions.
 */
class InlineSuggestionActionBar {

    private static final int BAR_HEIGHT = 28;
    private static final int PADDING_H = 8;
    private static final int GAP = 12;

    private final JComponent anchor;
    private JWindow window;
    private JButton acceptButton;
    private JButton dismissButton;
    private Runnable onAccept;
    private Runnable onDismiss;
    private EditorTheme theme;

    InlineSuggestionActionBar(JComponent anchor, EditorTheme theme,
                              Runnable onAccept, Runnable onDismiss) {
        this.anchor = anchor;
        this.theme = theme;
        this.onAccept = onAccept;
        this.onDismiss = onDismiss;
        buildUI();
    }

    private void buildUI() {
        Window owner = SwingUtilities.getWindowAncestor(anchor);
        window = new JWindow(owner);
        window.setFocusableWindowState(false);

        JPanel panel = new JPanel(new FlowLayout(FlowLayout.CENTER, GAP, 2));
        panel.setBackground(theme.completionBgColor);
        panel.setBorder(BorderFactory.createLineBorder(theme.completionBorderColor));

        acceptButton = createButton("Accept", true);
        acceptButton.addActionListener(e -> { if (onAccept != null) onAccept.run(); });
        dismissButton = createButton("Dismiss", false);
        dismissButton.addActionListener(e -> { if (onDismiss != null) onDismiss.run(); });

        panel.add(acceptButton);
        panel.add(dismissButton);
        window.setContentPane(panel);
        window.pack();
    }

    private JButton createButton(String text, boolean bold) {
        JButton btn = new JButton(text);
        btn.setFocusable(false);
        btn.setBorderPainted(false);
        btn.setContentAreaFilled(false);
        btn.setFont(btn.getFont().deriveFont(bold ? Font.BOLD : Font.PLAIN, 12f));
        btn.setForeground(bold ? theme.completionLabelColor : theme.completionDetailColor);
        btn.setCursor(Cursor.getPredefinedCursor(Cursor.HAND_CURSOR));
        return btn;
    }

    void showAtCursor() {
        if (!anchor.isShowing()) return;
        window.setVisible(true);
    }

    void updatePosition(float cursorX, float cursorY, float cursorHeight) {
        if (!anchor.isShowing()) return;
        Point screenLoc = anchor.getLocationOnScreen();
        int x = screenLoc.x + (int) cursorX;
        int y = screenLoc.y + (int) cursorY + (int) cursorHeight + 2;
        window.setLocation(x, y);
        if (!window.isVisible()) {
            window.setVisible(true);
        }
    }

    void dismiss() {
        if (window != null) {
            window.setVisible(false);
        }
    }

    boolean isShowing() {
        return window != null && window.isVisible();
    }

    void applyTheme(EditorTheme theme) {
        this.theme = theme;
        if (window != null) {
            Container content = window.getContentPane();
            content.setBackground(theme.completionBgColor);
            if (content instanceof JPanel panel) {
                panel.setBorder(BorderFactory.createLineBorder(theme.completionBorderColor));
            }
            if (acceptButton != null) acceptButton.setForeground(theme.completionLabelColor);
            if (dismissButton != null) dismissButton.setForeground(theme.completionDetailColor);
        }
    }
}
