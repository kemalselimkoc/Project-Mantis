// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Security.Principal;
using System.Threading;

namespace ProjectMantis
{
    // Hardening audit (spec 3.3). Reads Windows security settings — registry through the Registry class,
    // everything else through one PowerShell script that prints KEY=value lines — and reports each one.
    // Safe, reversible fixes are offered through SecurityFixes; risky ones only open the right Windows page.
    internal static class HardeningAuditor
    {
        private const string SystemPolicies = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string TerminalServer = @"SYSTEM\CurrentControlSet\Control\Terminal Server";
        private static bool registered;

        public static void RegisterFixes()
        {
            if (registered) return; registered = true;
            SecurityFixes.Register("FW_ON", () => PowerTools.Exec("netsh.exe", "advfirewall set allprofiles state on", 30000));
            SecurityFixes.Register("UAC_ON", () => SetDword(Registry.LocalMachine, SystemPolicies, "EnableLUA", 1));
            SecurityFixes.Register("UAC_PROMPT", () => SetDword(Registry.LocalMachine, SystemPolicies, "ConsentPromptBehaviorAdmin", 5));
            SecurityFixes.Register("RDP_OFF", () => SetDword(Registry.LocalMachine, TerminalServer, "fDenyTSConnections", 1));
            SecurityFixes.Register("SMB1_OFF", () => PowerTools.PowerShell("Disable-WindowsOptionalFeature -Online -FeatureName SMB1Protocol -NoRestart -ErrorAction Stop | Out-Null; exit 0", 5 * 60 * 1000));
            SecurityFixes.Register("WU_ENABLE", () => PowerTools.Exec("sc.exe", "config wuauserv start= demand", 30000));
            SecurityFixes.Register("AUTOPLAY_OFF", () => SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1));
            SecurityFixes.Register("ADMIN500_OFF", () => PowerTools.PowerShell("Get-LocalUser -ErrorAction Stop | Where-Object { $_.SID.Value -like '*-500' } | Disable-LocalUser -ErrorAction Stop; exit 0", 60000));
        }

        public static ModuleResult Run(CancellationToken token)
        {
            ModuleResult r = new ModuleResult { ModuleId = "hardening" };
            bool admin = IsAdmin();

            const string script =
                "try { Get-NetFirewallProfile -PolicyStore ActiveStore -ErrorAction Stop | ForEach-Object { 'FW_' + $_.Name + '=' + $_.Enabled } } catch { 'FW_ERR=1' }\n" +
                "try { $v = (New-Object -ComObject Shell.Application).NameSpace($env:SystemDrive + '\\').Self.ExtendedProperty('System.Volume.BitLockerProtection'); 'BL=' + $v } catch { 'BL_ERR=1' }\n" +
                "try { Get-CimInstance Win32_UserAccount -Filter 'LocalAccount=True' -ErrorAction Stop | Where-Object { $_.SID -like '*-500' } | ForEach-Object { 'ADMIN500=' + $_.Disabled } } catch { 'ADMIN_ERR=1' }\n" +
                "if (([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { try { 'SMB1=' + (Get-WindowsOptionalFeature -Online -FeatureName SMB1Protocol -ErrorAction Stop).State } catch { 'SMB1_ERR=1' } }";
            KeyValues kv = KeyValues.Parse(PowerTools.PowerShell(script, 90000).Output);
            token.ThrowIfCancellationRequested();

            CheckFirewall(r, kv);
            CheckDefender(r, DefenderInfo.Read());
            token.ThrowIfCancellationRequested();
            CheckUac(r);
            CheckBitLocker(r, kv);
            CheckSmb1(r, kv, admin);
            CheckRdp(r);
            CheckSecureBoot(r);
            CheckMemoryIntegrity(r);
            CheckWindowsUpdateService(r);
            CheckAutoplay(r);
            CheckBuiltinAdmin(r, kv);
            return r;
        }

        private static void CheckFirewall(ModuleResult r, KeyValues kv)
        {
            if (kv.Has("FW_ERR") || !kv.Has("FW_Public"))
            {
                r.Add(Finding.Issue("FW_UNKNOWN", Severity.Info, "Güvenlik Duvarı durumu doğrulanamadı", "Firewall state could not be verified", "Windows Güvenlik Duvarı bilgisi okunamadı.", "Windows Firewall information could not be read.")
                    .WithFix(FixKinds.OpenSettings, "windowsdefender://network", false, "", ""));
                return;
            }
            string off = "";
            foreach (string p in new[] { "Domain", "Private", "Public" }) if (!kv.Bool("FW_" + p)) off += (off.Length > 0 ? ", " : "") + p;
            if (off.Length == 0) { r.Add(Finding.Ok("FW_ON", "Güvenlik Duvarı tüm profillerde açık", "Firewall is on for all profiles")); return; }
            r.Add(Finding.Issue("FW_OFF", Severity.Medium, "Güvenlik Duvarı bazı profillerde kapalı", "Firewall is off for some profiles",
                    "Kapalı profiller: " + off + ". Bu ağlarda gelen bağlantılar süzülmüyor.", "Profiles turned off: " + off + ". Incoming connections are not filtered on these networks.")
                .Recommend("Tüm profillerde açık tut; başka bir güvenlik duvarı kullanmıyorsan.", "Keep it on for all profiles unless you use another firewall.")
                .WithFix(FixKinds.Command, "FW_ON", true,
                    "Windows Güvenlik Duvarı Etki Alanı, Özel ve Ortak profillerde açılacak (netsh advfirewall set allprofiles state on). Geri almak için Windows Güvenliği → Güvenlik duvarı ve ağ koruması.",
                    "Windows Firewall will be turned on for the Domain, Private and Public profiles (netsh advfirewall set allprofiles state on). To undo: Windows Security → Firewall & network protection."));
        }

        private static void CheckDefender(ModuleResult r, DefenderInfo d)
        {
            AntivirusProduct other = d.OtherActiveAntivirus();
            if (!d.Available)
            {
                if (other != null) r.Add(Finding.Ok("AV_OTHER", other.Name + " gerçek zamanlı koruma sağlıyor", other.Name + " provides real-time protection"));
                else r.Add(Finding.Issue("DEF_UNKNOWN", Severity.Medium, "Antivirüs durumu doğrulanamadı", "Antivirus state could not be verified", "Windows Defender durumu okunamadı ve kayıtlı başka bir antivirüs görünmüyor.", "Windows Defender status could not be read and no other antivirus is registered.")
                    .WithFix(FixKinds.Defender, "windowsdefender:", false, "", ""));
                return;
            }
            if (!d.RealTimeProtection || !d.AntivirusEnabled)
            {
                if (other != null) r.Add(Finding.Ok("AV_OTHER", other.Name + " gerçek zamanlı koruma sağlıyor", other.Name + " provides real-time protection"));
                else r.Add(Finding.Issue("DEF_RTP_OFF", Severity.High, "Gerçek zamanlı koruma kapalı", "Real-time protection is off", "Defender dosyaları açılırken taramıyor ve başka etkin bir antivirüs yok.", "Defender is not scanning files as they open and no other antivirus is active.")
                    .Recommend("Windows Güvenliği'nden gerçek zamanlı korumayı aç.", "Turn real-time protection back on in Windows Security.")
                    .WithFix(FixKinds.Defender, "windowsdefender://threatsettings", false, "", ""));
                return;
            }
            r.Add(Finding.Ok("DEF_RTP_ON", "Defender gerçek zamanlı koruması açık", "Defender real-time protection is on"));
            if (d.SignatureAgeDays > 7)
                r.Add(Finding.Issue("DEF_SIG_OLD", Severity.Medium, "Virüs tanımları " + d.SignatureAgeDays + " gün eski", "Virus definitions are " + d.SignatureAgeDays + " days old", "Eski tanımlar yeni tehditleri tanımayabilir.", "Old definitions may miss new threats.")
                    .Recommend("Windows Güvenliği'nde \"Güncelleştirmeleri denetle\".", "Use \"Check for updates\" in Windows Security.")
                    .WithFix(FixKinds.Defender, "windowsdefender://threat", false, "", ""));
            else if (d.SignatureAgeDays >= 0) r.Add(Finding.Ok("DEF_SIG_OK", "Virüs tanımları güncel", "Virus definitions are current"));
            if (!d.TamperProtected)
                r.Add(Finding.Issue("DEF_TAMPER_OFF", Severity.Low, "Kurcalama koruması kapalı", "Tamper Protection is off", "Kötü amaçlı yazılımlar Defender ayarlarını değiştirebilir.", "Malware could change Defender settings.")
                    .WithFix(FixKinds.Defender, "windowsdefender://threatsettings", false, "", ""));
            else r.Add(Finding.Ok("DEF_TAMPER_ON", "Kurcalama koruması açık", "Tamper Protection is on"));
        }

        private static void CheckUac(ModuleResult r)
        {
            int lua = ReadDword(Registry.LocalMachine, SystemPolicies, "EnableLUA", 1);
            if (lua == 0)
            {
                r.Add(Finding.Issue("UAC_OFF", Severity.High, "Kullanıcı Hesabı Denetimi (UAC) kapalı", "User Account Control (UAC) is off", "Her program sormadan yönetici yetkisiyle çalışabilir.", "Every program can run with administrator rights without asking.")
                    .Recommend("UAC'yi aç ve bilgisayarı yeniden başlat.", "Turn UAC on and restart the PC.")
                    .WithFix(FixKinds.Command, "UAC_ON", true, "HKLM\\...\\Policies\\System\\EnableLUA değeri 1 yapılacak. Etki için yeniden başlatma gerekir. Geri almak için değer tekrar 0 yapılabilir (önerilmez).", "HKLM\\...\\Policies\\System\\EnableLUA will be set to 1. A restart is required. To undo, set it back to 0 (not recommended)."));
                return;
            }
            int consent = ReadDword(Registry.LocalMachine, SystemPolicies, "ConsentPromptBehaviorAdmin", 5);
            if (consent == 0)
                r.Add(Finding.Issue("UAC_NEVER", Severity.Medium, "UAC hiç bildirimde bulunmuyor", "UAC never notifies", "Yönetici hesabında yükseltme istekleri sessizce onaylanıyor.", "Elevation requests are approved silently for administrators.")
                    .WithFix(FixKinds.Command, "UAC_PROMPT", true, "ConsentPromptBehaviorAdmin değeri Windows varsayılanı olan 5 yapılacak (yalnızca uygulamalar değişiklik yapmaya çalıştığında bildir). Geri almak için değer 0 yapılabilir.", "ConsentPromptBehaviorAdmin will be set to the Windows default 5 (notify only when apps try to make changes). To undo, set it back to 0."));
            else r.Add(Finding.Ok("UAC_ON", "Kullanıcı Hesabı Denetimi açık", "User Account Control is on"));
        }

        private static void CheckBitLocker(ModuleResult r, KeyValues kv)
        {
            string v = kv.Get("BL");
            bool home = IsHomeEdition();
            if (v == "1" || v == "3" || v == "6") { r.Add(Finding.Ok("BL_ON", "Sistem sürücüsü şifreli (BitLocker)", "System drive is encrypted (BitLocker)")); return; }
            if (v == "5")
            {
                r.Add(Finding.Issue("BL_SUSPENDED", Severity.Low, "BitLocker askıya alınmış", "BitLocker is suspended", "Şifreleme var ama koruma şu an askıda (genelde bir güncelleme sonrası).", "Encryption exists but protection is suspended (usually after an update).")
                    .WithFix(FixKinds.OpenSettings, "ms-settings:deviceencryption", false, "", ""));
                return;
            }
            if (v == "2")
            {
                r.Add(Finding.Issue("BL_OFF", Severity.Medium, "Sistem sürücüsü şifreli değil", "System drive is not encrypted", "Bilgisayar kaybolur veya çalınırsa diskteki dosyalar okunabilir.", "If the PC is lost or stolen, files on the disk can be read.")
                    .Recommend("BitLocker / Cihaz şifrelemeyi aç; kurtarma anahtarını güvenli bir yere kaydet.", "Turn on BitLocker / Device encryption and store the recovery key somewhere safe.")
                    .WithFix(FixKinds.OpenSettings, "ms-settings:deviceencryption", false, "", ""));
                return;
            }
            r.Add(Finding.Issue("BL_UNKNOWN", Severity.Info, home ? "Bu sürümde BitLocker yok" : "Disk şifreleme durumu doğrulanamadı", home ? "BitLocker is not available on this edition" : "Drive encryption state could not be verified",
                home ? "Windows Home'da yalnızca destekleyen cihazlarda \"Cihaz şifreleme\" bulunur." : "Sistem sürücüsünün şifreleme bilgisi okunamadı.", home ? "Windows Home only offers \"Device encryption\" on supported hardware." : "The system drive's encryption state could not be read.")
                .WithFix(FixKinds.OpenSettings, "ms-settings:deviceencryption", false, "", ""));
        }

        private static void CheckSmb1(ModuleResult r, KeyValues kv, bool admin)
        {
            bool? enabled = null;
            string state = kv.Get("SMB1");
            if (state != null) enabled = state.Equals("Enabled", StringComparison.OrdinalIgnoreCase) || state.Equals("EnablePending", StringComparison.OrdinalIgnoreCase);
            else
            {
                // Standard mode: the SMB1 client driver only exists (and is not disabled) when the feature is installed.
                int start = ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\mrxsmb10", "Start", -1);
                int server = ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", "SMB1", -1);
                enabled = (start >= 0 && start != 4) || server == 1;
            }
            if (enabled == true)
                r.Add(Finding.Issue("SMB1_ON", Severity.High, "SMBv1 açık", "SMBv1 is enabled", "Eski dosya paylaşım protokolü; WannaCry gibi fidye yazılımlarının kullandığı bir giriş.", "Legacy file-sharing protocol abused by ransomware such as WannaCry.")
                    .Recommend("Çok eski bir NAS veya yazıcı gerektirmiyorsa kapat.", "Turn it off unless a very old NAS or printer needs it.")
                    .WithFix(FixKinds.Command, "SMB1_OFF", true, "Windows özelliği SMB1Protocol kaldırılacak (Disable-WindowsOptionalFeature, yeniden başlatma sonrası tamamlanır). Geri almak için \"Windows özelliklerini aç veya kapat\" → SMB 1.0/CIFS.", "The SMB1Protocol Windows feature will be removed (Disable-WindowsOptionalFeature, completes after a restart). To undo: \"Turn Windows features on or off\" → SMB 1.0/CIFS."));
            else r.Add(Finding.Ok("SMB1_OFF", "SMBv1 kapalı", "SMBv1 is disabled"));
        }

        private static void CheckRdp(ModuleResult r)
        {
            int deny = ReadDword(Registry.LocalMachine, TerminalServer, "fDenyTSConnections", 1);
            if (deny != 0) { r.Add(Finding.Ok("RDP_OFF", "Uzak Masaüstü kapalı", "Remote Desktop is off")); return; }
            bool nla = ReadDword(Registry.LocalMachine, TerminalServer + @"\WinStations\RDP-Tcp", "UserAuthentication", 1) == 1;
            r.Add(Finding.Issue("RDP_ON", Severity.Medium, "Uzak Masaüstü açık", "Remote Desktop is on",
                    "Bu bilgisayara ağ üzerinden oturum açılabilir" + (nla ? "." : "; üstelik Ağ Düzeyinde Kimlik Doğrulama kapalı."), "This PC accepts remote sign-ins over the network" + (nla ? "." : ", and Network Level Authentication is off."))
                .Recommend("Bilerek kullanmıyorsan kapat; kullanıyorsan güçlü parola ve NLA kullan.", "Turn it off if you don't use it on purpose; if you do, use a strong password and NLA.")
                .WithFix(FixKinds.Command, "RDP_OFF", true, "fDenyTSConnections değeri 1 yapılarak Uzak Masaüstü kapatılacak. Geri almak için Ayarlar → Sistem → Uzak Masaüstü.", "Remote Desktop will be turned off by setting fDenyTSConnections to 1. To undo: Settings → System → Remote Desktop."));
        }

        private static void CheckSecureBoot(ModuleResult r)
        {
            int v = ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled", -1);
            if (v == 1) r.Add(Finding.Ok("SB_ON", "Secure Boot açık", "Secure Boot is on"));
            else if (v == 0)
                r.Add(Finding.Issue("SB_OFF", Severity.Low, "Secure Boot kapalı", "Secure Boot is off", "Açılışta yüklenen bileşenlerin imzası doğrulanmıyor (bootkit riskine karşı koruma yok).", "Boot components are not signature-checked (no protection against bootkits).")
                    .Recommend("Başka bir işletim sistemi için kapatmadıysan UEFI/BIOS ayarlarından aç.", "Turn it on in UEFI/BIOS settings unless you disabled it for another OS."));
            else r.Add(Finding.Issue("SB_UNKNOWN", Severity.Info, "Secure Boot doğrulanamadı", "Secure Boot could not be verified", "Bu cihaz eski (Legacy) BIOS modunda olabilir.", "This device may be using legacy BIOS mode."));
        }

        private static void CheckMemoryIntegrity(ModuleResult r)
        {
            int v = ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0);
            if (v == 1) r.Add(Finding.Ok("HVCI_ON", "Bellek bütünlüğü (Çekirdek yalıtımı) açık", "Memory integrity (Core isolation) is on"));
            else r.Add(Finding.Issue("HVCI_OFF", Severity.Low, "Bellek bütünlüğü kapalı", "Memory integrity is off", "Çekirdeğe kötü amaçlı sürücü yüklenmesine karşı ek koruma devre dışı.", "Extra protection against malicious drivers in the kernel is disabled.")
                .Recommend("Uyumsuz sürücü uyarısı yoksa Çekirdek yalıtımından aç (yeniden başlatma gerekir).", "Turn it on in Core isolation if no incompatible drivers are listed (restart required).")
                .WithFix(FixKinds.Defender, "windowsdefender://coreisolation", false, "", ""));
        }

        private static void CheckWindowsUpdateService(ModuleResult r)
        {
            int start = ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\wuauserv", "Start", -1);
            if (start == 4)
                r.Add(Finding.Issue("WU_DISABLED", Severity.Medium, "Windows Update hizmeti devre dışı", "Windows Update service is disabled", "Güvenlik yamaları indirilemez.", "Security patches cannot be downloaded.")
                    .WithFix(FixKinds.Command, "WU_ENABLE", true, "wuauserv hizmetinin başlangıç türü Windows varsayılanı olan \"El ile (tetiklenen)\" yapılacak (sc config wuauserv start= demand). Geri almak için Hizmetler'den Devre dışı seçilebilir.", "The wuauserv service start type will be set to the Windows default \"Manual (trigger)\" (sc config wuauserv start= demand). To undo, set it to Disabled in Services."));
            else if (start >= 0) r.Add(Finding.Ok("WU_OK", "Windows Update hizmeti çalışabilir durumda", "Windows Update service is available"));
        }

        private static void CheckAutoplay(ModuleResult r)
        {
            int disabled = ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 0);
            int policy = ReadDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 0);
            if (disabled == 1 || policy == 0xFF) { r.Add(Finding.Ok("AUTOPLAY_OFF", "Otomatik Kullan kapalı", "AutoPlay is off")); return; }
            r.Add(Finding.Issue("AUTOPLAY_ON", Severity.Low, "Otomatik Kullan açık", "AutoPlay is on", "Takılan USB bellek veya disk için Windows hemen bir işlem önerebilir; dikkatsiz bir tıklama içeriği çalıştırabilir.", "Windows offers an action as soon as a USB stick or disc is inserted; a careless click can run its content.")
                .WithFix(FixKinds.Command, "AUTOPLAY_OFF", false, "Yalnızca senin hesabın için Otomatik Kullan kapatılacak (HKCU\\...\\AutoplayHandlers\\DisableAutoplay = 1). Geri almak için Ayarlar → Bluetooth ve cihazlar → Otomatik Kullan.", "AutoPlay will be turned off for your account only (HKCU\\...\\AutoplayHandlers\\DisableAutoplay = 1). To undo: Settings → Bluetooth & devices → AutoPlay."));
        }

        private static void CheckBuiltinAdmin(ModuleResult r, KeyValues kv)
        {
            string v = kv.Get("ADMIN500");
            if (v == null) return;
            if (string.Equals(v, "False", StringComparison.OrdinalIgnoreCase))
                r.Add(Finding.Issue("ADMIN500_ON", Severity.Low, "Yerleşik Administrator hesabı etkin", "Built-in Administrator account is enabled", "Bu hesap UAC olmadan tam yetkiyle çalışır ve saldırganların ilk denediği hesaptır.", "This account runs with full rights without UAC and is the first one attackers try.")
                    .Recommend("Kullanmıyorsan devre dışı bırak.", "Disable it if you don't use it.")
                    .WithFix(FixKinds.Command, "ADMIN500_OFF", true, "SID'i -500 ile biten yerleşik Administrator hesabı devre dışı bırakılacak (silinmez). Geri almak için Enable-LocalUser veya Bilgisayar Yönetimi → Yerel Kullanıcılar.", "The built-in Administrator account (SID ending in -500) will be disabled, not deleted. To undo: Enable-LocalUser or Computer Management → Local Users."));
            else r.Add(Finding.Ok("ADMIN500_OFF", "Yerleşik Administrator hesabı devre dışı", "Built-in Administrator account is disabled"));
        }

        // ---------- helpers ----------

        private static int ReadDword(RegistryKey hive, string path, string name, int fallback)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive == Registry.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey k = baseKey.OpenSubKey(path))
                {
                    object v = k == null ? null : k.GetValue(name);
                    return v is int ? (int)v : fallback;
                }
            }
            catch { return fallback; }
        }

        private static CommandResult SetDword(RegistryKey hive, string path, string name, int value)
        {
            CommandResult r = new CommandResult { Started = true };
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive == Registry.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey k = baseKey.CreateSubKey(path)) k.SetValue(name, value, RegistryValueKind.DWord);
                r.ExitCode = 0;
            }
            catch (Exception ex) { r.Error = ex.Message; }
            return r;
        }

        private static bool IsHomeEdition()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    return k != null && Convert.ToString(k.GetValue("EditionID")).StartsWith("Core", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool IsAdmin()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } catch { return false; }
        }
    }
}
