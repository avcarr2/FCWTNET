using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FCWTNET.Transform.Wavelets
{
    /// <summary>
    /// The available analysing wavelets, and parsing of the <c>name[:parameter,...]</c>
    /// form used by the command line and the UI.
    /// </summary>
    public static class WaveletCatalog
    {
        /// <summary>Names of every wavelet that can be constructed.</summary>
        public static IReadOnlyList<string> Names { get; } = new[]
        {
            "morlet", "paul", "bump", "log-gabor", "dog"
        };

        /// <summary>A default instance of each wavelet, for listing and for the UI.</summary>
        public static IReadOnlyList<IWavelet> Defaults { get; } = new IWavelet[]
        {
            new MorletWavelet(),
            new PaulWavelet(),
            new BumpWavelet(),
            new LogGaborWavelet(),
            new DerivativeOfGaussianWavelet()
        };

        /// <summary>The wavelet used when none is chosen.</summary>
        public static IWavelet Default => new MorletWavelet();

        /// <summary>
        /// Builds a wavelet from a specification of the form <c>name</c> or
        /// <c>name:p1[,p2]</c>, for example <c>morlet:80</c>, <c>paul:8</c>,
        /// <c>bump:5,0.4</c>, <c>log-gabor:6,1.1</c>, <c>dog:4</c>.
        /// </summary>
        public static IWavelet Parse(string specification)
        {
            if (string.IsNullOrWhiteSpace(specification))
            {
                return Default;
            }

            string[] parts = specification.Split(':', 2);
            string name = parts[0].Trim().ToLowerInvariant();
            double[] parameters = parts.Length > 1
                ? ParseParameters(parts[1], specification)
                : Array.Empty<double>();

            switch (name)
            {
                case "morlet":
                    return parameters.Length switch
                    {
                        0 => new MorletWavelet(),
                        1 => new MorletWavelet(parameters[0]),
                        _ => throw Bad(specification, "morlet takes at most one parameter: c0")
                    };
                case "paul":
                    return parameters.Length switch
                    {
                        0 => new PaulWavelet(),
                        1 => new PaulWavelet(ToOrder(parameters[0], specification)),
                        _ => throw Bad(specification, "paul takes at most one parameter: order")
                    };
                case "bump":
                    return parameters.Length switch
                    {
                        0 => new BumpWavelet(),
                        1 => new BumpWavelet(parameters[0]),
                        2 => new BumpWavelet(parameters[0], parameters[1]),
                        _ => throw Bad(specification, "bump takes at most two parameters: centre, width")
                    };
                case "log-gabor":
                case "loggabor":
                    return parameters.Length switch
                    {
                        0 => new LogGaborWavelet(),
                        1 => new LogGaborWavelet(parameters[0]),
                        2 => new LogGaborWavelet(parameters[0], parameters[1]),
                        _ => throw Bad(specification, "log-gabor takes at most two parameters: centre, bandwidth")
                    };
                case "dog":
                case "mexican-hat":
                    return parameters.Length switch
                    {
                        0 => new DerivativeOfGaussianWavelet(),
                        1 => new DerivativeOfGaussianWavelet(ToOrder(parameters[0], specification)),
                        _ => throw Bad(specification, "dog takes at most one parameter: order")
                    };
                default:
                    throw new ArgumentException(
                        $"Unknown wavelet '{name}'. Available: {string.Join(", ", Names)}",
                        nameof(specification));
            }
        }

        private static double[] ParseParameters(string raw, string specification)
        {
            return raw.Split(',')
                .Select(part =>
                {
                    if (!double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                            out double value))
                    {
                        throw Bad(specification, $"'{part.Trim()}' is not a number");
                    }
                    return value;
                })
                .ToArray();
        }

        private static int ToOrder(double value, string specification)
        {
            if (value != Math.Floor(value))
            {
                throw Bad(specification, "order must be a whole number");
            }
            return (int)value;
        }

        private static ArgumentException Bad(string specification, string reason)
        {
            return new ArgumentException($"Invalid wavelet '{specification}': {reason}", nameof(specification));
        }
    }
}
