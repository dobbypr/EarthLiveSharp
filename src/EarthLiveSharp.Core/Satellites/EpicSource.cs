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
    /// Fetches whole-Earth imagery from the NASA DSCOVR EPIC camera at the L1 Lagrange point.
    /// API documentation: https://epic.gsfc.nasa.gov/about/api
    /// </summary>
    public sealed class EpicSource : ISatelliteSource
    {
        private const string ApiUrl = "https://epic.gsfc.nasa.gov/api/natural";
        private const string ArchiveBaseUrl = "https://epic.gsfc.nasa.gov/archive/natural";

        private readonly HttpClient _http;
        private string _lastImageId = string.Empty;

        public string Name => "NASA EPIC (DSCOVR)";

        public EpicSource(HttpClient httpClient)
        {
            _http = httpClient;
        }

        public async Task<string?> GetLatestImageIdAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string json = await _http.GetStringAsync(ApiUrl, cancellationToken);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                {
                    Trace.WriteLine("[NASA EPIC] empty response from API");
                    return null;
                }

                JsonElement latest = root[0];
                // Build a composite id: "image_name|date"
                string imageName = latest.GetProperty("image").GetString() ?? string.Empty;
                string date = latest.GetProperty("date").GetString() ?? string.Empty;
                string imageId = $"{imageName}|{date}";
                Trace.WriteLine($"[NASA EPIC] latest image id: {imageId}");
                return imageId;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[NASA EPIC] GetLatestImageIdAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DownloadTilesAsync(
            string imageId,
            string outputDirectory,
            int size,
            CancellationToken cancellationToken = default)
        {
            // NASA EPIC provides a single image, not tiles.  We download it as tile 0_0.png.
            string[] parts = imageId.Split('|');
            if (parts.Length < 2)
            {
                Trace.WriteLine($"[NASA EPIC] invalid imageId format: {imageId}");
                return false;
            }
            string imageName = parts[0];
            string date = parts[1]; // "YYYY-MM-DD HH:mm:ss"

            if (date.Length < 10)
            {
                Trace.WriteLine($"[NASA EPIC] invalid date in imageId: {imageId}");
                return false;
            }

            // Archive path: /YYYY/MM/DD/png/
            string datePath = $"{date[..4]}/{date[5..7]}/{date[8..10]}";
            string url = $"{ArchiveBaseUrl}/{datePath}/png/{imageName}.png";

            Directory.CreateDirectory(outputDirectory);
            try
            {
                byte[] data = await _http.GetByteArrayAsync(url, cancellationToken);
                // Save as 0_0.png — the compositor will handle single-tile images.
                string dest = Path.Combine(outputDirectory, "0_0.png");
                await File.WriteAllBytesAsync(dest, data, cancellationToken);
                Trace.WriteLine($"[NASA EPIC] downloaded image {imageName}");
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[NASA EPIC] DownloadTilesAsync failed: {ex.Message}");
                return false;
            }
        }

        public void ResetState()
        {
            _lastImageId = string.Empty;
        }
    }
}
