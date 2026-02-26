using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using EarthLiveSharp.Core;
using EarthLiveSharp.Core.Satellites;

namespace EarthLiveSharp.Cli
{
    /// <summary>
    /// CLI daemon entry point.  Runs as a long-lived background process that
    /// periodically fetches satellite imagery and sets it as the desktop wallpaper.
    /// </summary>
    internal static class Program
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private static int _sequenceCount = 0;
        private static string _lastImageId = string.Empty;

        static async Task<int> Main(string[] args)
        {
            // Configure tracing to a log file in the XDG cache dir
            string logDir = Path.GetDirectoryName(Config.GetDefaultImageFolder()) ?? ".";
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, "earthlivesharp.log");
            Trace.Listeners.Add(new TextWriterTraceListener(logPath));
            Trace.AutoFlush = true;
            Trace.WriteLine($"[EarthLiveSharp] starting at {DateTimeOffset.Now:o}");

            // Handle --help
            if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
            {
                PrintHelp();
                return 0;
            }

            // Handle --config <path> argument
            if (args.Length > 1 && args[0] == "--config")
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME",
                    Path.GetDirectoryName(args[1]));
            }

            // Load (or create) configuration
            Config cfg;
            try
            {
                cfg = Config.Load();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EarthLiveSharp] Failed to load configuration: {ex.Message}");
                Trace.WriteLine($"[EarthLiveSharp] Failed to load configuration: {ex}");
                return 1;
            }

            // Ensure image cache directory exists
            Directory.CreateDirectory(cfg.ImageFolder);

            // Build satellite source
            ISatelliteSource source = CreateSource(cfg.Satellite);
            Trace.WriteLine($"[EarthLiveSharp] using source: {source.Name}");

            // Set up autostart if requested
            if (cfg.Autostart)
            {
                AutostartManager.Set(true);
            }

            // Handle OS signals for graceful shutdown
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Trace.WriteLine("[EarthLiveSharp] shutdown requested");
                cts.Cancel();
            };

            AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

            Console.WriteLine($"[EarthLiveSharp] running — source: {source.Name}, interval: {cfg.IntervalMinutes} min");
            Console.WriteLine("[EarthLiveSharp] press Ctrl+C to stop");

            // Main daemon loop
            await RunDaemonLoopAsync(cfg, source, cts.Token);

            Trace.WriteLine("[EarthLiveSharp] exiting");
            return 0;
        }

        private static async Task RunDaemonLoopAsync(Config cfg, ISatelliteSource source, CancellationToken ct)
        {
            // Run immediately on startup, then on the configured interval.
            bool firstRun = true;
            while (!ct.IsCancellationRequested)
            {
                if (!firstRun)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(cfg.IntervalMinutes), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                firstRun = false;

                await UpdateWallpaperAsync(cfg, source, ct);

                // Reload config on each iteration so changes take effect without restarting.
                try { cfg = Config.Load(); } catch { /* keep using current config */ }
            }
        }

        private static async Task UpdateWallpaperAsync(Config cfg, ISatelliteSource source, CancellationToken ct)
        {
            try
            {
                string? imageId = await source.GetLatestImageIdAsync(ct);
                if (imageId == null)
                {
                    Trace.WriteLine("[EarthLiveSharp] no image id returned — skipping");
                    return;
                }

                if (imageId == _lastImageId)
                {
                    Trace.WriteLine("[EarthLiveSharp] image unchanged — skipping");
                    return;
                }

                Trace.WriteLine($"[EarthLiveSharp] new image id: {imageId}");

                bool ok = await source.DownloadTilesAsync(imageId, cfg.ImageFolder, cfg.Size, ct);
                if (!ok)
                {
                    Trace.WriteLine("[EarthLiveSharp] tile download failed");
                    return;
                }

                string wallpaperPath = ImageCompositor.Composite(cfg.ImageFolder, cfg.Size, cfg.Zoom);

                if (cfg.SetWallpaper)
                {
                    WallpaperSetter.Set(wallpaperPath);
                }

                if (cfg.SaveTexture && !string.IsNullOrEmpty(cfg.SaveDirectory) &&
                    cfg.SaveDirectory != "Selected Directory")
                {
                    SaveSnapshot(wallpaperPath, cfg);
                }

                _lastImageId = imageId;
            }
            catch (OperationCanceledException)
            {
                // Propagate cancellation
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EarthLiveSharp] UpdateWallpaperAsync error: {ex.Message}");
            }
        }

        private static void SaveSnapshot(string wallpaperPath, Config cfg)
        {
            try
            {
                if (_sequenceCount >= cfg.SaveMaxCount) _sequenceCount = 0;
                string dest = Path.Combine(cfg.SaveDirectory, $"wallpaper_{_sequenceCount}.png");
                File.Copy(wallpaperPath, dest, overwrite: true);
                _sequenceCount++;
                Trace.WriteLine($"[EarthLiveSharp] saved snapshot to {dest}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EarthLiveSharp] SaveSnapshot failed: {ex.Message}");
            }
        }

        private static ISatelliteSource CreateSource(string satellite) =>
            satellite?.ToUpperInvariant() switch
            {
                "NASAEPIC" or "EPIC" => new EpicSource(HttpClient),
                _ => new HimawariSource(HttpClient)  // default: Himawari-9
            };

        private static void PrintHelp()
        {
            Console.WriteLine("EarthLiveSharp - live satellite imagery wallpaper daemon");
            Console.WriteLine();
            Console.WriteLine("Usage: earthlivesharp [--config <path>] [--help]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --config <path>   Path to config.json (default: ~/.config/earthlivesharp/config.json)");
            Console.WriteLine("  --help, -h        Show this help text");
            Console.WriteLine();
            Console.WriteLine("Supported satellite sources (set in config.json):");
            Console.WriteLine("  Himawari9   Himawari-9 via RAMMB/CIRA SLIDER (default)");
            Console.WriteLine("  NasaEpic    NASA DSCOVR EPIC");
        }
    }
}
