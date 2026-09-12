using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET.Simulation;
using FCWTNET.Transform;
using FCWTNET.Visualization;

namespace FCWTNET.Analysis
{
    /// <summary>Parameters for a CWT run, independent of where the signal came from.</summary>
    public class CwtSettings
    {
        public int StartOctave { get; set; } = 2;
        public int EndOctave { get; set; } = 5;
        public int Voices { get; set; } = 48;
        public float C0 { get; set; } = 6f;

        /// <summary>Cap on how many samples are transformed; 0 means all of them.</summary>
        public int MaxSamples { get; set; } = 65536;

        /// <summary>Transform implementation; null selects the platform default.</summary>
        public ICwtBackend? Backend { get; set; }

        /// <summary>Ignore ridges below this fraction of the strongest one.</summary>
        public double PeakRelativeThreshold { get; set; } = 0.05;

        /// <summary>
        /// Minimum separation between reported ridges, in rows. Null derives one from
        /// <see cref="Voices"/> and <see cref="C0"/>: a component occupies roughly
        /// c0-dependent bandwidth, so suppressing anything closer than half the wavelet's
        /// own resolution keeps a single peak from being reported twice without merging
        /// genuinely distinct neighbours -- which matters for a charge state envelope,
        /// where adjacent states can sit only two or three voices apart.
        /// </summary>
        public int? PeakMinimumRowSeparation { get; set; }

        /// <summary>The row separation actually used.</summary>
        public int EffectivePeakSeparation()
        {
            if (PeakMinimumRowSeparation.HasValue)
            {
                return Math.Max(1, PeakMinimumRowSeparation.Value);
            }
            // Rows per octave is Voices; the wavelet's FWHM in octaves is
            // log2(1 + df/f), so half of that in rows is a conservative floor.
            double resolutionOctaves = Math.Log2(1 + CwtScales.RelativeFrequencyResolution(C0));
            return Math.Max(1, (int)Math.Round(0.5 * resolutionOctaves * Voices));
        }
    }

    /// <summary>
    /// Ties simulation, transform, rendering and peak detection into one step.
    ///
    /// This is the whole pipeline with no UI attached, so the Avalonia app is a thin shell
    /// over something that can be exercised headlessly, and the same code path is
    /// available to the CLI and to tests.
    /// </summary>
    public class CwtWorkflow
    {
        /// <summary>The outcome of one run.</summary>
        public class Result
        {
            public required double[] Samples { get; init; }
            public required CWTObject Cwt { get; init; }
            public required double[,] Modulus { get; init; }
            public required IReadOnlyList<DetectedPeak> Peaks { get; init; }
            public IReadOnlyList<IsotopologueTruth>? GroundTruth { get; init; }

            /// <summary>Duration of the analysed window, in seconds.</summary>
            public double WindowSeconds { get; init; }

            /// <summary>m/z for each CWT row, ascending in frequency.</summary>
            public double[] MzAxis => Cwt.FrequencyAxis!.MZValues!;

            /// <summary>True frequency in Hz for each CWT row.</summary>
            public double[] FrequencyAxis => Cwt.FrequencyAxis!.TrueFrequencies!;

            /// <summary>Render the modulus to an RGBA buffer.</summary>
            public byte[] Render(int width, int height, Colormap? colormap = null,
                IntensityScale scale = IntensityScale.Linear)
            {
                return ScalogramRenderer.ToRgba(Modulus, width, height, colormap, scale);
            }
        }

        /// <summary>Run a simulation and analyse the result.</summary>
        public Result RunSimulation(OrbitrapSimulatorConfig config, IReadOnlyList<IonSpecies> species,
            CwtSettings settings)
        {
            SimulationResult simulated = new OrbitrapSimulator(config).Simulate(species);
            return Analyse(simulated.Samples, simulated.SamplingRate, simulated.CalibrationCoefficient,
                settings, simulated.GroundTruth);
        }

        /// <summary>Analyse a transient that already exists.</summary>
        public Result Analyse(double[] samples, int samplingRate, double calibrationCoefficient,
            CwtSettings settings, IReadOnlyList<IsotopologueTruth>? groundTruth = null)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }
            if (samples.Length == 0)
            {
                throw new ArgumentException("No samples to analyse", nameof(samples));
            }

            double[] window = settings.MaxSamples > 0 && samples.Length > settings.MaxSamples
                ? samples.Take(settings.MaxSamples).ToArray()
                : samples;

            var cwt = new CWTObject(window, settings.StartOctave, settings.EndOctave, settings.Voices,
                settings.C0, 0, false, samplingRate, calibrationCoefficient)
            {
                Backend = settings.Backend ?? CwtBackends.Default
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.CalculateTimeAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();
            cwt.FrequencyAxis.CalculateMZValues();

            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                modulus, cwt.FrequencyAxis.MZValues!,
                relativeThreshold: settings.PeakRelativeThreshold,
                minimumRowSeparation: settings.EffectivePeakSeparation());

            return new Result
            {
                Samples = window,
                Cwt = cwt,
                Modulus = modulus,
                Peaks = peaks,
                GroundTruth = groundTruth,
                WindowSeconds = (double)window.Length / samplingRate
            };
        }

        /// <summary>
        /// Octave range whose scales bracket the given ion frequencies, with half an
        /// octave of margin either side so a ridge is not clipped against the edge. A
        /// drifting component sweeps, so both ends of its sweep are taken into account.
        /// </summary>
        public static (int StartOctave, int EndOctave) BracketOctaves(
            IReadOnlyList<IsotopologueTruth> truth, int samplingRate, double c0, double windowSeconds)
        {
            if (truth == null || truth.Count == 0)
            {
                throw new ArgumentException("Need at least one component to bracket", nameof(truth));
            }

            double lowest = truth.Min(t => Math.Min(t.FrequencyAt(0), t.FrequencyAt(windowSeconds))) / samplingRate;
            double highest = truth.Max(t => Math.Max(t.FrequencyAt(0), t.FrequencyAt(windowSeconds))) / samplingRate;
            double largestScale = CwtScales.ScaleForNormalizedFrequency(lowest, c0);
            double smallestScale = CwtScales.ScaleForNormalizedFrequency(highest, c0);

            int start = (int)Math.Floor(Math.Log2(smallestScale) - 0.5);
            int end = (int)Math.Ceiling(Math.Log2(largestScale) + 0.5);
            start = Math.Max(0, start);
            return (start, Math.Max(start + 1, end));
        }
    }
}
