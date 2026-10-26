// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ProjectMantis
{
    internal sealed class WindowsUpdateInfo
    {
        public bool Failed;
        public string Error;
        public int Pending;
        public int Important;           // security or critical category, or MSRC severity Critical/Important
        public bool RebootRequired;
        public DateTime? LastSearch;
        public DateTime When = DateTime.Now;
        public List<string> Titles = new List<string>();
    }

    // Reads pending Windows updates through the built-in Windows Update Agent COM API (read-only:
    // it searches, it never downloads or installs). Late-bound, so no interop assembly is needed.
    internal static class WindowsUpdateStatus
    {
        private const string SecurityUpdates = "0fa1201d-4330-4fa8-8ae9-b877473b6441";
        private const string CriticalUpdates = "e6cf1350-c01b-414d-a61f-263d14d133b4";

        public static WindowsUpdateInfo Read()
        {
            WindowsUpdateInfo info = new WindowsUpdateInfo();
            info.RebootRequired = RebootPending();
            info.LastSearch = LastSuccessfulSearch();
            List<object> com = new List<object>();
            try
            {
                Type t = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (t == null) throw new InvalidOperationException("Windows Update Agent is not available.");
                object session = Track(com, Activator.CreateInstance(t));
                object searcher = Track(com, Call(session, "CreateUpdateSearcher"));
                object result = Track(com, Call(searcher, "Search", "IsInstalled=0 and IsHidden=0 and Type='Software'"));
                object updates = Track(com, Get(result, "Updates"));
                int count = Convert.ToInt32(Get(updates, "Count"));
                info.Pending = count;
                for (int i = 0; i < count; i++)
                {
                    object u = Track(com, Get(updates, "Item", i));
                    string title = Convert.ToString(Get(u, "Title"));
                    if (!string.IsNullOrEmpty(title)) info.Titles.Add(title);
                    string severity = Convert.ToString(Get(u, "MsrcSeverity"));
                    bool important = severity == "Critical" || severity == "Important";
                    object cats = Track(com, Get(u, "Categories"));
                    int catCount = Convert.ToInt32(Get(cats, "Count"));
                    for (int j = 0; j < catCount && !important; j++)
                    {
                        string id = Convert.ToString(Get(Track(com, Get(cats, "Item", j)), "CategoryID"));
                        important = string.Equals(id, SecurityUpdates, StringComparison.OrdinalIgnoreCase) || string.Equals(id, CriticalUpdates, StringComparison.OrdinalIgnoreCase);
                    }
                    if (important) info.Important++;
                }
            }
            catch (Exception ex)
            {
                Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                info.Failed = true;
                info.Error = inner is COMException ? "0x" + ((COMException)inner).ErrorCode.ToString("X8") : inner.Message;
            }
            finally
            {
                for (int i = com.Count - 1; i >= 0; i--) try { if (com[i] != null && Marshal.IsComObject(com[i])) Marshal.FinalReleaseComObject(com[i]); } catch { }
            }
            info.When = DateTime.Now;
            return info;
        }

        private static bool RebootPending()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")) if (k != null) return true;
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending")) if (k != null) return true;
            }
            catch { }
            return false;
        }

        private static DateTime? LastSuccessfulSearch()
        {
            object auto = null, results = null;
            try
            {
                Type t = Type.GetTypeFromProgID("Microsoft.Update.AutoUpdate");
                if (t == null) return null;
                auto = Activator.CreateInstance(t);
                results = Get(auto, "Results");
                object value = Get(results, "LastSearchSuccessDate");
                if (value is DateTime && ((DateTime)value).Year > 2000) return ((DateTime)value).ToLocalTime();
            }
            catch { }
            finally
            {
                try { if (results != null && Marshal.IsComObject(results)) Marshal.FinalReleaseComObject(results); } catch { }
                try { if (auto != null && Marshal.IsComObject(auto)) Marshal.FinalReleaseComObject(auto); } catch { }
            }
            return null;
        }

        private static object Track(List<object> list, object o) { list.Add(o); return o; }
        private static object Call(object target, string name, params object[] args) { return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args); }
        private static object Get(object target, string name, params object[] args) { return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, args); }
    }
}
