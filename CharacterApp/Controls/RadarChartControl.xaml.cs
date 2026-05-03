// Controls/RadarChartControl.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CharacterApp.Controls
{
    public class RadarChartControl : Canvas
    {
        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(nameof(Values), typeof(int[]), typeof(RadarChartControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public int[]? Values
        {
            get => (int[]?)GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        private static readonly string[] Labels = { "СИЛ", "ЛОВ", "ВЫН", "ИНТ", "МДР", "ХАР" };

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth, h = ActualHeight;
            if (w < 10 || h < 10) return;

            double cx = w / 2, cy = h / 2;
            double r  = Math.Min(cx, cy) - 28;
            int    n  = 6;

            // Try to get theme brushes; fallback to hardcoded
            var accentBrush = TryGetBrush("AccentBrush",     Color.FromRgb(181, 101, 193));
            var dimBrush    = TryGetBrush("BorderMedBrush",  Color.FromRgb(44,  46,  74));
            var fillBrush   = TryGetBrush("AccentDimBrush",  Color.FromArgb(40, 181, 101, 193));
            var textBrush   = TryGetBrush("TextMutedBrush",  Color.FromRgb(120, 128, 176));

            var gridPen  = new Pen(dimBrush,   1.0);
            var axisPen  = new Pen(dimBrush,   0.7) { DashStyle = DashStyles.Dash };
            var valuePen = new Pen(accentBrush, 2.0);

            // Draw grid rings (20, 40, 60, 80, 100 % of max 30)
            for (int ring = 1; ring <= 5; ring++)
            {
                double rr = r * ring / 5.0;
                var poly = new PathGeometry();
                var fig  = new PathFigure { IsClosed = true };
                for (int i = 0; i < n; i++)
                {
                    double angle = Math.PI / 2 + 2 * Math.PI * i / n;
                    double px = cx + rr * Math.Cos(angle);
                    double py = cy - rr * Math.Sin(angle);
                    if (i == 0) fig.StartPoint = new Point(px, py);
                    else fig.Segments.Add(new LineSegment(new Point(px, py), true));
                }
                poly.Figures.Add(fig);
                dc.DrawGeometry(null, gridPen, poly);
            }

            // Draw axes + labels
            for (int i = 0; i < n; i++)
            {
                double angle = Math.PI / 2 + 2 * Math.PI * i / n;
                double ex = cx + r * Math.Cos(angle);
                double ey = cy - r * Math.Sin(angle);
                dc.DrawLine(axisPen, new Point(cx, cy), new Point(ex, ey));

                // Labels slightly beyond tip
                double lx = cx + (r + 16) * Math.Cos(angle);
                double ly = cy - (r + 16) * Math.Sin(angle);
                var ft = new FormattedText(Labels[i],
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11, textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(lx - ft.Width / 2, ly - ft.Height / 2));
            }

            // Draw value polygon
            var vals = Values ?? new int[6];
            while (vals.Length < 6) vals = [.. vals, 10];

            var valPoly = new PathGeometry();
            var valFig  = new PathFigure { IsClosed = true };
            for (int i = 0; i < n; i++)
            {
                double angle = Math.PI / 2 + 2 * Math.PI * i / n;
                double pct   = Math.Clamp(vals[i], 0, 30) / 30.0;
                double px    = cx + r * pct * Math.Cos(angle);
                double py    = cy - r * pct * Math.Sin(angle);
                if (i == 0) valFig.StartPoint = new Point(px, py);
                else valFig.Segments.Add(new LineSegment(new Point(px, py), true));
            }
            valPoly.Figures.Add(valFig);
            dc.DrawGeometry(fillBrush, valuePen, valPoly);

            // Dots at vertices
            for (int i = 0; i < n; i++)
            {
                double angle = Math.PI / 2 + 2 * Math.PI * i / n;
                double pct   = Math.Clamp(vals[i], 0, 30) / 30.0;
                double px    = cx + r * pct * Math.Cos(angle);
                double py    = cy - r * pct * Math.Sin(angle);
                dc.DrawEllipse(accentBrush, null, new Point(px, py), 4, 4);

                // Value labels
                var numTxt = new FormattedText(vals[i].ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10, accentBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(numTxt, new Point(px + 5, py - 10));
            }
        }

        private Brush TryGetBrush(string key, Color fallback)
        {
            try { return (Brush)Application.Current.FindResource(key); }
            catch { return new SolidColorBrush(fallback); }
        }
    }
}
