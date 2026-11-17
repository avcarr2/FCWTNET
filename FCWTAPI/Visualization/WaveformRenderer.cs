using System;

namespace FCWTNET.Visualization
{
    /// <summary>
    /// Draws a 1-D signal into an RGBA buffer.
    ///
    /// Kept next to <see cref="ScalogramRenderer"/> and free of UI types so the transient
    /// preview in the desktop app is testable headlessly, and so the same drawing is
    /// available anywhere a small waveform thumbnail is useful.
    /// </summary>
    public static class WaveformRenderer
    {
        /// <summary>
        /// Render <paramref name="samples"/> as a line plot.
        ///
        /// When there are more samples than pixels each column shows the min-to-max span
        /// of the samples falling in it, which is the standard way to draw a waveform
        /// without aliasing the peaks away.
        /// </summary>
        public static byte[] ToRgba(double[] samples, int width, int height,
            RgbColor? background = null, RgbColor? foreground = null, RgbColor? axis = null)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }
            if (samples.Length == 0)
            {
                throw new ArgumentException("Nothing to draw", nameof(samples));
            }
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive");
            }

            RgbColor back = background ?? new RgbColor(0x0C, 0x0C, 0x12);
            RgbColor fore = foreground ?? new RgbColor(0x5E, 0xC9, 0x62);
            RgbColor axisColor = axis ?? new RgbColor(0x2A, 0x2A, 0x38);

            byte[] pixels = new byte[width * height * 4];
            Fill(pixels, back);

            double peak = 0;
            foreach (double sample in samples)
            {
                double magnitude = Math.Abs(sample);
                if (!double.IsNaN(magnitude) && magnitude > peak)
                {
                    peak = magnitude;
                }
            }
            if (peak <= 0)
            {
                peak = 1;
            }

            int midline = height / 2;
            for (int x = 0; x < width; x++)
            {
                SetPixel(pixels, width, height, x, midline, axisColor);
            }

            for (int x = 0; x < width; x++)
            {
                // Samples belonging to this column.
                long from = (long)x * samples.Length / width;
                long to = (long)(x + 1) * samples.Length / width;
                if (to <= from)
                {
                    to = from + 1;
                }
                if (from >= samples.Length)
                {
                    break;
                }
                to = Math.Min(to, samples.Length);

                double low = double.PositiveInfinity;
                double high = double.NegativeInfinity;
                for (long i = from; i < to; i++)
                {
                    double value = samples[i];
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        continue;
                    }
                    low = Math.Min(low, value);
                    high = Math.Max(high, value);
                }
                if (double.IsInfinity(low) || double.IsInfinity(high))
                {
                    continue;
                }

                int yHigh = ValueToRow(high, peak, height);
                int yLow = ValueToRow(low, peak, height);
                for (int y = Math.Min(yHigh, yLow); y <= Math.Max(yHigh, yLow); y++)
                {
                    SetPixel(pixels, width, height, x, y, fore);
                }
            }

            return pixels;
        }

        /// <summary>Row for a sample value; positive values are drawn above the midline.</summary>
        private static int ValueToRow(double value, double peak, int height)
        {
            double normalised = Math.Clamp(value / peak, -1.0, 1.0);
            double row = (1 - normalised) * 0.5 * (height - 1);
            return (int)Math.Clamp(Math.Round(row), 0, height - 1);
        }

        private static void Fill(byte[] pixels, RgbColor color)
        {
            for (int offset = 0; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
                pixels[offset + 3] = 255;
            }
        }

        private static void SetPixel(byte[] pixels, int width, int height, int x, int y, RgbColor color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }
            int offset = (y * width + x) * 4;
            pixels[offset] = color.R;
            pixels[offset + 1] = color.G;
            pixels[offset + 2] = color.B;
            pixels[offset + 3] = 255;
        }
    }
}
