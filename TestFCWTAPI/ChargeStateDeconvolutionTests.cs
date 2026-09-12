using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET.Analysis;
using FCWTNET.Simulation;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class ChargeStateDeconvolutionTests
    {
        private const double MyoglobinMass = 16940.0;
        private const double CarbonicAnhydraseMass = 29020.0;
        private const double UbiquitinMass = 8560.0;

        /// <summary>Exact m/z values for a species at the given charge states.</summary>
        private static (List<double> Mz, List<double> Magnitude) ExactPeaks(
            double mass, IEnumerable<int> charges, double magnitude = 1.0)
        {
            var mz = new List<double>();
            var mag = new List<double>();
            foreach (int z in charges)
            {
                mz.Add(mass / z + ChargeStateDeconvolution.ProtonMass);
                mag.Add(magnitude);
            }
            return (mz, mag);
        }

        [Test]
        public void CombScoreIsExactlyOneAtTheTrueMass()
        {
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, Enumerable.Range(8, 19));
            // Every implied charge is an integer, so every phasor aligns.
            Assert.AreEqual(1.0, ChargeStateDeconvolution.CombScore(mz, mag, MyoglobinMass), 1e-9);
        }

        [Test]
        public void CombScoreFallsAwayFromTheTrueMass()
        {
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, Enumerable.Range(8, 19));
            // A mass unrelated to the true one puts the phasors out of step. 1.37x is chosen
            // to be nowhere near an integer multiple, where the score would legitimately
            // return to 1.
            Assert.Less(ChargeStateDeconvolution.CombScore(mz, mag, MyoglobinMass * 1.37), 0.3);
            Assert.Less(ChargeStateDeconvolution.CombScore(mz, mag, MyoglobinMass * 0.62), 0.3);
        }

        /// <summary>
        /// The comb score cannot by itself tell M from 2M: doubling the mass doubles every
        /// implied charge and they all stay whole numbers. Pinning this makes it explicit
        /// that the contiguity prior, not the score, is what resolves the ambiguity.
        /// </summary>
        [Test]
        public void CombScoreIsEquallyHappyAtIntegerMultiples()
        {
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, Enumerable.Range(8, 19));
            foreach (int multiple in new[] { 1, 2, 3, 4 })
            {
                Assert.AreEqual(1.0, ChargeStateDeconvolution.CombScore(mz, mag, MyoglobinMass * multiple), 1e-9,
                    $"expected perfect coherence at {multiple}x the true mass");
            }
        }

        /// <summary>
        /// Contiguity is what breaks that tie: the real mass assigns consecutive charge
        /// states, the 2x alias assigns only even ones.
        /// </summary>
        [Test]
        public void ContiguityDistinguishesTheMassFromItsHarmonic()
        {
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, Enumerable.Range(8, 19));
            ChargeStateGroup group = ChargeStateDeconvolution.Deconvolve(mz, mag).First();

            Assert.AreEqual(MyoglobinMass, group.MonoisotopicMass, MyoglobinMass * 1e-6);
            Assert.AreEqual(1.0, group.Contiguity, 1e-12, "a real envelope has no gaps");
            // Consecutive charges, so no common factor.
            Assert.AreEqual(group.MaxCharge - group.MinCharge + 1, group.Peaks.Count);
        }

        [Test]
        public void ImpliedChargeRecoversTheSimulatedCharge()
        {
            foreach (int z in new[] { 1, 8, 17, 26, 55 })
            {
                double mz = MyoglobinMass / z + ChargeStateDeconvolution.ProtonMass;
                Assert.AreEqual(z, ChargeStateDeconvolution.ImpliedCharge(mz, MyoglobinMass), 1e-9);
            }
        }

        [Test]
        public void MassForChargeInvertsImpliedCharge()
        {
            double mz = MyoglobinMass / 13 + ChargeStateDeconvolution.ProtonMass;
            Assert.AreEqual(MyoglobinMass, ChargeStateDeconvolution.MassForCharge(mz, 13), 1e-9);
        }

        [Test]
        public void RecoversASingleSpeciesFromExactPeaks()
        {
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, Enumerable.Range(8, 19));
            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, mag);

            Assert.AreEqual(1, groups.Count, "one species in, one species out");
            ChargeStateGroup group = groups[0];
            Assert.AreEqual(MyoglobinMass, group.MonoisotopicMass, MyoglobinMass * 1e-6);
            Assert.AreEqual(19, group.Peaks.Count);
            Assert.AreEqual(8, group.MinCharge);
            Assert.AreEqual(26, group.MaxCharge);
            Assert.AreEqual(DeconvolutionMethod.Both, group.Method,
                "comb and voting should independently agree on a clean case");
        }

        [Test]
        public void EveryPeakIsAssignedItsSimulatedChargeState()
        {
            int[] charges = Enumerable.Range(8, 19).ToArray();
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, charges);
            ChargeStateGroup group = ChargeStateDeconvolution.Deconvolve(mz, mag).Single();
            CollectionAssert.AreEqual(charges, group.Peaks.Select(p => p.Charge).ToArray());
        }

        /// <summary>
        /// Real peak lists have holes where weak charge states fall below threshold. The
        /// voting method bridges them because it tries charge differences greater than one.
        /// </summary>
        [Test]
        public void RecoversMassWithChargeStatesMissing()
        {
            // A realistic pattern: a few states dropped out of an otherwise full envelope.
            int[] charges = { 9, 10, 12, 13, 14, 16, 17, 18, 19, 22 };
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, charges);
            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, mag);

            Assert.IsNotEmpty(groups);
            ChargeStateGroup best = groups.OrderByDescending(g => g.Peaks.Count).First();
            Assert.AreEqual(MyoglobinMass, best.MonoisotopicMass, MyoglobinMass * 1e-5);
            CollectionAssert.AreEqual(charges, best.Peaks.Select(p => p.Charge).ToArray());
        }

        /// <summary>
        /// A peak list containing only every third charge state is genuinely ambiguous: the
        /// same peaks are exactly consistent with a species of mass M/3 carrying consecutive
        /// charges. Nothing in the peak positions can break that tie, and the contiguous
        /// explanation is the better one, so that is what gets reported. Recorded here so
        /// the behaviour is a known limit rather than a surprise -- resolving it needs the
        /// isotope spacing, which fixes the charge independently.
        /// </summary>
        [Test]
        public void EveryThirdChargeStateIsAmbiguousWithAThirdOfTheMass()
        {
            int[] charges = { 9, 12, 15, 18, 21, 24 };
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, charges);
            ChargeStateGroup best = ChargeStateDeconvolution.Deconvolve(mz, mag)
                .OrderByDescending(g => g.Peaks.Count).First();

            Assert.AreEqual(MyoglobinMass / 3, best.MonoisotopicMass, MyoglobinMass / 3 * 1e-5);
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7, 8 },
                best.Peaks.Select(p => p.Charge).ToArray());
            Assert.AreEqual(1.0, best.Contiguity, 1e-12);
        }

        [Test]
        public void SeparatesTwoInterleavedSpecies()
        {
            (List<double> mzA, List<double> magA) = ExactPeaks(MyoglobinMass, Enumerable.Range(10, 12), 1.0);
            (List<double> mzB, List<double> magB) = ExactPeaks(CarbonicAnhydraseMass, Enumerable.Range(14, 12), 0.6);

            var mz = mzA.Concat(mzB).ToList();
            var mag = magA.Concat(magB).ToList();

            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, mag);
            double[] masses = groups.Select(g => g.MonoisotopicMass).ToArray();

            Assert.GreaterOrEqual(groups.Count, 2, "both species should be found");
            Assert.IsTrue(masses.Any(m => Math.Abs(m - MyoglobinMass) / MyoglobinMass < 1e-4),
                $"myoglobin not recovered; got [{string.Join(", ", masses.Select(m => m.ToString("F1")))}]");
            Assert.IsTrue(masses.Any(m => Math.Abs(m - CarbonicAnhydraseMass) / CarbonicAnhydraseMass < 1e-4),
                $"carbonic anhydrase not recovered; got [{string.Join(", ", masses.Select(m => m.ToString("F1")))}]");
        }

        [Test]
        public void SeparatesThreeSpecies()
        {
            var mz = new List<double>();
            var mag = new List<double>();
            foreach ((double mass, int from, double intensity) in new[]
                     {
                         (UbiquitinMass, 6, 1.0),
                         (MyoglobinMass, 10, 0.8),
                         (CarbonicAnhydraseMass, 16, 0.5)
                     })
            {
                (List<double> m, List<double> a) = ExactPeaks(mass, Enumerable.Range(from, 10), intensity);
                mz.AddRange(m);
                mag.AddRange(a);
            }

            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, mag);
            double[] masses = groups.Select(g => g.MonoisotopicMass).ToArray();

            foreach (double expected in new[] { UbiquitinMass, MyoglobinMass, CarbonicAnhydraseMass })
            {
                Assert.IsTrue(masses.Any(m => Math.Abs(m - expected) / expected < 1e-4),
                    $"{expected:F0} Da not recovered; got [{string.Join(", ", masses.Select(m => m.ToString("F1")))}]");
            }
        }

        /// <summary>
        /// A comb of period A/M is partly consistent with M/2, because every second tooth
        /// still lines up. The sub-harmonic must not be reported as a separate species.
        /// </summary>
        [Test]
        public void DoesNotReportSubHarmonicsAsSeparateSpecies()
        {
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, Enumerable.Range(8, 19));
            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, mag);

            foreach (ChargeStateGroup group in groups)
            {
                foreach (int divisor in new[] { 2, 3, 4 })
                {
                    double subHarmonic = MyoglobinMass / divisor;
                    Assert.Greater(Math.Abs(group.MonoisotopicMass - subHarmonic) / subHarmonic, 1e-3,
                        $"reported a sub-harmonic at M/{divisor} = {subHarmonic:F1} Da");
                }
            }
        }

        [Test]
        public void ToleratesRealisticPeakPositionError()
        {
            // Measured ridge positions in this project land within a few hundred ppm.
            var random = new Random(4242);
            int[] charges = Enumerable.Range(9, 15).ToArray();
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, charges);
            for (int i = 0; i < mz.Count; i++)
            {
                double ppm = (random.NextDouble() * 2 - 1) * 400;
                mz[i] *= 1 + ppm * 1e-6;
            }

            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, mag);
            Assert.IsNotEmpty(groups);
            ChargeStateGroup best = groups.OrderByDescending(g => g.Peaks.Count).First();

            // Averaging over charge states beats the per-peak error.
            Assert.AreEqual(MyoglobinMass, best.MonoisotopicMass, MyoglobinMass * 500e-6);
            CollectionAssert.AreEqual(charges, best.Peaks.Select(p => p.Charge).ToArray());
        }

        [Test]
        public void ReportsMassUncertaintyThatReflectsScatter()
        {
            var random = new Random(99);
            (List<double> exactMz, List<double> mag) = ExactPeaks(MyoglobinMass, Enumerable.Range(9, 15));
            ChargeStateGroup clean = ChargeStateDeconvolution.Deconvolve(exactMz, mag)
                .OrderByDescending(g => g.Peaks.Count).First();

            var noisyMz = exactMz.Select(m => m * (1 + (random.NextDouble() * 2 - 1) * 300e-6)).ToList();
            ChargeStateGroup noisy = ChargeStateDeconvolution.Deconvolve(noisyMz, mag)
                .OrderByDescending(g => g.Peaks.Count).First();

            Assert.Less(clean.MassUncertaintyPpm, 1.0);
            Assert.Greater(noisy.MassUncertaintyPpm, clean.MassUncertaintyPpm);
        }

        [Test]
        public void ReturnsNothingForTooFewPeaks()
        {
            var mz = new List<double> { 1000, 1200 };
            var mag = new List<double> { 1, 1 };
            Assert.IsEmpty(ChargeStateDeconvolution.Deconvolve(mz, mag));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            var mz = new List<double> { 1000, 1200, 1500 };
            var mag = new List<double> { 1, 1, 1 };

            Assert.Throws<ArgumentNullException>(() =>
                ChargeStateDeconvolution.Deconvolve((IReadOnlyList<double>)null!, mag));
            Assert.Throws<ArgumentException>(() =>
                ChargeStateDeconvolution.Deconvolve(mz, new List<double> { 1, 1 }));
            Assert.Throws<ArgumentException>(() =>
                ChargeStateDeconvolution.Deconvolve(mz, mag, new ChargeDeconvolutionOptions { MinMass = 0 }));
            Assert.Throws<ArgumentException>(() =>
                ChargeStateDeconvolution.Deconvolve(mz, mag, new ChargeDeconvolutionOptions { MinCharge = 0 }));
            // m/z below the proton mass is not physical.
            Assert.Throws<ArgumentException>(() =>
                ChargeStateDeconvolution.Deconvolve(new List<double> { 0.5, 1000, 1200 }, mag));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ChargeStateDeconvolution.CombScore(mz, mag, 0));
        }

        /// <summary>
        /// The charge envelope built by the simulator must come back out of the
        /// deconvolution, which ties the two halves of the project together.
        /// </summary>
        [Test]
        public void RoundTripsAChargeStateEnvelopeFromTheSimulator()
        {
            IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = IonizationMode.Denatured });

            var mz = envelope.Select(c => c.Mz).ToList();
            var mag = envelope.Select(c => c.Abundance).ToList();

            ChargeStateGroup group = ChargeStateDeconvolution.Deconvolve(mz, mag)
                .OrderByDescending(g => g.Peaks.Count).First();

            Assert.AreEqual(MyoglobinMass, group.MonoisotopicMass, MyoglobinMass * 1e-6);
            CollectionAssert.AreEqual(
                envelope.Select(c => c.Charge).ToArray(),
                group.Peaks.Select(p => p.Charge).ToArray());
        }

        [Test]
        public void NativeAndDenaturedEnvelopesBothRecoverTheSameMass()
        {
            foreach (IonizationMode mode in Enum.GetValues<IonizationMode>())
            {
                IReadOnlyList<ChargeStateAbundance> envelope = ChargeStateEnvelope.Generate(
                    MyoglobinMass, new ChargeStateEnvelopeOptions { Mode = mode });

                ChargeStateGroup group = ChargeStateDeconvolution.Deconvolve(
                        envelope.Select(c => c.Mz).ToList(),
                        envelope.Select(c => c.Abundance).ToList())
                    .OrderByDescending(g => g.Peaks.Count).First();

                Assert.AreEqual(MyoglobinMass, group.MonoisotopicMass, MyoglobinMass * 1e-6, mode.ToString());
            }
        }

        [Test]
        public void AcceptsDetectedPeaksDirectly()
        {
            (List<double> mz, List<double> mag) = ExactPeaks(MyoglobinMass, Enumerable.Range(10, 10));
            var peaks = mz.Zip(mag, (m, a) => new DetectedPeak { Position = m, Magnitude = a }).ToList();

            ChargeStateGroup group = ChargeStateDeconvolution.Deconvolve(peaks)
                .OrderByDescending(g => g.Peaks.Count).First();
            Assert.AreEqual(MyoglobinMass, group.MonoisotopicMass, MyoglobinMass * 1e-6);
        }
    }
}
