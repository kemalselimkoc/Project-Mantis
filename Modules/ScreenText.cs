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

        // Translation runs only on an explicit click: the text is sent to Google Translate's public endpoint.
        // Long text goes in chunks so each request stays well under URL length limits.
        public static string Translate(string text, string target, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (!PowerTools.IsSafeToken(target)) { error = "bad language"; return null; }
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                StringBuilder all = new StringBuilder();
                foreach (string chunk in Chunks(text, 1500))
                {
                    string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&dt=t&tl=" + target + "&q=" + Uri.EscapeDataString(chunk);
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.Timeout = 12000; req.UserAgent = "Mozilla/5.0 ProjectMantis/" + AppInfo.Version;
                    string json;
                    using (WebResponse r = req.GetResponse()) using (StreamReader sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) json = sr.ReadToEnd();
                    object[] root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json) as object[];
                    object[] parts = root != null && root.Length > 0 ? root[0] as object[] : null;
                    if (parts == null) throw new InvalidDataException("unexpected response");
                    foreach (object p in parts) { object[] seg = p as object[]; if (seg != null && seg.Length > 0 && seg[0] is string) all.Append((string)seg[0]); }
                }
                return all.ToString();
            }
            catch (WebException ex) { error = ex.Status == WebExceptionStatus.ProtocolError ? "service refused (" + ((HttpWebResponse)ex.Response).StatusCode + ")" : "offline"; }
            catch (Exception ex) { error = ex.Message; }
            return null;
        }

        private static IEnumerable<string> Chunks(string text, int max)
        {
            StringBuilder cur = new StringBuilder();
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (cur.Length > 0 && cur.Length + line.Length + 1 > max) { yield return cur.ToString(); cur.Clear(); }
                if (line.Length > max) { for (int i = 0; i < line.Length; i += max) yield return line.Substring(i, Math.Min(max, line.Length - i)); continue; }
                if (cur.Length > 0) cur.Append('\n');
                cur.Append(line);
            }
            if (cur.Length > 0) yield return cur.ToString();
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
