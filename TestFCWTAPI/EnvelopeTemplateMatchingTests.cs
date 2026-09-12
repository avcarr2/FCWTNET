using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET.Analysis;
using FCWTNET.Simulation;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class EnvelopeTemplateMatchingTests
    {
        private const double MyoglobinMass = 16940.0;
        private const double UbiquitinMass = 8560.0;

        /// <summary>The peak list an ideal detector would return for one protein.</summary>
        private static (List<double> Mz, List<double> Magnitude) Observe(
            double mass, IonizationMode mode, double scale = 1.0)
        {
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                mass, new ChargeStateEnvelopeOptions { Mode = mode });
            return (envelope.Select(s => s.Mz).ToList(),
                    envelope.Select(s => s.Abundance * scale).ToList());
        }

        [TestCase(IonizationMode.Denatured)]
        [TestCase(IonizationMode.Native)]
        public void MatchesItsOwnEnvelopeExactly(IonizationMode mode)
        {
            (List<double> mz, List<double> mag) = Observe(MyoglobinMass, mode);

            EnvelopeMatch? match = EnvelopeTemplateMatching.Match(mz, mag, MyoglobinMass, mode);

            Assert.IsNotNull(match);
            Assert.AreEqual(1.0, match!.Similarity, 1e-9, "the observed pattern is the model's own");
            Assert.AreEqual(1.0, match.Coverage, 1e-12, "every predicted state was observed");
            Assert.AreEqual(mz.Count, match.Peaks.Count);
        }

        /// <summary>
        /// Cosine similarity compares shape, not scale, which is what makes it the right
        /// measure: the envelope model predicts relative abundance and nothing about how
        /// many ions were actually trapped.
        /// </summary>
        [Test]
        public void ScoreIsIndifferentToOverallIntensity()
        {
            (List<double> mz, List<double> mag) = Observe(MyoglobinMass, IonizationMode.Denatured);
            var scaled = mag.Select(m => m * 4217.0).ToList();

            EnvelopeMatch? a = EnvelopeTemplateMatching.Match(mz, mag, MyoglobinMass, IonizationMode.Denatured);
            EnvelopeMatch? b = EnvelopeTemplateMatching.Match(mz, scaled, MyoglobinMass, IonizationMode.Denatured);

            Assert.AreEqual(a!.Score, b!.Score, 1e-12);
        }

        [Test]
        public void WrongMassScoresWorseThanTheRightOne()
        {
            (List<double> mz, List<double> mag) = Observe(MyoglobinMass, IonizationMode.Denatured);

            EnvelopeMatch? truth = EnvelopeTemplateMatching.BestMatch(mz, mag, MyoglobinMass);
            // A mass nowhere near the true one puts the predicted comb between the observed
            // peaks, so few states are matched and the intensities do not line up.
            EnvelopeMatch? wrong = EnvelopeTemplateMatching.BestMatch(mz, mag, MyoglobinMass * 1.37);

            Assert.IsNotNull(truth);
            Assert.Greater(truth!.Score, 0.9);
            Assert.Less(wrong?.Score ?? 0, 0.5);
        }

        /// <summary>
        /// The point of the method beyond corroborating a mass: which ionisation regime the
        /// sample was in falls out of the fit, because folded and unfolded proteins populate
        /// entirely different charge states.
        /// </summary>
        [TestCase(IonizationMode.Native)]
        [TestCase(IonizationMode.Denatured)]
        public void RecoversWhichIonizationModeProducedThePeaks(IonizationMode actual)
        {
            (List<double> mz, List<double> mag) = Observe(MyoglobinMass, actual);

            EnvelopeMatch? best = EnvelopeTemplateMatching.BestMatch(mz, mag, MyoglobinMass);

            Assert.IsNotNull(best);
            Assert.AreEqual(actual, best!.Mode,
                $"scored {best.Similarity:F3} similarity at {best.Coverage:F2} coverage");
        }

        [Test]
        public void ReportsTheModelCentroidCharge()
        {
            (List<double> mz, List<double> mag) = Observe(MyoglobinMass, IonizationMode.Native);

            EnvelopeMatch? match = EnvelopeTemplateMatching.Match(mz, mag, MyoglobinMass, IonizationMode.Native);

            // The Rayleigh limit for a 16.9 kDa globular protein, which is what makes native
            // myoglobin come out around 10+.
            Assert.AreEqual(ChargeStateEnvelope.RayleighCharge(MyoglobinMass), match!.CentroidCharge, 1e-9);
            Assert.AreEqual(10.1, match.CentroidCharge, 0.3);
        }

        [Test]
        public void MissingChargeStatesCostCoverageButNotSimilarity()
        {
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = IonizationMode.Denatured });

            // Drop the four weakest states, as a real detection threshold would.
            List<ChargeStateAbundance> seen = envelope
                .OrderBy(s => s.Abundance).Skip(4).ToList();
            var mz = seen.Select(s => s.Mz).ToList();
            var mag = seen.Select(s => s.Abundance).ToList();

            EnvelopeMatch? match = EnvelopeTemplateMatching.Match(
                mz, mag, MyoglobinMass, IonizationMode.Denatured);

            Assert.IsNotNull(match);
            Assert.Less(match!.Coverage, 1.0);
            // The states that were dropped are the weak ones, so the shape of what remains
            // is still the model's.
            Assert.Greater(match.Similarity, 0.99);
        }

        /// <summary>
        /// The converse, and the reason similarity is worth reporting separately from
        /// coverage: losing the states at the centre of the envelope is not a missing-data
        /// problem, it is a different shape, and the score has to say so.
        /// </summary>
        [Test]
        public void LosingTheMostAbundantChargeStatesWrecksSimilarity()
        {
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = IonizationMode.Denatured });

            List<ChargeStateAbundance> seen = envelope
                .OrderByDescending(s => s.Abundance).Skip(4).ToList();

            EnvelopeMatch? match = EnvelopeTemplateMatching.Match(
                seen.Select(s => s.Mz).ToList(), seen.Select(s => s.Abundance).ToList(),
                MyoglobinMass, IonizationMode.Denatured);

            Assert.IsNotNull(match);
            Assert.Less(match!.Similarity, 0.8);
        }

        [Test]
        public void ReturnsNullWhenNothingIsNearThePredictedPeaks()
        {
            var mz = new List<double> { 300.0, 310.0, 320.0 };
            var mag = new List<double> { 1.0, 1.0, 1.0 };

            // Myoglobin's denatured envelope sits around m/z 650-2000; nothing here is
            // within tolerance of any of it.
            Assert.IsNull(EnvelopeTemplateMatching.Match(
                mz, mag, MyoglobinMass, IonizationMode.Denatured, null, 50));
        }

        /// <summary>
        /// The whole chain: two proteins in one spectrum, deconvolved by position, then
        /// annotated by envelope shape.
        /// </summary>
        [Test]
        public void AnnotatesDeconvolvedGroupsOfAMixture()
        {
            var mz = new List<double>();
            var mag = new List<double>();
            foreach (double mass in new[] { MyoglobinMass, UbiquitinMass })
            {
                (List<double> m, List<double> a) = Observe(mass, IonizationMode.Denatured);
                mz.AddRange(m);
                mag.AddRange(a);
            }

            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, mag);
            IReadOnlyList<(ChargeStateGroup Group, EnvelopeMatch? Match)> annotated =
                EnvelopeTemplateMatching.Annotate(groups, mz, mag);

            Assert.AreEqual(2, annotated.Count);
            foreach ((ChargeStateGroup group, EnvelopeMatch? match) in annotated)
            {
                Assert.IsNotNull(match, $"no envelope match for {group}");
                TestContext.WriteLine($"{group} -> {match}");
                Assert.AreEqual(IonizationMode.Denatured, match!.Mode);
                Assert.Greater(match.Similarity, 0.9);
            }
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var mz = new List<double> { 1000.0 };
            var mag = new List<double> { 1.0 };

            Assert.Throws<ArgumentNullException>(() =>
                EnvelopeTemplateMatching.Match(null!, mag, 1000, IonizationMode.Native));
            Assert.Throws<ArgumentNullException>(() =>
                EnvelopeTemplateMatching.Match(mz, null!, 1000, IonizationMode.Native));
            Assert.Throws<ArgumentException>(() =>
                EnvelopeTemplateMatching.Match(mz, new List<double> { 1, 2 }, 1000, IonizationMode.Native));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EnvelopeTemplateMatching.Match(mz, mag, 0, IonizationMode.Native));
            Assert.Throws<ArgumentNullException>(() =>
                EnvelopeTemplateMatching.Annotate(null!, mz, mag));
        }
    }
}
