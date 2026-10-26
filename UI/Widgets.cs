// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ProjectMantis
{
    // Circular progress gauge (0–100). Content placed in the grid sits centered inside the ring.
    // Reused for the live summary strip and, later, the Check-Up health score.
    internal sealed class Ring : Grid
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(Ring), new PropertyMetadata(0.0, (d, e) => ((Ring)d).Redraw()));

        private readonly Path arc;
        private readonly double size, thickness;

        public Ring(double size, double thickness, string color)
        {
            this.size = size; this.thickness = thickness;
            Width = size; Height = size;
            Children.Add(new Ellipse { Stroke = Theme.Brush(Theme.Alpha(Theme.TextPrimary, 0x14)), StrokeThickness = thickness });
            arc = new Path { Stroke = Theme.Brush(color), StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            Children.Add(arc);
        }

        public double Value { get { return (double)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }

        public void SetColor(string color) { arc.Stroke = Theme.Brush(color); }

        public void AnimateTo(double value, int milliseconds)
        {
            BeginAnimation(ValueProperty, new DoubleAnimation(Math.Max(0, Math.Min(100, value)), TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        private void Redraw()
        {
            double v = Math.Max(0, Math.Min(100, Value));
            double r = (size - thickness) / 2;
            Point c = new Point(size / 2, size / 2);
            if (v < 0.1) { arc.Data = null; return; }
            if (v > 99.9) { arc.Data = new EllipseGeometry(c, r, r); return; }
            double angle = v / 100 * 360, rad = (angle - 90) * Math.PI / 180;
            PathFigure f = new PathFigure { StartPoint = new Point(c.X, c.Y - r), IsClosed = false };
            f.Segments.Add(new ArcSegment(new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad)), new Size(r, r), 0, angle > 180, SweepDirection.Clockwise, true));
            PathGeometry g = new PathGeometry(); g.Figures.Add(f); g.Freeze();
            arc.Data = g;
        }
    }

    // Small right-aligned trend line with a soft area fill; values are 0–100.
    internal sealed class Sparkline : Grid
    {
        private readonly Polyline line;
        private readonly Polygon area;
        private readonly int capacity;
        private IList<double> values = new List<double>();

        public Sparkline(string color, int capacity)
        {
            this.capacity = Math.Max(2, capacity);
            ClipToBounds = true;
            LinearGradientBrush fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            fill.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Alpha(color, 0x45)), 0));
            fill.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Alpha(color, 0)), 1));
            fill.Freeze();
            area = new Polygon { Fill = fill };
            line = new Polyline { Stroke = Theme.Brush(color), StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round };
            Children.Add(area); Children.Add(line);
            SizeChanged += delegate { Redraw(); };
        }

        public void SetValues(IList<double> latest) { values = latest; Redraw(); }

        private void Redraw()
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0 || values == null || values.Count == 0) { line.Points = null; area.Points = null; return; }
            int n = Math.Min(values.Count, capacity);
            if (n < 2) { line.Points = null; area.Points = null; return; }
            // Until the buffer fills, the samples we have are spread over the full width.
            double step = w / (n - 1), x0 = 0;
            PointCollection pts = new PointCollection(n), poly = new PointCollection(n + 2);
            for (int i = 0; i < n; i++)
            {
                double v = Math.Max(0, Math.Min(100, values[values.Count - n + i]));
                Point p = new Point(x0 + step * i, 1.5 + (h - 3) * (1 - v / 100));
                pts.Add(p); poly.Add(p);
            }
            poly.Add(new Point(w, h)); poly.Add(new Point(x0, h));
            line.Points = pts; area.Points = poly;
        }
    }
}
