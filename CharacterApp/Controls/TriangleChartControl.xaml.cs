// Controls/TriangleChartControl.xaml.cs
// Трёхосевая диаграмма-паутина для треугольника развития Тело-Разум-Дух
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CharacterApp.Controls
{
    public class TriangleChartControl : System.Windows.Controls.Canvas
    {
        // ── Dependency Properties ────────────────────────────────────────────

        public static readonly DependencyProperty BodyValueProperty =
            DependencyProperty.Register(nameof(BodyValue), typeof(int), typeof(TriangleChartControl),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MindValueProperty =
            DependencyProperty.Register(nameof(MindValue), typeof(int), typeof(TriangleChartControl),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SpiritValueProperty =
            DependencyProperty.Register(nameof(SpiritValue), typeof(int), typeof(TriangleChartControl),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(int), typeof(TriangleChartControl),
                new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.AffectsRender));

        public int BodyValue
        {
            get => (int)GetValue(BodyValueProperty);
            set => SetValue(BodyValueProperty, value);
        }
        public int MindValue
        {
            get => (int)GetValue(MindValueProperty);
            set => SetValue(MindValueProperty, value);
        }
        public int SpiritValue
        {
            get => (int)GetValue(SpiritValueProperty);
            set => SetValue(SpiritValueProperty, value);
        }
        public int MaxValue
        {
            get => (int)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        // ── Labels ──────────────────────────────────────────────────────────
        private static readonly string[] Labels = { "ТЕЛО", "РАЗУМ", "ДУХ" };

        // ── Render ──────────────────────────────────────────────────────────
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth, h = ActualHeight;
            if (w < 20 || h < 20) return;

            double cx = w / 2;
            double cy = h / 2;
            double r  = Math.Min(cx, cy) - 32;
            int    n  = 3;
            int    rings = 4;
            int    max = Math.Max(1, MaxValue);

            // Brushes
            var accentBrush = TryGetBrush("AccentBrush",    Color.FromRgb(181, 101, 193));
            var dimBrush    = TryGetBrush("BorderMedBrush", Color.FromRgb(44,  46,  74));
            var fillBrush   = TryGetBrush("AccentDimBrush", Color.FromArgb(50, 181, 101, 193));
            var textBrush   = TryGetBrush("TextMutedBrush", Color.FromRgb(120, 128, 176));

            var gridPen  = new Pen(dimBrush,   1.0);
            var axisPen  = new Pen(dimBrush,   0.8) { DashStyle = DashStyles.Dash };
            var valuePen = new Pen(accentBrush, 2.2);

            // Угол первой оси: сверху (Тело - 90°), затем по часовой
            // Тело=вверх, Разум=низ-право, Дух=низ-лево
            double[] angles = new double[n];
            for (int i = 0; i < n; i++)
                angles[i] = Math.PI / 2 - 2 * Math.PI * i / n; // против часовой от верха

            // ── Grid rings ───────────────────────────────────────────────────
            for (int ring = 1; ring <= rings; ring++)
            {
                double rr = r * ring / (double)rings;
                var path = MakeTrianglePath(cx, cy, rr, angles);
                dc.DrawGeometry(null, gridPen, path);
            }

            // ── Axes + labels ─────────────────────────────────────────────────
            int[] vals = { BodyValue, MindValue, SpiritValue };
            for (int i = 0; i < n; i++)
            {
                double ex = cx + r * Math.Cos(angles[i]);
                double ey = cy - r * Math.Sin(angles[i]);
                dc.DrawLine(axisPen, new Point(cx, cy), new Point(ex, ey));

                // Label
                double lx = cx + (r + 20) * Math.Cos(angles[i]);
                double ly = cy - (r + 20) * Math.Sin(angles[i]);
                var ft = MakeText(Labels[i], 12, textBrush, FontWeights.SemiBold);
                dc.DrawText(ft, new Point(lx - ft.Width / 2, ly - ft.Height / 2));
            }

            // ── Value polygon ─────────────────────────────────────────────────
            var valPath = new PathGeometry();
            var valFig  = new PathFigure { IsClosed = true };
            for (int i = 0; i < n; i++)
            {
                double pct = Math.Clamp(vals[i], 0, max) / (double)max;
                double px  = cx + r * pct * Math.Cos(angles[i]);
                double py  = cy - r * pct * Math.Sin(angles[i]);
                if (i == 0) valFig.StartPoint = new Point(px, py);
                else valFig.Segments.Add(new LineSegment(new Point(px, py), true));
            }
            valPath.Figures.Add(valFig);
            dc.DrawGeometry(fillBrush, valuePen, valPath);

            // ── Vertex dots + value numbers ───────────────────────────────────
            for (int i = 0; i < n; i++)
            {
                double pct = Math.Clamp(vals[i], 0, max) / (double)max;
                double px  = cx + r * pct * Math.Cos(angles[i]);
                double py  = cy - r * pct * Math.Sin(angles[i]);
                dc.DrawEllipse(accentBrush, null, new Point(px, py), 5, 5);

                var numFt = MakeText(vals[i].ToString(), 10, accentBrush, FontWeights.Bold);
                // Offset label away from center
                double ox = Math.Cos(angles[i]) * 10;
                double oy = -Math.Sin(angles[i]) * 10;
                dc.DrawText(numFt, new Point(px + ox - numFt.Width / 2, py + oy - numFt.Height / 2));
            }

            // ── Ring percentage labels (right side) ───────────────────────────
            for (int ring = 1; ring <= rings; ring++)
            {
                double rr  = r * ring / (double)rings;
                int    pct = ring * 100 / rings;
                var pctFt  = MakeText($"{pct}%", 9, textBrush, FontWeights.Normal);
                dc.DrawText(pctFt, new Point(cx + 4, cy - rr - pctFt.Height / 2));
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static PathGeometry MakeTrianglePath(double cx, double cy, double r, double[] angles)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { IsClosed = true };
            for (int i = 0; i < angles.Length; i++)
            {
                double px = cx + r * Math.Cos(angles[i]);
                double py = cy - r * Math.Sin(angles[i]);
                if (i == 0) fig.StartPoint = new Point(px, py);
                else fig.Segments.Add(new LineSegment(new Point(px, py), true));
            }
            geo.Figures.Add(fig);
            return geo;
        }

        private FormattedText MakeText(string text, double size, Brush brush, FontWeight weight)
            => new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
                size, brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

        private Brush TryGetBrush(string key, Color fallback)
        {
            try { return (Brush)Application.Current.FindResource(key); }
            catch { return new SolidColorBrush(fallback); }
        }
    }
}
