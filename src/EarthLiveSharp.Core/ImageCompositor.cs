using System;
using System.IO;
using SkiaSharp;
using System.Diagnostics;

namespace EarthLiveSharp.Core
{
    /// <summary>
    /// Composites a grid of PNG tile files into a single wallpaper BMP using SkiaSharp.
    /// </summary>
    public static class ImageCompositor
    {
        private const int TilePixels = 550;

        /// <summary>
        /// Reads <c>{tileDirectory}/{row}_{col}.png</c> tiles and joins them into
        /// <c>{tileDirectory}/wallpaper.png</c>.  If <paramref name="zoomPercent"/> is
        /// less than 100 the composited image is scaled down accordingly.
        /// </summary>
        /// <param name="tileDirectory">Directory containing the downloaded tile files.</param>
        /// <param name="size">Tile grid dimension (1 = 1×1, 2 = 2×2, etc.).</param>
        /// <param name="zoomPercent">Output scale percentage (1–100).</param>
        /// <returns>Full path to the composited wallpaper file.</returns>
        public static string Composite(string tileDirectory, int size, int zoomPercent = 100)
        {
            int fullSize = TilePixels * size;
            int outSize = zoomPercent >= 100 ? fullSize : Math.Max(1, fullSize * zoomPercent / 100);

            using SKBitmap canvas = new SKBitmap(outSize, outSize, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKCanvas skCanvas = new SKCanvas(canvas);
            skCanvas.Clear(SKColors.Black);

            using var paint = new SKPaint
            {
                IsAntialias = outSize < fullSize,
                FilterQuality = outSize < fullSize ? SKFilterQuality.High : SKFilterQuality.None
            };

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    string tilePath = Path.Combine(tileDirectory, $"{row}_{col}.png");
                    if (!File.Exists(tilePath))
                    {
                        Trace.WriteLine($"[Compositor] missing tile {tilePath}");
                        continue;
                    }

                    using SKBitmap tile = SKBitmap.Decode(tilePath);
                    if (tile == null)
                    {
                        Trace.WriteLine($"[Compositor] failed to decode tile {tilePath}");
                        continue;
                    }

                    float scale = (float)outSize / fullSize;
                    float x = col * TilePixels * scale;
                    float y = row * TilePixels * scale;
                    float w = TilePixels * scale;
                    float h = TilePixels * scale;

                    SKRect dest = new SKRect(x, y, x + w, y + h);
                    skCanvas.DrawBitmap(tile, dest, paint);
                }
            }

            skCanvas.Flush();

            string outPath = Path.Combine(tileDirectory, "wallpaper.png");
            using SKFileWStream stream = new SKFileWStream(outPath);
            canvas.Encode(stream, SKEncodedImageFormat.Png, 100);
            Trace.WriteLine($"[Compositor] wrote wallpaper to {outPath}");
            return outPath;
        }
    }
}
