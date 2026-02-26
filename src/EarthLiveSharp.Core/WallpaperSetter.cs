using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace EarthLiveSharp.Core
{
    /// <summary>
    /// Sets the desktop wallpaper in a cross-platform manner.
    /// On Linux the correct mechanism is selected based on the
    /// <c>XDG_CURRENT_DESKTOP</c> / <c>DESKTOP_SESSION</c> environment variables.
    /// On Windows the legacy Win32 <c>SystemParametersInfo</c> API is used.
    /// </summary>
    public static class WallpaperSetter
    {
        public static void Set(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                Trace.WriteLine($"[WallpaperSetter] file not found: {imagePath}");
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetWindows(imagePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SetLinux(imagePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetMacOS(imagePath);
            }
            else
            {
                Trace.WriteLine("[WallpaperSetter] unsupported platform");
            }
        }

        // -----------------------------------------------------------------
        // Linux
        // -----------------------------------------------------------------
        private static void SetLinux(string imagePath)
        {
            string de = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty).ToUpperInvariant();
            string session = (Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? string.Empty).ToUpperInvariant();
            string fileUri = $"file://{imagePath}";

            Trace.WriteLine($"[WallpaperSetter] XDG_CURRENT_DESKTOP={de}  DESKTOP_SESSION={session}");

            if (de.Contains("GNOME") || de.Contains("UNITY") || de.Contains("UBUNTU") ||
                session.Contains("GNOME") || session.Contains("UBUNTU"))
            {
                RunCommand("gsettings", $"set org.gnome.desktop.background picture-uri '{fileUri}'");
                RunCommand("gsettings", $"set org.gnome.desktop.background picture-uri-dark '{fileUri}'");
            }
            else if (de.Contains("CINNAMON") || session.Contains("CINNAMON"))
            {
                RunCommand("gsettings", $"set org.cinnamon.desktop.background picture-uri '{fileUri}'");
            }
            else if (de.Contains("MATE") || session.Contains("MATE"))
            {
                RunCommand("gsettings", $"set org.mate.background picture-filename '{imagePath}'");
            }
            else if (de.Contains("XFCE") || session.Contains("XFCE"))
            {
                RunCommand("xfconf-query",
                    $"-c xfce4-desktop -p /backdrop/screen0/monitor0/workspace0/last-image -s \"{imagePath}\"");
            }
            else if (de.Contains("KDE") || session.Contains("KDE") || session.Contains("PLASMA"))
            {
                // plasma-apply-wallpaperimage is available since Plasma 5.18
                RunCommand("plasma-apply-wallpaperimage", imagePath);
            }
            else if (de.Contains("SWAY") || session.Contains("SWAY"))
            {
                // swaybg is typically managed via the config; reload is needed
                RunCommand("swaymsg", $"output '*' bg '{imagePath}' fill");
            }
            else if (de.Contains("HYPRLAND") || session.Contains("HYPRLAND"))
            {
                RunCommand("hyprctl", $"hyprpaper wallpaper ,{imagePath}");
            }
            else
            {
                // Generic X11 fallback using feh
                RunCommand("feh", $"--bg-fill \"{imagePath}\"");
            }
        }

        // -----------------------------------------------------------------
        // macOS
        // -----------------------------------------------------------------
        private static void SetMacOS(string imagePath)
        {
            string script = $"tell application \"Finder\" to set desktop picture to POSIX file \"{imagePath}\"";
            RunCommand("osascript", $"-e '{script}'");
        }

        // -----------------------------------------------------------------
        // Windows (P/Invoke kept in its own region so the rest of the class
        // has no Windows dependencies at compile time)
        // -----------------------------------------------------------------
#if WINDOWS
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        private const int SPI_SETDESKWALLPAPER = 20;
#endif

        private static void SetWindows(string imagePath)
        {
#if WINDOWS
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, 1);
#else
            // On non-Windows builds without the conditional, use reg.exe as fallback
            RunCommand("reg", $"add \"HKCU\\Control Panel\\Desktop\" /v Wallpaper /t REG_SZ /d \"{imagePath}\" /f");
            RunCommand("RUNDLL32.EXE", "user32.dll,UpdatePerUserSystemParameters");
#endif
        }

        private static void RunCommand(string command, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo(command, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
                if (proc != null && !proc.HasExited)
                {
                    try { proc.Kill(); } catch { /* best-effort */ }
                }
                Trace.WriteLine($"[WallpaperSetter] {command} {arguments} → exit {proc?.ExitCode}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WallpaperSetter] failed to run '{command}': {ex.Message}");
            }
        }
    }
}
