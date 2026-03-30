using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

	namespace SweetEditor {
	public class EditorRenderer : IDisposable {
		// Desktop demos on other platforms use point-based font sizes.
		// Avalonia uses DIP units, so convert pt -> DIP for visual parity.
		private const float PointsToDip = 96f / 72f;
		private const float DefaultTextFontSize = 11f * PointsToDip;
		private const float DefaultInlayHintFontSize = 9.5f * PointsToDip;
		private const string BaseTextFontFamily = "Consolas";
		private const string BaseInlayHintFontFamily = "Segoe UI";
		private float textFontSize = DefaultTextFontSize;
		private float inlayHintFontSize = DefaultInlayHintFontSize;

		private EditorTheme currentTheme;
		private Typeface regularTypeface;
		private Typeface boldTypeface;
		private Typeface italicTypeface;
		private Typeface boldItalicTypeface;
		private Typeface inlayHintTypeface;
		private Typeface inlayHintBoldTypeface;
		private Typeface inlayHintItalicTypeface;
		private Typeface inlayHintBoldItalicTypeface;
		private EditorIconProvider? editorIconProvider;
		private int currentDrawingLineNumber = -1;

		private readonly Dictionary<int, ISolidColorBrush> brushCache = new Dictionary<int, ISolidColorBrush>();

		public EditorRenderer(EditorTheme theme) {
			currentTheme = theme;
			regularTypeface = new Typeface(BaseTextFontFamily, FontStyle.Normal, FontWeight.Regular);
			boldTypeface = new Typeface(BaseTextFontFamily, FontStyle.Normal, FontWeight.Bold);
			italicTypeface = new Typeface(BaseTextFontFamily, FontStyle.Italic, FontWeight.Regular);
			boldItalicTypeface = new Typeface(BaseTextFontFamily, FontStyle.Italic, FontWeight.Bold);
			inlayHintTypeface = new Typeface(BaseInlayHintFontFamily, FontStyle.Normal, FontWeight.Regular);
			inlayHintBoldTypeface = new Typeface(BaseInlayHintFontFamily, FontStyle.Normal, FontWeight.Bold);
			inlayHintItalicTypeface = new Typeface(BaseInlayHintFontFamily, FontStyle.Italic, FontWeight.Regular);
			inlayHintBoldItalicTypeface = new Typeface(BaseInlayHintFontFamily, FontStyle.Italic, FontWeight.Bold);
		}

		public EditorTheme Theme => currentTheme;

		public EditorCore.TextMeasurer GetTextMeasurer() {
			return new EditorCore.TextMeasurer {
				MeasureTextWidth = OnMeasureText,
				MeasureInlayHintWidth = OnMeasureInlayHintText,
				MeasureIconWidth = OnMeasureIconWidth,
				GetFontMetrics = OnGetFontMetrics
			};
		}

		public void SetEditorIconProvider(EditorIconProvider? provider) {
			editorIconProvider = provider;
		}

		public EditorIconProvider? GetEditorIconProvider() => editorIconProvider;

		public void ApplyTheme(EditorTheme theme) {
			currentTheme = theme;
		}

		public void SyncPlatformScale(float scale) {
			if (scale <= 0f) return;
			textFontSize = Math.Max(1f, DefaultTextFontSize * scale);
			inlayHintFontSize = Math.Max(1f, DefaultInlayHintFontSize * scale);
			regularTypeface = new Typeface(BaseTextFontFamily, FontStyle.Normal, FontWeight.Regular);
			boldTypeface = new Typeface(BaseTextFontFamily, FontStyle.Normal, FontWeight.Bold);
			italicTypeface = new Typeface(BaseTextFontFamily, FontStyle.Italic, FontWeight.Regular);
			boldItalicTypeface = new Typeface(BaseTextFontFamily, FontStyle.Italic, FontWeight.Bold);
			inlayHintTypeface = new Typeface(BaseInlayHintFontFamily, FontStyle.Normal, FontWeight.Regular);
			inlayHintBoldTypeface = new Typeface(BaseInlayHintFontFamily, FontStyle.Normal, FontWeight.Bold);
			inlayHintItalicTypeface = new Typeface(BaseInlayHintFontFamily, FontStyle.Italic, FontWeight.Regular);
			inlayHintBoldItalicTypeface = new Typeface(BaseInlayHintFontFamily, FontStyle.Italic, FontWeight.Bold);
		}

		private Typeface GetTypefaceByStyle(int fontStyle) {
			bool isBold = (fontStyle & EditorControl.FONT_STYLE_BOLD) != 0;
			bool isItalic = (fontStyle & EditorControl.FONT_STYLE_ITALIC) != 0;
			if (isBold && isItalic) return boldItalicTypeface;
			if (isBold) return boldTypeface;
			if (isItalic) return italicTypeface;
			return regularTypeface;
		}

		private Typeface GetInlayHintTypefaceByStyle(int fontStyle) {
			bool isBold = (fontStyle & EditorControl.FONT_STYLE_BOLD) != 0;
			bool isItalic = (fontStyle & EditorControl.FONT_STYLE_ITALIC) != 0;
			if (isBold && isItalic) return inlayHintBoldItalicTypeface;
			if (isBold) return inlayHintBoldTypeface;
			if (isItalic) return inlayHintItalicTypeface;
			return inlayHintTypeface;
		}

		private ISolidColorBrush GetOrCreateBrush(int argb) {
			if (!brushCache.TryGetValue(argb, out var b)) {
				b = new SolidColorBrush(Color.FromUInt32((uint)argb));
				brushCache[argb] = b;
			}
			return b;
		}

		private static int WithAlpha(int argb, byte alpha) {
			return (argb & 0x00FFFFFF) | (alpha << 24);
		}

		private FormattedText CreateFormattedText(string text, Typeface typeface, float fontSize, ISolidColorBrush brush) {
			return new FormattedText(
				text,
				CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight,
				typeface,
				fontSize,
				brush);
		}

		private static float MeasureFormattedTextWidth(FormattedText formattedText) {
			return (float)Math.Max(
				formattedText.Width,
				formattedText.WidthIncludingTrailingWhitespace);
		}

		private static bool ContainsOnlyWhitespace(string text) {
			for (int i = 0; i < text.Length; i++) {
				if (!char.IsWhiteSpace(text[i])) {
					return false;
				}
			}
			return text.Length > 0;
		}

		private float MeasureWhitespaceFallback(string text, Typeface typeface, float fontSize) {
			try {
				var nbspProbe = CreateFormattedText("\u00A0", typeface, fontSize, GetOrCreateBrush(unchecked((int)0xFF000000)));
				float unit = MeasureFormattedTextWidth(nbspProbe);
				if (unit > 0f) {
					return unit * text.Length;
				}
			} catch {
				// Ignore and use numeric fallback below.
			}
			return Math.Max(1f, text.Length * fontSize * 0.5f);
		}

		private float GetFontAscent(Typeface typeface, float fontSize) {
			try {
				var probe = CreateFormattedText("M", typeface, fontSize, GetOrCreateBrush(unchecked((int)0xFF000000)));
				return (float)probe.Baseline;
			} catch {
				return fontSize * 0.8f;
			}
		}

		#region TextMeasurer Callbacks

		private float OnMeasureText(string text, int fontStyle) {
			if (string.IsNullOrEmpty(text)) return 0f;
			try {
				var typeface = GetTypefaceByStyle(fontStyle);
				var formattedText = CreateFormattedText(text, typeface, textFontSize, GetOrCreateBrush(unchecked((int)0xFF000000)));
				float width = MeasureFormattedTextWidth(formattedText);
				if (width <= 0f && ContainsOnlyWhitespace(text)) {
					return MeasureWhitespaceFallback(text, typeface, textFontSize);
				}
				return width;
			} catch {
				return text.Length * textFontSize * 0.6f;
			}
		}

		private float OnMeasureInlayHintText(string text) {
			string value = string.IsNullOrEmpty(text) ? "M" : text;
			try {
				var formattedText = CreateFormattedText(value, inlayHintTypeface, inlayHintFontSize, GetOrCreateBrush(unchecked((int)0xFF000000)));
				float width = MeasureFormattedTextWidth(formattedText);
				if (width <= 0f && ContainsOnlyWhitespace(value)) {
					return MeasureWhitespaceFallback(value, inlayHintTypeface, inlayHintFontSize);
				}
				return width;
			} catch {
				return value.Length * inlayHintFontSize * 0.6f;
			}
		}

		private float OnMeasureIconWidth(int iconId) {
			try {
				var formattedText = CreateFormattedText("M", regularTypeface, textFontSize, GetOrCreateBrush(unchecked((int)0xFF000000)));
				return MeasureFormattedTextWidth(formattedText);
			} catch {
				return textFontSize;
			}
		}

		private void OnGetFontMetrics(IntPtr arrPtr, UIntPtr length) {
			if (arrPtr == IntPtr.Zero || length.ToUInt64() < 2) {
				return;
			}
			try {
				var formattedText = CreateFormattedText("M", regularTypeface, textFontSize, GetOrCreateBrush(unchecked((int)0xFF000000)));
				float ascent = (float)formattedText.Baseline;
				float[] metrics = {
					-ascent,
					(float)(formattedText.Height - ascent)
				};
				System.Runtime.InteropServices.Marshal.Copy(metrics, 0, arrPtr, 2);
			} catch {
				float[] fallbackMetrics = {
					-textFontSize * 0.8f,
					textFontSize * 0.2f
				};
				System.Runtime.InteropServices.Marshal.Copy(fallbackMetrics, 0, arrPtr, 2);
			}
		}

		#endregion

		#region Rendering

		public void Render(DrawingContext context, EditorRenderModel? model, EditorTheme theme, Size size) {
			if (context == null) return;
			
			var backgroundBrush = GetOrCreateBrush((int)theme.BackgroundColor);
			context.FillRectangle(backgroundBrush, new Rect(0, 0, size.Width, size.Height));

			if (!model.HasValue) {
				return;
			}
			EditorRenderModel modelValue = model.Value;

			DrawCurrentLineDecoration(context, modelValue, 0f, size.Width);
			DrawSelectionRects(context, modelValue);
			DrawLines(context, modelValue);
			DrawGuideSegments(context, modelValue);
			if (modelValue.CompositionDecoration.Active) {
				DrawCompositionDecoration(context, modelValue.CompositionDecoration);
			}
			DrawDiagnosticDecorations(context, modelValue);
			DrawLinkedEditingRects(context, modelValue);
			DrawBracketHighlightRects(context, modelValue);
			DrawCursor(context, modelValue);
			DrawGutterOverlay(context, modelValue, size.Height);
			DrawLineNumbers(context, modelValue);
			DrawScrollbars(context, modelValue);
		}

		private void DrawLines(DrawingContext context, EditorRenderModel model) {
			List<VisualLine> lines = model.VisualLines;
			if (lines == null) return;
			foreach (var line in lines) {
				if (line.Runs == null) continue;
				foreach (var run in line.Runs) {
					DrawVisualRun(context, run);
				}
			}
		}

		private void DrawGutterOverlay(DrawingContext context, EditorRenderModel model, double clientHeight) {
			if (model.SplitX <= 0) return;
			var backgroundBrush = GetOrCreateBrush((int)currentTheme.BackgroundColor);
			context.FillRectangle(backgroundBrush, new Rect(0, 0, model.SplitX, clientHeight));
			DrawCurrentLineDecoration(context, model, 0f, model.SplitX);
			if (model.SplitLineVisible) {
				DrawLineSplit(context, model.SplitX, clientHeight);
			}
		}

		private void DrawLineNumbers(DrawingContext context, EditorRenderModel model) {
			if (!model.GutterVisible) return;
			List<VisualLine> lines = model.VisualLines;
			if (lines == null) return;
			List<GutterIconRenderItem>? gutterIcons = model.GutterIcons;
			List<FoldMarkerRenderItem>? foldMarkers = model.FoldMarkers;
			int iconCount = gutterIcons?.Count ?? 0;
			int markerCount = foldMarkers?.Count ?? 0;
			int iconCursor = 0;
			int markerCursor = 0;
			int activeLogicalLine = GetActiveLogicalLine(model);
			var activeLineColor = GetCurrentLineAccentColor();
			currentDrawingLineNumber = -1;
			foreach (var line in lines) {
				if (line.WrapIndex != 0 || line.IsPhantomLine) continue;
				int logicalLine = line.LogicalLine;

				while (iconCursor < iconCount && gutterIcons![iconCursor].LogicalLine < logicalLine) {
					iconCursor++;
				}
				int iconStart = iconCursor;
				while (iconCursor < iconCount && gutterIcons![iconCursor].LogicalLine == logicalLine) {
					iconCursor++;
				}
				int iconEnd = iconCursor;

				while (markerCursor < markerCount && foldMarkers![markerCursor].LogicalLine < logicalLine) {
					markerCursor++;
				}
				bool hasMarker = false;
				FoldMarkerRenderItem foldMarker = default;
				while (markerCursor < markerCount && foldMarkers![markerCursor].LogicalLine == logicalLine) {
					if (!hasMarker) {
						foldMarker = foldMarkers[markerCursor];
						hasMarker = true;
					}
					markerCursor++;
				}

				DrawLineNumber(
					context,
					line,
					model,
					gutterIcons,
					iconStart,
					iconEnd,
					hasMarker,
					foldMarker,
					logicalLine == activeLogicalLine,
					activeLineColor);
			}
		}

		private void DrawScrollbars(DrawingContext context, EditorRenderModel model) {
			ScrollbarModel vertical = model.VerticalScrollbar;
			ScrollbarModel horizontal = model.HorizontalScrollbar;
			bool hasVertical = vertical.Visible && vertical.Track.Width > 0 && vertical.Track.Height > 0;
			bool hasHorizontal = horizontal.Visible && horizontal.Track.Width > 0 && horizontal.Track.Height > 0;
			if (!hasVertical && !hasHorizontal) return;

			var trackBrush = GetOrCreateBrush((int)currentTheme.ScrollbarTrackColor);
			Rect verticalTrackRect = new Rect(0, 0, 0, 0);
			Rect horizontalTrackRect = new Rect(0, 0, 0, 0);

			if (hasVertical) {
				var vThumbColor = vertical.ThumbActive ? currentTheme.ScrollbarThumbActiveColor : currentTheme.ScrollbarThumbColor;
				var vThumbBrush = GetOrCreateBrush((int)vThumbColor);
				verticalTrackRect = new Rect(
					vertical.Track.Origin.X, vertical.Track.Origin.Y,
					vertical.Track.Width, vertical.Track.Height);
				Rect verticalThumbRect = new Rect(
					vertical.Thumb.Origin.X, vertical.Thumb.Origin.Y,
					vertical.Thumb.Width, vertical.Thumb.Height);
				context.FillRectangle(trackBrush, verticalTrackRect);
				context.FillRectangle(vThumbBrush, verticalThumbRect);
			}

			if (hasHorizontal) {
				var hThumbColor = horizontal.ThumbActive ? currentTheme.ScrollbarThumbActiveColor : currentTheme.ScrollbarThumbColor;
				var hThumbBrush = GetOrCreateBrush((int)hThumbColor);
				horizontalTrackRect = new Rect(
					horizontal.Track.Origin.X, horizontal.Track.Origin.Y,
					horizontal.Track.Width, horizontal.Track.Height);
				Rect horizontalThumbRect = new Rect(
					horizontal.Thumb.Origin.X, horizontal.Thumb.Origin.Y,
					horizontal.Thumb.Width, horizontal.Thumb.Height);
				context.FillRectangle(trackBrush, horizontalTrackRect);
				context.FillRectangle(hThumbBrush, horizontalThumbRect);
			}

			if (hasVertical && hasHorizontal) {
				var corner = new Rect(
					verticalTrackRect.X, horizontalTrackRect.Y,
					verticalTrackRect.Width, horizontalTrackRect.Height);
				context.FillRectangle(trackBrush, corner);
			}
		}

		private void DrawLineNumber(DrawingContext context, VisualLine visualLine, EditorRenderModel model,
			List<GutterIconRenderItem>? gutterIcons,
			int iconStart, int iconEnd,
			bool hasFoldMarker, FoldMarkerRenderItem foldMarker,
			bool isCurrentLine, uint activeLineColor) {
			PointF position = visualLine.LineNumberPosition;
			bool overlayMode = model.MaxGutterIcons == 0;
			bool hasIcons = editorIconProvider != null && iconEnd > iconStart;
			int newLineNumber = visualLine.LogicalLine + 1;
			if (overlayMode && hasIcons) {
				DrawOverlayGutterIcon(context, gutterIcons![iconStart]);
				currentDrawingLineNumber = newLineNumber;
			} else if (newLineNumber != currentDrawingLineNumber) {
				var textColor = isCurrentLine ? activeLineColor : currentTheme.LineNumberColor;
				var textBrush = GetOrCreateBrush((int)textColor);
				var formattedText = CreateFormattedText(newLineNumber.ToString(), regularTypeface, textFontSize, textBrush);
				float topY = position.Y - GetFontAscent(regularTypeface, textFontSize);
				context.DrawText(formattedText, new Point(position.X, topY));
				currentDrawingLineNumber = newLineNumber;
			}

			if (!overlayMode && hasIcons) {
				for (int i = iconStart; i < iconEnd; i++) {
					DrawGutterIcon(context, gutterIcons![i]);
				}
			}

			if (hasFoldMarker) {
				DrawFoldMarker(context, foldMarker, isCurrentLine ? activeLineColor : currentTheme.LineNumberColor);
			}
		}

		private void DrawLineSplit(DrawingContext context, float x, double clientHeight) {
			var pen = new Pen(GetOrCreateBrush((int)currentTheme.SplitLineColor), 1f);
			context.DrawLine(pen, new Point(x, 0), new Point(x, clientHeight));
		}

		private void DrawOverlayGutterIcon(DrawingContext context, GutterIconRenderItem item) {
			DrawGutterIcon(context, item);
		}

		private bool DrawGutterIcon(DrawingContext context, GutterIconRenderItem item) {
			if (item.Width <= 0 || item.Height <= 0) return false;
			int iconId = item.IconId;
			var image = editorIconProvider?.GetIconImage(iconId);
			if (image == null) return false;
			context.DrawImage(image, new Rect(item.Origin.X, item.Origin.Y, item.Width, item.Height));
			return true;
		}

		private void DrawFoldMarker(DrawingContext context, FoldMarkerRenderItem marker, uint color) {
			if (marker.Width <= 0 || marker.Height <= 0) return;
			if (marker.FoldState == FoldState.NONE) return;

			float centerX = marker.Origin.X + marker.Width * 0.5f;
			float centerY = marker.Origin.Y + marker.Height * 0.5f;
			float halfSize = Math.Min(marker.Width, marker.Height) * 0.28f;

			var pen = new Pen(GetOrCreateBrush((int)color), Math.Max(1f, marker.Height * 0.1f));
			var geometry = new StreamGeometry();

			using (var geometryContext = geometry.Open()) {
				if (marker.FoldState == FoldState.FOLDED) {
					geometryContext.BeginFigure(new Point(centerX - halfSize * 0.5f, centerY - halfSize), true);
					geometryContext.LineTo(new Point(centerX + halfSize * 0.5f, centerY));
					geometryContext.LineTo(new Point(centerX - halfSize * 0.5f, centerY + halfSize));
				} else {
					geometryContext.BeginFigure(new Point(centerX - halfSize, centerY - halfSize * 0.5f), true);
					geometryContext.LineTo(new Point(centerX, centerY + halfSize * 0.5f));
					geometryContext.LineTo(new Point(centerX + halfSize, centerY - halfSize * 0.5f));
				}
				geometryContext.EndFigure(true);
			}

			context.DrawGeometry(null, pen, geometry);
		}

		private void DrawVisualRun(DrawingContext context, VisualRun visualRun) {
			string? text = visualRun.Text;
			string drawTextContent = text ?? string.Empty;
			bool hasText = !string.IsNullOrEmpty(text);
			if (!hasText &&
				visualRun.Type != VisualRunType.INLAY_HINT &&
				visualRun.Type != VisualRunType.FOLD_PLACEHOLDER) {
				return;
			}
			var typeface = (visualRun.Type == VisualRunType.INLAY_HINT)
				? GetInlayHintTypefaceByStyle(visualRun.Style.FontStyle)
				: GetTypefaceByStyle(visualRun.Style.FontStyle);
			float fontSize = visualRun.Type == VisualRunType.INLAY_HINT ? inlayHintFontSize : textFontSize;
			uint fallbackColor = visualRun.Type == VisualRunType.INLAY_HINT
				? currentTheme.InlayHintTextColor
				: currentTheme.ForegroundColor;
			uint color = (visualRun.Style.Color != 0)
				? (uint)visualRun.Style.Color
				: fallbackColor;

			float ascent = GetFontAscent(typeface, fontSize);
			double topY = visualRun.Y - ascent;
			double lineHeight = Math.Max(1.0, fontSize);
			FormattedText formattedText;
			try {
				formattedText = CreateFormattedText(drawTextContent, typeface, fontSize, GetOrCreateBrush((int)color));
				lineHeight = Math.Max(lineHeight, formattedText.Height);
			} catch {
				return;
			}

			double drawWidth = Math.Max(visualRun.Width, MeasureFormattedTextWidth(formattedText));
			if (drawWidth < 1) drawWidth = 1;

			if (visualRun.Type == VisualRunType.FOLD_PLACEHOLDER) {
				double mgn = visualRun.Margin;
				double bgLeft = visualRun.X + mgn;
				double bgTop = topY;
				double bgWidth = Math.Max(1, visualRun.Width - mgn * 2);
				double bgHeight = lineHeight;
				double radius = bgHeight * 0.2;
				context.DrawRectangle(
					GetOrCreateBrush((int)currentTheme.FoldPlaceholderBgColor),
					null,
					new RoundedRect(new Rect(bgLeft, bgTop, bgWidth, bgHeight), (float)radius));
				if (hasText) {
					double textX = visualRun.X + mgn + visualRun.Padding;
					var foldPlaceholderText = CreateFormattedText(
						drawTextContent,
						typeface,
						fontSize,
						GetOrCreateBrush((int)currentTheme.FoldPlaceholderTextColor));
					context.DrawText(foldPlaceholderText, new Point(textX, topY));
				}
				return;
			}

			if (visualRun.Type == VisualRunType.INLAY_HINT) {
				double mgn = visualRun.Margin;
				double bgLeft = visualRun.X + mgn;
				double bgTop = topY;
				double bgWidth = Math.Max(1, visualRun.Width - mgn * 2);
				double bgHeight = lineHeight;

				if (visualRun.ColorValue != 0) {
					double blockSize = Math.Min(bgWidth, bgHeight);
					context.FillRectangle(
						GetOrCreateBrush(visualRun.ColorValue),
						new Rect(bgLeft, bgTop, blockSize, blockSize));
					return;
				}

				context.DrawRectangle(
					GetOrCreateBrush((int)currentTheme.InlayHintBgColor),
					null,
					new RoundedRect(new Rect(bgLeft, bgTop, bgWidth, bgHeight), (float)(bgHeight * 0.2)));

				if (visualRun.IconId > 0 && editorIconProvider != null) {
					double iconSize = Math.Min(bgWidth, bgHeight);
					double iconLeft = bgLeft + (bgWidth - iconSize) * 0.5;
					double iconTop = bgTop + (bgHeight - iconSize) * 0.5;
					DrawGutterIcon(context, new GutterIconRenderItem {
						LogicalLine = -1,
						IconId = visualRun.IconId,
						Origin = new PointF((float)iconLeft, (float)iconTop),
						Width = (float)iconSize,
						Height = (float)iconSize
					});
				} else if (hasText) {
					double textX = visualRun.X + mgn + visualRun.Padding;
					context.DrawText(formattedText, new Point(textX, topY));
				}
				return;
			}

			if (visualRun.Style.BackgroundColor != 0) {
				var bgBrush = GetOrCreateBrush(visualRun.Style.BackgroundColor);
				context.FillRectangle(bgBrush, new Rect(visualRun.X, topY, drawWidth, lineHeight));
			}

			if (visualRun.Type == VisualRunType.PHANTOM_TEXT) {
				var phantomText = CreateFormattedText(
					drawTextContent,
					typeface,
					fontSize,
					GetOrCreateBrush((int)currentTheme.PhantomTextColor));
				context.DrawText(phantomText, new Point(visualRun.X, topY));
			} else {
				context.DrawText(formattedText, new Point(visualRun.X, topY));
			}

			if ((visualRun.Style.FontStyle & EditorControl.FONT_STYLE_STRIKETHROUGH) != 0) {
				var strikePen = new Pen(GetOrCreateBrush((int)color), 1f);
				double strikeY = topY + ascent * 0.5f;
				context.DrawLine(strikePen, new Point(visualRun.X, strikeY), new Point(visualRun.X + visualRun.Width, strikeY));
			}
		}

		private void DrawCurrentLineDecoration(DrawingContext context, EditorRenderModel model, float left, double width) {
			if (width <= 0f) return;
			double lineH = model.Cursor.Height > 0 ? model.Cursor.Height : textFontSize;
			if (model.CurrentLineRenderMode == CurrentLineRenderMode.NONE) return;
			if (model.CurrentLineRenderMode == CurrentLineRenderMode.BORDER) {
				var pen = new Pen(GetOrCreateBrush((int)GetCurrentLineBorderColor()), 1f);
				context.DrawRectangle(null, pen, new Rect(left, model.CurrentLine.Y, width, lineH));
				return;
			}
			var brush = GetOrCreateBrush((int)currentTheme.CurrentLineColor);
			context.FillRectangle(brush, new Rect(left, model.CurrentLine.Y, width, lineH));
		}

		private int GetActiveLogicalLine(EditorRenderModel model) => model.Cursor.TextPosition.Line;

		private uint GetCurrentLineAccentColor() {
			uint argb = currentTheme.CurrentLineNumberColor;
			if (argb == 0) argb = currentTheme.LineNumberColor;
			return argb | 0xFF000000u;
		}

		private uint GetCurrentLineBorderColor() {
			uint argb = currentTheme.CurrentLineColor;
			if (argb == 0) argb = currentTheme.LineNumberColor;
			uint alpha = (argb >> 24) & 0xFF;
			if (alpha < 0xA0) {
				argb = (argb & 0x00FFFFFF) | 0xA0000000;
			}
			return argb;
		}

		private void DrawSelectionRects(DrawingContext context, EditorRenderModel model) {
			if (model.SelectionRects == null || model.SelectionRects.Count == 0) return;
			var brush = GetOrCreateBrush((int)currentTheme.SelectionBackgroundColor);
			foreach (var rect in model.SelectionRects) {
				context.FillRectangle(brush, new Rect(rect.Origin.X, rect.Origin.Y, rect.Width, rect.Height));
			}
		}

		private void DrawCursor(DrawingContext context, EditorRenderModel model) {
			if (!model.Cursor.Visible) return;
			var brush = GetOrCreateBrush((int)currentTheme.CursorColor);
			context.FillRectangle(brush, new Rect(model.Cursor.Position.X, model.Cursor.Position.Y, 2f, model.Cursor.Height));
		}

		private void DrawCompositionDecoration(DrawingContext context, CompositionDecoration comp) {
			double y = comp.Origin.Y + comp.Height;
			var pen = new Pen(GetOrCreateBrush((int)currentTheme.CompositionColor), 2f);
			context.DrawLine(pen, new Point(comp.Origin.X, y), new Point(comp.Origin.X + comp.Width, y));
		}

		private void DrawDiagnosticDecorations(DrawingContext context, EditorRenderModel model) {
			if (model.DiagnosticDecorations == null || model.DiagnosticDecorations.Count == 0) return;
			foreach (var diag in model.DiagnosticDecorations) {
				uint color = diag.Color != 0
					? (uint)diag.Color
					: diag.Severity switch {
						0 => currentTheme.DiagnosticErrorColor,
						1 => currentTheme.DiagnosticWarningColor,
						2 => currentTheme.DiagnosticInfoColor,
						_ => currentTheme.DiagnosticHintColor,
					};

				double startX = diag.Origin.X;
				double endX = startX + diag.Width;
				double baseY = diag.Origin.Y + diag.Height - 1f;
				var pen = new Pen(GetOrCreateBrush((int)color), 2.0f);

				if (diag.Severity == 3) {
					pen.DashStyle = DashStyle.Dash;
					context.DrawLine(pen, new Point(startX, baseY), new Point(endX, baseY));
				} else {
					const double halfWave = 7.0;
					const double amplitude = 3.5;
					var path = new StreamGeometry();
					using (var pathContext = path.Open()) {
						pathContext.BeginFigure(new Point(startX, baseY), false);
						double x = startX;
						int step = 0;
						while (x < endX) {
							double nextX = Math.Min(x + halfWave, endX);
							double midX = (x + nextX) * 0.5;
							double peakY = (step % 2 == 0) ? (baseY - amplitude) : (baseY + amplitude);
							pathContext.QuadraticBezierTo(new Point(midX, peakY), new Point(nextX, baseY));
							x = nextX;
							step++;
						}
					}
					context.DrawGeometry(null, pen, path);
				}
			}
		}

		private void DrawLinkedEditingRects(DrawingContext context, EditorRenderModel model) {
			if (model.LinkedEditingRects == null || model.LinkedEditingRects.Count == 0) return;
			foreach (var rect in model.LinkedEditingRects) {
				if (rect.IsActive) {
					uint activeFillColor = (currentTheme.LinkedEditingActiveColor & 0x00FFFFFFu) | 0x33000000u;
					var fillBrush = GetOrCreateBrush((int)activeFillColor);
					var pen = new Pen(GetOrCreateBrush((int)currentTheme.LinkedEditingActiveColor), 2f);
					context.FillRectangle(fillBrush, new Rect(rect.Origin.X, rect.Origin.Y, rect.Width, rect.Height));
					context.DrawRectangle(null, pen, new Rect(rect.Origin.X, rect.Origin.Y, rect.Width, rect.Height));
				} else {
					var pen = new Pen(GetOrCreateBrush((int)currentTheme.LinkedEditingInactiveColor), 1f);
					context.DrawRectangle(null, pen, new Rect(rect.Origin.X, rect.Origin.Y, rect.Width, rect.Height));
				}
			}
		}

		private void DrawBracketHighlightRects(DrawingContext context, EditorRenderModel model) {
			if (model.BracketHighlightRects == null || model.BracketHighlightRects.Count == 0) return;
			foreach (var rect in model.BracketHighlightRects) {
				var fillBrush = GetOrCreateBrush((int)currentTheme.BracketHighlightBgColor);
				var pen = new Pen(GetOrCreateBrush((int)currentTheme.BracketHighlightBorderColor), 1.5f);
				context.FillRectangle(fillBrush, new Rect(rect.Origin.X, rect.Origin.Y, rect.Width, rect.Height));
				context.DrawRectangle(null, pen, new Rect(rect.Origin.X, rect.Origin.Y, rect.Width, rect.Height));
			}
		}

		private void DrawGuideSegments(DrawingContext context, EditorRenderModel model) {
			if (model.GuideSegments == null || model.GuideSegments.Count == 0) return;
			foreach (var seg in model.GuideSegments) {
				uint color = seg.Type switch {
					GuideType.SEPARATOR => currentTheme.SeparatorColor,
					_ => currentTheme.GuideColor,
				};
				float lineWidth = seg.Type == GuideType.INDENT ? 1f : 1.2f;
				var pen = new Pen(GetOrCreateBrush((int)color), lineWidth);

				if (seg.ArrowEnd) {
					float arrowLen = (seg.Type == GuideType.FLOW ? 9f : 8f);
					float arrowAngle = (float)(Math.PI * 28.0 / 180.0);
					float arrowDepth = (float)(arrowLen * Math.Cos(arrowAngle));
					double dx = seg.End.X - seg.Start.X;
					double dy = seg.End.Y - seg.Start.Y;
					double len = Math.Sqrt(dx * dx + dy * dy);
					double trim = arrowDepth + lineWidth * 0.5;
					if (len > trim) {
						double ratio = (len - trim) / len;
						double lineEndX = seg.Start.X + dx * ratio;
						double lineEndY = seg.Start.Y + dy * ratio;
						context.DrawLine(pen, new Point(seg.Start.X, seg.Start.Y), new Point(lineEndX, lineEndY));
					}
					DrawArrowHead(context, color, seg.Start, seg.End, arrowLen, arrowAngle);
				} else {
					context.DrawLine(pen, new Point(seg.Start.X, seg.Start.Y), new Point(seg.End.X, seg.End.Y));
				}
			}
		}

		private void DrawArrowHead(DrawingContext context, uint color, PointF from, PointF to, float arrowLen, float arrowAngle) {
			double dx = to.X - from.X;
			double dy = to.Y - from.Y;
			double len = Math.Sqrt(dx * dx + dy * dy);
			if (len < 1f) return;
			double ux = dx / len;
			double uy = dy / len;
			double cosA = Math.Cos(arrowAngle);
			double sinA = Math.Sin(arrowAngle);
			double ax1 = to.X - arrowLen * (ux * cosA - uy * sinA);
			double ay1 = to.Y - arrowLen * (uy * cosA + ux * sinA);
			double ax2 = to.X - arrowLen * (ux * cosA + uy * sinA);
			double ay2 = to.Y - arrowLen * (uy * cosA - ux * sinA);

			var brush = GetOrCreateBrush((int)color);
			var geometry = new StreamGeometry();
			using (var ctx = geometry.Open()) {
				ctx.BeginFigure(new Point(to.X, to.Y), true);
				ctx.LineTo(new Point(ax1, ay1));
				ctx.LineTo(new Point(ax2, ay2));
				ctx.EndFigure(true);
			}
			var geometryBrush = GetOrCreateBrush((int)color);
			context.DrawGeometry(geometryBrush, null, geometry);
		}

		#endregion

		public void Dispose() {
			brushCache.Clear();
		}
	}
}
