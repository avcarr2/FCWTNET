using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET.Simulation;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class ChargeStateEnvelopeTests
    {
        /// <summary>Apo-myoglobin, the worked example throughout.</summary>
        private const double MyoglobinMass = 16940.0;

        [Test]
        public void RayleighChargeMatchesThePublishedRelation()
        {
            // z = 0.0778 * sqrt(M); for myoglobin that is ~10, which is what native ESI
            // actually produces.
            Assert.AreEqual(0.0778 * Math.Sqrt(MyoglobinMass), ChargeStateEnvelope.RayleighCharge(MyoglobinMass), 1e-9);
            Assert.AreEqual(10.1, ChargeStateEnvelope.RayleighCharge(MyoglobinMass), 0.2);
        }

        [Test]
        public void DenaturedCentroidIsMuchHigherThanNative()
        {
            double native = ChargeStateEnvelope.CentroidCharge(MyoglobinMass, IonizationMode.Native);
            double denatured = ChargeStateEnvelope.CentroidCharge(MyoglobinMass, IonizationMode.Denatured);

            Assert.AreEqual(10.1, native, 0.3, "native should sit at the Rayleigh limit");
            Assert.AreEqual(16.9, denatured, 0.3, "denatured should centre near m/z 1000");
            Assert.Greater(denatured, native);
        }

        [Test]
        public void DenaturedCentroidLandsNearMzOneThousand()
        {
            // That is the empirical regularity the denatured model encodes.
            double centroid = ChargeStateEnvelope.CentroidCharge(MyoglobinMass, IonizationMode.Denatured);
            int charge = (int)Math.Round(centroid);
            double mz = (MyoglobinMass + charge * IonSpecies.ProtonMass) / charge;
            Assert.AreEqual(1000, mz, 60);
        }

        [Test]
        public void DenaturedEnvelopeIsBroaderThanNative()
        {
            Assert.Greater(
                ChargeStateEnvelope.EnvelopeWidth(MyoglobinMass, IonizationMode.Denatured),
                ChargeStateEnvelope.EnvelopeWidth(MyoglobinMass, IonizationMode.Native));
        }

        [Test]
        public void NativeEnvelopeIsNarrowAndLowCharge()
        {
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = IonizationMode.Native });

            Assert.That(envelope.Count, Is.InRange(3, 7), "native myoglobin shows only a few charge states");
            Assert.That(envelope.Min(c => c.Charge), Is.InRange(6, 9));
            Assert.That(envelope.Max(c => c.Charge), Is.InRange(11, 14));
        }

        [Test]
        public void DenaturedEnvelopeIsBroadAndHighCharge()
        {
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = IonizationMode.Denatured });

            // Real denatured myoglobin runs roughly 9+ to 26+.
            Assert.That(envelope.Count, Is.InRange(12, 25));
            Assert.That(envelope.Min(c => c.Charge), Is.InRange(6, 10));
            Assert.That(envelope.Max(c => c.Charge), Is.InRange(23, 29));
        }

        [Test]
        public void AbundancesSumToOneAndPeakAtTheCentroid()
        {
            foreach (IonizationMode mode in Enum.GetValues<IonizationMode>())
            {
                IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                    MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = mode });

                Assert.AreEqual(1.0, envelope.Sum(c => c.Abundance), 1e-12, mode.ToString());

                double centroid = ChargeStateEnvelope.CentroidCharge(MyoglobinMass, mode);
                int mostAbundant = envelope.OrderByDescending(c => c.Abundance).First().Charge;
                Assert.AreEqual(centroid, mostAbundant, 1.0,
                    $"{mode}: the most abundant charge state should sit at the centroid");
            }
        }

        [Test]
        public void EnvelopeIsSortedByChargeAndCarriesMz()
        {
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(MyoglobinMass);
            for (int i = 1; i < envelope.Count; i++)
            {
                Assert.Greater(envelope[i].Charge, envelope[i - 1].Charge);
                // Higher charge means lower m/z.
                Assert.Less(envelope[i].Mz, envelope[i - 1].Mz);
            }
            ChargeStateAbundance first = envelope[0];
            Assert.AreEqual((MyoglobinMass + first.Charge * IonSpecies.ProtonMass) / first.Charge, first.Mz, 1e-9);
        }

        [Test]
        public void CentroidAndWidthCanBeOverridden()
        {
            var options = new ChargeStateEnvelopeOptions
            {
                Mode = IonizationMode.Native,
                CentroidCharge = 20,
                Width = 1.0
            };
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(MyoglobinMass, options);

            Assert.AreEqual(20, envelope.OrderByDescending(c => c.Abundance).First().Charge);
            // A narrow override should produce a narrow envelope regardless of mode.
            Assert.LessOrEqual(envelope.Count, 7);
        }

        [Test]
        public void ChargeRangeCanBeClamped()
        {
            var options = new ChargeStateEnvelopeOptions
            {
                Mode = IonizationMode.Denatured,
                MinCharge = 15,
                MaxCharge = 18
            };
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(MyoglobinMass, options);

            Assert.AreEqual(15, envelope.Min(c => c.Charge));
            Assert.AreEqual(18, envelope.Max(c => c.Charge));
            Assert.AreEqual(1.0, envelope.Sum(c => c.Abundance), 1e-12);
        }

        [Test]
        public void RaisingTheThresholdNarrowsTheEnvelope()
        {
            int wide = ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { RelativeThreshold = 0.01 }).Count;
            int narrow = ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { RelativeThreshold = 0.5 }).Count;
            Assert.Less(narrow, wide);
        }

        [Test]
        public void MaxChargeStatesCapsTheEnvelopeKeepingTheMostAbundant()
        {
            IReadOnlyList<ChargeStateAbundance> capped = ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { MaxChargeStates = 5 });
            Assert.AreEqual(5, capped.Count);

            double centroid = ChargeStateEnvelope.CentroidCharge(MyoglobinMass, IonizationMode.Denatured);
            // The survivors should straddle the centroid, not be an arbitrary five.
            Assert.LessOrEqual(capped.Min(c => c.Charge), centroid);
            Assert.GreaterOrEqual(capped.Max(c => c.Charge), centroid);
        }

        [Test]
        public void ChargeNeverDropsBelowOne()
        {
            // A small molecule's envelope would otherwise run to zero or negative charge.
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(500);
            Assert.GreaterOrEqual(envelope.Min(c => c.Charge), 1);
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ChargeStateEnvelope.RayleighCharge(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChargeStateEnvelope.Generate(-1));
            Assert.Throws<ArgumentException>(() => ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { RelativeThreshold = 0 }));
            Assert.Throws<ArgumentException>(() => ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { RelativeThreshold = 1 }));
            Assert.Throws<ArgumentException>(() => ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { MaxChargeStates = 0 }));
            Assert.Throws<ArgumentException>(() => ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { CentroidCharge = -1 }));
            Assert.Throws<ArgumentException>(() => ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { Width = 0 }));
            Assert.Throws<ArgumentException>(() => ChargeStateEnvelope.Generate(MyoglobinMass,
                new ChargeStateEnvelopeOptions { MinCharge = 20, MaxCharge = 10 }));
        }

        [Test]
        public void CreateSpeciesSharesTheAbundanceAcrossChargeStates()
        {
            IReadOnlyList<IonSpecies> species = ChargeStateEnvelope.CreateSpecies(
                MyoglobinMass,
                new ChargeStateEnvelopeOptions { Mode = IonizationMode.Native },
                isotopePeaks: 4,
                totalAbundance: 2.0);

            Assert.AreEqual(2.0, species.Sum(s => s.Abundance), 1e-12);
            Assert.IsTrue(species.All(s => s.IsotopePeaks == 4));
            Assert.IsTrue(species.All(s => Math.Abs(s.MonoisotopicMass - MyoglobinMass) < 1e-9));
            // One species per charge state, all distinct.
            Assert.AreEqual(species.Count, species.Select(s => s.Charge).Distinct().Count());
        }

        [Test]
        public void CreateSpeciesPassesThroughDecayAndDrift()
        {
            IReadOnlyList<IonSpecies> species = ChargeStateEnvelope.CreateSpecies(
                MyoglobinMass, isotopePeaks: 2, decayConstant: 0.02, frequencyDriftPerSecond: 0.5);

            Assert.IsTrue(species.All(s => s.DecayConstant == 0.02));
            Assert.IsTrue(species.All(s => s.FrequencyDriftPerSecond == 0.5));
        }

        [Test]
        public void ChargeStatesLandAtDistinctFrequenciesSpanningAboutAnOctave()
        {
            var config = new OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = Presets.SampleCalibrationCoefficient,
                SamplingRate = Presets.SampleSamplingRate,
                DurationSeconds = 0.001,
                Decay = DecayModel.None,
                SignalToNoise = null
            };
            var simulator = new OrbitrapSimulator(config);

            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = IonizationMode.Denatured });
            double[] frequencies = envelope.Select(c => simulator.FrequencyForMz(c.Mz)).ToArray();

            // Every charge state has its own frequency, all inside Nyquist.
            Assert.AreEqual(frequencies.Length, frequencies.Distinct().Count());
            Assert.IsTrue(frequencies.All(f => f < config.SamplingRate / 2.0));
            // Denatured myoglobin spans roughly one octave in frequency.
            double span = Math.Log2(frequencies.Max() / frequencies.Min());
            Assert.That(span, Is.InRange(0.5, 1.6));
        }

        [Test]
        public void NativeSpansLessFrequencyThanDenatured()
        {
            var config = new OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = Presets.SampleCalibrationCoefficient,
                SamplingRate = Presets.SampleSamplingRate,
                DurationSeconds = 0.001
            };
            var simulator = new OrbitrapSimulator(config);

            double Span(IonizationMode mode)
            {
                double[] f = ChargeStateEnvelope
                    .Generate(MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = mode })
                    .Select(c => simulator.FrequencyForMz(c.Mz))
                    .ToArray();
                return Math.Log2(f.Max() / f.Min());
            }

            Assert.Less(Span(IonizationMode.Native), Span(IonizationMode.Denatured));
        }

        [Test]
        public void PresetsExposeTheirChargeEnvelopeTemplate()
        {
            SimulationPreset denatured = Presets.Get("myoglobin-denatured");
            SimulationPreset native = Presets.Get("myoglobin-native");

            Assert.IsNotNull(denatured.ChargeEnvelope);
            Assert.IsNotNull(native.ChargeEnvelope);
            Assert.AreEqual(IonizationMode.Denatured, denatured.ChargeEnvelope!.Options.Mode);
            Assert.AreEqual(IonizationMode.Native, native.ChargeEnvelope!.Options.Mode);

            // Species count matches the envelope the template describes.
            Assert.AreEqual(denatured.ChargeEnvelope.Envelope().Count, denatured.Species.Count);
            Assert.Greater(denatured.Species.Count, native.Species.Count,
                "denatured should produce more charge states than native");

            // Presets whose species are given directly carry no template.
            Assert.IsNull(Presets.Get("single-peak").ChargeEnvelope);
        }

        [Test]
        public void TemplateCanRegenerateWithDifferentSettings()
        {
            ChargeEnvelopeTemplate template = Presets.Get("myoglobin-denatured").ChargeEnvelope!;
            IReadOnlyList<IonSpecies> asNative = template.CreateSpecies(
                new ChargeStateEnvelopeOptions { Mode = IonizationMode.Native });

            Assert.Less(asNative.Count, template.CreateSpecies().Count);
            Assert.Less(asNative.Max(s => s.Charge), template.CreateSpecies().Max(s => s.Charge));
        }
    }
}
