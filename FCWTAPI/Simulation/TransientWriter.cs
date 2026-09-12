using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace FCWTNET.Simulation
{
    /// <summary>
    /// Writes simulated transients in the same CSV layout <see cref="TransientData"/>
    /// reads, so simulated data feeds straight into the existing analysis pipeline:
    ///
    ///     line 1: calibration coefficient
    ///     line 2: sampling rate
    ///     line 3+: one sample per line
    /// </summary>
    public static class TransientWriter
    {
        /// <summary>Write the transient to <paramref name="path"/> as CSV.</summary>
        public static void WriteCsv(SimulationResult result, string path)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (Path.GetExtension(path) != ".csv")
            {
                throw new ArgumentException("Transient files must use the .csv extension", nameof(path));
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var writer = new StreamWriter(path);
            // TransientData parses with Convert.ToDouble, which is culture-sensitive on
            // read; write invariant so the files are portable regardless.
            writer.WriteLine(result.CalibrationCoefficient.ToString("R", CultureInfo.InvariantCulture));
            writer.WriteLine(result.SamplingRate.ToString("F1", CultureInfo.InvariantCulture));
            foreach (double sample in result.Samples)
            {
                writer.WriteLine(sample.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Write the ground truth alongside the transient, so an analysis run can be
        /// scored against the values that produced it.
        /// </summary>
        public static void WriteGroundTruthJson(SimulationResult result, string path)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = new
            {
                result.SamplingRate,
                result.CalibrationCoefficient,
                result.DurationSeconds,
                SampleCount = result.Samples.Length,
                Components = result.GroundTruth
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(payload, options));
        }
    }
}
