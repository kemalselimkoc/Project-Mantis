// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace ProjectMantis
{
    // Check-Up report (spec 4): one run is written as HTML (single file, inline CSS, Mantis theme),
    // JSON (machine-readable, hand-serialized) and plain text under %LOCALAPPDATA%\ProjectMantis\reports.
    // Reports stay on this PC; nothing is uploaded.
    internal static class SecurityReport
    {
        public static string Folder { get { return Path.Combine(Settings.DataDir, "reports"); } }

        public static string LatestHtml()
        {
            try { return Directory.Exists(Folder) ? Directory.GetFiles(Folder, "checkup-*.html").OrderByDescending(f => f).FirstOrDefault() : null; } catch { return null; }
        }

        // Returns the HTML path, or null when the report could not be written.
        public static string Write(IList<SecurityModule> modules, IDictionary<string, ModuleResult> results, string language)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                DateTime now = DateTime.Now;
                string stem = Path.Combine(Folder, "checkup-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
                List<ModuleResult> ordered = modules.Where(m => results.ContainsKey(m.Id)).Select(m => results[m.Id]).ToList();
                int score = SecurityScore.Compute(ordered.SelectMany(r => r.Findings));
                bool tr = language == "tr";
                File.WriteAllText(stem + ".json", Json(modules, ordered, score, now), new UTF8Encoding(false));
                File.WriteAllText(stem + ".txt", Text(modules, ordered, score, now, tr), new UTF8Encoding(false));
                File.WriteAllText(stem + ".html", Html(modules, ordered, score, now, tr), new UTF8Encoding(false));
                return stem + ".html";
            }
            catch { return null; }
        }

        private static string Name(IList<SecurityModule> modules, string id, bool tr)
        {
            SecurityModule m = modules.FirstOrDefault(x => x.Id == id);
            return m == null ? id : (tr ? m.NameTr : m.NameEn);
        }

        private static string Sev(Finding f, bool tr)
        {
            if (f.Passed) return tr ? "GEÇTİ" : "PASSED";
            switch (f.Severity) { case Severity.High: return tr ? "YÜKSEK" : "HIGH"; case Severity.Medium: return tr ? "ORTA" : "MEDIUM"; case Severity.Low: return tr ? "DÜŞÜK" : "LOW"; default: return tr ? "BİLGİ" : "INFO"; }
        }

        private static string Verdict(int score, bool tr)
        {
            if (score >= 90) return tr ? "Harika durumdasın" : "In great shape";
            if (score >= 75) return tr ? "İyi durumdasın" : "In good shape";
            if (score >= 50) return tr ? "Biraz dikkat istiyor" : "Needs some attention";
            return tr ? "Risk altında" : "At risk";
        }

        private static IEnumerable<Finding> Sorted(ModuleResult r)
        {
            return r.Findings.OrderBy(f => f.Passed ? 1 : 0).ThenByDescending(f => f.Severity);
        }

        // ---------- Text ----------

        private static string Text(IList<SecurityModule> modules, List<ModuleResult> results, int score, DateTime when, bool tr)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("PROJECT MANTIS — CHECK-UP " + (tr ? "RAPORU" : "REPORT"));
            sb.AppendLine(when.ToString("yyyy-MM-dd HH:mm") + "  ·  " + Environment.MachineName + "  ·  v" + AppInfo.Version);
            sb.AppendLine((tr ? "Skor: " : "Score: ") + score + "/100 — " + Verdict(score, tr));
            sb.AppendLine(new string('=', 64));
            foreach (ModuleResult r in results)
            {
                sb.AppendLine();
                sb.AppendLine("## " + Name(modules, r.ModuleId, tr) + (r.Error != null ? (tr ? "  (tamamlanamadı: " : "  (incomplete: ") + r.Error + ")" : "") + (r.NeedsAdmin ? (tr ? "  (bazı kontroller yönetici ister)" : "  (some checks need administrator)") : ""));
                foreach (Finding f in Sorted(r))
                {
                    sb.AppendLine("[" + Sev(f, tr) + "] " + (tr ? f.TitleTr : f.TitleEn));
                    string d = tr ? f.DetailTr : f.DetailEn, rec = tr ? f.RecommendationTr : f.RecommendationEn;
                    if (!string.IsNullOrEmpty(d)) foreach (string line in d.Split('\n')) sb.AppendLine("    " + line);
                    if (!string.IsNullOrEmpty(rec)) sb.AppendLine("    → " + rec);
                }
            }
            sb.AppendLine();
            sb.AppendLine(tr ? "Check-Up gerçek zamanlı koruma değildir; Windows Defender'ı tamamlar. Bu rapor yalnızca bu bilgisayarda saklanır." : "Check-Up is not real-time protection; it complements Windows Defender. This report is stored on this PC only.");
            return sb.ToString();
        }

        // ---------- JSON ----------

        private static string Json(IList<SecurityModule> modules, List<ModuleResult> results, int score, DateTime when)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n  \"tool\": \"Project Mantis\",\n  \"version\": ").Append(Q(AppInfo.Version))
              .Append(",\n  \"generated\": ").Append(Q(when.ToString("s", CultureInfo.InvariantCulture)))
              .Append(",\n  \"machine\": ").Append(Q(Environment.MachineName))
              .Append(",\n  \"score\": ").Append(score.ToString(CultureInfo.InvariantCulture))
              .Append(",\n  \"modules\": [");
            for (int i = 0; i < results.Count; i++)
            {
                ModuleResult r = results[i];
                sb.Append(i == 0 ? "\n" : ",\n").Append("    {\n      \"id\": ").Append(Q(r.ModuleId))
                  .Append(",\n      \"completed\": ").Append(r.Completed ? "true" : "false")
                  .Append(",\n      \"needsAdmin\": ").Append(r.NeedsAdmin ? "true" : "false")
                  .Append(",\n      \"error\": ").Append(r.Error == null ? "null" : Q(r.Error))
                  .Append(",\n      \"when\": ").Append(Q(r.When.ToString("s", CultureInfo.InvariantCulture)))
                  .Append(",\n      \"findings\": [");
                List<Finding> fs = Sorted(r).ToList();
                for (int j = 0; j < fs.Count; j++)
                {
                    Finding f = fs[j];
                    sb.Append(j == 0 ? "\n" : ",\n").Append("        { \"id\": ").Append(Q(f.Id))
                      .Append(", \"severity\": ").Append(Q(f.Severity.ToString().ToLowerInvariant()))
                      .Append(", \"passed\": ").Append(f.Passed ? "true" : "false")
                      .Append(", \"title\": { \"tr\": ").Append(Q(f.TitleTr)).Append(", \"en\": ").Append(Q(f.TitleEn)).Append(" }")
                      .Append(", \"detail\": { \"tr\": ").Append(Q(f.DetailTr)).Append(", \"en\": ").Append(Q(f.DetailEn)).Append(" }")
                      .Append(", \"recommendation\": { \"tr\": ").Append(Q(f.RecommendationTr)).Append(", \"en\": ").Append(Q(f.RecommendationEn)).Append(" }")
                      .Append(", \"fix\": ").Append(Q(f.FixKind)).Append(", \"requiresAdmin\": ").Append(f.RequiresAdmin ? "true" : "false").Append(" }");
                }
                sb.Append(fs.Count > 0 ? "\n      ]\n    }" : "]\n    }");
            }
            sb.Append(results.Count > 0 ? "\n  ]\n}\n" : "]\n}\n");
            return sb.ToString();
        }

        private static string Q(string s)
        {
            if (s == null) return "null";
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }

        // ---------- HTML ----------

        private static string H(string s) { return WebUtility.HtmlEncode(s ?? "").Replace("\n", "<br>"); }

        private static string SevClass(Finding f) { return f.Passed ? "ok" : f.Severity == Severity.High ? "high" : f.Severity == Severity.Medium ? "med" : f.Severity == Severity.Low ? "low" : "info"; }

        private static string Html(IList<SecurityModule> modules, List<ModuleResult> results, int score, DateTime when, bool tr)
        {
            List<Finding> all = results.SelectMany(r => r.Findings).ToList();
            string ring = score >= 90 ? "#75FF9B" : score >= 75 ? "#9BDB2E" : score >= 50 ? "#FFD166" : "#FF7A6B";
            double circumference = 2 * Math.PI * 54, dash = circumference * score / 100.0;
            StringBuilder sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"").Append(tr ? "tr" : "en").Append("\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
              .Append("<title>Mantis Check-Up · ").Append(score).Append("/100</title><style>")
              .Append(":root{--bg:#050706;--panel:#0f1412;--line:rgba(255,255,255,.09);--text:#edf2ea;--muted:#8c978e;--faint:#58625a;--lime:#b7f34a;--high:#ff7a6b;--med:#ffd166;--low:#75ff9b;--info:#67c6ff;--ok:#75ff9b}")
              .Append("*{box-sizing:border-box}body{margin:0;background:var(--bg);background-image:linear-gradient(rgba(255,255,255,.035) 1px,transparent 1px),linear-gradient(90deg,rgba(255,255,255,.035) 1px,transparent 1px);background-size:32px 32px;color:var(--text);font:14px/1.55 'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif;padding:40px 16px}")
              .Append(".wrap{max-width:980px;margin:0 auto}.mono{font-family:'Cascadia Mono',Consolas,monospace;letter-spacing:.04em}.eyebrow{font-size:11px;color:var(--lime);text-transform:uppercase}.eyebrow:before{content:'';display:inline-block;width:18px;height:2px;background:var(--lime);vertical-align:middle;margin-right:8px}")
              .Append("h1,h2{font-family:Bahnschrift,'Segoe UI',sans-serif;font-weight:600;margin:6px 0}h1{font-size:34px}h2{font-size:19px;margin-top:34px}")
              .Append(".panel{background:var(--panel);border:1px solid var(--line);clip-path:polygon(0 0,calc(100% - 18px) 0,100% 18px,100% 100%,0 100%);padding:22px 24px}")
              .Append(".hero{display:flex;gap:28px;align-items:center;margin-top:22px}.hero svg{flex:none}.verdict{font-family:Bahnschrift,sans-serif;font-size:28px;font-weight:600}.chips span{display:inline-block;margin:10px 8px 0 0;padding:2px 9px;border:1px solid;font-size:12px}")
              .Append(".f{display:grid;grid-template-columns:4px 1fr;gap:14px;background:var(--panel);border:1px solid var(--line);padding:14px 16px;margin:8px 0}.f .bar{background:currentColor}.f .t{font-weight:600}.f .d{color:var(--muted);font-size:13px}.f .r{color:var(--text);font-size:13px;margin-top:4px}.tag{font-size:11px}")
              .Append(".high{color:var(--high)}.med{color:var(--med)}.low{color:var(--low)}.info{color:var(--info)}.ok{color:var(--ok)}.f .t,.f .d,.f .r{color:inherit}.f>div:last-child{color:var(--text)}.f>div:last-child .d{color:var(--muted)}")
              .Append("footer{color:var(--faint);font-size:12px;margin-top:40px;border-top:1px solid var(--line);padding-top:14px}@media(max-width:600px){.hero{flex-direction:column;align-items:flex-start}}</style></head><body><div class=\"wrap\">");
            sb.Append("<div class=\"eyebrow mono\">Project Mantis · Check-Up</div><h1>").Append(tr ? "Sistem sağlık raporu" : "System health report").Append("</h1>")
              .Append("<div class=\"mono\" style=\"color:var(--muted);font-size:12px\">").Append(H(when.ToString("yyyy-MM-dd HH:mm"))).Append(" · ").Append(H(Environment.MachineName)).Append(" · v").Append(H(AppInfo.Version)).Append("</div>");
            sb.Append("<div class=\"panel hero\"><svg width=\"132\" height=\"132\" viewBox=\"0 0 132 132\"><circle cx=\"66\" cy=\"66\" r=\"54\" fill=\"none\" stroke=\"rgba(255,255,255,.08)\" stroke-width=\"10\"/>")
              .Append("<circle cx=\"66\" cy=\"66\" r=\"54\" fill=\"none\" stroke=\"").Append(ring).Append("\" stroke-width=\"10\" stroke-dasharray=\"").Append(dash.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ").Append(circumference.ToString("0.0", CultureInfo.InvariantCulture)).Append("\" transform=\"rotate(-90 66 66)\"/>")
              .Append("<text x=\"66\" y=\"72\" text-anchor=\"middle\" fill=\"#edf2ea\" font-family=\"Bahnschrift,sans-serif\" font-size=\"38\" font-weight=\"600\">").Append(score).Append("</text><text x=\"66\" y=\"92\" text-anchor=\"middle\" fill=\"#8c978e\" font-size=\"11\">/ 100</text></svg>")
              .Append("<div><div class=\"eyebrow mono\">").Append(tr ? "Sağlık skoru" : "Health score").Append("</div><div class=\"verdict\">").Append(H(Verdict(score, tr))).Append("</div><div class=\"chips mono\">");
            Action<int, string, string> chip = delegate(int n, string label, string cls) { if (n > 0) sb.Append("<span class=\"").Append(cls).Append("\">").Append(n).Append(" ").Append(H(label)).Append("</span>"); };
            chip(all.Count(f => !f.Passed && f.Severity == Severity.High), tr ? "yüksek" : "high", "high");
            chip(all.Count(f => !f.Passed && f.Severity == Severity.Medium), tr ? "orta" : "medium", "med");
            chip(all.Count(f => !f.Passed && f.Severity == Severity.Low), tr ? "düşük" : "low", "low");
            chip(all.Count(f => f.Passed), tr ? "kontrol geçti" : "checks passed", "info");
            sb.Append("</div><div class=\"mono\" style=\"color:var(--faint);font-size:11px;margin-top:10px\">").Append(tr ? "Puan: yüksek −20 · orta −8 · düşük −3 · bilgi 0" : "Scoring: high −20 · medium −8 · low −3 · info 0").Append("</div></div></div>");
            foreach (ModuleResult r in results)
            {
                sb.Append("<h2>").Append(H(Name(modules, r.ModuleId, tr))).Append("</h2>");
                if (r.Error != null) sb.Append("<div class=\"f med\"><div class=\"bar\"></div><div><div class=\"t\">").Append(tr ? "Modül tamamlanamadı" : "Module did not complete").Append("</div><div class=\"d\">").Append(H(r.Error)).Append("</div></div></div>");
                if (r.NeedsAdmin) sb.Append("<div class=\"f med\"><div class=\"bar\"></div><div><div class=\"t\">").Append(tr ? "Bazı kontroller yönetici ister" : "Some checks need administrator").Append("</div></div></div>");
                foreach (Finding f in Sorted(r))
                {
                    string d = tr ? f.DetailTr : f.DetailEn, rec = tr ? f.RecommendationTr : f.RecommendationEn;
                    sb.Append("<div class=\"f ").Append(SevClass(f)).Append("\"><div class=\"bar\"></div><div><div class=\"tag mono ").Append(SevClass(f)).Append("\">").Append(H(Sev(f, tr))).Append("</div><div class=\"t\">").Append(H(tr ? f.TitleTr : f.TitleEn)).Append("</div>");
                    if (!string.IsNullOrEmpty(d)) sb.Append("<div class=\"d\">").Append(H(d)).Append("</div>");
                    if (!string.IsNullOrEmpty(rec)) sb.Append("<div class=\"r\">→ ").Append(H(rec)).Append("</div>");
                    sb.Append("</div></div>");
                }
            }
            sb.Append("<footer>").Append(tr ? "Check-Up gerçek zamanlı koruma değildir ve antivirüsün yerine geçmez; Windows Defender'ı tamamlar. Mantis hiçbir ayarı onayın olmadan değiştirmez ve dosya silmez. Bu rapor yalnızca bu bilgisayarda oluşturuldu ve hiçbir yere gönderilmedi." : "Check-Up is not real-time protection and does not replace an antivirus; it complements Windows Defender. Mantis changes no setting without your approval and deletes no files. This report was created on this PC and was not sent anywhere.")
              .Append("<br>github.com/kemalselimkoc/Project-Mantis</footer></div></body></html>");
            return sb.ToString();
        }
    }
}
