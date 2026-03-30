using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Demo {
	public sealed class PerfGraphControl : Control {
		private readonly List<double> fpsSeries = new();
		private readonly List<double> renderSeries = new();
		private readonly List<double> cpuSeries = new();

		private static readonly ISolidColorBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#3A0E141C"));
		private static readonly ISolidColorBrush BorderBrush = new SolidColorBrush(Color.Parse("#7A4F657A"));
		private static readonly ISolidColorBrush GridBrush = new SolidColorBrush(Color.Parse("#3F6E8498"));
		private static readonly Pen BorderPen = new Pen(BorderBrush, 1);
		private static readonly Pen GridPen = new Pen(GridBrush, 1);
		private static readonly Pen FpsPen = new Pen(new SolidColorBrush(Color.Parse("#D45EEB8F")), 1.5);
		private static readonly Pen RenderPen = new Pen(new SolidColorBrush(Color.Parse("#D45BCBFF")), 1.5);
		private static readonly Pen CpuPen = new Pen(new SolidColorBrush(Color.Parse("#D4FFB75B")), 1.5);

		public void UpdateSeries(IReadOnlyList<double> fps, IReadOnlyList<double> renderMs, IReadOnlyList<double> cpuPercent) {
			fpsSeries.Clear();
			renderSeries.Clear();
			cpuSeries.Clear();
			if (fps != null) {
				fpsSeries.AddRange(fps);
			}
			if (renderMs != null) {
				renderSeries.AddRange(renderMs);
			}
			if (cpuPercent != null) {
				cpuSeries.AddRange(cpuPercent);
			}
			InvalidateVisual();
		}

		public override void Render(DrawingContext context) {
			base.Render(context);

			Rect bounds = new Rect(Bounds.Size);
			if (bounds.Width <= 1 || bounds.Height <= 1) {
				return;
			}

			context.FillRectangle(BackgroundBrush, bounds);
			context.DrawRectangle(null, BorderPen, bounds);
			DrawGrid(context, bounds);

			if (fpsSeries.Count >= 2) {
				double fpsMax = Math.Max(60, fpsSeries.Max());
				DrawSeries(context, bounds, fpsSeries, fpsMax, FpsPen);
			}

			if (renderSeries.Count >= 2) {
				double renderMax = Math.Max(16, renderSeries.Max());
				DrawSeries(context, bounds, renderSeries, renderMax, RenderPen);
			}

			if (cpuSeries.Count >= 2) {
				DrawSeries(context, bounds, cpuSeries, 100, CpuPen);
			}
		}

		private static void DrawGrid(DrawingContext context, Rect bounds) {
			double width = bounds.Width - 1;
			double height = bounds.Height - 1;
			double x0 = bounds.X + 0.5;
			double y0 = bounds.Y + 0.5;

			for (int i = 1; i <= 3; i++) {
				double y = y0 + height * i / 4.0;
				context.DrawLine(GridPen, new Point(x0, y), new Point(x0 + width, y));
			}
		}

		private static void DrawSeries(DrawingContext context, Rect bounds, IReadOnlyList<double> values, double scaleMax, Pen pen) {
			if (values.Count < 2 || scaleMax <= 0) {
				return;
			}

			double left = bounds.X + 1;
			double top = bounds.Y + 1;
			double width = Math.Max(1, bounds.Width - 2);
			double height = Math.Max(1, bounds.Height - 2);
			int count = values.Count;
			double stepX = count > 1 ? width / (count - 1) : width;

			var geometry = new StreamGeometry();
			using (var gc = geometry.Open()) {
				for (int i = 0; i < count; i++) {
					double ratio = Math.Clamp(values[i] / scaleMax, 0, 1);
					double x = left + stepX * i;
					double y = top + (1 - ratio) * height;
					if (i == 0) {
						gc.BeginFigure(new Point(x, y), false);
					} else {
						gc.LineTo(new Point(x, y));
					}
				}
			}
			context.DrawGeometry(null, pen, geometry);
		}
	}
}
