using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace EarthLiveSharp.Core
{
    /// <summary>
    /// Application configuration stored as JSON at
    /// <c>~/.config/earthlivesharp/config.json</c> (XDG Base Directory).
    /// </summary>
    public sealed class Config
    {
        // ----------------------------------------------------------------
        // Properties
        // ----------------------------------------------------------------

        /// <summary>Application version string (informational).</summary>
        public string Version { get; set; } = "v4.0";

        /// <summary>Satellite source name.  "Himawari9" or "NasaEpic".</summary>
        public string Satellite { get; set; } = "Himawari9";

        /// <summary>Update interval in minutes.</summary>
        public int IntervalMinutes { get; set; } = 20;

        /// <summary>Enable autostart on login.</summary>
        public bool Autostart { get; set; } = false;

        /// <summary>Set the desktop wallpaper after compositing.</summary>
        public bool SetWallpaper { get; set; } = true;

        /// <summary>Tile grid dimension (1, 2, 4, 8, or 16).</summary>
        public int Size { get; set; } = 1;

        /// <summary>Output zoom percentage (1–100).</summary>
        public int Zoom { get; set; } = 100;

        /// <summary>Save a sequence of wallpaper snapshots.</summary>
        public bool SaveTexture { get; set; } = false;

        /// <summary>Directory for saved wallpaper snapshots.</summary>
        public string SaveDirectory { get; set; } = string.Empty;

        /// <summary>Maximum number of wallpaper snapshots to keep.</summary>
        public int SaveMaxCount { get; set; } = 10;

        // ----------------------------------------------------------------
        // Paths
        // ----------------------------------------------------------------

        /// <summary>
        /// Directory where downloaded tile images are stored.
        /// Defaults to <c>~/.cache/earthlivesharp/images</c>.
        /// </summary>
        [JsonIgnore]
        public string ImageFolder { get; set; } = GetDefaultImageFolder();

        // ----------------------------------------------------------------
        // Persistence
        // ----------------------------------------------------------------

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>Returns the canonical config file path (XDG Base Directory).</summary>
        public static string GetConfigPath()
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, "earthlivesharp", "config.json");
        }

        /// <summary>Returns the canonical image cache directory (XDG Base Directory).</summary>
        public static string GetDefaultImageFolder()
        {
            string cacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
            return Path.Combine(cacheHome, "earthlivesharp", "images");
        }

        /// <summary>
        /// Loads configuration from the XDG config file, or returns a default
        /// <see cref="Config"/> if the file does not exist.
        /// </summary>
        public static Config Load()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                Trace.WriteLine($"[Config] no config file found at {path}, using defaults");
                var defaults = new Config();
                defaults.ImageFolder = GetDefaultImageFolder();
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? new Config();
                cfg.ImageFolder = GetDefaultImageFolder();
                Trace.WriteLine($"[Config] loaded from {path}");
                return cfg;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Config] failed to load {path}: {ex.Message}");
                throw;
            }
        }

        /// <summary>Saves this configuration to the XDG config file.</summary>
        public void Save()
        {
            string path = GetConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(path, json);
            Trace.WriteLine($"[Config] saved to {path}");
        }
    }
}
