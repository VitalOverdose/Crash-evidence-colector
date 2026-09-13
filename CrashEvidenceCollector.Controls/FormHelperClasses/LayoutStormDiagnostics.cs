using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.FormHelperClasses
{
    internal static class LayoutStormDiagnostics
    {
#if DEBUG
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        private static readonly Stopwatch Window = Stopwatch.StartNew();
        private static int _inFlush;

        public static bool Enabled { get; set; }

        public static void Hit(string owner, string evt)
        {
            if (!Enabled)
                return;

            lock (SyncRoot)
            {
                string key = $"{owner}.{evt}";
                Counts.TryGetValue(key, out int count);
                Counts[key] = count + 1;

                if (Window.ElapsedMilliseconds >= 1000)
                    FlushLocked();
            }
        }

        public static string NameOf(Control control)
        {
            if (!Enabled)
                return string.Empty;

            string typeName = control.GetType().Name;
            string controlName = string.IsNullOrEmpty(control.Name) ? "(unnamed)" : control.Name;
            string ownName = $"{typeName}:{controlName}";

            List<string> parents = new List<string>();
            Control? current = control.Parent;

            while (current != null && parents.Count < 8)
            {
                string name = string.IsNullOrEmpty(current.Name) ? "(unnamed)" : current.Name;
                parents.Add($"{current.GetType().Name}:{name}");
                current = current.Parent;
            }

            parents.Reverse();
            return parents.Count == 0
                ? ownName
                : $"{string.Join(" > ", parents)} > {ownName}";
        }

        private static void FlushLocked()
        {
            if (Counts.Count == 0 || Interlocked.Exchange(ref _inFlush, 1) == 1)
                return;

            try
            {
                var snapshot = Counts
                    .OrderByDescending(pair => pair.Value)
                    .Take(25)
                    .ToList();

                Debug.WriteLine("");
                Debug.WriteLine($"[LayoutStorm] {Window.ElapsedMilliseconds}ms window, top event counts:");
                foreach (var pair in snapshot)
                    Debug.WriteLine($"[LayoutStorm] {pair.Value,5}  {pair.Key}");

                Counts.Clear();
                Window.Restart();
            }
            finally
            {
                Interlocked.Exchange(ref _inFlush, 0);
            }
        }
#else
        public static bool Enabled { get; set; }
        public static void Hit(string owner, string evt) { }
#endif
    }
}
