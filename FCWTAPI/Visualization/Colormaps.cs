using System;
using System.Collections.Generic;
using OxyPlot;

namespace FCWTNET.Visualization
{
    /// <summary>An RGB colour, 0-255 per channel.</summary>
    public readonly struct RgbColor
    {
        public RgbColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        /// <summary>
        /// Relative luminance per WCAG 2.x. Used to verify that a ramp rises
        /// monotonically in lightness, which is what makes a sequential map readable.
        /// </summary>
        public double RelativeLuminance()
        {
            static double Channel(byte value)
            {
                double srgb = value / 255.0;
                return srgb <= 0.03928 ? srgb / 12.92 : Math.Pow((srgb + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(R) + 0.7152 * Channel(G) + 0.0722 * Channel(B);
        }
    }

    /// <summary>
    /// A sequential colour ramp: magnitude, low to high.
    ///
    /// Scalogram magnitude is a "how much" quantity, so it takes a sequential map whose
    /// lightness increases monotonically. That monotonicity is the property that makes
    /// the image readable in greyscale, in print, and to colourblind readers -- and it is
    /// exactly what a rainbow palette lacks: rainbow's lightness rises and falls, which
    /// invents banding that is not in the data and hides real structure. The ramps here
    /// are all verified monotonic by test.
    /// </summary>
    public class Colormap
    {
        private readonly RgbColor[] _anchors;

        public Colormap(string name, IReadOnlyList<string> hexAnchors)
        {
            if (hexAnchors == null || hexAnchors.Count < 2)
            {
                throw new ArgumentException("A colormap needs at least two anchors", nameof(hexAnchors));
            }
            Name = name;
            _anchors = new RgbColor[hexAnchors.Count];
            for (int i = 0; i < hexAnchors.Count; i++)
            {
                _anchors[i] = ParseHex(hexAnchors[i]);
            }
        }

        public string Name { get; }

        /// <summary>The ramp's anchor colours, low to high.</summary>
        public IReadOnlyList<RgbColor> Anchors => _anchors;

        /// <summary>
        /// Colour at position <paramref name="t"/> in [0, 1], linearly interpolated
        /// between anchors. Values outside the range clamp to the ends.
        /// </summary>
        public RgbColor Map(double t)
        {
            if (double.IsNaN(t))
            {
                t = 0;
            }
            t = Math.Clamp(t, 0.0, 1.0);
            double scaled = t * (_anchors.Length - 1);
            int index = (int)Math.Floor(scaled);
            if (index >= _anchors.Length - 1)
            {
                return _anchors[^1];
            }
            double fraction = scaled - index;
            RgbColor low = _anchors[index];
            RgbColor high = _anchors[index + 1];
            return new RgbColor(
                Lerp(low.R, high.R, fraction),
                Lerp(low.G, high.G, fraction),
                Lerp(low.B, high.B, fraction));
        }

        /// <summary>Evenly spaced samples across the ramp.</summary>
        public RgbColor[] Sample(int steps)
        {
            if (steps < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), "Need at least two steps");
            }
            RgbColor[] samples = new RgbColor[steps];
            for (int i = 0; i < steps; i++)
            {
                samples[i] = Map((double)i / (steps - 1));
            }
            return samples;
        }

        /// <summary>The ramp as an OxyPlot palette, for heat maps and colour axes.</summary>
        public OxyPalette ToOxyPalette(int steps = 256)
        {
            RgbColor[] samples = Sample(steps);
            var colors = new List<OxyColor>(steps);
            foreach (RgbColor color in samples)
            {
                colors.Add(OxyColor.FromRgb(color.R, color.G, color.B));
            }
            return new OxyPalette(colors);
        }

        private static byte Lerp(byte from, byte to, double fraction)
        {
            return (byte)Math.Round(from + (to - from) * fraction);
        }

        private static RgbColor ParseHex(string hex)
        {
            string trimmed = (hex ?? string.Empty).TrimStart('#');
            if (trimmed.Length != 6)
            {
                throw new ArgumentException($"Expected a 6-digit hex colour, got '{hex}'", nameof(hex));
            }
            try
            {
                return new RgbColor(
                    Convert.ToByte(trimmed.Substring(0, 2), 16),
                    Convert.ToByte(trimmed.Substring(2, 2), 16),
                    Convert.ToByte(trimmed.Substring(4, 2), 16));
            }
            catch (FormatException)
            {
                throw new ArgumentException($"'{hex}' is not a valid hex colour", nameof(hex));
            }
        }
    }

    /// <summary>The available sequential ramps.</summary>
    public static class Colormaps
    {
        /// <summary>
        /// Viridis: perceptually uniform, monotonic in lightness, and readable with any
        /// form of colour vision deficiency. The default for scalograms, where the dark
        /// low end keeps the background quiet and ridges read as bright.
        /// </summary>
        public static Colormap Viridis { get; } = new Colormap("viridis", new[]
        {
            "#440154", "#472D7B", "#3B528B", "#2C728E", "#21918C",
            "#28AE80", "#5EC962", "#ADDC30", "#FDE725"
        });

        /// <summary>
        /// Magma: also perceptually uniform, with a black low end. Higher contrast at the
        /// bottom of the range than viridis, which helps when most of the scalogram is
        /// near-zero and only a few ridges matter.
        /// </summary>
        public static Colormap Magma { get; } = new Colormap("magma", new[]
        {
            "#000004", "#1C1044", "#4F127B", "#812581", "#B5367A",
            "#E55064", "#FB8861", "#FEC287", "#FCFDBF"
        });

        /// <summary>
        /// A single-hue blue ramp running dark to light, for when a strictly one-hue
        /// sequential map is wanted.
        /// </summary>
        public static Colormap Blues { get; } = new Colormap("blues", new[]
        {
            "#08306B", "#08519C", "#2171B5", "#4292C6", "#6BAED6",
            "#9ECAE1", "#C6DBEF", "#DEEBF7", "#F7FBFF"
        });

        /// <summary>Greyscale, for print and for checking that structure survives without colour.</summary>
        public static Colormap Greys { get; } = new Colormap("greys", new[]
        {
            "#000000", "#FFFFFF"
        });

        /// <summary>The ramp used when none is specified.</summary>
        public static Colormap Default => Viridis;

        /// <summary>Every named ramp.</summary>
        public static IReadOnlyDictionary<string, Colormap> All { get; } =
            new Dictionary<string, Colormap>(StringComparer.OrdinalIgnoreCase)
            {
                [Viridis.Name] = Viridis,
                [Magma.Name] = Magma,
                [Blues.Name] = Blues,
                [Greys.Name] = Greys
            };

        /// <summary>Look up a ramp by name.</summary>
        public static Colormap Get(string name)
        {
            if (All.TryGetValue(name, out Colormap? map))
            {
                return map;
            }
            throw new ArgumentException(
                $"Unknown colormap '{name}'. Available: {string.Join(", ", All.Keys)}", nameof(name));
        }
    }
}
