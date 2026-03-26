package com.qiplat.sweeteditor.core;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;

import com.qiplat.sweeteditor.EditorTheme;
import com.qiplat.sweeteditor.core.adornment.TextStyle;

import org.junit.Test;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;

public class ProtocolEncoderBatchTextStylesTest {

    @Test
    public void packBatchTextStyles_encodesThemeStylesInStyleIdOrder() {
        EditorTheme theme = new EditorTheme()
                .defineTextStyle(101, new TextStyle(0xFFAA5500, 0x11000000, TextStyle.BOLD | TextStyle.ITALIC))
                .defineTextStyle(7, new TextStyle(0xFF336699, TextStyle.STRIKETHROUGH));

        ByteBuffer payload = ProtocolEncoder.packBatchTextStyles(theme.textStyles);

        assertNotNull(payload);
        assertEquals(ByteOrder.LITTLE_ENDIAN, payload.order());
        assertEquals(2, payload.getInt());

        assertEquals(7, payload.getInt());
        assertEquals(0xFF336699, payload.getInt());
        assertEquals(0, payload.getInt());
        assertEquals(TextStyle.STRIKETHROUGH, payload.getInt());

        assertEquals(101, payload.getInt());
        assertEquals(0xFFAA5500, payload.getInt());
        assertEquals(0x11000000, payload.getInt());
        assertEquals(TextStyle.BOLD | TextStyle.ITALIC, payload.getInt());
        assertEquals(0, payload.remaining());
    }
}
