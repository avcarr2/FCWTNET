using System;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace FCWTNET.Visualization
{
    /// <summary>
    /// Writes raw RGBA buffers to PNG via SkiaSharp, which is already on the dependency
    /// list for plot export and works headlessly.
    /// </summary>
    public static class PngWriter
    {
        /// <summary>
        /// Write a row-major RGBA buffer (4 bytes per pixel) to a PNG file.
        /// </summary>
        public static void Write(string path, byte[] rgba, int width, int height)
        {
            if (rgba == null)
            {
                throw new ArgumentNullException(nameof(rgba));
            }
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive");
            }
            if (rgba.Length != width * height * 4)
            {
                throw new ArgumentException(
                    $"Expected {width * height * 4} bytes for a {width}x{height} RGBA image, got {rgba.Length}",
                    nameof(rgba));
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var bitmap = new SKBitmap(info);

            GCHandle handle = GCHandle.Alloc(rgba, GCHandleType.Pinned);
            try
            {
                bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);
                using SKImage image = SKImage.FromBitmap(bitmap);
                using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                using FileStream stream = File.Create(path);
                encoded.SaveTo(stream);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
