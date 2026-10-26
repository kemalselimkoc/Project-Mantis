// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;

namespace ProjectMantis
{
    internal sealed class AntivirusProduct { public string Name; public bool Enabled; public bool UpToDate; }

    // Windows Defender state (Get-MpComputerStatus) plus every antivirus registered in Security Center.
    // Shared by the Hardening audit and the Malware scan so both tell the same story.
    internal sealed class DefenderInfo
    {
        public bool Available;            // Get-MpComputerStatus answered
        public bool AntivirusEnabled, RealTimeProtection, ServiceEnabled, TamperProtected;
        public int SignatureAgeDays = -1, QuickScanAgeDays = -1;
        public string Error;
        public List<AntivirusProduct> Products = new List<AntivirusProduct>();

        // Another, active antivirus legitimately turns Defender's real-time protection off.
        public AntivirusProduct OtherActiveAntivirus()
        {
            foreach (AntivirusProduct p in Products)
                if (p.Enabled && p.Name.IndexOf("Defender", StringComparison.OrdinalIgnoreCase) < 0) return p;
            return null;
        }

        public static DefenderInfo Read()
        {
            const string script =
                "try { $m = Get-MpComputerStatus -ErrorAction Stop; 'MP_OK=1'; 'MP_AV=' + $m.AntivirusEnabled; 'MP_RTP=' + $m.RealTimeProtectionEnabled; 'MP_SVC=' + $m.AMServiceEnabled; 'MP_TAMPER=' + $m.IsTamperProtected; 'MP_SIGAGE=' + $m.AntivirusSignatureAge; 'MP_QSAGE=' + $m.QuickScanAge } catch { 'MP_ERR=' + $_.Exception.HResult }\n" +
                "try { Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct -ErrorAction Stop | ForEach-Object { 'AV=' + $_.productState + '|' + $_.displayName } } catch { }";
            DefenderInfo d = new DefenderInfo();
            CommandResult r = PowerTools.PowerShell(script, 60000);
            KeyValues kv = KeyValues.Parse(r.Output);
            if (!r.Started) { d.Error = "PowerShell unavailable"; return d; }
            d.Available = kv.Get("MP_OK") == "1";
            if (!d.Available) d.Error = kv.Get("MP_ERR") ?? (r.TimedOut ? "timeout" : "unavailable");
            d.AntivirusEnabled = kv.Bool("MP_AV");
            d.RealTimeProtection = kv.Bool("MP_RTP");
            d.ServiceEnabled = kv.Bool("MP_SVC");
            d.TamperProtected = kv.Bool("MP_TAMPER");
            d.SignatureAgeDays = kv.Int("MP_SIGAGE", -1);
            d.QuickScanAgeDays = kv.Int("MP_QSAGE", -1);
            foreach (string av in kv.All("AV"))
            {
                int bar = av.IndexOf('|');
                if (bar <= 0) continue;
                int state; if (!int.TryParse(av.Substring(0, bar), out state)) continue;
                // productState: bits 12-15 = on/off (1 = on), bits 4-7 = signatures (0 = up to date).
                d.Products.Add(new AntivirusProduct { Name = av.Substring(bar + 1).Trim(), Enabled = ((state >> 12) & 0xF) == 1, UpToDate = ((state >> 4) & 0xF) == 0 });
            }
            return d;
        }
    }

    // "KEY=value" lines printed by our own scripts: the format we parse is the one we wrote,
    // so results never depend on the Windows display language.
    internal sealed class KeyValues
    {
        private readonly Dictionary<string, List<string>> values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static KeyValues Parse(string text)
        {
            KeyValues kv = new KeyValues();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                if (key.IndexOf(' ') >= 0) continue;
                List<string> list;
                if (!kv.values.TryGetValue(key, out list)) { list = new List<string>(); kv.values[key] = list; }
                list.Add(line.Substring(eq + 1).Trim());
            }
            return kv;
        }

        public string Get(string key) { List<string> l; return values.TryGetValue(key, out l) && l.Count > 0 ? l[0] : null; }
        public bool Has(string key) { return values.ContainsKey(key); }
        public IEnumerable<string> All(string key) { List<string> l; return values.TryGetValue(key, out l) ? l : new List<string>(); }
        public bool Bool(string key) { return string.Equals(Get(key), "True", StringComparison.OrdinalIgnoreCase) || Get(key) == "1"; }
        public int Int(string key, int fallback) { int v; return int.TryParse(Get(key), out v) ? v : fallback; }
    }
}
