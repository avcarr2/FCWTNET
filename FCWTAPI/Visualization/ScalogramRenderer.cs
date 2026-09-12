using System;

namespace FCWTNET.Visualization
{
    /// <summary>How magnitudes are mapped onto the colour ramp.</summary>
    public enum IntensityScale
    {
        /// <summary>Straight linear mapping between the chosen limits.</summary>
        Linear,

        /// <summary>
        /// log(1 + x/x0). Mass spectra span a wide dynamic range; a log scale lets small
        /// isotope peaks stay visible next to a base peak that is orders of magnitude larger.
        /// </summary>
        Logarithmic,

        /// <summary>Square root: a gentler compression than log.</summary>
        SquareRoot
    }

    /// <summary>
    /// Turns a CWT magnitude matrix into pixels.
    ///
    /// Lives outside the UI so the PNG exporter and the Avalonia view render identically,
    /// and so the mapping from magnitude to colour can be unit-tested without a display.
    /// </summary>
    public static class ScalogramRenderer
    {
        /// <summary>
        /// Map a magnitude matrix to a row-major RGBA byte buffer, 4 bytes per pixel.
        ///
        /// The input is indexed [frequencyRow, timeColumn] with row 0 the lowest
        /// frequency. Images are drawn top-down, so rows are flipped: the highest
        /// frequency ends up at the top, which is how a scalogram is conventionally read.
        /// </summary>
        /// <param name="magnitude">CWT magnitudes, [frequency, time].</param>
        /// <param name="width">Output width in pixels; columns are resampled to fit.</param>
        /// <param name="height">Output height in pixels; rows are resampled to fit.</param>
        /// <param name="colormap">Ramp to use; defaults to viridis.</param>
        /// <param name="scale">Intensity mapping.</param>
        /// <param name="minimum">Lower limit; defaults to the matrix minimum.</param>
        /// <param name="maximum">Upper limit; defaults to the matrix maximum.</param>
        public static byte[] ToRgba(double[,] magnitude, int width, int height,
            Colormap? colormap = null, IntensityScale scale = IntensityScale.Linear,
            double? minimum = null, double? maximum = null)
        {
            if (magnitude == null)
            {
                throw new ArgumentNullException(nameof(magnitude));
            }
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive");
            }

            int rows = magnitude.GetLength(0);
            int columns = magnitude.GetLength(1);
            if (rows == 0 || columns == 0)
            {
                throw new ArgumentException("Magnitude matrix must not be empty", nameof(magnitude));
            }

            Colormap map = colormap ?? Colormaps.Default;
            (double low, double high) = ResolveLimits(magnitude, minimum, maximum);

            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                // Flip vertically so high frequencies sit at the top of the image.
                int sourceRow = MapIndex(height - 1 - y, height, rows);
                for (int x = 0; x < width; x++)
                {
                    int sourceColumn = MapIndex(x, width, columns);
                    double normalised = Normalise(magnitude[sourceRow, sourceColumn], low, high, scale);
                    RgbColor color = map.Map(normalised);

                    int offset = (y * width + x) * 4;
                    pixels[offset] = color.R;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 2] = color.B;
                    pixels[offset + 3] = 255;
                }
            }
            return pixels;
        }

        /// <summary>
        /// Map a magnitude onto [0, 1] under the given limits and intensity scale.
        /// Exposed so colour-bar ticks can be placed with the same mapping the image uses.
        /// </summary>
        public static double Normalise(double value, double low, double high, IntensityScale scale)
        {
            if (high <= low)
            {
                return 0;
            }
            double clamped = Math.Clamp(value, low, high);
            double fraction = (clamped - low) / (high - low);

            switch (scale)
            {
                case IntensityScale.Logarithmic:
                    // log1p over a fixed decade count keeps small values visible without
                    // the mapping depending on the absolute magnitudes.
                    const double Decades = 1000.0;
                    return Math.Log(1 + fraction * Decades) / Math.Log(1 + Decades);
                case IntensityScale.SquareRoot:
                    return Math.Sqrt(fraction);
                default:
                    return fraction;
            }
        }

        /// <summary>Smallest and largest finite values in the matrix.</summary>
        public static (double Minimum, double Maximum) Extent(double[,] magnitude)
        {
            double low = double.PositiveInfinity;
            double high = double.NegativeInfinity;
            int rows = magnitude.GetLength(0);
            int columns = magnitude.GetLength(1);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    double value = magnitude[i, j];
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        continue;
                    }
                    if (value < low)
                    {
                        low = value;
                    }
                    if (value > high)
                    {
                        high = value;
                    }
                }
            }
            if (double.IsInfinity(low) || double.IsInfinity(high))
            {
                return (0, 1);
            }
            return (low, high);
        }

        private static (double, double) ResolveLimits(double[,] magnitude, double? minimum, double? maximum)
        {
            if (minimum.HasValue && maximum.HasValue)
            {
                return (minimum.Value, maximum.Value);
            }
            (double low, double high) = Extent(magnitude);
            return (minimum ?? low, maximum ?? high);
        }

        /// <summary>
        /// Nearest-neighbour index mapping from an output position to a source index.
        /// Nearest-neighbour rather than averaging on purpose: a scalogram ridge can be a
        /// single row wide, and averaging would dim it out of existence when downsampling.
        /// </summary>
        private static int MapIndex(int position, int outputSize, int sourceSize)
        {
            if (outputSize == 1)
            {
                return 0;
            }
            long scaled = (long)position * sourceSize / outputSize;
            return (int)Math.Clamp(scaled, 0, sourceSize - 1);
        }
    }
}
