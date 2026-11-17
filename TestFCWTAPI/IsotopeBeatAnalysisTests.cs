using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET;
using FCWTNET.Analysis;
using FCWTNET.Simulation;
using FCWTNET.Transform;
using FCWTNET.Transform.Wavelets;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class IsotopeBeatAnalysisTests
    {
        private const double SampleCalibration = 2.11751487582899e14;

        [Test]
        public void BeatFrequencyAndChargeAreInverses()
        {
            double carrier = 460746;
            foreach (int z in new[] { 1, 8, 17, 30 })
            {
                double beat = IsotopeBeatAnalysis.BeatFrequencyForCharge(carrier, z, SampleCalibration);
                Assert.AreEqual(z, IsotopeBeatAnalysis.ChargeFromBeatFrequency(carrier, beat, SampleCalibration),
                    1e-9, $"charge {z}");
            }
        }

        /// <summary>
        /// The numbers that decide whether this is usable at all: myoglobin's beat is around
        /// 14 Hz, so a transient needs to run for a good fraction of a second.
        /// </summary>
        [Test]
        public void MyoglobinBeatIsAroundFourteenHertz()
        {
            // Apo-myoglobin 17+ sits near m/z 997.5, which is 460.7 kHz under this calibration.
            double beat = IsotopeBeatAnalysis.BeatFrequencyForCharge(460746, 17, SampleCalibration);
            Assert.AreEqual(13.6, beat, 0.5);
            Assert.AreEqual(73.4, 1000 / beat, 3.0, "beat period in ms");
        }

        [Test]
        public void HigherChargeBeatsMoreSlowly()
        {
            double low = IsotopeBeatAnalysis.BeatFrequencyForCharge(460746, 8, SampleCalibration);
            double high = IsotopeBeatAnalysis.BeatFrequencyForCharge(460746, 30, SampleCalibration);
            Assert.Greater(low, high);
        }

        [Test]
        public void RequiredTransientLengthIsReported()
        {
            double seconds = IsotopeBeatAnalysis.RequiredTransientSeconds(460746, 17, SampleCalibration);
            // 1.5 cycles of a 73 ms beat.
            Assert.AreEqual(0.11, seconds, 0.02);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                IsotopeBeatAnalysis.BeatFrequencyForCharge(0, 10, SampleCalibration));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                IsotopeBeatAnalysis.BeatFrequencyForCharge(1000, 0, SampleCalibration));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                IsotopeBeatAnalysis.BeatFrequencyForCharge(1000, 10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                IsotopeBeatAnalysis.ChargeFromBeatFrequency(1000, 0, SampleCalibration));
            Assert.Throws<ArgumentNullException>(() =>
                IsotopeBeatAnalysis.Analyse(null!, 1000, 100, SampleCalibration));
            Assert.Throws<ArgumentException>(() =>
                IsotopeBeatAnalysis.Analyse(new double[4], 1000, 100, SampleCalibration));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                IsotopeBeatAnalysis.Analyse(new double[64], 0, 100, SampleCalibration));
        }

        [Test]
        public void FindsNoBeatInAFlatEnvelope()
        {
            double[] flat = Enumerable.Repeat(1.0, 4096).ToArray();
            Assert.IsNull(IsotopeBeatAnalysis.Analyse(flat, 100_000, 10_000, 1e9));
        }

        [Test]
        public void FindsNoBeatInAPurelyDecayingEnvelope()
        {
            // An exponential decay must not be mistaken for a modulation.
            double[] decaying = Enumerable.Range(0, 8192).Select(i => Math.Exp(-i / 2000.0)).ToArray();
            Assert.IsNull(IsotopeBeatAnalysis.Analyse(decaying, 100_000, 10_000, 1e9));
        }

        /// <summary>
        /// Recovers the charge from a synthetic envelope modulated at exactly the frequency
        /// the physics predicts, including on top of a decay.
        /// </summary>
        [TestCase(5, false)]
        [TestCase(5, true)]
        [TestCase(12, false)]
        [TestCase(12, true)]
        public void RecoversChargeFromASyntheticBeat(int charge, bool withDecay)
        {
            const int samplingRate = 100_000;
            const double carrier = 10_000;
            const double calibration = 1e9;
            double beat = IsotopeBeatAnalysis.BeatFrequencyForCharge(carrier, charge, calibration);

            // Enough samples for several beat cycles.
            int n = (int)(6 * samplingRate / beat);
            double[] envelope = new double[n];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / samplingRate;
                double modulation = 1 + 0.4 * Math.Cos(2 * Math.PI * beat * t);
                envelope[i] = withDecay ? modulation * Math.Exp(-t / 0.05) : modulation;
            }

            IsotopeBeatResult? result = IsotopeBeatAnalysis.Analyse(envelope, samplingRate, carrier, calibration);
            Assert.IsNotNull(result, $"no beat found for charge {charge}");
            Assert.AreEqual(charge, result!.Charge, $"fractional charge was {result.FractionalCharge:F3}");
            Assert.AreEqual(beat, result.BeatFrequencyHz, beat * 0.05);
            Assert.Less(result.ChargeResidual, 0.2);
        }

        [Test]
        public void RejectsAWindowTooShortToContainEnoughBeats()
        {
            const int samplingRate = 100_000;
            const double carrier = 10_000;
            const double calibration = 1e9;
            double beat = IsotopeBeatAnalysis.BeatFrequencyForCharge(carrier, 10, calibration);

            // Less than one cycle: there is nothing to measure, and saying so is better
            // than reporting a charge from a trend.
            int n = (int)(0.6 * samplingRate / beat);
            double[] envelope = Enumerable.Range(0, n)
                .Select(i => 1 + 0.4 * Math.Cos(2 * Math.PI * beat * i / samplingRate))
                .ToArray();

            Assert.IsNull(IsotopeBeatAnalysis.Analyse(envelope, samplingRate, carrier, calibration));
        }

        /// <summary>
        /// The end-to-end claim: simulate two unresolved isotopes, transform with a CWT far
        /// too blunt to separate them, and still read the charge off the ridge's envelope.
        /// </summary>
        [TestCase(4)]
        [TestCase(7)]
        public void RecoversChargeFromASimulatedTransientThroughTheCwt(int charge)
        {
            // A deliberately small system so the beat is fast enough to test cheaply. The
            // beat period scales as 2*(m/z)/(spacing/z) carrier cycles regardless of
            // sampling rate, so a low m/z is what keeps the transient short.
            // Sized from beat = spacing * f / (2 * (m/z) * z): a small m/z and a high
            // carrier are what make the beat fast enough to see in a short window. Real
            // proteins sit at m/z ~1000 and beat at ~14 Hz, needing a transient of a
            // second; that is the physics, not a limitation of the method.
            const int samplingRate = 200_000;
            const double calibration = 2e10;
            double mass = 196.0;

            var config = new OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = calibration,
                SamplingRate = samplingRate,
                DurationSeconds = 0.35,
                Decay = DecayModel.Exponential,
                DefaultDecayConstant = 0.5,
                SignalToNoise = null,
                Seed = 31337
            };
            var species = new[]
            {
                new IonSpecies
                {
                    MonoisotopicMass = mass,
                    Charge = charge,
                    IsotopePeaks = 2,
                    InitialPhase = 0
                }
            };

            SimulationResult simulated = new OrbitrapSimulator(config).Simulate(species);
            double carrier = simulated.GroundTruth[0].Frequency;

            // Predicted beat, and a check that the test is actually exercising the effect.
            double expectedBeat = IsotopeBeatAnalysis.BeatFrequencyForCharge(carrier, charge, calibration);
            Assert.Greater(expectedBeat * config.DurationSeconds, 2,
                "test window must contain at least a couple of beat cycles");

            // A handful of scales around the carrier is all that is needed; the wavelet is
            // nowhere near sharp enough to resolve the isotopes, which is the point.
            var wavelet = new MorletWavelet(60);
            double normalised = carrier / samplingRate;
            var grid = ScaleGrid.ForFrequencyRange(normalised * 0.9, normalised * 1.1, 8, wavelet,
                simulated.Samples.Length);

            var cwt = new CWTObject(simulated.Samples, 0, 1, 8, 6f, 0, false,
                samplingRate, calibration)
            {
                ScaleGrid = grid
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();

            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            double[] perScale = RidgeExtraction.TimeAveragedMagnitude(modulus);
            int ridgeRow = Array.IndexOf(perScale, perScale.Max());

            int columns = modulus.GetLength(1);
            double[] envelope = new double[columns];
            for (int t = 0; t < columns; t++)
            {
                envelope[t] = modulus[ridgeRow, t];
            }

            // Trim the wavelet's edge effects before measuring.
            int margin = columns / 10;
            double[] interior = envelope.Skip(margin).Take(columns - 2 * margin).ToArray();

            IsotopeBeatResult? result = IsotopeBeatAnalysis.Analyse(
                interior, samplingRate, cwt.FrequencyAxis!.TrueFrequencies![ridgeRow], calibration);

            Assert.IsNotNull(result, "no beat detected in the ridge envelope");
            TestContext.WriteLine($"charge {charge}: {result}");
            Assert.AreEqual(charge, result!.Charge,
                $"expected charge {charge}, measured {result.FractionalCharge:F2} " +
                $"from a {result.BeatFrequencyHz:F1} Hz beat");
        }

        /// <summary>
        /// The bundled scenario, end to end: two peptides at different charges, one CWT
        /// blunt enough that neither one's isotopes are resolved, and both charges read off
        /// the ridge envelopes without ever grouping the peaks.
        /// </summary>
        [Test]
        public void RecoversBothChargesFromTheIsotopeBeatPreset()
        {
            SimulationPreset preset = Presets.Get("isotope-beat");
            SimulationResult simulated = preset.Run();

            var wavelet = new MorletWavelet(preset.SuggestedC0);
            var grid = ScaleGrid.ForMzRange(400, 900, simulated.CalibrationCoefficient,
                simulated.SamplingRate, 48, wavelet, simulated.Samples.Length);

            var cwt = new CWTObject(simulated.Samples, 0, 1, 48, preset.SuggestedC0, 0, false,
                simulated.SamplingRate, simulated.CalibrationCoefficient)
            {
                ScaleGrid = grid,
                Wavelet = wavelet
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();
            cwt.FrequencyAxis.CalculateMZValues();

            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                modulus, cwt.FrequencyAxis.MZValues!, relativeThreshold: 0.1);

            Assert.AreEqual(2, peaks.Count, "expected one ridge per species");

            int columns = modulus.GetLength(1);
            int margin = columns / 10;
            int count = columns - 2 * margin;

            var byCharge = new Dictionary<int, IsotopeBeatResult>();
            foreach (DetectedPeak peak in peaks)
            {
                var envelope = new double[count];
                for (int t = 0; t < count; t++)
                {
                    envelope[t] = modulus[peak.RowIndex, margin + t];
                }

                IsotopeBeatResult? beat = IsotopeBeatAnalysis.Analyse(envelope,
                    simulated.SamplingRate, cwt.FrequencyAxis.TrueFrequencies![peak.RowIndex],
                    simulated.CalibrationCoefficient);
                Assert.IsNotNull(beat, $"no beat on the ridge at m/z {peak.Position:F3}");
                TestContext.WriteLine($"m/z {peak.Position:F3}: {beat}");
                byCharge[beat!.Charge] = beat;
            }

            CollectionAssert.AreEquivalent(new[] { 2, 3 }, byCharge.Keys,
                "both charges should come straight off the envelopes");
            // The higher charge beats more slowly, which is the whole basis of the method.
            Assert.Greater(byCharge[2].BeatFrequencyHz, byCharge[3].BeatFrequencyHz);
            Assert.Greater(byCharge[2].ObservedBeats, 20);
            Assert.Greater(byCharge[3].ObservedBeats, 8);
        }

        /// <summary>
        /// Charge from the beat needs no assumption about which peaks belong together, so it
        /// settles the case the comb and voting methods cannot: a peak list holding only
        /// every third charge state, which is equally consistent with a third of the mass.
        /// </summary>
        [Test]
        public void BeatChargeDisambiguatesWhatPeakPositionsAloneCannot()
        {
            const double calibration = SampleCalibration;
            double mass = 16940.0;

            // The ambiguous list from ChargeStateDeconvolutionTests: charges 9, 12, 15...
            // are equally consistent with mass/3 at charges 3, 4, 5...
            foreach (int trueCharge in new[] { 9, 12, 15 })
            {
                double mz = mass / trueCharge + IonSpecies.ProtonMass;
                double carrier = Math.Sqrt(calibration / mz);

                double beatForTruth = IsotopeBeatAnalysis.BeatFrequencyForCharge(carrier, trueCharge, calibration);
                double beatForAlias = IsotopeBeatAnalysis.BeatFrequencyForCharge(carrier, trueCharge / 3, calibration);

                // The two hypotheses predict beats a factor of three apart, which is far
                // outside any plausible measurement error.
                Assert.AreEqual(3.0, beatForAlias / beatForTruth, 1e-9);
                Assert.AreEqual(trueCharge,
                    IsotopeBeatAnalysis.ChargeFromBeatFrequency(carrier, beatForTruth, calibration), 1e-9);
            }
        }
    }
}
