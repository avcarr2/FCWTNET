using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FCWTNET;
using FCWTNET.Analysis;
using FCWTNET.Simulation;
using FCWTNET.Transform;
using FCWTNET.Transform.Wavelets;
using FCWTNET.Visualization;

namespace FCWTNET.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return 1;
                }

                var options = CommandLine.Parse(args.Skip(1).ToArray());
                switch (args[0].ToLowerInvariant())
                {
                    case "simulate":
                        return Simulate(options);
                    case "analyze":
                    case "analyse":
                        return Analyze(options);
                    case "demo":
                        return Demo(options);
                    case "presets":
                        return ListPresets();
                    case "wavelets":
                        return ListWavelets();
                    case "help":
                    case "--help":
                    case "-h":
                        PrintUsage();
                        return 0;
                    default:
                        Console.Error.WriteLine($"Unknown command '{args[0]}'.");
                        PrintUsage();
                        return 1;
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"error: {exception.Message}");
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"fcwtnet - simulate and analyse Orbitrap transients with a CWT

Usage:
  fcwtnet simulate --preset <name> --out <dir> [--seed N] [--duration S]
  fcwtnet analyze <transient.csv> --out <dir> [options]
  fcwtnet demo --out <dir>
  fcwtnet presets

Commands:
  simulate   Generate a transient CSV plus its ground truth JSON.
  analyze    Run a CWT over a transient and write scalogram PNGs and a peak table.
  demo       Simulate and analyse every preset.
  presets    List the available simulation presets.
  wavelets   List the available analysing wavelets.

Analysis options:
  --octaves <lo:hi>   Scale range, e.g. 2:6            (default 2:6)
  --voices N          Scales per octave                (default 48)
  --c0 F              Morlet central frequency         (default 6)
  --wavelet SPEC      morlet[:c0] | paul[:order] | bump[:centre,width]
                      | log-gabor[:centre,bandwidth] | dog[:order]
  --colormap NAME     viridis | magma | blues | greys  (default viridis)
  --scale NAME        linear | log | sqrt              (default linear)
  --max-samples N     Truncate long transients         (default 65536)
  --backend NAME      auto | managed | native          (default auto)
  --peaks N           Peaks to report                  (default 10)");
        }

        private static int ListPresets()
        {
            Console.WriteLine("Available presets:");
            foreach (SimulationPreset preset in Presets.All.Values.OrderBy(p => p.Name))
            {
                Console.WriteLine($"  {preset.Name,-20} {preset.Description}");
            }
            return 0;
        }

        private static int ListWavelets()
        {
            Console.WriteLine("Available wavelets (name[:parameters]):");
            foreach (IWavelet wavelet in WaveletCatalog.Defaults)
            {
                Console.WriteLine($"  {wavelet.Name,-12} {wavelet.Description}");
                Console.WriteLine($"  {"",-12} at defaults: resolves components " +
                                  $">{wavelet.RelativeFrequencyResolution * 100:F1}% apart");
            }
            return 0;
        }

        private static int Simulate(CommandLine options)
        {
            string presetName = options.GetString("preset", "single-peak");
            string outputDirectory = options.GetString("out", "./output");
            SimulationPreset preset = Presets.Get(presetName);

            OrbitrapSimulatorConfig config = preset.Config;
            if (options.TryGetInt("seed", out int seed))
            {
                config.Seed = seed;
            }
            if (options.TryGetDouble("duration", out double duration))
            {
                config.DurationSeconds = duration;
            }

            Console.WriteLine($"Simulating preset '{preset.Name}'");
            Console.WriteLine($"  {preset.Description}");
            SimulationResult result = new OrbitrapSimulator(config).Simulate(preset.Species);

            Directory.CreateDirectory(outputDirectory);
            string csvPath = Path.Combine(outputDirectory, $"{preset.Name}.csv");
            string truthPath = Path.Combine(outputDirectory, $"{preset.Name}.truth.json");
            TransientWriter.WriteCsv(result, csvPath);
            TransientWriter.WriteGroundTruthJson(result, truthPath);

            Console.WriteLine($"  {result.Samples.Length} samples at {result.SamplingRate} Hz " +
                              $"({result.DurationSeconds * 1000:F1} ms)");
            Console.WriteLine($"  {result.GroundTruth.Count} isotopologues, " +
                              $"m/z {result.GroundTruth.Min(t => t.Mz):F3} to {result.GroundTruth.Max(t => t.Mz):F3}");
            Console.WriteLine($"  wrote {csvPath}");
            Console.WriteLine($"  wrote {truthPath}");
            return 0;
        }

        private static int Analyze(CommandLine options)
        {
            if (options.Positional.Count == 0)
            {
                Console.Error.WriteLine("analyze needs a transient CSV path.");
                return 1;
            }
            string transientPath = options.Positional[0];
            string outputDirectory = options.GetString("out", "./output");
            Directory.CreateDirectory(outputDirectory);

            var transient = new TransientData(transientPath);
            int maxSamples = options.GetInt("max-samples", 65536);
            transient.ImportTransientData(maxSamples);

            string name = Path.GetFileNameWithoutExtension(transientPath);
            AnalyzeTransient(name, transient.Data!, transient.SamplingRate!.Value,
                transient.CalibrationFactor!.Value, outputDirectory, options, groundTruth: null);
            return 0;
        }

        private static int Demo(CommandLine options)
        {
            string outputDirectory = options.GetString("out", "./demo-output");
            Directory.CreateDirectory(outputDirectory);

            foreach (SimulationPreset preset in Presets.All.Values.OrderBy(p => p.Name))
            {
                Console.WriteLine();
                Console.WriteLine($"=== {preset.Name} ===");
                Console.WriteLine(preset.Description);
                SimulationResult result = preset.Run();

                string csvPath = Path.Combine(outputDirectory, $"{preset.Name}.csv");
                TransientWriter.WriteCsv(result, csvPath);
                TransientWriter.WriteGroundTruthJson(result,
                    Path.Combine(outputDirectory, $"{preset.Name}.truth.json"));

                // Analyse a window of the transient: the full 4 MHz acquisition is far
                // more samples than a scalogram needs to show the structure.
                int maxSamples = options.GetInt("max-samples", 65536);
                double[] samples = result.Samples.Take(maxSamples).ToArray();
                AnalyzeTransient(preset.Name, samples, result.SamplingRate,
                    result.CalibrationCoefficient, outputDirectory, options, result.GroundTruth,
                    preset.SuggestedC0);
            }

            Console.WriteLine();
            Console.WriteLine($"Done. Output in {Path.GetFullPath(outputDirectory)}");
            return 0;
        }

        private static void AnalyzeTransient(string name, double[] samples, int samplingRate,
            double calibrationCoefficient, string outputDirectory, CommandLine options,
            IReadOnlyList<IsotopologueTruth>? groundTruth, float defaultC0 = 6f)
        {
            (int startOctave, int endOctave) = options.GetOctaveRange("octaves", 2, 6);
            int voices = options.GetInt("voices", 48);
            // Each preset declares the c0 its scenario needs: a charge state envelope
            // whose neighbours differ by ~3% cannot be resolved by the c0 = 6 that suits
            // a single isolated peak.
            float c0 = (float)options.GetDouble("c0", defaultC0);
            // An explicit --wavelet wins; otherwise a Morlet at the effective c0.
            IWavelet wavelet = options.Has("wavelet")
                ? WaveletCatalog.Parse(options.GetString("wavelet", "morlet"))
                : new MorletWavelet(c0);
            Colormap colormap = Colormaps.Get(options.GetString("colormap", "viridis"));
            IntensityScale scale = ParseScale(options.GetString("scale", "linear"));

            // Pick an octave range that actually brackets the ions when we know them.
            double windowSeconds = (double)samples.Length / samplingRate;
            if (groundTruth != null && groundTruth.Count > 0 && !options.Has("octaves"))
            {
                (startOctave, endOctave) = CwtWorkflow.BracketOctaves(
                    groundTruth, samplingRate, wavelet, windowSeconds);
            }

            ICwtBackend backend = options.GetString("backend", "auto").ToLowerInvariant() switch
            {
                "managed" => CwtBackends.Managed,
                "native" => CwtBackends.Native,
                _ => CwtBackends.Default
            };

            Console.WriteLine($"  CWT: octaves {startOctave}:{endOctave}, {voices} voices, " +
                              $"wavelet {wavelet.Name}, {samples.Length} samples");
            Console.WriteLine($"  resolves components " +
                              $">{wavelet.RelativeFrequencyResolution * 100:F1}% apart");

            var cwt = new CWTObject(samples, startOctave, endOctave, voices, c0, 0, false,
                samplingRate, calibrationCoefficient, outputDirectory)
            {
                Backend = backend,
                Wavelet = wavelet
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.CalculateTimeAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();
            cwt.FrequencyAxis.CalculateMZValues();

            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();

            WriteScalogramPng(modulus, Path.Combine(outputDirectory, $"{name}.scalogram.png"), colormap, scale);
            cwt.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, $"{name}.heatmap.png",
                CWTFrequencies.FrequencyUnits.TrueFrequency, name);

            IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                modulus, cwt.FrequencyAxis.MZValues!,
                relativeThreshold: 0.1,
                minimumRowSeparation: new CwtSettings { Voices = voices, Wavelet = wavelet }
                    .EffectivePeakSeparation());

            int peakCount = options.GetInt("peaks", 10);
            WritePeakTable(peaks.Take(peakCount).ToList(), groundTruth,
                Path.Combine(outputDirectory, $"{name}.peaks.csv"));
        }

        private static void WriteScalogramPng(double[,] modulus, string path, Colormap colormap, IntensityScale scale)
        {
            // Fixed output size rather than one capped at the matrix dimensions: a
            // two-octave CWT is only ~100 rows tall, which would otherwise render as a
            // 1200x100 sliver. Nearest-neighbour resampling keeps thin ridges visible.
            const int Width = 1200;
            const int Height = 600;
            int width = Width;
            int height = Height;
            byte[] rgba = ScalogramRenderer.ToRgba(modulus, width, height, colormap, scale);
            PngWriter.Write(path, rgba, width, height);
            Console.WriteLine($"  wrote {path}");
        }

        private static void WritePeakTable(IReadOnlyList<DetectedPeak> peaks,
            IReadOnlyList<IsotopologueTruth>? groundTruth, string path)
        {
            using var writer = new StreamWriter(path);
            bool scored = groundTruth != null && groundTruth.Count > 0;
            writer.WriteLine(scored
                ? "rank,recovered_mz,magnitude,nearest_true_mz,error_mz,error_ppm"
                : "rank,recovered_mz,magnitude");

            int rank = 1;
            foreach (DetectedPeak peak in peaks)
            {
                if (scored)
                {
                    IsotopologueTruth nearest = groundTruth!
                        .OrderBy(t => Math.Abs(t.Mz - peak.Frequency))
                        .First();
                    double error = peak.Frequency - nearest.Mz;
                    double ppm = error / nearest.Mz * 1e6;
                    writer.WriteLine(string.Join(",", new[]
                    {
                        rank.ToString(CultureInfo.InvariantCulture),
                        peak.Frequency.ToString("F5", CultureInfo.InvariantCulture),
                        peak.Magnitude.ToString("E4", CultureInfo.InvariantCulture),
                        nearest.Mz.ToString("F5", CultureInfo.InvariantCulture),
                        error.ToString("F5", CultureInfo.InvariantCulture),
                        ppm.ToString("F1", CultureInfo.InvariantCulture)
                    }));
                }
                else
                {
                    writer.WriteLine(string.Join(",", new[]
                    {
                        rank.ToString(CultureInfo.InvariantCulture),
                        peak.Frequency.ToString("F5", CultureInfo.InvariantCulture),
                        peak.Magnitude.ToString("E4", CultureInfo.InvariantCulture)
                    }));
                }
                rank++;
            }
            Console.WriteLine($"  wrote {path} ({peaks.Count} peaks)");

            if (scored && peaks.Count > 0)
            {
                DetectedPeak strongest = peaks[0];
                IsotopologueTruth nearest = groundTruth!
                    .OrderBy(t => Math.Abs(t.Mz - strongest.Frequency))
                    .First();
                double ppm = (strongest.Frequency - nearest.Mz) / nearest.Mz * 1e6;
                Console.WriteLine($"  strongest ridge m/z {strongest.Frequency:F4} vs true {nearest.Mz:F4} " +
                                  $"({ppm:F0} ppm)");
            }
        }

        private static IntensityScale ParseScale(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "log" or "logarithmic" => IntensityScale.Logarithmic,
                "sqrt" or "squareroot" => IntensityScale.SquareRoot,
                "linear" => IntensityScale.Linear,
                _ => throw new ArgumentException($"Unknown intensity scale '{name}'; use linear, log or sqrt")
            };
        }
    }
}
