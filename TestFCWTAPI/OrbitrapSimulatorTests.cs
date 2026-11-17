using System;
using System.IO;
using System.Linq;
using FCWTNET;
using FCWTNET.Simulation;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class OrbitrapSimulatorTests
    {
        private static OrbitrapSimulatorConfig QuickConfig()
        {
            // 40 kHz over 50 ms is 2000 samples: fast, and low enough that a 1046 Th ion
            // would break Nyquist, so the tests below pick frequencies deliberately.
            return new OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = 1e9,
                SamplingRate = 40_000,
                DurationSeconds = 0.05,
                Decay = DecayModel.None,
                SignalToNoise = null,
                PinkNoiseFraction = 0,
                Seed = 12345
            };
        }

        private static IonSpecies SingleIon(double mass = 1000, int charge = 1, double phase = 0)
        {
            return new IonSpecies
            {
                MonoisotopicMass = mass,
                Charge = charge,
                Abundance = 1.0,
                IsotopePeaks = 1,
                InitialPhase = phase
            };
        }

        [Test]
        public void FrequencyAndMzAreInverseUnderTheCalibration()
        {
            var simulator = new OrbitrapSimulator(QuickConfig());
            double mz = 1046.5;
            double frequency = simulator.FrequencyForMz(mz);
            Assert.AreEqual(mz, simulator.MzForFrequency(frequency), 1e-9);
        }

        [Test]
        public void FrequencyMatchesTheAnalysisSideCalibration()
        {
            // The simulator inverts the same m/z = A/f^2 relation CWTFrequencies uses, so
            // a frequency generated here must map back through CWTFrequencies to the same m/z.
            var config = QuickConfig();
            var simulator = new OrbitrapSimulator(config);
            double mz = 1046.5;
            double frequency = simulator.FrequencyForMz(mz);

            var frequencies = new CWTFrequencies(
                new[] { frequency / config.SamplingRate },
                pnbvoice: 10,
                samplingRate: config.SamplingRate,
                calibrationCoefficient: config.CalibrationCoefficient);
            frequencies.CalculateTrueFrequencies();
            frequencies.CalculateMZValues();

            Assert.AreEqual(frequency, frequencies.TrueFrequencies![0], frequency * 1e-12);
            Assert.AreEqual(mz, frequencies.MZValues![0], mz * 1e-9);
        }

        [Test]
        public void MzForIsotopeUsesChargeScaledSpacing()
        {
            var ion = new IonSpecies { MonoisotopicMass = 2000, Charge = 4 };
            double spacing = ion.MzForIsotope(1) - ion.MzForIsotope(0);
            Assert.AreEqual(IonSpecies.IsotopeSpacing / 4, spacing, 1e-12);
        }

        [Test]
        public void MzAccountsForChargeCarryingProtons()
        {
            var ion = new IonSpecies { MonoisotopicMass = 2000, Charge = 2 };
            Assert.AreEqual((2000 + 2 * IonSpecies.ProtonMass) / 2, ion.MonoisotopicMz, 1e-12);
        }

        [Test]
        public void IsotopeIntensitiesSumToOneAndPeakWhereExpected()
        {
            double[] small = IsotopeEnvelope.RelativeIntensities(1000, 6);
            Assert.AreEqual(1.0, small.Sum(), 1e-12);
            // lambda = 0.000594*1000 - 0.03091 = 0.563, so the monoisotopic peak dominates.
            Assert.AreEqual(0, Array.IndexOf(small, small.Max()));

            double[] large = IsotopeEnvelope.RelativeIntensities(20000, 30);
            Assert.AreEqual(1.0, large.Sum(), 1e-12);
            // lambda = 11.85, so the cluster peaks well away from monoisotopic.
            int peak = Array.IndexOf(large, large.Max());
            Assert.AreEqual(11, peak, 1.0, "expected the mode near floor(lambda)");
        }

        [Test]
        public void SuggestedPeakCountGrowsWithMass()
        {
            Assert.Less(IsotopeEnvelope.SuggestedPeakCount(1000), IsotopeEnvelope.SuggestedPeakCount(20000));
            Assert.GreaterOrEqual(IsotopeEnvelope.SuggestedPeakCount(100), 1);
        }

        [Test]
        public void SameSeedProducesIdenticalTransients()
        {
            var config = QuickConfig();
            config.SignalToNoise = 10;
            config.PinkNoiseFraction = 0.5;
            // No fixed phase, so the RNG drives phases as well as noise.
            var species = new[] { new IonSpecies { MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 3 } };

            double[] first = new OrbitrapSimulator(config).Simulate(species).Samples;
            double[] second = new OrbitrapSimulator(config).Simulate(species).Samples;
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void DifferentSeedsProduceDifferentTransients()
        {
            var config = QuickConfig();
            config.SignalToNoise = 10;
            var species = new[] { SingleIon() };

            double[] first = new OrbitrapSimulator(config).Simulate(species).Samples;
            config.Seed += 1;
            double[] second = new OrbitrapSimulator(config).Simulate(species).Samples;
            Assert.IsFalse(first.SequenceEqual(second));
        }

        [Test]
        public void NoiselessSingleIonIsAPureCosine()
        {
            var config = QuickConfig();
            var simulator = new OrbitrapSimulator(config);
            IonSpecies ion = SingleIon();
            SimulationResult result = simulator.Simulate(new[] { ion });

            double frequency = simulator.FrequencyForMz(ion.MonoisotopicMz);
            for (int i = 0; i < result.Samples.Length; i += 37)
            {
                double t = (double)i / config.SamplingRate;
                double expected = Math.Cos(2 * Math.PI * frequency * t);
                Assert.AreEqual(expected, result.Samples[i], 1e-9);
            }
        }

        [Test]
        public void ExponentialEnvelopeDecaysAtTheRequestedTimeConstant()
        {
            var config = QuickConfig();
            config.Decay = DecayModel.Exponential;
            config.DefaultDecayConstant = 0.01;
            var simulator = new OrbitrapSimulator(config);
            // Phase 0 means sample 0 sits at the envelope peak.
            SimulationResult result = simulator.Simulate(new[] { SingleIon() });

            // After one time constant the envelope should be down by 1/e. Compare
            // envelopes rather than samples by taking the local peak amplitude.
            double envelopeAt(double seconds)
            {
                int centre = (int)(seconds * config.SamplingRate);
                int window = config.SamplingRate / 2000;
                return Enumerable.Range(centre - window, 2 * window)
                                 .Select(i => Math.Abs(result.Samples[i]))
                                 .Max();
            }

            double atStart = envelopeAt(0.005);
            double atOneTau = envelopeAt(0.005 + config.DefaultDecayConstant);
            Assert.AreEqual(Math.Exp(-1), atOneTau / atStart, 0.02);
        }

        [Test]
        public void GaussianEnvelopeDecaysFasterLateAndSlowerEarly()
        {
            var config = QuickConfig();
            config.DefaultDecayConstant = 0.02;
            config.Decay = DecayModel.Gaussian;
            SimulationResult gaussian = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });
            config.Decay = DecayModel.Exponential;
            SimulationResult exponential = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });

            // exp(-(t/tau)^2/2) exceeds exp(-t/tau) while t < 2*tau.
            int early = (int)(0.005 * config.SamplingRate);
            Assert.Greater(Math.Abs(gaussian.Samples[early]), Math.Abs(exponential.Samples[early]));
        }

        /// <summary>
        /// The phase of a drifting component must be the integral of its instantaneous
        /// frequency. Writing phi(t) = 2*pi*f(t)*t instead doubles the chirp rate, which
        /// this test detects by measuring the frequency at the end of the transient.
        /// </summary>
        [Test]
        public void FrequencyDriftIntegratesPhaseRatherThanScalingIt()
        {
            var config = QuickConfig();
            config.DurationSeconds = 0.2;
            const double drift = 0.5; // large, to make the effect unmistakable
            var ion = SingleIon();
            ion.FrequencyDriftPerSecond = drift;

            var simulator = new OrbitrapSimulator(config);
            SimulationResult result = simulator.Simulate(new[] { ion });
            double f0 = result.GroundTruth[0].Frequency;

            // Total accumulated phase over the transient, from the analytic form.
            double t = config.DurationSeconds;
            double expectedPhase = 2 * Math.PI * f0 * (t + 0.5 * drift * t * t);
            // The wrong formulation would give 2*pi*f0*(1 + drift*t)*t, i.e. twice the
            // drift contribution.
            double wrongPhase = 2 * Math.PI * f0 * (1 + drift * t) * t;
            Assert.AreNotEqual(expectedPhase, wrongPhase, "test would not discriminate");

            int lastIndex = result.Samples.Length - 1;
            double tLast = (double)lastIndex / config.SamplingRate;
            double phaseAtLast = 2 * Math.PI * f0 * (tLast + 0.5 * drift * tLast * tLast);
            Assert.AreEqual(Math.Cos(phaseAtLast), result.Samples[lastIndex], 1e-6);
        }

        [Test]
        public void InstantaneousFrequencyReportsDrift()
        {
            var truth = new IsotopologueTruth { Frequency = 1000, FrequencyDriftPerSecond = 0.01 };
            Assert.AreEqual(1000, truth.FrequencyAt(0), 1e-12);
            Assert.AreEqual(1010, truth.FrequencyAt(1), 1e-12);
        }

        [Test]
        public void NoiseHitsTheRequestedSignalToNoiseRatio()
        {
            var config = QuickConfig();
            var noiseless = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });

            config.SignalToNoise = 20;
            var noisy = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });

            double Rms(double[] v) => Math.Sqrt(v.Sum(x => x * x) / v.Length);
            double[] noise = noisy.Samples.Zip(noiseless.Samples, (a, b) => a - b).ToArray();
            double measured = Rms(noiseless.Samples) / Rms(noise);
            Assert.AreEqual(20, measured, 20 * 0.05);
        }

        [Test]
        public void PinkNoiseIsConcentratedAtLowFrequencies()
        {
            var config = QuickConfig();
            config.SignalToNoise = 1;
            config.PinkNoiseFraction = 1.0;
            var pink = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });

            config.PinkNoiseFraction = 0.0;
            var white = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });

            var noiseless = new OrbitrapSimulator(
                new OrbitrapSimulatorConfig
                {
                    CalibrationCoefficient = config.CalibrationCoefficient,
                    SamplingRate = config.SamplingRate,
                    DurationSeconds = config.DurationSeconds,
                    Decay = DecayModel.None,
                    SignalToNoise = null,
                    Seed = config.Seed
                }).Simulate(new[] { SingleIon() });

            double[] pinkNoise = pink.Samples.Zip(noiseless.Samples, (a, b) => a - b).ToArray();
            double[] whiteNoise = white.Samples.Zip(noiseless.Samples, (a, b) => a - b).ToArray();

            // Successive differences suppress low frequencies, so pink noise (dominated by
            // low frequencies) should have a much smaller mean absolute difference
            // relative to its own amplitude than white noise does.
            double Roughness(double[] v)
            {
                double diff = 0;
                for (int i = 1; i < v.Length; i++)
                {
                    diff += Math.Abs(v[i] - v[i - 1]);
                }
                double magnitude = v.Sum(Math.Abs);
                return diff / magnitude;
            }

            Assert.Less(Roughness(pinkNoise), Roughness(whiteNoise) * 0.8,
                "1/f noise should vary more slowly than white noise");
        }

        [Test]
        public void AdcQuantisesToDiscreteLevels()
        {
            var config = QuickConfig();
            config.AdcBits = 6;
            config.AdcFullScale = 1.0;
            var result = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });

            double step = 1.0 / Math.Pow(2, 5);
            foreach (double sample in result.Samples)
            {
                double codes = sample / step;
                Assert.AreEqual(Math.Round(codes), codes, 1e-9, "samples must land on ADC codes");
                Assert.LessOrEqual(Math.Abs(sample), 1.0 + 1e-12, "samples must stay within full scale");
            }
            // A 6-bit converter cannot produce more than 2^6 distinct values.
            Assert.LessOrEqual(result.Samples.Distinct().Count(), 64);
        }

        [Test]
        public void AdcClipsBeyondFullScale()
        {
            var config = QuickConfig();
            config.AdcBits = 8;
            config.AdcFullScale = 0.25; // the noiseless cosine reaches 1.0
            var result = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });

            // A two's-complement converter is asymmetric: codes run from -2^(n-1) to
            // 2^(n-1)-1, so the positive rail sits one step below full scale.
            double step = 0.25 / Math.Pow(2, 7);
            double positiveRail = 0.25 - step;
            Assert.AreEqual(positiveRail, result.Samples.Max(), 1e-12);
            Assert.AreEqual(-0.25, result.Samples.Min(), 1e-12);
            // Clipping flattens the peaks, so the rail codes repeat.
            Assert.Greater(result.Samples.Count(s => s >= positiveRail - 1e-12), 10);
            Assert.Greater(result.Samples.Count(s => s <= -0.25 + 1e-12), 10);
        }

        [Test]
        public void RejectsIonsAboveNyquist()
        {
            var config = QuickConfig();
            var simulator = new OrbitrapSimulator(config);
            // A very light ion oscillates too fast for a 40 kHz sampling rate.
            var tooFast = new IonSpecies { MonoisotopicMass = 1, Charge = 1, IsotopePeaks = 1 };
            var exception = Assert.Throws<ArgumentException>(() => simulator.Simulate(new[] { tooFast }));
            StringAssert.Contains("Nyquist", exception!.Message);
        }

        [Test]
        public void RejectsInvalidConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new OrbitrapSimulator(null!));
            Assert.Throws<ArgumentException>(() =>
                new OrbitrapSimulator(new OrbitrapSimulatorConfig { SamplingRate = 0 }));
            Assert.Throws<ArgumentException>(() =>
                new OrbitrapSimulator(new OrbitrapSimulatorConfig { DurationSeconds = 0 }));
            Assert.Throws<ArgumentException>(() =>
                new OrbitrapSimulator(new OrbitrapSimulatorConfig { PinkNoiseFraction = 1.5 }));
            Assert.Throws<ArgumentException>(() =>
                new OrbitrapSimulator(QuickConfig()).Simulate(Array.Empty<IonSpecies>()));
        }

        [Test]
        public void GroundTruthCoversEveryIsotopologue()
        {
            var config = QuickConfig();
            var species = new[]
            {
                new IonSpecies { MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 3 },
                new IonSpecies { MonoisotopicMass = 1200, Charge = 2, IsotopePeaks = 4 }
            };
            SimulationResult result = new OrbitrapSimulator(config).Simulate(species);

            Assert.AreEqual(7, result.GroundTruth.Count);
            // Amplitudes of each cluster sum to that species' abundance.
            Assert.AreEqual(1.0, result.GroundTruth.Take(3).Sum(t => t.Amplitude), 1e-12);
            Assert.AreEqual(1.0, result.GroundTruth.Skip(3).Sum(t => t.Amplitude), 1e-12);
            Assert.AreEqual(new[] { 0, 1, 2, 0, 1, 2, 3 }, result.GroundTruth.Select(t => t.IsotopeIndex).ToArray());
        }

        [Test]
        public void TransientRoundTripsThroughTransientData()
        {
            var config = QuickConfig();
            SimulationResult result = new OrbitrapSimulator(config).Simulate(new[] { SingleIon() });

            string path = Path.Combine(Path.GetTempPath(), $"fcwtnet-roundtrip-{Guid.NewGuid():N}.csv");
            try
            {
                TransientWriter.WriteCsv(result, path);
                var reloaded = new TransientData(path);
                reloaded.ImportTransientData();

                Assert.AreEqual(config.SamplingRate, reloaded.SamplingRate);
                Assert.AreEqual(config.CalibrationCoefficient, reloaded.CalibrationFactor!.Value,
                    config.CalibrationCoefficient * 1e-12);
                Assert.AreEqual(result.Samples.Length, reloaded.Data!.Length);
                for (int i = 0; i < result.Samples.Length; i += 53)
                {
                    Assert.AreEqual(result.Samples[i], reloaded.Data[i], 1e-12);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void WriterRejectsNonCsvPaths()
        {
            SimulationResult result = new OrbitrapSimulator(QuickConfig()).Simulate(new[] { SingleIon() });
            Assert.Throws<ArgumentException>(() => TransientWriter.WriteCsv(result, "transient.txt"));
        }

        [Test]
        public void GroundTruthJsonIsWritten()
        {
            SimulationResult result = new OrbitrapSimulator(QuickConfig()).Simulate(new[] { SingleIon() });
            string path = Path.Combine(Path.GetTempPath(), $"fcwtnet-truth-{Guid.NewGuid():N}.json");
            try
            {
                TransientWriter.WriteGroundTruthJson(result, path);
                string json = File.ReadAllText(path);
                Assert.IsNotEmpty(json);
                StringAssert.Contains("Mz", json);
                StringAssert.Contains("Frequency", json);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void EveryPresetRunsAndStaysWithinNyquist()
        {
            foreach (SimulationPreset preset in Presets.All.Values)
            {
                SimulationResult result = preset.Run();
                Assert.Greater(result.Samples.Length, 0, preset.Name);
                Assert.Greater(result.GroundTruth.Count, 0, preset.Name);
                foreach (IsotopologueTruth component in result.GroundTruth)
                {
                    Assert.Less(component.Frequency, result.NyquistFrequency,
                        $"{preset.Name}: m/z {component.Mz:F3} exceeds Nyquist");
                }
            }
        }

        [Test]
        public void UnknownPresetNameIsReportedWithTheAvailableOnes()
        {
            var exception = Assert.Throws<ArgumentException>(() => Presets.Get("no-such-preset"));
            StringAssert.Contains("single-peak", exception!.Message);
        }

        [Test]
        public void MyoglobinPresetLandsNearTheSampleTransientFrequency()
        {
            // Apo-myoglobin at 13+ is around m/z 1304, which under the bundled
            // calibration oscillates near 403 kHz -- comfortably inside a 4 MHz
            // acquisition and consistent with the real sample transient.
            SimulationPreset preset = Presets.Get("myoglobin-13plus");
            SimulationResult result = preset.Run();
            IsotopologueTruth monoisotopic = result.GroundTruth[0];

            Assert.AreEqual(1304, monoisotopic.Mz, 2.0);
            Assert.AreEqual(403_000, monoisotopic.Frequency, 5_000);
        }
    }
}
