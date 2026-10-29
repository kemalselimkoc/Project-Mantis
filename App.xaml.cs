// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Threading;
using System.Windows;

namespace ProjectMantis
{
    public partial class App : Application
    {
        private Mutex instanceMutex;

        // Session-only language from --lang=tr|en (used for screenshots); never written to settings.ini.
        internal static string LanguageOverride;
        internal static double SnapshotWait = 1.6;
        // Set by --self-test / --snapshot / --run so unattended developer runs never block on a dialog.
        internal static bool DevAutomation;

        internal static void Log(string message)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                Log("Main entered");
                App app = new App();
                if (args != null)
                    foreach (string arg in args)
                    {
                        if (arg == "--lang=en" || arg == "--lang=tr") LanguageOverride = arg.Substring(7);
                        if (arg.StartsWith("--terms-snapshot=", StringComparison.OrdinalIgnoreCase)) { Terms.Snapshot(arg.Substring(17), LanguageOverride != "en"); Environment.Exit(0); }
                        if (arg == "--self-test" || arg.StartsWith("--snapshot=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--run=", StringComparison.OrdinalIgnoreCase)) DevAutomation = true;
                    }
                if (RunSetupModes(app, args)) Environment.Exit(0);
                if (!DevAutomation && !Terms.EnsureAccepted()) { Log("Terms declined"); Environment.Exit(0); }
                MainWindow window = new MainWindow();
                Log("MainWindow constructed");
                if (args != null && Array.IndexOf(args, "--self-test") >= 0)
                {
                    window.SelfTestClose();
                    Environment.Exit(0);
                }
                if (args != null)
                    foreach (string arg in args)
                    {
                        if (arg.StartsWith("--page=", StringComparison.OrdinalIgnoreCase)) window.OpenPage(arg.Substring(7).ToLowerInvariant());
                        else if (arg.StartsWith("--snapshot=", StringComparison.OrdinalIgnoreCase)) window.SnapshotAndExit(arg.Substring(11));
                        else if (arg.StartsWith("--run=", StringComparison.OrdinalIgnoreCase)) window.AutoRun(arg.Substring(6));
                        else if (arg.StartsWith("--wait=", StringComparison.OrdinalIgnoreCase)) double.TryParse(arg.Substring(7), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out SnapshotWait);
                        else if (arg.StartsWith("--size=", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] wh = arg.Substring(7).Split('x');
                            double w, h;
                            if (wh.Length == 2 && double.TryParse(wh[0], out w) && double.TryParse(wh[1], out h)) { window.Width = Math.Max(window.MinWidth, w); window.Height = Math.Max(window.MinHeight, h); }
                        }
                    }
                Log("Application.Run starting");
                app.Run(window);
                Log("Application.Run returned");
            }
            catch (Exception ex)
            {
                Log("FATAL " + ex);
                try
                {
                    string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectMantis");
                    System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine);
                }
                catch { }
                Environment.Exit(1);
            }
        }

        // Setup (exe named *Setup*.exe or --install), uninstall (--uninstall) and the setup snapshot dev aid
        // run their own window and exit; they never start the main app or take the single-instance mutex.
        private static bool RunSetupModes(App app, string[] args)
        {
            string saved = System.IO.File.Exists(System.IO.Path.Combine(Settings.DataDir, "settings.ini")) ? Settings.Load().Language : null;
            bool tr = (LanguageOverride ?? saved ?? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en")) == "tr";
            string snapshot = null; int step = 0;
            foreach (string arg in args ?? new string[0])
            {
                if (arg.StartsWith("--setup-snapshot=", StringComparison.OrdinalIgnoreCase)) snapshot = arg.Substring(17);
                else if (arg.StartsWith("--setup-step=", StringComparison.OrdinalIgnoreCase)) int.TryParse(arg.Substring(13), out step);
            }
            bool uninstall = Installer.IsUninstallRun(args), setup = !uninstall && Installer.IsSetupRun(args);
            if (snapshot == null && !uninstall && !setup) return false;
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (snapshot != null) SetupWizard.Snapshot(snapshot, tr, step);
            else if (uninstall) UninstallUi.Run(tr);
            else new SetupWizard(tr).ShowDialog();
            return true;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            bool created;
            instanceMutex = new Mutex(true, "ProjectMantis.SingleInstance.5", out created);
            // An elevated restart starts while the previous instance is still shutting down; wait for it briefly.
            if (!created && Array.IndexOf(Environment.GetCommandLineArgs(), "--elevated") >= 0)
            {
                try { created = instanceMutex.WaitOne(8000); } catch (AbandonedMutexException) { created = true; }
            }
            Log("OnStartup mutex created=" + created);
            if (!created)
            {
                MessageBox.Show("Project Mantis is already running.", "Project Mantis");
                Shutdown();
                return;
            }
            base.OnStartup(e);
        }
    }
}
