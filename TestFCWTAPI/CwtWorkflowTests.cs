using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET.Analysis;
using FCWTNET.Simulation;
using FCWTNET.Transform;
using FCWTNET.Visualization;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class CwtWorkflowTests
    {
        private static OrbitrapSimulatorConfig Config()
        {
            return new OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = 1e11,
                SamplingRate = 100_000,
                DurationSeconds = 0.08,
                Decay = DecayModel.None,
                SignalToNoise = null,
                Seed = 99
            };
        }

        private static CwtSettings Settings() => new()
        {
            StartOctave = 2,
            EndOctave = 5,
            Voices = 32,
            C0 = 6f,
            Backend = CwtBackends.Managed
        };

        private static IonSpecies[] OneIon() => new[]
        {
            new IonSpecies { MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 1, InitialPhase = 0 }
        };

        [Test]
        public void RunSimulationProducesAConsistentResult()
        {
            CwtWorkflow.Result result = new CwtWorkflow().RunSimulation(Config(), OneIon(), Settings());

            Assert.AreEqual(8000, result.Samples.Length);
            Assert.AreEqual(result.Samples.Length, result.Modulus.GetLength(1));
            Assert.AreEqual(result.Modulus.GetLength(0), result.MzAxis.Length);
            Assert.AreEqual(result.Modulus.GetLength(0), result.FrequencyAxis.Length);
            Assert.AreEqual(0.08, result.WindowSeconds, 1e-9);
            Assert.IsNotNull(result.GroundTruth);
            Assert.IsNotEmpty(result.Peaks);
        }

        [Test]
        public void MaxSamplesTruncatesTheWindow()
        {
            CwtSettings settings = Settings();
            settings.MaxSamples = 2048;
            CwtWorkflow.Result result = new CwtWorkflow().RunSimulation(Config(), OneIon(), settings);

            Assert.AreEqual(2048, result.Samples.Length);
            Assert.AreEqual(2048 / 100_000.0, result.WindowSeconds, 1e-9);
        }

        [Test]
        public void ZeroMaxSamplesKeepsEverything()
        {
            CwtSettings settings = Settings();
            settings.MaxSamples = 0;
            CwtWorkflow.Result result = new CwtWorkflow().RunSimulation(Config(), OneIon(), settings);
            Assert.AreEqual(8000, result.Samples.Length);
        }

        [Test]
        public void RenderMatchesTheRequestedSize()
        {
            CwtWorkflow.Result result = new CwtWorkflow().RunSimulation(Config(), OneIon(), Settings());
            byte[] pixels = result.Render(128, 64, Colormaps.Magma, IntensityScale.Logarithmic);
            Assert.AreEqual(128 * 64 * 4, pixels.Length);
        }

        [Test]
        public void AnalyseWorksWithoutGroundTruth()
        {
            SimulationResult simulated = new OrbitrapSimulator(Config()).Simulate(OneIon());
            CwtWorkflow.Result result = new CwtWorkflow().Analyse(
                simulated.Samples, simulated.SamplingRate, simulated.CalibrationCoefficient, Settings());

            Assert.IsNull(result.GroundTruth);
            Assert.IsNotEmpty(result.Peaks);
        }

        [Test]
        public void AnalyseRejectsEmptyInput()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CwtWorkflow().Analyse(null!, 1000, 1e11, Settings()));
            Assert.Throws<ArgumentException>(() =>
                new CwtWorkflow().Analyse(Array.Empty<double>(), 1000, 1e11, Settings()));
        }

        [Test]
        public void BracketOctavesContainsTheIonFrequencies()
        {
            var truth = new List<IsotopologueTruth>
            {
                new() { Frequency = 10_000, FrequencyDriftPerSecond = 0 }
            };
            (int start, int end) = CwtWorkflow.BracketOctaves(truth, 100_000, 6.0, 0.08);

            // The ion's scale must fall inside [2^start, 2^end].
            double scale = CwtScales.ScaleForNormalizedFrequency(10_000 / 100_000.0, 6.0);
            Assert.LessOrEqual(Math.Pow(2, start), scale);
            Assert.GreaterOrEqual(Math.Pow(2, end), scale);
        }

        /// <summary>
        /// A drifting component sweeps across scales; if only its starting frequency is
        /// considered the ridge runs off the top of the scalogram partway through.
        /// </summary>
        [Test]
        public void BracketOctavesCoversBothEndsOfADriftingSweep()
        {
            var truth = new List<IsotopologueTruth>
            {
                new() { Frequency = 10_000, FrequencyDriftPerSecond = 2.0 }
            };
            double window = 0.5; // frequency doubles over the window
            (int start, int end) = CwtWorkflow.BracketOctaves(truth, 100_000, 6.0, window);

            double startScale = CwtScales.ScaleForNormalizedFrequency(10_000 / 100_000.0, 6.0);
            double endScale = CwtScales.ScaleForNormalizedFrequency(20_000 / 100_000.0, 6.0);
            Assert.LessOrEqual(Math.Pow(2, start), Math.Min(startScale, endScale));
            Assert.GreaterOrEqual(Math.Pow(2, end), Math.Max(startScale, endScale));
        }

        [Test]
        public void BracketOctavesNeedsComponents()
        {
            Assert.Throws<ArgumentException>(() =>
                CwtWorkflow.BracketOctaves(Array.Empty<IsotopologueTruth>(), 1000, 6.0, 1.0));
        }

        [Test]
        public void BracketOctavesAlwaysSpansAtLeastOneOctave()
        {
            var truth = new List<IsotopologueTruth> { new() { Frequency = 10_000 } };
            (int start, int end) = CwtWorkflow.BracketOctaves(truth, 100_000, 6.0, 0.01);
            Assert.Greater(end, start);
        }
    }

    public class WaveformRendererTests
    {
        [Test]
        public void ProducesOpaqueRgbaOfTheRequestedSize()
        {
            double[] samples = Enumerable.Range(0, 1000)
                .Select(i => Math.Sin(2 * Math.PI * i / 50)).ToArray();
            byte[] pixels = WaveformRenderer.ToRgba(samples, 200, 80);

            Assert.AreEqual(200 * 80 * 4, pixels.Length);
            for (int i = 3; i < pixels.Length; i += 4)
            {
                Assert.AreEqual(255, pixels[i]);
            }
        }

        [Test]
        public void DrawsSomethingOtherThanBackground()
        {
            double[] samples = Enumerable.Range(0, 500)
                .Select(i => Math.Sin(2 * Math.PI * i / 25)).ToArray();
            var background = new RgbColor(0, 0, 0);
            var foreground = new RgbColor(255, 255, 255);
            byte[] pixels = WaveformRenderer.ToRgba(samples, 100, 50, background, foreground, background);

            int litPixels = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i] == 255)
                {
                    litPixels++;
                }
            }
            Assert.Greater(litPixels, 100, "the waveform should actually be drawn");
        }

        /// <summary>
        /// With more samples than pixels each column must span min to max, otherwise
        /// point-sampling would alias a high-frequency waveform into nonsense.
        /// </summary>
        [Test]
        public void DownsamplingPreservesPeakToPeakExtent()
        {
            // One full-scale excursion per output column.
            double[] samples = Enumerable.Range(0, 10000)
                .Select(i => i % 2 == 0 ? 1.0 : -1.0).ToArray();
            var background = new RgbColor(0, 0, 0);
            var foreground = new RgbColor(255, 255, 255);
            byte[] pixels = WaveformRenderer.ToRgba(samples, 100, 51, background, foreground, background);

            // Every column should be drawn from top to bottom.
            for (int x = 0; x < 100; x++)
            {
                int topOffset = (0 * 100 + x) * 4;
                int bottomOffset = (50 * 100 + x) * 4;
                Assert.AreEqual(255, pixels[topOffset], $"column {x} should reach the top");
                Assert.AreEqual(255, pixels[bottomOffset], $"column {x} should reach the bottom");
            }
        }

        [Test]
        public void HandlesFlatAndDegenerateInput()
        {
            // An all-zero signal must not divide by a zero peak.
            byte[] pixels = WaveformRenderer.ToRgba(new double[100], 20, 10);
            Assert.AreEqual(20 * 10 * 4, pixels.Length);

            Assert.Throws<ArgumentNullException>(() => WaveformRenderer.ToRgba(null!, 10, 10));
            Assert.Throws<ArgumentException>(() => WaveformRenderer.ToRgba(Array.Empty<double>(), 10, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveformRenderer.ToRgba(new double[10], 0, 10));
        }

        [Test]
        public void IgnoresNonFiniteSamples()
        {
            double[] samples = { 0, 1, double.NaN, -1, double.PositiveInfinity, 0.5 };
            Assert.DoesNotThrow(() => WaveformRenderer.ToRgba(samples, 12, 8));
        }
    }
}
