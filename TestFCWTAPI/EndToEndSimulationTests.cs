using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET;
using FCWTNET.Analysis;
using FCWTNET.Simulation;
using FCWTNET.Transform;
using NUnit.Framework;

namespace TestFCWTAPI
{
    /// <summary>
    /// Simulate a transient with known ion content, run it through the CWT, and check the
    /// m/z values that come back out. This is the test that says the whole pipeline works.
    /// </summary>
    public class EndToEndSimulationTests
    {
        /// <summary>
        /// A deliberately small acquisition: low sampling rate and a low calibration
        /// coefficient, chosen so a realistic m/z lands at a frequency a short transient
        /// can resolve without producing a huge CWT matrix.
        /// </summary>
        private static OrbitrapSimulatorConfig AnalysisConfig()
        {
            return new OrbitrapSimulatorConfig
            {
                // Puts m/z 1000 at 10 kHz against a 100 kHz sampling rate.
                CalibrationCoefficient = 1e11,
                SamplingRate = 100_000,
                DurationSeconds = 0.08, // 8000 samples
                Decay = DecayModel.Exponential,
                DefaultDecayConstant = 1.0,
                SignalToNoise = null,
                PinkNoiseFraction = 0,
                Seed = 4242
            };
        }

        /// <summary>
        /// Builds the CWT over a scale range that brackets the ions of interest.
        /// psoctave 2..5 with c0 = 6 covers roughly 0.0075 to 0.24 cycles/sample, i.e.
        /// 750 Hz to 24 kHz at 100 kHz sampling.
        /// </summary>
        private static CWTObject Analyse(SimulationResult result, int voices = 96)
        {
            var cwt = new CWTObject(
                result.Samples,
                psoctave: 2,
                pendoctave: 5,
                pnbvoice: voices,
                c0: 6f,
                nthreads: 0,
                use_optimization_schemes: false,
                samplingRate: result.SamplingRate,
                calibrationCoefficient: result.CalibrationCoefficient)
            {
                Backend = CwtBackends.Managed
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.CalculateTimeAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();
            cwt.FrequencyAxis.CalculateMZValues();
            return cwt;
        }

        [Test]
        public void RecoversTheMzOfASingleSimulatedIon()
        {
            OrbitrapSimulatorConfig config = AnalysisConfig();
            var simulator = new OrbitrapSimulator(config);
            var ion = new IonSpecies
            {
                MonoisotopicMass = 1000,
                Charge = 1,
                IsotopePeaks = 1,
                InitialPhase = 0
            };
            SimulationResult result = simulator.Simulate(new[] { ion });
            double expectedMz = result.GroundTruth[0].Mz;

            CWTObject cwt = Analyse(result);
            IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                cwt.OutputCWT!.ModulusCalculation(),
                cwt.FrequencyAxis!.MZValues!,
                relativeThreshold: 0.3,
                minimumRowSeparation: 10);

            Assert.IsNotEmpty(peaks, "expected at least one ridge");
            DetectedPeak strongest = peaks.OrderByDescending(p => p.Magnitude).First();

            // The Morlet wavelet at 96 voices/octave resolves about 0.7% per voice; allow
            // a couple of percent for the envelope and interpolation.
            Assert.AreEqual(expectedMz, strongest.Position, expectedMz * 0.02,
                $"expected m/z {expectedMz:F3}, recovered {strongest.Position:F3}");
        }

        [Test]
        public void RecoversTwoWellSeparatedSpecies()
        {
            OrbitrapSimulatorConfig config = AnalysisConfig();
            var simulator = new OrbitrapSimulator(config);
            var species = new[]
            {
                new IonSpecies { MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 1, InitialPhase = 0 },
                new IonSpecies { MonoisotopicMass = 4000, Charge = 1, IsotopePeaks = 1, InitialPhase = 0, Abundance = 0.7 }
            };
            SimulationResult result = simulator.Simulate(species);
            double[] expected = result.GroundTruth.Select(t => t.Mz).OrderBy(m => m).ToArray();

            CWTObject cwt = Analyse(result);
            IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                cwt.OutputCWT!.ModulusCalculation(),
                cwt.FrequencyAxis!.MZValues!,
                relativeThreshold: 0.2,
                minimumRowSeparation: 20);

            double[] recovered = peaks.OrderByDescending(p => p.Magnitude)
                                      .Take(2)
                                      .Select(p => p.Position)
                                      .OrderBy(m => m)
                                      .ToArray();

            Assert.AreEqual(2, recovered.Length, "expected two ridges");
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(expected[i], recovered[i], expected[i] * 0.03,
                    $"species {i}: expected m/z {expected[i]:F2}, recovered {recovered[i]:F2}");
            }
        }

        [Test]
        public void DecayIsVisibleAsAFadingRidge()
        {
            OrbitrapSimulatorConfig config = AnalysisConfig();
            config.DefaultDecayConstant = 0.02; // a quarter of the acquisition
            var simulator = new OrbitrapSimulator(config);
            SimulationResult result = simulator.Simulate(new[]
            {
                new IonSpecies { MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 1, InitialPhase = 0 }
            });

            CWTObject cwt = Analyse(result, voices: 32);
            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            double[] averaged = RidgeExtraction.TimeAveragedMagnitude(modulus);
            int ridgeRow = Array.IndexOf(averaged, averaged.Max());

            int columns = modulus.GetLength(1);
            // Sample the ridge early and late, away from the edges where the wavelet
            // support runs off the end of the signal.
            double early = modulus[ridgeRow, columns / 8];
            double late = modulus[ridgeRow, 7 * columns / 8];

            double elapsed = (7.0 / 8 - 1.0 / 8) * config.DurationSeconds;
            double expectedRatio = Math.Exp(-elapsed / config.DefaultDecayConstant);
            Assert.AreEqual(expectedRatio, late / early, expectedRatio * 0.25,
                "the ridge should decay at the simulated time constant");
        }

        [Test]
        public void DriftIsVisibleAsAMovingRidge()
        {
            OrbitrapSimulatorConfig config = AnalysisConfig();
            config.Decay = DecayModel.None;
            var simulator = new OrbitrapSimulator(config);
            // 1%/s over 80 ms shifts the frequency by 0.08%, which at 96 voices/octave
            // (0.7% per voice) is small; use a much larger drift so the ridge moves by
            // several voices and the test is unambiguous.
            var ion = new IonSpecies
            {
                MonoisotopicMass = 1000,
                Charge = 1,
                IsotopePeaks = 1,
                InitialPhase = 0,
                FrequencyDriftPerSecond = 1.0
            };
            SimulationResult result = simulator.Simulate(new[] { ion });

            CWTObject cwt = Analyse(result);
            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            int[] ridge = RidgeExtraction.RidgePerTimePoint(modulus);

            int columns = modulus.GetLength(1);
            int earlyRow = ridge[columns / 8];
            int lateRow = ridge[7 * columns / 8];

            // Frequency rises with time, and the axis ascends, so the ridge must move up.
            Assert.Greater(lateRow, earlyRow, "a rising frequency should move the ridge up the axis");

            double[] frequencies = cwt.FrequencyAxis!.TrueFrequencies!;
            double f0 = result.GroundTruth[0].Frequency;
            double tEarly = (columns / 8.0) / config.SamplingRate;
            double tLate = (7 * columns / 8.0) / config.SamplingRate;
            Assert.AreEqual(f0 * (1 + tEarly), frequencies[earlyRow], f0 * 0.02);
            Assert.AreEqual(f0 * (1 + tLate), frequencies[lateRow], f0 * 0.02);
        }

        [Test]
        public void RidgeSurvivesNoise()
        {
            OrbitrapSimulatorConfig config = AnalysisConfig();
            config.SignalToNoise = 5;
            config.PinkNoiseFraction = 0.3;
            var simulator = new OrbitrapSimulator(config);
            SimulationResult result = simulator.Simulate(new[]
            {
                new IonSpecies { MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 1 }
            });
            double expectedMz = result.GroundTruth[0].Mz;

            CWTObject cwt = Analyse(result);
            IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                cwt.OutputCWT!.ModulusCalculation(),
                cwt.FrequencyAxis!.MZValues!,
                relativeThreshold: 0.4,
                minimumRowSeparation: 20);

            Assert.IsNotEmpty(peaks);
            DetectedPeak strongest = peaks.OrderByDescending(p => p.Magnitude).First();
            Assert.AreEqual(expectedMz, strongest.Position, expectedMz * 0.03);
        }

        [Test]
        public void SimulatedTransientFlowsThroughTransientDataIntoTheCwt()
        {
            // The writer emits the format TransientData parses, so a simulated file can be
            // analysed exactly like a recorded one.
            OrbitrapSimulatorConfig config = AnalysisConfig();
            SimulationResult result = new OrbitrapSimulator(config).Simulate(new[]
            {
                new IonSpecies { MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 1, InitialPhase = 0 }
            });

            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"fcwtnet-e2e-{Guid.NewGuid():N}.csv");
            try
            {
                TransientWriter.WriteCsv(result, path);
                var loaded = new TransientData(path);
                loaded.ImportTransientData();

                var cwt = new CWTObject(loaded.Data!, 2, 5, 48, 6f, 0, false,
                    loaded.SamplingRate, loaded.CalibrationFactor)
                {
                    Backend = CwtBackends.Managed
                };
                cwt.PerformCWT();
                cwt.CalculateFrequencyAxis();
                cwt.FrequencyAxis!.CalculateTrueFrequencies();
                cwt.FrequencyAxis.CalculateMZValues();

                IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                    cwt.OutputCWT!.ModulusCalculation(),
                    cwt.FrequencyAxis.MZValues!,
                    relativeThreshold: 0.3,
                    minimumRowSeparation: 10);

                Assert.IsNotEmpty(peaks);
                double expectedMz = result.GroundTruth[0].Mz;
                Assert.AreEqual(expectedMz, peaks.OrderByDescending(p => p.Magnitude).First().Position,
                    expectedMz * 0.03);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Test]
        public void PeakFinderRejectsMismatchedAxes()
        {
            Assert.Throws<ArgumentException>(() =>
                RidgeExtraction.FindPeaks(new double[5], new double[4]));
            Assert.Throws<ArgumentNullException>(() =>
                RidgeExtraction.FindPeaks((double[])null!, new double[4]));
            Assert.Throws<ArgumentNullException>(() =>
                RidgeExtraction.TimeAveragedMagnitude(null!));
        }

        [Test]
        public void PeakFinderInterpolatesBetweenRows()
        {
            // A symmetric triple peaks exactly on the centre row; skewing it moves the
            // interpolated position toward the heavier side.
            double[] axis = { 1.0, 2.0, 4.0, 8.0, 16.0 };
            var symmetric = RidgeExtraction.FindPeaks(new[] { 0.0, 1.0, 2.0, 1.0, 0.0 }, axis);
            Assert.AreEqual(1, symmetric.Count);
            Assert.AreEqual(2.0, symmetric[0].InterpolatedRow, 1e-9);
            Assert.AreEqual(4.0, symmetric[0].Position, 1e-9);

            var skewed = RidgeExtraction.FindPeaks(new[] { 0.0, 1.5, 2.0, 1.0, 0.0 }, axis);
            Assert.Greater(skewed[0].InterpolatedRow, 1.5);
            Assert.Less(skewed[0].InterpolatedRow, 2.0);
            Assert.Less(skewed[0].Position, 4.0);
        }
    }
}
