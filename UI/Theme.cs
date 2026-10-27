// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ProjectMantis
{
    // Design tokens. Every color, spacing step, radius, type size and duration in the UI comes from here.
    internal static class Theme
    {
        // "Organic precision": matte graphite instrument panels, hairline edges, one sharp mantis-lime accent,
        // DIN-style display type and mono labels. The chamfered corner (Chamfer) is the signature shape.

        // Backgrounds
        public const string BgBase = "#050706";
        public const string BgElevated = "#0A0E0C";
        public const string BgSidebar = "#F00A0E0C";
        public const string GridLine = "#0BFFFFFF";

        // Surfaces (matte panels, from deepest to highest)
        public const string SurfaceSunken = "#C0070A09";
        public const string Surface = "#F00F1412";
        public const string SurfaceHover = "#F5151C19";
        public const string SurfaceRaised = "#121816";
        public const string Control = "#161D1A";
        public const string ControlHover = "#1F2824";

        // Strokes: neutral hairlines; the accent is reserved for state and focus
        public const string Stroke = "#1CFFFFFF";
        public const string StrokeStrong = "#3DB7F34A";
        public const string StrokeSubtle = "#0FFFFFFF";
        public const string EdgeHighlight = "#2EFFFFFF";

        // Text
        public const string TextPrimary = "#EDF2EA";
        public const string TextMuted = "#8C978E";
        public const string TextFaint = "#58625A";
        public const string OnAccent = "#0A1203";

        // Semantic colors (one meaning, one color, everywhere)
        public const string AccentPrimary = "#B7F34A";     // mantis lime: brand, primary actions, focus
        public const string AccentSecondary = "#9BDB2E";
        public const string Info = "#67C6FF";
        public const string Success = "#75FF9B";
        public const string Warning = "#FFD166";
        public const string Danger = "#FF7A6B";
        public const string Violet = "#B49BFF";
        public const string Amber = "#FFAE57";

        // Section accents: same saturation/lightness family, one hue per area
        public const string SectionOverview = AccentPrimary;
        public const string SectionCleaner = "#6FE8B4";
        public const string SectionTools = Info;
        public const string SectionUpdates = Violet;
        public const string SectionSecurity = Amber;
        public const string SectionSettings = "#A3ADA5";

        // Spacing scale
        public const double S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32;

        // Corner radii: tight, machined corners; Chamfer is the 45° cut on a panel's top-right corner
        public const double RadiusSm = 5, RadiusMd = 7, RadiusLg = 9, RadiusXl = 12, Chamfer = 14;

        // Type scale
        public const double Display = 32, H2 = 20, H3 = 16, Body = 13, Small = 12, Micro = 11, Caption = 10;
        public static readonly FontFamily TextFont = new FontFamily("Segoe UI Variable Text, Segoe UI");
        public static readonly FontFamily DisplayFont = new FontFamily("Bahnschrift, Segoe UI Variable Display, Segoe UI");
        public static readonly FontFamily MonoFont = new FontFamily("Cascadia Mono, Consolas");

        // Motion
        public static readonly Duration Fast = new Duration(TimeSpan.FromMilliseconds(120));
        public static readonly Duration Normal = new Duration(TimeSpan.FromMilliseconds(240));
        public static readonly Duration Slow = new Duration(TimeSpan.FromMilliseconds(650));

        // Shell layout
        public const double SidebarWidth = 244, TitleBarHeight = 58, ShellMargin = 18, ShellGap = 14;
        public const double ControlHeight = 40, IconStroke = 1.8;

        private static readonly Dictionary<string, SolidColorBrush> brushes = new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);

        public static Color ToColor(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }

        // Shared, frozen brush. Use LiveBrush when the color will be animated.
        public static SolidColorBrush Brush(string hex)
        {
            SolidColorBrush b;
            if (brushes.TryGetValue(hex, out b)) return b;
            b = new SolidColorBrush(ToColor(hex)); b.Freeze(); brushes[hex] = b; return b;
        }

        public static SolidColorBrush LiveBrush(string hex) { return new SolidColorBrush(ToColor(hex)); }

        // Same hue with a new alpha: Alpha("#75FF9B", 0x1F) -> "#1F75FF9B".
        public static string Alpha(string hex, byte alpha)
        {
            Color c = ToColor(hex);
            return "#" + alpha.ToString("X2") + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        // Linear mix toward another color, keeping the source alpha.
        public static string Mix(string hex, string toward, double amount)
        {
            Color a = ToColor(hex), b = ToColor(toward);
            Func<byte, byte, byte> m = delegate(byte x, byte y) { return (byte)Math.Round(x + (y - x) * amount); };
            return "#" + a.A.ToString("X2") + m(a.R, b.R).ToString("X2") + m(a.G, b.G).ToString("X2") + m(a.B, b.B).ToString("X2");
        }

        // Glass border: a brighter 1px light on the top edge fading into the normal stroke.
        public static Brush EdgeLight(string stroke)
        {
            LinearGradientBrush b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            b.GradientStops.Add(new GradientStop(ToColor(Mix(stroke, EdgeHighlight, 0.55)), 0));
            b.GradientStops.Add(new GradientStop(ToColor(stroke), 0.14));
            b.GradientStops.Add(new GradientStop(ToColor(Alpha(stroke, (byte)(ToColor(stroke).A * 0.6))), 1));
            b.Freeze();
            return b;
        }

        // Elevation layers: 1 = resting card, 2 = floating / hovered.
        public static DropShadowEffect Elevation(int level)
        {
            return new DropShadowEffect
            {
                Color = Colors.Black, Direction = 270, RenderingBias = RenderingBias.Performance,
                BlurRadius = level >= 2 ? 36 : 24, ShadowDepth = level >= 2 ? 10 : 5, Opacity = level >= 2 ? 0.48 : 0.32
            };
        }

        // Faint instrument grid behind the whole window (32px cells, hairline lines).
        public static Brush Grid()
        {
            GeometryGroup lines = new GeometryGroup();
            lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(32, 0)));
            lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 32)));
            DrawingBrush b = new DrawingBrush(new GeometryDrawing(null, new Pen(Brush(GridLine), 1), lines))
            {
                TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 32, 32), ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 32, 32), ViewboxUnits = BrushMappingMode.Absolute
            };
            b.Freeze();
            return b;
        }

        // Panel outline with a 45° chamfer on the top-right corner (and optionally bottom-left), radius r elsewhere.
        public static Geometry ChamferShape(double w, double h, double cut, double r, bool bottomLeft)
        {
            cut = Math.Min(cut, Math.Min(w, h) / 3);
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new Point(r, 0), true, true);
                c.LineTo(new Point(w - cut, 0), true, false);
                c.LineTo(new Point(w, cut), true, false);
                c.LineTo(new Point(w, h - r), true, false);
                c.ArcTo(new Point(w - r, h), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                if (bottomLeft) { c.LineTo(new Point(cut, h), true, false); c.LineTo(new Point(0, h - cut), true, false); }
                else { c.LineTo(new Point(r, h), true, false); c.ArcTo(new Point(0, h - r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false); }
                c.LineTo(new Point(0, r), true, false);
                c.ArcTo(new Point(r, 0), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            return g;
        }

        // Soft radial light used behind pages for depth (never as a hard-edged shape).
        public static RadialGradientBrush Glow(string hex, double strength)
        {
            Color c = ToColor(hex);
            RadialGradientBrush b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * strength), c.R, c.G, c.B), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * strength * 0.35), c.R, c.G, c.B), 0.45));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
            return b;
        }

        // Load colors: green under 70%, amber under 90%, red above.
        public static string LoadColor(double percent) { return percent < 70 ? Success : percent < 90 ? Warning : Danger; }
    }
}
