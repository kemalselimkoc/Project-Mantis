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
                        if (arg == "--lang=en" || arg == "--lang=tr") LanguageOverride = arg.Substring(7);
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
