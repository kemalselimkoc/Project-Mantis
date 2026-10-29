// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System.Windows;

namespace ProjectMantis
{
    // One-time notice shown before first use (and again whenever Version changes): the user accepts the
    // MIT "as is" terms and the responsibilities that come with changing their own Windows. Declining exits.
    internal static class Terms
    {
        public const string Version = "1";

        public static bool EnsureAccepted()
        {
            Settings s = Settings.Load();
            if (s.TermsAccepted == Version) return true;
            bool tr = (App.LanguageOverride ?? s.Language) != "en";
            string text = tr
                ? "Project Mantis'i kullanmadan önce lütfen okuyun.\n\n" +
                  "• Ücretsiz, açık kaynaklı (MIT Lisansı) bir yazılımdır ve \"OLDUĞU GİBİ\", hiçbir garanti olmadan sunulur. Yasaların izin verdiği ölçüde, kullanımından doğan veri kaybı, arıza veya diğer zararlardan geliştirici sorumlu tutulamaz. Tüketici olarak yasal zorunlu haklarınız saklıdır.\n" +
                  "• Mantis yalnızca sizin bilgisayarınızda ve sizin onayınızla değişiklik yapar. Değişiklikten önce önemli dosyalarınızı yedekleyin; mümkünse bir sistem geri yükleme noktası oluşturun.\n" +
                  "• Yerel ağ taramasını yalnızca size ait ya da taramak için izin aldığınız ağlarda kullanın.\n" +
                  "• Güvenlik Check-Up'ı bir antivirüs değildir; dosya silmez, Windows Defender'a yönlendirir. Sonuçlar tavsiye niteliğindedir.\n" +
                  "• Uygulama güncellemeleri winget üzerinden, yayıncıların kendi lisanslarıyla yüklenir. Google Translate ve api.ipify.org yalnızca siz tıkladığınızda ve kendi koşullarıyla kullanılır.\n" +
                  "• Mantis telemetri toplamaz; hiçbir kişisel veriyi geliştiriciye göndermez.\n" +
                  "• Microsoft ile bağlantılı değildir. Windows, Microsoft Defender ve Xbox, Microsoft Corporation'ın ticari markalarıdır.\n\n" +
                  "Ayrıntılar: LICENSE, EULA.txt ve PRIVACY.md (GitHub deposunda).\n\n" +
                  "Bu koşulları kabul ediyor musunuz?"
                : "Please read before using Project Mantis.\n\n" +
                  "• It is free, open-source software (MIT License) provided \"AS IS\", without warranty of any kind. To the extent permitted by law, the developer is not liable for data loss, malfunction or other damages arising from its use. Your mandatory consumer rights are not affected.\n" +
                  "• Mantis changes only your own computer, and only after you confirm. Back up important files before making changes and, where possible, create a System Restore point.\n" +
                  "• Use the local network scan only on networks you own or have permission to test.\n" +
                  "• The Security Check-Up is not an antivirus; it never deletes files and points you to Microsoft Defender. Its results are advisory.\n" +
                  "• App updates are installed through winget under each publisher's own license. Google Translate and api.ipify.org are used only when you click, under their own terms.\n" +
                  "• Mantis collects no telemetry and sends no personal data to the developer.\n" +
                  "• Not affiliated with Microsoft. Windows, Microsoft Defender and Xbox are trademarks of Microsoft Corporation.\n\n" +
                  "Details: LICENSE, EULA.txt and PRIVACY.md (in the GitHub repository).\n\n" +
                  "Do you accept these terms?";
            MessageBoxResult answer = MessageBox.Show(text, tr ? "Project Mantis — Kullanım koşulları" : "Project Mantis — Terms of use", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return false;
            s.TermsAccepted = Version;
            s.Save();
            return true;
        }
    }
}
