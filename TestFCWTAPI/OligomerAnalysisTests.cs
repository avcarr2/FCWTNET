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
    public class OligomerAnalysisTests
    {
        private const double Subunit = 13215.0;
        private const double Calibration = Presets.SampleCalibrationCoefficient;

        /// <summary>Exact peaks for an oligomer at the given charges.</summary>
        private static void AddOligomer(List<double> mz, List<double> magnitude,
            double subunitMass, int order, IEnumerable<int> charges, double abundance = 1.0)
        {
            double mass = subunitMass * order;
            foreach (int z in charges)
            {
                mz.Add(mass / z + IonSpecies.ProtonMass);
                magnitude.Add(abundance);
            }
        }

        /// <summary>Charges spanning the native envelope of a given mass.</summary>
        private static IEnumerable<int> NativeCharges(double mass)
        {
            return ChargeStateEnvelope
                .Generate(mass, new ChargeStateEnvelopeOptions { Mode = IonizationMode.Native })
                .Select(s => s.Charge);
        }

        [Test]
        public void TheTrueSubunitExplainsEveryPeakPlausibly()
        {
            var mz = new List<double>();
            var magnitude = new List<double>();
            for (int order = 1; order <= 4; order++)
            {
                AddOligomer(mz, magnitude, Subunit, order, NativeCharges(Subunit * order));
            }

            // Bounded at the stoichiometry that was actually simulated. Left unbounded the
            // fit reaches past it: a tetramer peak can also be read as a plausible 5-mer or
            // 6-mer at some other charge, so MaxOrder is worth setting when it is known.
            OligomerLadder ladder = OligomerAnalysis.Explain(mz, magnitude, Subunit,
                new OligomerOptions { MaxOrder = 4 });

            TestContext.WriteLine(ladder.ToString());
            Assert.AreEqual(1.0, ladder.Coverage, 1e-9, "every peak should be explained");
            Assert.Greater(ladder.Plausibility, 0.6);
            Assert.AreEqual(1.0, ladder.Completeness, 1e-9);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, ladder.Orders);
        }

        /// <summary>
        /// The discriminator. Explaining the same peaks as oligomers of twice the subunit
        /// forces every charge to double, and the monomer's own states then sit outside
        /// what the Rayleigh limit supports for a dimer.
        /// </summary>
        [Test]
        public void TwiceTheSubunitHasToInventImplausibleCharges()
        {
            var mz = new List<double>();
            var magnitude = new List<double>();
            for (int order = 1; order <= 4; order++)
            {
                AddOligomer(mz, magnitude, Subunit, order, NativeCharges(Subunit * order));
            }

            OligomerLadder truth = OligomerAnalysis.Explain(mz, magnitude, Subunit);
            OligomerLadder doubled = OligomerAnalysis.Explain(mz, magnitude, 2 * Subunit);

            TestContext.WriteLine($"true    {truth}  score {truth.Score:F3}");
            TestContext.WriteLine($"doubled {doubled}  score {doubled.Score:F3}");
            Assert.Greater(truth.Score, doubled.Score);
        }

        [Test]
        public void FindLadderRecoversTheSubunitFromAnExactPeakList()
        {
            var mz = new List<double>();
            var magnitude = new List<double>();
            for (int order = 1; order <= 4; order++)
            {
                AddOligomer(mz, magnitude, Subunit, order, NativeCharges(Subunit * order));
            }

            // Hand it the dimer's mass, which is what the charge-state comb reports on this
            // ladder because every oligomer is an integer multiple of every other.
            OligomerLadder? found = OligomerAnalysis.FindLadder(mz, magnitude, 2 * Subunit);

            Assert.IsNotNull(found);
            Assert.AreEqual(Subunit, found!.SubunitMass, Subunit * 1e-6);
        }

        /// <summary>
        /// A gappy ladder is the signature of a subunit guessed too small: more orders to
        /// choose from means each peak finds some order that fits, and the orders it lands
        /// on are not consecutive.
        /// </summary>
        [Test]
        public void ContiguityPenalisesALadderWithMissingOrders()
        {
            var mz = new List<double>();
            var magnitude = new List<double>();
            AddOligomer(mz, magnitude, Subunit, 1, NativeCharges(Subunit));
            AddOligomer(mz, magnitude, Subunit, 2, NativeCharges(2 * Subunit));
            AddOligomer(mz, magnitude, Subunit, 3, NativeCharges(3 * Subunit));

            OligomerLadder consecutive = OligomerAnalysis.Explain(mz, magnitude, Subunit,
                new OligomerOptions { MaxOrder = 3 });
            Assert.AreEqual(1.0, consecutive.Completeness, 1e-9);

            // A third of the subunit reaches the same masses only at orders 3, 6 and 9, so
            // whatever it explains leaves the bottom of the ladder empty.
            OligomerLadder gappy = OligomerAnalysis.Explain(mz, magnitude, Subunit / 3,
                new OligomerOptions { MaxOrder = 12 });
            TestContext.WriteLine($"consecutive {consecutive}");
            TestContext.WriteLine($"gappy       {gappy}");
            Assert.Less(gappy.Completeness, 1.0);
            Assert.Greater(consecutive.Score, gappy.Score);
        }

        /// <summary>
        /// A ladder on m/k contains the ladder on m as its higher orders, so a smaller
        /// subunit can never fit worse -- with more orders to choose from, every peak finds
        /// a more plausible home. Preferring it therefore needs positive evidence, and the
        /// evidence that is missing here is the bottom of the ladder: read as a quarter-mass
        /// subunit, a lone tetramer is a 3-to-5-mer series with no monomer and no dimer.
        /// </summary>
        [Test]
        public void ASmallerSubunitIsNotAdoptedWithoutEvidence()
        {
            // A single clean oligomer: nothing here argues for a smaller repeating unit.
            var mz = new List<double>();
            var magnitude = new List<double>();
            AddOligomer(mz, magnitude, Subunit, 4, NativeCharges(4 * Subunit));

            OligomerLadder? found = OligomerAnalysis.FindLadder(mz, magnitude, 4 * Subunit);

            Assert.IsNotNull(found);
            TestContext.WriteLine(found!.ToString());
            // It should stay at the mass it was given rather than splitting it up.
            Assert.AreEqual(4 * Subunit, found.SubunitMass, 1e-6);
            // It does still reach for a dimer of that mass at the top of the envelope,
            // where doubling the charge stays inside the Rayleigh band. Bounding MaxOrder
            // to the stoichiometry expected is the way to stop that; what matters here is
            // that the repeating unit is not halved, and that the monomer dominates.
            Assert.AreEqual(1, OligomerAnalysis.AbundanceByOrder(found)
                .OrderByDescending(e => e.Value).First().Key);
        }

        [Test]
        public void AbundanceByOrderSumsTheAssignedPeaks()
        {
            var mz = new List<double>();
            var magnitude = new List<double>();
            AddOligomer(mz, magnitude, Subunit, 1, NativeCharges(Subunit), abundance: 1.0);
            AddOligomer(mz, magnitude, Subunit, 2, NativeCharges(2 * Subunit), abundance: 3.0);

            OligomerLadder ladder = OligomerAnalysis.Explain(mz, magnitude, Subunit,
                new OligomerOptions { MaxOrder = 2 });
            IReadOnlyDictionary<int, double> byOrder = OligomerAnalysis.AbundanceByOrder(ladder);

            Assert.AreEqual(2, byOrder.Count);
            Assert.Greater(byOrder[2], byOrder[1], "the dimer was given three times the abundance");
        }

        /// <summary>
        /// The whole point, end to end: simulate a homotetramer ladder, transform it, and
        /// recover the subunit -- where the charge-state comb returns a single species at
        /// the dimer's mass and is not wrong to.
        /// </summary>
        [Test]
        public void RecoversTheSubunitFromTheSimulatedHomotetramer()
        {
            SimulationPreset preset = Presets.Get("homotetramer");
            SimulationResult simulated = preset.Run();

            var wavelet = new MorletWavelet(preset.SuggestedC0);
            ScaleGrid grid = ScaleGrid.ForMzRange(1100, 4000, Calibration, simulated.SamplingRate,
                64, wavelet, simulated.Samples.Length);
            var cwt = new CWTObject(simulated.Samples, 0, 1, 64, preset.SuggestedC0, 0, false,
                simulated.SamplingRate, Calibration)
            {
                ScaleGrid = grid,
                Wavelet = wavelet
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();
            cwt.FrequencyAxis.CalculateMZValues();

            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            int separation = new CwtSettings { Voices = 64, Wavelet = wavelet }.EffectivePeakSeparation();
            IReadOnlyList<DetectedPeak> peaks = MultiComponentFit.RefinePeaks(
                RidgeExtraction.FindPeaks(modulus, cwt.FrequencyAxis.MZValues!, 0.1, separation, grid),
                modulus, grid, cwt.FrequencyAxis.MZValues!);

            var mz = peaks.Select(p => p.Position).ToList();
            var magnitude = peaks.Select(p => p.Magnitude).ToList();

            // What the comb makes of it: one species, at a multiple of the real subunit.
            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, magnitude);
            Assert.IsNotEmpty(groups);
            TestContext.WriteLine($"comb: {groups[0]}");

            OligomerLadder? ladder = OligomerAnalysis.FindLadder(mz, magnitude,
                groups[0].MonoisotopicMass);

            Assert.IsNotNull(ladder);
            TestContext.WriteLine($"ladder: {ladder}");
            foreach (KeyValuePair<int, double> entry in
                     OligomerAnalysis.AbundanceByOrder(ladder!).OrderBy(e => e.Key))
            {
                TestContext.WriteLine($"  {entry.Key}-mer: {entry.Value:E2}");
            }

            // The ridge sits on the isotope cluster's centroid rather than the monoisotopic
            // peak, which is a constant few hundred ppm high -- so the tolerance is on that
            // scale, not on the numerics.
            Assert.AreEqual(Subunit, ladder!.SubunitMass, Subunit * 1.5e-3,
                $"expected a {Subunit} Da subunit, got {ladder.SubunitMass:F1}");
            CollectionAssert.IsSupersetOf(ladder.Orders, new[] { 1, 2, 3, 4 });
            // The tetramer was simulated as the most abundant stoichiometry.
            Assert.AreEqual(4, OligomerAnalysis.AbundanceByOrder(ladder)
                .OrderByDescending(e => e.Value).First().Key);
        }

        /// <summary>
        /// And the degenerate case on its own: every monomer peak sits exactly on a dimer
        /// peak, so nothing about position can separate them.
        /// </summary>
        [Test]
        public void SeparatesAMonomerFromItsDimerWhereEveryPeakCoincides()
        {
            SimulationPreset preset = Presets.Get("oligomer-overlap");
            SimulationResult simulated = preset.Run();

            var wavelet = new MorletWavelet(preset.SuggestedC0);
            ScaleGrid grid = ScaleGrid.ForMzRange(1200, 2400, Calibration, simulated.SamplingRate,
                64, wavelet, simulated.Samples.Length);
            var cwt = new CWTObject(simulated.Samples, 0, 1, 64, preset.SuggestedC0, 0, false,
                simulated.SamplingRate, Calibration)
            {
                ScaleGrid = grid,
                Wavelet = wavelet
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();
            cwt.FrequencyAxis.CalculateMZValues();

            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            int separation = new CwtSettings { Voices = 64, Wavelet = wavelet }.EffectivePeakSeparation();
            IReadOnlyList<DetectedPeak> peaks = MultiComponentFit.RefinePeaks(
                RidgeExtraction.FindPeaks(modulus, cwt.FrequencyAxis.MZValues!, 0.1, separation, grid),
                modulus, grid, cwt.FrequencyAxis.MZValues!);

            var mz = peaks.Select(p => p.Position).ToList();
            var magnitude = peaks.Select(p => p.Magnitude).ToList();
            IReadOnlyList<ChargeStateGroup> groups = ChargeStateDeconvolution.Deconvolve(mz, magnitude);
            Assert.IsNotEmpty(groups);

            OligomerLadder? ladder = OligomerAnalysis.FindLadder(mz, magnitude,
                groups[0].MonoisotopicMass);

            Assert.IsNotNull(ladder);
            TestContext.WriteLine($"comb:   {groups[0]}");
            TestContext.WriteLine($"ladder: {ladder}");
            Assert.AreEqual(Subunit, ladder!.SubunitMass, Subunit * 1.5e-3);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, ladder.Orders,
                "a monomer and its dimer, and nothing else");
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var mz = new List<double> { 1000.0 };
            var magnitude = new List<double> { 1.0 };

            Assert.Throws<ArgumentNullException>(() => OligomerAnalysis.Explain(null!, magnitude, 100));
            Assert.Throws<ArgumentNullException>(() => OligomerAnalysis.Explain(mz, null!, 100));
            Assert.Throws<ArgumentException>(() =>
                OligomerAnalysis.Explain(mz, new List<double> { 1, 2 }, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => OligomerAnalysis.Explain(mz, magnitude, 0));
            Assert.Throws<ArgumentException>(() =>
                OligomerAnalysis.Explain(mz, magnitude, 100, new OligomerOptions { MaxOrder = 0 }));
            Assert.Throws<ArgumentException>(() =>
                OligomerAnalysis.Explain(mz, magnitude, 100, new OligomerOptions { ChargeTolerance = 0.6 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OligomerAnalysis.FindLadder(mz, magnitude, 0));
            Assert.Throws<ArgumentException>(() => OligomerAnalysis.FindLadder(mz, magnitude, 100,
                new OligomerOptions { SubunitImprovementFactor = 0.5 }));
            Assert.Throws<ArgumentNullException>(() => OligomerAnalysis.AbundanceByOrder(null!));
        }
    }
}
