using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET.Simulation;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class ProteinComplexTests
    {
        private static readonly ProteinSubunit Streptavidin = new("S", 13215.0);
        private static readonly ProteinSubunit Alpha = new("a", 15742.9);
        private static readonly ProteinSubunit Beta = new("b", 16483.7);

        [Test]
        public void NonCovalentAssemblyLosesNothing()
        {
            // Subunits associate without condensing, so an n-mer weighs exactly n times its
            // subunit. Anything else would be a covalent multimer, which is not this.
            for (int n = 1; n <= 6; n++)
            {
                var complex = new ProteinComplex(new[] { (Streptavidin, n) });
                Assert.AreEqual(Streptavidin.MonoisotopicMass * n, complex.MonoisotopicMass, 1e-9);
                Assert.AreEqual(n, complex.SubunitCount);
                Assert.IsTrue(complex.IsHomomeric);
            }
        }

        [Test]
        public void HeteroMassIsTheSumOfItsChains()
        {
            var hb = ComplexAssembly.Assemble((Alpha, 2), (Beta, 2));
            Assert.AreEqual(2 * 15742.9 + 2 * 16483.7, hb.MonoisotopicMass, 1e-9);
            Assert.AreEqual(4, hb.SubunitCount);
            Assert.AreEqual("a2b2", hb.Formula);
            Assert.IsFalse(hb.IsHomomeric);
            Assert.AreEqual(2, hb.CountOf(Alpha));
            Assert.AreEqual(0, hb.CountOf(Streptavidin));
        }

        [Test]
        public void FormulaOmitsACountOfOne()
        {
            Assert.AreEqual("ab2", ComplexAssembly.Assemble((Alpha, 1), (Beta, 2)).Formula);
            Assert.AreEqual("a", ComplexAssembly.Assemble((Alpha, 1)).Formula);
        }

        [Test]
        public void HomooligomerLadderRunsFromMonomerToTheIntactComplex()
        {
            IReadOnlyList<ProteinComplex> ladder = ComplexAssembly.Homooligomer(Streptavidin, 4);

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, ladder.Select(c => c.SubunitCount).ToArray());
            CollectionAssert.AreEqual(new[] { "S", "S2", "S3", "S4" }, ladder.Select(c => c.Formula).ToArray());
            Assert.AreEqual(1.0, ladder.Sum(c => c.Abundance), 1e-12, "abundances should be shares of one");
            // IntactDominant is the default, so the tetramer carries the most.
            Assert.AreEqual(4, ladder.OrderByDescending(c => c.Abundance).First().SubunitCount);
        }

        [Test]
        public void TurningOffSubComplexesLeavesOnlyTheIntactAssembly()
        {
            IReadOnlyList<ProteinComplex> ladder = ComplexAssembly.Homooligomer(Streptavidin, 4,
                new AssemblyOptions { IncludeSubComplexes = false });

            Assert.AreEqual(1, ladder.Count);
            Assert.AreEqual(4, ladder[0].SubunitCount);
            Assert.AreEqual(1.0, ladder[0].Abundance, 1e-12);
        }

        [Test]
        public void MonomerDominantInvertsTheLadder()
        {
            IReadOnlyList<ProteinComplex> ladder = ComplexAssembly.Homooligomer(Streptavidin, 4,
                new AssemblyOptions { Model = AssemblyAbundance.MonomerDominant });

            Assert.AreEqual(1, ladder.OrderByDescending(c => c.Abundance).First().SubunitCount);
            // Strictly decreasing with size.
            var byOrder = ladder.OrderBy(c => c.SubunitCount).Select(c => c.Abundance).ToArray();
            for (int i = 1; i < byOrder.Length; i++)
            {
                Assert.Less(byOrder[i], byOrder[i - 1]);
            }
        }

        /// <summary>
        /// A hetero-complex dissociates into every sub-stoichiometry, not just the chains:
        /// alpha2beta2 gives alpha, beta, alpha2, alphabeta, beta2, alpha2beta, alphabeta2.
        /// </summary>
        [Test]
        public void HeteroLadderEnumeratesEverySubStoichiometry()
        {
            IReadOnlyList<ProteinComplex> ladder = ComplexAssembly.Hetero(
                new[] { (Alpha, 2), (Beta, 2) },
                new AssemblyOptions { Model = AssemblyAbundance.Uniform, RelativeThreshold = 0 });

            // 3 * 3 combinations minus the empty one.
            Assert.AreEqual(8, ladder.Count);
            CollectionAssert.AreEquivalent(
                new[] { "a", "b", "a2", "ab", "b2", "a2b", "ab2", "a2b2" },
                ladder.Select(c => c.Formula).ToArray());
            Assert.AreEqual(1.0, ladder.Sum(c => c.Abundance), 1e-12);
        }

        [Test]
        public void ThresholdAndCapTrimTheLadder()
        {
            IReadOnlyList<ProteinComplex> ladder = ComplexAssembly.Hetero(
                new[] { (Alpha, 2), (Beta, 2) },
                new AssemblyOptions { MaxSpecies = 3, RelativeThreshold = 0 });

            Assert.AreEqual(3, ladder.Count);
            Assert.AreEqual(1.0, ladder.Sum(c => c.Abundance), 1e-12, "what survives is renormalised");
            // IntactDominant keeps the largest.
            Assert.AreEqual("a2b2", ladder.OrderByDescending(c => c.Abundance).First().Formula);
        }

        /// <summary>
        /// The physics that makes complex spectra look the way they do: the Rayleigh limit
        /// puts an n-mer at sqrt(n) times the monomer's m/z, not n times, so the oligomers
        /// crowd together instead of spreading out.
        /// </summary>
        [Test]
        public void OligomersMarchToHigherMzAsTheSquareRootOfTheirOrder()
        {
            var options = new ChargeStateEnvelopeOptions { Mode = IonizationMode.Native };
            double monomerCentroid = ChargeStateEnvelope.CentroidCharge(
                Streptavidin.MonoisotopicMass, IonizationMode.Native);

            foreach (int n in new[] { 2, 3, 4 })
            {
                double mass = Streptavidin.MonoisotopicMass * n;
                double centroid = ChargeStateEnvelope.CentroidCharge(mass, IonizationMode.Native);

                // z goes as sqrt(M), so an n-mer carries sqrt(n) times the charge...
                Assert.AreEqual(Math.Sqrt(n), centroid / monomerCentroid, 1e-9);
                // ...and therefore sits at sqrt(n) times the m/z.
                double monomerMz = Streptavidin.MonoisotopicMass / monomerCentroid;
                Assert.AreEqual(Math.Sqrt(n), (mass / centroid) / monomerMz, 1e-9);
            }
        }

        /// <summary>
        /// The degeneracy itself. An n-mer at charge n*z is at exactly the monomer's m/z --
        /// not nearly, exactly -- so no resolution separates them and no positional method
        /// can say which is present.
        /// </summary>
        [Test]
        public void AnOligomerCoincidesExactlyWithItsMonomer()
        {
            var monomer = new ProteinComplex(new[] { (Streptavidin, 1) });
            var dimer = new ProteinComplex(new[] { (Streptavidin, 2) });

            IReadOnlyList<(int ChargeA, int ChargeB, double Mz)> shared =
                ComplexAssembly.SharedMzValues(monomer, dimer, maxCharge: 20);

            Assert.IsNotEmpty(shared);
            foreach ((int monomerCharge, int dimerCharge, double mz) in shared)
            {
                Assert.AreEqual(2 * monomerCharge, dimerCharge, "the dimer takes twice the charge");
                // Exact to the last bit the arithmetic allows, not merely close.
                double fromMonomer = monomer.MonoisotopicMass / monomerCharge + IonSpecies.ProtonMass;
                double fromDimer = dimer.MonoisotopicMass / dimerCharge + IonSpecies.ProtonMass;
                Assert.AreEqual(fromMonomer, fromDimer, 1e-9);
                Assert.AreEqual(fromMonomer, mz, 1e-9);
            }
        }

        [Test]
        public void SpeciesCarryTheirShareOfTheIonCurrent()
        {
            IReadOnlyList<ProteinComplex> ladder = ComplexAssembly.Homooligomer(Streptavidin, 4);
            IReadOnlyList<IonSpecies> species = ComplexAssembly.CreateSpecies(ladder);

            Assert.IsNotEmpty(species);
            // Each complex's charge envelope sums to its own abundance, so the whole sample
            // sums to one.
            Assert.AreEqual(1.0, species.Sum(s => s.Abundance), 1e-9);

            foreach (int n in new[] { 1, 2, 3, 4 })
            {
                double mass = Streptavidin.MonoisotopicMass * n;
                IReadOnlyList<IonSpecies> forOrder =
                    species.Where(s => Math.Abs(s.MonoisotopicMass - mass) < 1e-6).ToList();
                Assert.IsNotEmpty(forOrder, $"{n}-mer should be present");
                Assert.AreEqual(ladder.First(c => c.SubunitCount == n).Abundance,
                    forOrder.Sum(s => s.Abundance), 1e-9);
            }
        }

        /// <summary>
        /// The isotope trap. Above a few tens of kDa the cluster's mode is far from the
        /// monoisotopic peak, so a window taken from zero simulates the wrong m/z rather
        /// than a truncated cluster.
        /// </summary>
        [Test]
        public void LargeComplexesSimulateTheIsotopeWindowNotTheMonoisotopicTail()
        {
            IReadOnlyList<IonSpecies> species = ComplexAssembly.CreateSpecies(
                new[] { ComplexAssembly.Assemble((Alpha, 2), (Beta, 2)) }, maxIsotopePeaks: 24);

            IonSpecies ion = species.First();
            double lambda = IsotopeEnvelope.PoissonMean(ion.MonoisotopicMass);
            Assert.Greater(lambda, 30, "a 64 kDa complex should have a mode tens of neutrons up");
            Assert.Greater(ion.FirstIsotopePeak, 0, "the window must not start at the monoisotopic peak");

            // The window should straddle the mode.
            Assert.Less(ion.FirstIsotopePeak, lambda);
            Assert.Greater(ion.FirstIsotopePeak + ion.IsotopePeaks, lambda);
        }

        /// <summary>
        /// And the reason it matters: the simulated cluster has to sit where the ion is.
        /// Counting 24 peaks from zero would place it around 38 Da low.
        /// </summary>
        [Test]
        public void TheSimulatedClusterSitsOnTheRealIon()
        {
            ProteinComplex hb = ComplexAssembly.Assemble((Alpha, 2), (Beta, 2));
            IonSpecies ion = ComplexAssembly.CreateSpecies(new[] { hb }, maxIsotopePeaks: 24)
                .OrderByDescending(s => s.Abundance).First();

            double[] intensities = IsotopeEnvelope.RelativeIntensities(
                ion.MonoisotopicMass, ion.IsotopePeaks, ion.FirstIsotopePeak);

            // Intensity-weighted mean isotope, in neutrons above monoisotopic.
            double centroid = intensities
                .Select((v, k) => v * (ion.FirstIsotopePeak + k)).Sum() / intensities.Sum();
            double lambda = IsotopeEnvelope.PoissonMean(ion.MonoisotopicMass);

            Assert.AreEqual(lambda, centroid, 1.0,
                "the simulated cluster's centroid should land on the distribution's mean");
        }

        [Test]
        public void ThePresetsBuildAndCoverTheirStatedMzRanges()
        {
            foreach (string name in new[] { "homotetramer", "hetero-complex", "oligomer-overlap" })
            {
                SimulationPreset preset = Presets.Get(name);
                Assert.IsNotEmpty(preset.Species, name);

                SimulationResult result = preset.Run();
                Assert.IsNotEmpty(result.GroundTruth, name);
                TestContext.WriteLine($"{name}: {result.GroundTruth.Count} isotopologues, " +
                                      $"m/z {result.GroundTruth.Min(t => t.Mz):F0}-" +
                                      $"{result.GroundTruth.Max(t => t.Mz):F0}");

                // Every ion must be below Nyquist or the simulator would have thrown; assert
                // the band is the native-complex one rather than a peptide's.
                Assert.Greater(result.GroundTruth.Min(t => t.Mz), 1000, name);
            }
        }

        /// <summary>
        /// The overlap preset has to actually overlap, or it is not testing anything.
        /// </summary>
        [Test]
        public void TheOverlapPresetPutsEveryMonomerPeakOnADimerPeak()
        {
            SimulationPreset preset = Presets.Get("oligomer-overlap");
            double subunit = 13215.0;

            var monomerStates = preset.Species
                .Where(s => Math.Abs(s.MonoisotopicMass - subunit) < 1e-6).ToList();
            var dimerStates = preset.Species
                .Where(s => Math.Abs(s.MonoisotopicMass - 2 * subunit) < 1e-6).ToList();

            Assert.IsNotEmpty(monomerStates);
            Assert.IsNotEmpty(dimerStates);

            foreach (IonSpecies monomer in monomerStates)
            {
                IonSpecies? twin = dimerStates.FirstOrDefault(d => d.Charge == 2 * monomer.Charge);
                Assert.IsNotNull(twin, $"no dimer at {2 * monomer.Charge}+ for monomer {monomer.Charge}+");
                Assert.AreEqual(monomer.MonoisotopicMz, twin!.MonoisotopicMz, 1e-9);
            }

            // And the dimer's odd states fall between, which is what makes the envelope look
            // like one series at half the spacing.
            Assert.IsTrue(dimerStates.Any(d => d.Charge % 2 == 1));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => new ProteinSubunit("", 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProteinSubunit("x", 0));
            Assert.Throws<ArgumentNullException>(() => new ProteinComplex(null!));
            Assert.Throws<ArgumentException>(() =>
                new ProteinComplex(Array.Empty<(ProteinSubunit, int)>()));
            Assert.Throws<ArgumentException>(() => new ProteinComplex(new[] { (Streptavidin, 0) }));
            Assert.Throws<ArgumentException>(() =>
                new ProteinComplex(new[] { (Streptavidin, 1), (Streptavidin, 2) }),
                "a subunit may not appear twice in one composition");

            Assert.Throws<ArgumentNullException>(() => ComplexAssembly.Homooligomer(null!, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ComplexAssembly.Homooligomer(Streptavidin, 0));
            Assert.Throws<ArgumentNullException>(() => ComplexAssembly.Hetero(null!));
            Assert.Throws<ArgumentException>(() =>
                ComplexAssembly.Hetero(Array.Empty<(ProteinSubunit, int)>()));
            Assert.Throws<ArgumentNullException>(() => ComplexAssembly.CreateSpecies(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ComplexAssembly.CreateSpecies(Array.Empty<ProteinComplex>(), maxIsotopePeaks: 0));

            Assert.Throws<ArgumentException>(() => ComplexAssembly.Homooligomer(Streptavidin, 4,
                new AssemblyOptions { DecayPerSubunit = 0 }));
            Assert.Throws<ArgumentException>(() => ComplexAssembly.Homooligomer(Streptavidin, 4,
                new AssemblyOptions { MaxSpecies = 0 }));
            Assert.Throws<ArgumentException>(() => ComplexAssembly.Homooligomer(Streptavidin, 4,
                new AssemblyOptions { Model = AssemblyAbundance.Explicit, Abundances = new[] { 1.0 } }),
                "an explicit list must have one entry per stoichiometry");
        }
    }
}
