using System;
using System.Collections.Generic;
using System.Globalization;

namespace FCWTNET.Cli
{
    /// <summary>
    /// Minimal `--name value` / `--flag` argument parser. Deliberately dependency-free:
    /// the CLI exists to drive the library, not to showcase an options framework.
    /// </summary>
    public class CommandLine
    {
        private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _positional = new();

        public IReadOnlyList<string> Positional => _positional;

        public static CommandLine Parse(string[] args)
        {
            var parsed = new CommandLine();
            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                if (argument.StartsWith("--", StringComparison.Ordinal))
                {
                    string key = argument.Substring(2);
                    // A following token that is not itself an option becomes the value;
                    // otherwise this is a bare flag.
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        parsed._options[key] = args[i + 1];
                        i++;
                    }
                    else
                    {
                        parsed._options[key] = null;
                    }
                }
                else
                {
                    parsed._positional.Add(argument);
                }
            }
            return parsed;
        }

        public bool Has(string name) => _options.ContainsKey(name);

        public string GetString(string name, string fallback)
        {
            return _options.TryGetValue(name, out string? value) && value != null ? value : fallback;
        }

        public int GetInt(string name, int fallback)
        {
            return TryGetInt(name, out int value) ? value : fallback;
        }

        public bool TryGetInt(string name, out int value)
        {
            value = 0;
            if (!_options.TryGetValue(name, out string? raw) || raw == null)
            {
                return false;
            }
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                throw new ArgumentException($"--{name} expects an integer, got '{raw}'");
            }
            return true;
        }

        public double GetDouble(string name, double fallback)
        {
            return TryGetDouble(name, out double value) ? value : fallback;
        }

        public bool TryGetDouble(string name, out double value)
        {
            value = 0;
            if (!_options.TryGetValue(name, out string? raw) || raw == null)
            {
                return false;
            }
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new ArgumentException($"--{name} expects a number, got '{raw}'");
            }
            return true;
        }

        /// <summary>Parses a "lo:hi" octave range.</summary>
        public (int, int) GetOctaveRange(string name, int fallbackLow, int fallbackHigh)
        {
            if (!_options.TryGetValue(name, out string? raw) || raw == null)
            {
                return (fallbackLow, fallbackHigh);
            }
            string[] parts = raw.Split(':');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int low)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int high))
            {
                throw new ArgumentException($"--{name} expects a range like 2:6, got '{raw}'");
            }
            if (high < low)
            {
                throw new ArgumentException($"--{name}: the high octave must not be below the low one");
            }
            return (low, high);
        }
    }
}
