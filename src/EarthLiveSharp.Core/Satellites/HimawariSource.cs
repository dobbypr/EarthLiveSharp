using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace EarthLiveSharp.Core.Satellites
{
    /// <summary>
    /// Fetches Himawari-9 full-disk imagery from the RAMMB/CIRA SLIDER tile service.
    /// </summary>
    public sealed class HimawariSource : ISatelliteSource
    {
        // RAMMB SLIDER API for Himawari-9 full-disk (AHI) latest timestamp
        private const string LatestJsonUrl =
            "https://rammb-slider.cira.colostate.edu/data/json/himawari/full_disk/natural_color/latest_times.json";

        // RAMMB SLIDER tile URL pattern:
        // {base}/{satellite}/{sector}/{product}/{zoom}/{timestamp}/{row}_{col}.png
        private const string TileUrlTemplate =
            "https://rammb-slider.cira.colostate.edu/data/imagery/{date_path}/himawari---full_disk/natural_color/{timestamp}/{zoom:D2}/{row:D3}_{col:D3}.png";

        private readonly HttpClient _http;
        private string _lastImageId = string.Empty;

        public string Name => "Himawari-9 (RAMMB/CIRA SLIDER)";

        public HimawariSource(HttpClient httpClient)
        {
            _http = httpClient;
        }

        public async Task<string?> GetLatestImageIdAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string json = await _http.GetStringAsync(LatestJsonUrl, cancellationToken);
                using JsonDocument doc = JsonDocument.Parse(json);
                // Response: { "timestamps_int": [ 20250101123000, ... ] }
                if (doc.RootElement.TryGetProperty("timestamps_int", out JsonElement arr) &&
                    arr.GetArrayLength() > 0)
                {
                    string timestamp = arr[0].GetRawText().Trim('"');
                    Trace.WriteLine($"[Himawari-9] latest image id: {timestamp}");
                    return timestamp;
                }
                Trace.WriteLine("[Himawari-9] could not parse latest_times.json");
                return null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Himawari-9] GetLatestImageIdAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DownloadTilesAsync(
            string imageId,
            string outputDirectory,
            int size,
            CancellationToken cancellationToken = default)
        {
            // imageId is a 14-digit integer string: YYYYMMDDHHmmss
            if (imageId.Length < 14)
            {
                Trace.WriteLine($"[Himawari-9] invalid imageId: {imageId}");
                return false;
            }

            string datePath = $"{imageId[..4]}/{imageId[4..6]}/{imageId[6..8]}";
            // Zoom level mapping: size → RAMMB zoom level index
            // At zoom 0 the full disk is 1 tile; each step doubles the tile count per axis.
            int zoom = size switch
            {
                1  => 0,
                2  => 1,
                4  => 2,
                8  => 3,
                16 => 4,
                _  => 0
            };

            Directory.CreateDirectory(outputDirectory);
            try
            {
                for (int row = 0; row < size; row++)
                {
                    for (int col = 0; col < size; col++)
                    {
                        string url = $"https://rammb-slider.cira.colostate.edu/data/imagery/{datePath}/himawari---full_disk/natural_color/{imageId}/{zoom:D2}/{row:D3}_{col:D3}.png";
                        string dest = Path.Combine(outputDirectory, $"{row}_{col}.png");
                        byte[] data = await _http.GetByteArrayAsync(url, cancellationToken);
                        await File.WriteAllBytesAsync(dest, data, cancellationToken);
                        Trace.WriteLine($"[Himawari-9] downloaded tile {row},{col}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Himawari-9] DownloadTilesAsync failed: {ex.Message}");
                return false;
            }
        }

        public void ResetState()
        {
            _lastImageId = string.Empty;
        }
    }
}
