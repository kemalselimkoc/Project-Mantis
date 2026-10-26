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
        // Backgrounds
        public const string BgBase = "#070A08";
        public const string BgElevated = "#0D1510";
        public const string BgSidebar = "#D90E1510";

        // Surfaces (glass layers, from deepest to highest)
        public const string SurfaceSunken = "#99090E0B";
        public const string Surface = "#D9161D19";
        public const string SurfaceHover = "#E81D2721";
        public const string SurfaceRaised = "#17231B";
        public const string Control = "#1C2721";
        public const string ControlHover = "#26352C";

        // Strokes
        public const string Stroke = "#2E75FF9B";
        public const string StrokeStrong = "#5C75FF9B";
        public const string StrokeSubtle = "#12FFFFFF";
        public const string EdgeHighlight = "#3DFFFFFF";

        // Text
        public const string TextPrimary = "#F3F7F4";
        public const string TextMuted = "#97A39A";
        public const string TextFaint = "#5F6B63";
        public const string OnAccent = "#06100A";

        // Semantic colors (one meaning, one color, everywhere)
        public const string AccentPrimary = "#75FF9B";
        public const string AccentSecondary = "#2AD66F";
        public const string Info = "#76C8FF";
        public const string Success = "#75FF9B";
        public const string Warning = "#FFD873";
        public const string Danger = "#FF8E8E";
        public const string Violet = "#C29CFF";
        public const string Amber = "#FFB86B";

        // Section accents: same saturation/lightness family, one hue per area
        public const string SectionOverview = AccentPrimary;
        public const string SectionCleaner = AccentPrimary;
        public const string SectionTools = Info;
        public const string SectionUpdates = Violet;
        public const string SectionSecurity = Amber;
        public const string SectionSettings = "#A9BBAF";

        // Spacing scale
        public const double S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32;

        // Corner radii
        public const double RadiusSm = 10, RadiusMd = 14, RadiusLg = 20, RadiusXl = 28;

        // Type scale
        public const double Display = 30, H2 = 20, H3 = 16, Body = 13, Small = 12, Micro = 11, Caption = 10;
        public static readonly FontFamily TextFont = new FontFamily("Segoe UI Variable Text, Segoe UI");
        public static readonly FontFamily DisplayFont = new FontFamily("Segoe UI Variable Display, Segoe UI");

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
