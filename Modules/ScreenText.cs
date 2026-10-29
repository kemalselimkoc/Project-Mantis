// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ProjectMantis
{
    // Screen text (OCR) with the offline Windows.Media.Ocr engine. The WinRT types are referenced from
    // C:\Windows\System32\WinMetadata at build time, so no SDK or NuGet package is needed. Everything WinRT
    // lives in OcrCore so a machine without it (Windows 7/8) only gets a gentle "not available".
    internal static class ScreenText
    {
        public static bool Available()
        {
            try { return OcrCore.Languages().Count > 0; } catch { return false; }
        }

        // BCP-47 tag + display name of each installed recognizer language.
        public static List<KeyValuePair<string, string>> Languages()
        {
            try { return OcrCore.Languages(); } catch { return new List<KeyValuePair<string, string>>(); }
        }

        // png = encoded image; language = tag from Languages() or null for the user's profile languages.
        public static string Recognize(byte[] png, string language, out string error)
        {
            error = null;
            try { return OcrCore.Recognize(png, language).Result; }
            catch (AggregateException ex) { error = ex.InnerException != null ? ex.InnerException.Message : ex.Message; }
            catch (Exception ex) { error = ex is TypeLoadException || ex is FileNotFoundException ? "unavailable" : ex.Message; }
            return null;
        }

        // Mantis never calls a translation API itself: it opens the public Google Translate website in the
        // user's browser, so the service is used by the user under Google's own terms. Returns null when the
        // text is too long for a link; the caller then opens the site empty and the user pastes it.
        public static string TranslateUrl(string text, string target)
        {
            if (!PowerTools.IsSafeToken(target)) return null;
            string url = "https://translate.google.com/?sl=auto&tl=" + target + "&op=translate";
            string q = Uri.EscapeDataString(text ?? "");
            return q.Length <= 6000 ? url + "&text=" + q : null;
        }

        public static string TranslateHome(string target)
        {
            return "https://translate.google.com/?sl=auto&op=translate" + (PowerTools.IsSafeToken(target) ? "&tl=" + target : "");
        }
    }

    // The only class that touches WinRT types. Never inlined so a missing runtime fails inside the try above.
    internal static class OcrCore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static List<KeyValuePair<string, string>> Languages()
        {
            return Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Select(l => new KeyValuePair<string, string>(l.LanguageTag, l.NativeName)).ToList();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> Recognize(byte[] png, string language)
        {
            Windows.Media.Ocr.OcrEngine engine = null;
            if (!string.IsNullOrEmpty(language)) engine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language(language));
            if (engine == null) engine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine == null) throw new InvalidOperationException("no OCR language installed");
            Windows.Storage.Streams.InMemoryRandomAccessStream stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            Windows.Storage.Streams.DataWriter writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0));
            writer.WriteBytes(png);
            await Await(writer.StoreAsync());
            stream.Seek(0);
            Windows.Graphics.Imaging.BitmapDecoder decoder = await Await(Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream));
            Windows.Graphics.Imaging.SoftwareBitmap bitmap = await Await(decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied));
            Windows.Media.Ocr.OcrResult result = await Await(engine.RecognizeAsync(bitmap));
            return string.Join(Environment.NewLine, result.Lines.Select(l => l.Text));
        }

        // System.Runtime.WindowsRuntime's awaiter needs the SDK's unified Windows.winmd, so await by hand.
        private static Task<T> Await<T>(Windows.Foundation.IAsyncOperation<T> op)
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            op.Completed = delegate(Windows.Foundation.IAsyncOperation<T> o, Windows.Foundation.AsyncStatus s)
            {
                if (s == Windows.Foundation.AsyncStatus.Completed) tcs.TrySetResult(o.GetResults());
                else if (s == Windows.Foundation.AsyncStatus.Error) tcs.TrySetException(o.ErrorCode);
                else tcs.TrySetCanceled();
            };
            return tcs.Task;
        }
    }
}
