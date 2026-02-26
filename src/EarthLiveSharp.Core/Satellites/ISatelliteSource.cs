using System.Threading;
using System.Threading.Tasks;

namespace EarthLiveSharp.Core.Satellites
{
    /// <summary>
    /// Represents a satellite imagery source.
    /// </summary>
    public interface ISatelliteSource
    {
        /// <summary>Gets the display name of the satellite source.</summary>
        string Name { get; }

        /// <summary>
        /// Retrieves the identifier for the latest available image.
        /// Returns null if no new image is available or the request fails.
        /// </summary>
        Task<string?> GetLatestImageIdAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the image tiles for the given image identifier into <paramref name="outputDirectory"/>.
        /// </summary>
        /// <param name="imageId">Opaque image identifier returned by <see cref="GetLatestImageIdAsync"/>.</param>
        /// <param name="outputDirectory">Directory where tile files will be written.</param>
        /// <param name="size">Tile grid dimension (e.g. 1 = 1×1, 2 = 2×2, 4 = 4×4).</param>
        /// <returns>true on success, false on failure.</returns>
        Task<bool> DownloadTilesAsync(string imageId, string outputDirectory, int size, CancellationToken cancellationToken = default);

        /// <summary>Resets any cached state so the next poll will re-download the image.</summary>
        void ResetState();
    }
}
