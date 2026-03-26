package com.nanib.pydroid;

import android.content.Context;
import android.os.SystemClock;
import android.util.SparseArray;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.qiplat.sweeteditor.core.Document;
import com.qiplat.sweeteditor.core.adornment.FoldRegion;
import com.qiplat.sweeteditor.core.adornment.GutterIcon;
import com.qiplat.sweeteditor.decoration.DecorationContext;
import com.qiplat.sweetline.DocumentHighlight;
import com.qiplat.sweetline.IndentGuideLine;
import com.qiplat.sweetline.IndentGuideResult;
import com.qiplat.sweetline.LineHighlight;
import com.qiplat.sweetline.TokenSpan;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.List;
import java.util.Locale;

final class SweetLineAnalysisLogger {
    private static final String LOG_RELATIVE_PATH = "logs/sweetline_analysis.log";
    private static final long MAX_LOG_BYTES = 2L * 1024L * 1024L;
    private static final long MIN_DUMP_INTERVAL_MS = 1200L;
    private static final int MAX_LOG_LINES = 260;

    private final Context appContext;
    private long lastDumpAtMs;
    @NonNull
    private String lastDumpKey = "";

    SweetLineAnalysisLogger(@NonNull Context appContext) {
        this.appContext = appContext.getApplicationContext();
    }

    void logIfPython(@NonNull String fileName,
                     @NonNull DecorationContext context,
                     @NonNull Document document,
                     @Nullable DocumentHighlight highlight,
                     @Nullable IndentGuideResult guideResult,
                     int guideLineOffset,
                     @NonNull List<FoldRegion> foldRegions,
                     @NonNull SparseArray<List<GutterIcon>> gutterIcons) {
        if (!fileName.toLowerCase(Locale.ROOT).endsWith(".py")) {
            return;
        }

        long now = SystemClock.elapsedRealtime();
        String key = fileName
                + "|" + context.visibleStartLine + ":" + context.visibleEndLine
                + "|changes=" + context.textChanges.size()
                + "|lines=" + context.totalLineCount;
        if (key.equals(lastDumpKey) && now - lastDumpAtMs < MIN_DUMP_INTERVAL_MS) {
            return;
        }
        lastDumpKey = key;
        lastDumpAtMs = now;

        String dump = buildDump(
                fileName,
                context,
                document,
                highlight,
                guideResult,
                guideLineOffset,
                foldRegions,
                gutterIcons
        );
        appendToFile(dump);
    }

    @NonNull
    private String buildDump(@NonNull String fileName,
                             @NonNull DecorationContext context,
                             @NonNull Document document,
                             @Nullable DocumentHighlight highlight,
                             @Nullable IndentGuideResult guideResult,
                             int guideLineOffset,
                             @NonNull List<FoldRegion> foldRegions,
                             @NonNull SparseArray<List<GutterIcon>> gutterIcons) {
        StringBuilder sb = new StringBuilder(8192);
        sb.append("\n=== SweetLine Snapshot ")
                .append(new SimpleDateFormat("yyyy-MM-dd HH:mm:ss.SSS", Locale.US).format(new Date()))
                .append(" ===\n");
        sb.append("file=").append(fileName)
                .append(", editorLines=").append(document.getLineCount())
                .append(", contextTotalLines=").append(context.totalLineCount)
                .append(", visible=").append(context.visibleStartLine).append("-").append(context.visibleEndLine)
                .append(", textChanges=").append(context.textChanges.size())
                .append(", guideOffset=").append(guideLineOffset)
                .append('\n');

        int emitted = 0;
        int docLineCount = document.getLineCount();
        int docLogLineEnd = Math.min(docLineCount - 1, Math.max(context.visibleEndLine + 4, 20));
        sb.append("[document-lines]\n");
        for (int i = 0; i <= docLogLineEnd; i++) {
            sb.append("L").append(i).append(": ").append(safeOneLine(document.getLineText(i))).append('\n');
            emitted++;
            if (emitted > MAX_LOG_LINES) {
                sb.append("...truncated...\n");
                break;
            }
        }

        int highlightLineCount = highlight == null || highlight.lines == null ? 0 : highlight.lines.size();
        sb.append("[highlight]\n")
                .append("lineCount=").append(highlightLineCount).append('\n');
        if (highlight != null && highlight.lines != null && !highlight.lines.isEmpty()) {
            int tokenStart = Math.max(0, context.visibleStartLine - 2);
            int tokenEnd = Math.min(highlight.lines.size() - 1, Math.max(context.visibleEndLine + 8, 30));
            for (int line = tokenStart; line <= tokenEnd; line++) {
                LineHighlight lh = highlight.lines.get(line);
                if (lh == null || lh.spans == null || lh.spans.isEmpty()) {
                    continue;
                }
                for (TokenSpan span : lh.spans) {
                    if (span == null || span.range == null || span.range.start == null || span.range.end == null) {
                        continue;
                    }
                    if (span.range.start.line != span.range.end.line) {
                        continue;
                    }
                    int startLine = span.range.start.line;
                    int startCol = span.range.start.column;
                    int endCol = span.range.end.column;
                    String literal = literal(document, startLine, startCol, endCol);
                    sb.append("token line=").append(startLine)
                            .append(" col=").append(startCol).append("-").append(endCol)
                            .append(" style=").append(span.styleId)
                            .append(" literal=").append('"').append(safeOneLine(literal)).append('"')
                            .append('\n');
                    emitted++;
                    if (emitted > MAX_LOG_LINES) {
                        sb.append("...truncated...\n");
                        break;
                    }
                }
                if (emitted > MAX_LOG_LINES) {
                    break;
                }
            }
        }

        sb.append("[indent-guides]\n");
        if (guideResult == null || guideResult.guideLines == null) {
            sb.append("null\n");
        } else {
            int idx = 0;
            for (IndentGuideLine g : guideResult.guideLines) {
                if (g == null) continue;
                sb.append("#").append(idx++)
                        .append(" raw=").append(g.startLine).append("-").append(g.endLine)
                        .append(" col=").append(g.column)
                        .append(" normalized=").append(g.startLine - guideLineOffset)
                        .append("-").append(g.endLine - guideLineOffset)
                        .append('\n');
                if (idx > 80) {
                    sb.append("...truncated...\n");
                    break;
                }
            }
        }

        sb.append("[fold-regions]\n");
        int foldIndex = 0;
        int suspectShift = 0;
        for (FoldRegion fold : foldRegions) {
            sb.append("#").append(foldIndex++)
                    .append(' ')
                    .append(fold.startLine).append("-").append(fold.endLine)
                    .append('\n');
            if (fold.startLine > 0) {
                String current = safeOneLine(document.getLineText(fold.startLine)).trim();
                String previous = safeOneLine(document.getLineText(fold.startLine - 1)).trim();
                if (!current.endsWith(":") && previous.endsWith(":")) {
                    suspectShift++;
                }
            }
            if (foldIndex > 80) {
                sb.append("...truncated...\n");
                break;
            }
        }
        if (!foldRegions.isEmpty()) {
            sb.append("suspectShiftCount=").append(suspectShift)
                    .append("/").append(foldRegions.size()).append('\n');
        }

        sb.append("[gutter-icons]\n");
        for (int i = 0; i < gutterIcons.size(); i++) {
            int line = gutterIcons.keyAt(i);
            List<GutterIcon> icons = gutterIcons.valueAt(i);
            sb.append("line=").append(line).append(" icons=");
            if (icons == null || icons.isEmpty()) {
                sb.append("[]");
            } else {
                sb.append('[');
                for (int j = 0; j < icons.size(); j++) {
                    if (j > 0) sb.append(',');
                    sb.append(icons.get(j).iconId);
                }
                sb.append(']');
            }
            sb.append('\n');
        }

        sb.append("logPath=")
                .append(new File(appContext.getFilesDir(), LOG_RELATIVE_PATH).getAbsolutePath())
                .append("\n");
        return sb.toString();
    }

    private void appendToFile(@NonNull String content) {
        File outFile = new File(appContext.getFilesDir(), LOG_RELATIVE_PATH);
        File parent = outFile.getParentFile();
        if (parent != null && !parent.exists()) {
            //noinspection ResultOfMethodCallIgnored
            parent.mkdirs();
        }
        if (outFile.exists() && outFile.length() > MAX_LOG_BYTES) {
            //noinspection ResultOfMethodCallIgnored
            outFile.delete();
        }

        try (OutputStreamWriter writer = new OutputStreamWriter(
                new FileOutputStream(outFile, true), StandardCharsets.UTF_8)) {
            writer.write(content);
            writer.flush();
        } catch (IOException ignored) {
        }
    }

    @NonNull
    private static String literal(@NonNull Document document, int line, int start, int end) {
        if (line < 0) return "";
        String text = document.getLineText(line);
        if (text == null || start < 0 || end <= start || end > text.length()) {
            return "";
        }
        return text.substring(start, end);
    }

    @NonNull
    private static String safeOneLine(@Nullable String raw) {
        if (raw == null) {
            return "";
        }
        String normalized = raw
                .replace("\\", "\\\\")
                .replace("\n", "\\n")
                .replace("\r", "\\r")
                .replace("\t", "\\t");
        if (normalized.length() > 220) {
            return normalized.substring(0, 220) + "...";
        }
        return normalized;
    }
}
