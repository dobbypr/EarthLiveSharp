using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EarthLiveSharp.Core
{
    /// <summary>
    /// Manages application autostart.
    /// On Linux this creates/removes an XDG autostart .desktop file.
    /// On Windows this writes to the CurrentUser\Run registry key.
    /// </summary>
    public static class AutostartManager
    {
        private const string AppName = "EarthLiveSharp";

        /// <summary>
        /// Enables or disables autostart for the current executable.
        /// </summary>
        /// <param name="enabled">true to enable, false to disable.</param>
        /// <param name="executablePath">
        /// Absolute path to the executable.  Defaults to the current process path.
        /// </param>
        /// <returns>true if the operation succeeded.</returns>
        public static bool Set(bool enabled, string? executablePath = null)
        {
            executablePath ??= Environment.ProcessPath ?? AppContext.BaseDirectory;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return SetLinux(enabled, executablePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return SetWindows(enabled, executablePath);
            }
            else
            {
                Trace.WriteLine("[Autostart] unsupported platform");
                return false;
            }
        }

        // -----------------------------------------------------------------
        // Linux — XDG autostart
        // -----------------------------------------------------------------
        private static bool SetLinux(bool enabled, string executablePath)
        {
            string autostartDir = Path.Combine(
                Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
                "autostart");
            string desktopFilePath = Path.Combine(autostartDir, $"{AppName}.desktop");

            try
            {
                if (enabled)
                {
                    Directory.CreateDirectory(autostartDir);
                    string content = $"""
                        [Desktop Entry]
                        Type=Application
                        Name={AppName}
                        Exec={executablePath}
                        Hidden=false
                        NoDisplay=false
                        X-GNOME-Autostart-enabled=true
                        Comment=Live satellite imagery wallpaper
                        """;
                    File.WriteAllText(desktopFilePath, content);
                    Trace.WriteLine($"[Autostart] created {desktopFilePath}");
                }
                else
                {
                    if (File.Exists(desktopFilePath))
                    {
                        File.Delete(desktopFilePath);
                        Trace.WriteLine($"[Autostart] removed {desktopFilePath}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Autostart] Linux autostart failed: {ex.Message}");
                return false;
            }
        }

        // -----------------------------------------------------------------
        // Windows — Registry HKCU\Run
        // -----------------------------------------------------------------
        [SupportedOSPlatform("windows")]
        private static bool SetWindows(bool enabled, string executablePath)
        {
            const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
                if (runKey == null)
                {
                    Trace.WriteLine("[Autostart] Windows registry key not found");
                    return false;
                }

                if (enabled)
                {
                    runKey.SetValue(AppName, executablePath);
                }
                else
                {
                    try { runKey.DeleteValue(AppName); } catch { /* key may not exist */ }
                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Autostart] Windows autostart failed: {ex.Message}");
                return false;
            }
        }
    }
}
