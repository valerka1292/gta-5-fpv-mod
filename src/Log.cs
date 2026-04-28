using System;
using System.IO;

namespace FpvDroneMod
{
    // Minimal append-only logger so silent catch{} blocks become visible.
    // Writes to scripts/FpvDroneMod.log alongside the DLL. Failures inside
    // the logger itself are swallowed (we can't log a logging failure).
    //
    // Audit #4 fix: empty catches were "swallowing failures" of TimeScale
    // restore and camera cleanup. Now the catch blocks at least leave a
    // trail in the log file that the user/dev can grep through after a
    // session if something locked up.
    internal static class Log
    {
        private static readonly object _lock = new object();
        private static string _path;

        public static void Init()
        {
            try
            {
                string dir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? Directory.GetCurrentDirectory();
                _path = Path.Combine(dir, "FpvDroneMod.log");
                // Truncate at start of session so the file doesn't grow
                // unbounded across many launches.
                File.WriteAllText(_path,
                    $"[{DateTime.Now:HH:mm:ss}] FPV Drone Mod log opened\r\n");
            }
            catch
            {
                _path = null;
            }
        }

        public static void Info(string msg)  => Write("INFO ", msg, null);
        public static void Warn(string msg)  => Write("WARN ", msg, null);
        public static void Error(string msg, Exception ex = null)
            => Write("ERROR", msg, ex);

        private static void Write(string level, string msg, Exception ex)
        {
            if (_path == null) return;
            try
            {
                lock (_lock)
                {
                    string line = $"[{DateTime.Now:HH:mm:ss}] {level} {msg}";
                    if (ex != null)
                        line += $" :: {ex.GetType().Name}: {ex.Message}";
                    File.AppendAllText(_path, line + "\r\n");
                }
            }
            catch
            {
                // Logger failures are non-fatal; ignore.
            }
        }
    }
}
