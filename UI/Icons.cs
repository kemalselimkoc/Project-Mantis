// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System.Globalization;
using System.Windows.Media;

namespace ProjectMantis
{
    // One stroke icon set on a 24x24 grid, drawn by MainWindow.Icon() with a single stroke width,
    // round caps and joins. Replaces emoji glyphs that render inconsistently across fonts.
    internal static class Icons
    {
        public static readonly Geometry Home = P("M3.5,10.5 L12,3.5 L20.5,10.5 M5.5,9 V20 H18.5 V9 M10,20 V14.5 H14 V20");
        public static readonly Geometry Sparkle = P("M11,3.5 C11.6,8.4 13.1,9.9 18,10.5 C13.1,11.1 11.6,12.6 11,17.5 C10.4,12.6 8.9,11.1 4,10.5 C8.9,9.9 10.4,8.4 11,3.5 Z M18,15.5 C18.25,17.3 18.7,17.75 20.5,18 C18.7,18.25 18.25,18.7 18,20.5 C17.75,18.7 17.3,18.25 15.5,18 C17.3,17.75 17.75,17.3 18,15.5 Z");
        public static readonly Geometry Apps = P("M4.5,4.5 H10 V10 H4.5 Z M14,4.5 H19.5 V10 H14 Z M4.5,14 H10 V19.5 H4.5 Z M14,14 H19.5 V19.5 H14 Z");
        public static readonly Geometry Sliders = P("M4,7 H9 M13,7 H20 M4,12 H14 M18,12 H20 M4,17 H7 M11,17 H20", Circle(11, 7, 2), Circle(16, 12, 2), Circle(9, 17, 2));
        public static readonly Geometry Download = P("M12,4 V15 M7.5,10.5 L12,15 L16.5,10.5 M5,19.5 H19");
        public static readonly Geometry Shield = P("M12,3.5 L19,6.2 V11.5 C19,15.8 16.1,19 12,20.5 C7.9,19 5,15.8 5,11.5 V6.2 Z");
        public static readonly Geometry ShieldCheck = P("M12,3.5 L19,6.2 V11.5 C19,15.8 16.1,19 12,20.5 C7.9,19 5,15.8 5,11.5 V6.2 Z M9,12 L11.2,14.2 L15.2,10");
        public static readonly Geometry Bolt = P("M13,3 L5,13.5 H11.5 L10.5,21 L19,10.5 H12.5 Z");
        public static readonly Geometry Chip = P("M7,7 H17 V17 H7 Z M10,10 H14 V14 H10 Z M9.5,4 V7 M14.5,4 V7 M9.5,17 V20 M14.5,17 V20 M4,9.5 H7 M4,14.5 H7 M17,9.5 H20 M17,14.5 H20");
        public static readonly Geometry Search = P(Circle(10.5, 10.5, 6), "M15,15 L20,20");
        public static readonly Geometry Coffee = P("M5,9 H16 V13.5 C16,16.5 13.8,19 11,19 H10 C7.2,19 5,16.5 5,13.5 Z M16,10.5 H17.5 C18.9,10.5 20,11.6 20,13 C20,14.4 18.9,15.5 17.5,15.5 H16 M8,3.5 V6 M11,3.5 V6 M14,3.5 V6");
        public static readonly Geometry Refresh = P("M19.5,12 A7.5,7.5 0 1 1 17.3,6.7 M18,3.2 L17.6,7 L13.8,6.6");
        public static readonly Geometry Layers = P("M12,4 L20.5,8.5 L12,13 L3.5,8.5 Z M3.5,12.5 L12,17 L20.5,12.5 M3.5,16.5 L12,21 L20.5,16.5");
        public static readonly Geometry Headphones = P("M4,15 V12 A8,8 0 0 1 20,12 V15 M4,14 H7.5 V20 H5.5 C4.7,20 4,19.3 4,18.5 Z M20,14 H16.5 V20 H18.5 C19.3,20 20,19.3 20,18.5 Z");
        public static readonly Geometry Globe = P(Circle(12, 12, 8.5), "M3.5,12 H20.5 M12,3.5 C9.4,6.3 9.4,17.7 12,20.5 C14.6,17.7 14.6,6.3 12,3.5");
        public static readonly Geometry Power = P("M12,3.5 V11 M7.2,6.4 A7.5,7.5 0 1 0 16.8,6.4");
        public static readonly Geometry Monitor = P("M4,5 H20 V15.5 H4 Z M9,19.5 H15 M12,15.5 V19.5");
        public static readonly Geometry FileText = P("M6,3.5 H14 L18.5,8 V20.5 H6 Z M14,3.5 V8 H18.5 M9,12.5 H15.5 M9,16 H15.5");
        public static readonly Geometry Code = P("M8.5,7 L3.5,12 L8.5,17 M15.5,7 L20.5,12 L15.5,17");
        public static readonly Geometry Folder = P("M3.5,7 C3.5,6.2 4.2,5.5 5,5.5 H9.5 L11.5,7.5 H19 C19.8,7.5 20.5,8.2 20.5,9 V17.5 C20.5,18.3 19.8,19 19,19 H5 C4.2,19 3.5,18.3 3.5,17.5 Z");
        public static readonly Geometry Film = P("M4,5 H20 V19 H4 Z M8,5 V19 M16,5 V19 M4,9.5 H8 M4,14.5 H8 M16,9.5 H20 M16,14.5 H20");
        public static readonly Geometry Lock = P("M6,11 H18 V20 H6 Z M8.5,11 V8 A3.5,3.5 0 0 1 15.5,8 V11 M12,14.5 V16.5");
        public static readonly Geometry Check = P("M5,12.5 L10,17.5 L19,7");
        public static readonly Geometry CheckCircle = P(Circle(12, 12, 8.5), "M8.2,12.3 L11,15 L16,9.6");
        public static readonly Geometry Alert = P("M12,4 L21,19.5 H3 Z M12,10 V14 M12,16.8 V17");
        public static readonly Geometry InfoCircle = P(Circle(12, 12, 8.5), "M12,11 V16 M12,7.8 V8");
        public static readonly Geometry ArrowRight = P("M5,12 H19 M13.5,6.5 L19,12 L13.5,17.5");
        public static readonly Geometry ArrowUpRight = P("M7,17 L17,7 M9,7 H17 V15");
        public static readonly Geometry ChevronRight = P("M9.5,6 L15.5,12 L9.5,18");
        public static readonly Geometry Minimize = P("M6,12 H18");
        public static readonly Geometry Maximize = P("M6.5,6.5 H17.5 V17.5 H6.5 Z");
        public static readonly Geometry Restore = P("M6,9 H15 V18 H6 Z M9,9 V6 H18 V15 H15");
        public static readonly Geometry Close = P("M6.5,6.5 L17.5,17.5 M17.5,6.5 L6.5,17.5");

        private static Geometry P(params string[] parts)
        {
            Geometry g = Geometry.Parse(string.Join(" ", parts));
            g.Freeze();
            return g;
        }

        private static string Circle(double cx, double cy, double r)
        {
            return string.Format(CultureInfo.InvariantCulture, "M{0},{1} A{2},{2} 0 1 0 {3},{1} A{2},{2} 0 1 0 {0},{1} Z", cx - r, cy, r, cx + r);
        }
    }
}
