using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FCWTNET.Simulation
{
    /// <summary>One kind of polypeptide chain, the building block of a complex.</summary>
    public class ProteinSubunit
    {
        public ProteinSubunit(string name, double monoisotopicMass)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A subunit needs a name", nameof(name));
            }
            if (monoisotopicMass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monoisotopicMass), "mass must be positive");
            }
            Name = name;
            MonoisotopicMass = monoisotopicMass;
        }

        /// <summary>Short label used to build a stoichiometry formula, e.g. "alpha".</summary>
        public string Name { get; }

        /// <summary>
        /// Neutral monoisotopic mass in Da. Bound cofactors are folded into this -- a
        /// haem-carrying globin is one subunit of its holo mass, not a protein plus a
        /// ligand -- because nothing downstream distinguishes them.
        /// </summary>
        public double MonoisotopicMass { get; }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "{0} ({1:F1} Da)", Name, MonoisotopicMass);
    }

    /// <summary>
    /// A stoichiometry of subunits, held together non-covalently.
    ///
    /// <para>
    /// Non-covalent assembly loses nothing, so the mass is simply the sum: an n-mer weighs
    /// exactly n times its subunit. That is what makes an oligomer series so awkward to
    /// read, as <see cref="ComplexAssembly"/> explains.
    /// </para>
    /// </summary>
    public class ProteinComplex
    {
        public ProteinComplex(IReadOnlyList<(ProteinSubunit Subunit, int Count)> composition,
            double abundance = 1.0)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }
            if (composition.Count == 0)
            {
                throw new ArgumentException("A complex needs at least one subunit", nameof(composition));
            }
            if (composition.Any(c => c.Count < 1))
            {
                throw new ArgumentException("Subunit counts must be positive", nameof(composition));
            }
            if (composition.Select(c => c.Subunit.Name).Distinct(StringComparer.Ordinal).Count()
                != composition.Count)
            {
                throw new ArgumentException("Each subunit may appear only once in a composition",
                    nameof(composition));
            }
            if (abundance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(abundance), "abundance must not be negative");
            }

            Composition = composition;
            Abundance = abundance;
        }

        public IReadOnlyList<(ProteinSubunit Subunit, int Count)> Composition { get; }

        /// <summary>Share of the total ion current carried by this stoichiometry.</summary>
        public double Abundance { get; init; }

        public double MonoisotopicMass => Composition.Sum(c => c.Subunit.MonoisotopicMass * c.Count);

        /// <summary>Total number of chains, whatever they are.</summary>
        public int SubunitCount => Composition.Sum(c => c.Count);

        /// <summary>Stoichiometry as a formula, e.g. "alpha2beta2" or "A4".</summary>
        public string Formula
        {
            get
            {
                var builder = new StringBuilder();
                foreach ((ProteinSubunit subunit, int count) in Composition)
                {
                    builder.Append(subunit.Name);
                    if (count > 1)
                    {
                        builder.Append(count.ToString(CultureInfo.InvariantCulture));
                    }
                }
                return builder.ToString();
            }
        }

        /// <summary>True when every chain is the same kind.</summary>
        public bool IsHomomeric => Composition.Count == 1;

        /// <summary>How many of the given subunit this stoichiometry contains.</summary>
        public int CountOf(ProteinSubunit subunit) =>
            Composition.FirstOrDefault(c => c.Subunit.Name == subunit?.Name).Count;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "{0}: {1:F1} Da, {2} chains, abundance {3:F3}",
            Formula, MonoisotopicMass, SubunitCount, Abundance);
    }

    /// <summary>How abundance is shared across the stoichiometries in a sample.</summary>
    public enum AssemblyAbundance
    {
        /// <summary>Every stoichiometry equally abundant. Rarely realistic, useful for tests.</summary>
        Uniform,

        /// <summary>
        /// The intact complex dominates and each missing chain costs a factor. This is a
        /// well-preserved native spray, where sub-complexes are dissociation products.
        /// </summary>
        IntactDominant,

        /// <summary>
        /// The smallest species dominates and each additional chain costs a factor: a
        /// weakly associating system, or conditions that pull the complex apart.
        /// </summary>
        MonomerDominant,

        /// <summary>Abundances supplied by the caller, in the order the species are generated.</summary>
        Explicit
    }

    /// <summary>Knobs for building an assembly ladder.</summary>
    public class AssemblyOptions
    {
        public AssemblyAbundance Model { get; set; } = AssemblyAbundance.IntactDominant;

        /// <summary>Factor per chain gained or lost, under the two geometric models.</summary>
        public double DecayPerSubunit { get; set; } = 0.45;

        /// <summary>Abundances for <see cref="AssemblyAbundance.Explicit"/>.</summary>
        public IReadOnlyList<double>? Abundances { get; set; }

        /// <summary>Drop stoichiometries below this fraction of the most abundant one.</summary>
        public double RelativeThreshold { get; set; } = 0.02;

        /// <summary>Hard cap on how many stoichiometries are produced.</summary>
        public int MaxSpecies { get; set; } = 32;

        /// <summary>
        /// Include partial assemblies as well as the intact complex. Turning this off gives
        /// a single clean stoichiometry, which is what a very gentle spray looks like.
        /// </summary>
        public bool IncludeSubComplexes { get; set; } = true;
    }

    /// <summary>
    /// Builds the ladder of stoichiometries a native electrospray actually produces, and
    /// turns it into ion species.
    ///
    /// <para><b>Why complexes make spectra hard.</b> Charge in native electrospray is set
    /// by the Rayleigh limit, <c>z ~ 0.0778*sqrt(M)</c>, so an n-mer carries only
    /// <c>sqrt(n)</c> times the monomer's charge, not n times. Its charge states therefore
    /// land at <c>sqrt(n)</c> times the monomer's m/z, and every oligomer's envelope is
    /// spread across the same few thousand Th, interleaved with the others.</para>
    ///
    /// <para><b>And why they are genuinely ambiguous.</b> An n-mer at charge <c>n*z</c> has
    /// m/z <c>nM/(nz) = M/z</c> -- exactly the monomer at charge z. Half a dimer's charge
    /// states sit precisely on top of the monomer's, not nearly but exactly, and no amount
    /// of resolution separates them. This is the same M-versus-2M degeneracy that
    /// <see cref="Analysis.ChargeStateDeconvolution"/> resolves with a contiguity prior,
    /// except that here both masses are real, so the prior is answering the wrong question.
    /// What does separate them is charge: at one shared m/z the dimer carries twice the
    /// charge, so its isotope beat runs at half the rate -- see
    /// <see cref="Analysis.IsotopeBeatAnalysis"/>.</para>
    /// </summary>
    public static class ComplexAssembly
    {
        /// <summary>Shorthand for one stoichiometry.</summary>
        public static ProteinComplex Assemble(params (ProteinSubunit Subunit, int Count)[] composition)
            => new(composition);

        /// <summary>
        /// The homo-oligomer ladder for one subunit: monomer, dimer, ... up to
        /// <paramref name="maxOrder"/>.
        /// </summary>
        public static IReadOnlyList<ProteinComplex> Homooligomer(ProteinSubunit subunit, int maxOrder,
            AssemblyOptions? options = null)
        {
            if (subunit == null)
            {
                throw new ArgumentNullException(nameof(subunit));
            }
            if (maxOrder < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxOrder), "maxOrder must be at least 1");
            }
            options ??= new AssemblyOptions();

            int lowest = options.IncludeSubComplexes ? 1 : maxOrder;
            var ladder = new List<ProteinComplex>();
            for (int order = lowest; order <= maxOrder; order++)
            {
                ladder.Add(new ProteinComplex(new[] { (subunit, order) }));
            }
            return ApplyAbundances(ladder, maxOrder, options);
        }

        /// <summary>
        /// The intact complex and, unless turned off, every partial assembly of it: for
        /// alpha2beta2 that is alpha, beta, alpha2, alphabeta, beta2, alpha2beta and so on.
        /// </summary>
        public static IReadOnlyList<ProteinComplex> Hetero(
            IReadOnlyList<(ProteinSubunit Subunit, int Count)> intact, AssemblyOptions? options = null)
        {
            if (intact == null)
            {
                throw new ArgumentNullException(nameof(intact));
            }
            if (intact.Count == 0)
            {
                throw new ArgumentException("An assembly needs at least one subunit", nameof(intact));
            }
            if (intact.Any(c => c.Count < 1))
            {
                throw new ArgumentException("Subunit counts must be positive", nameof(intact));
            }
            options ??= new AssemblyOptions();

            int fullSize = intact.Sum(c => c.Count);
            if (!options.IncludeSubComplexes)
            {
                return ApplyAbundances(new List<ProteinComplex> { new(intact) }, fullSize, options);
            }

            // Every sub-stoichiometry, as the cross product of 0..count for each subunit.
            var ladder = new List<ProteinComplex>();
            var counts = new int[intact.Count];
            void Enumerate(int index)
            {
                if (index == intact.Count)
                {
                    var composition = new List<(ProteinSubunit, int)>();
                    for (int i = 0; i < counts.Length; i++)
                    {
                        if (counts[i] > 0)
                        {
                            composition.Add((intact[i].Subunit, counts[i]));
                        }
                    }
                    if (composition.Count > 0)
                    {
                        ladder.Add(new ProteinComplex(composition));
                    }
                    return;
                }
                for (int n = 0; n <= intact[index].Count; n++)
                {
                    counts[index] = n;
                    Enumerate(index + 1);
                }
            }
            Enumerate(0);

            // Smallest first, so an explicit abundance list reads in a predictable order.
            ladder = ladder
                .OrderBy(c => c.SubunitCount)
                .ThenBy(c => c.MonoisotopicMass)
                .ToList();
            return ApplyAbundances(ladder, fullSize, options);
        }

        /// <summary>
        /// Expands stoichiometries into ion species, one per charge state, using the
        /// native-mode charge envelope unless told otherwise.
        /// </summary>
        /// <param name="complexes">Stoichiometries and their shares of the ion current.</param>
        /// <param name="envelope">
        /// Charge envelope settings. Null uses the native model, which is the regime
        /// complexes survive in; a denatured envelope would take them apart.
        /// </param>
        /// <param name="maxIsotopePeaks">
        /// Cap on isotope peaks per charge state. The window is centred on the cluster's
        /// mode, so capping narrows it rather than moving it -- see
        /// <see cref="IsotopeEnvelope.SuggestedWindow"/>. A CWT cannot resolve isotopes of
        /// anything this size anyway, so the cluster's width matters and its detail does not.
        /// </param>
        public static IReadOnlyList<IonSpecies> CreateSpecies(IReadOnlyList<ProteinComplex> complexes,
            ChargeStateEnvelopeOptions? envelope = null, int maxIsotopePeaks = 24,
            double? decayConstant = null)
        {
            if (complexes == null)
            {
                throw new ArgumentNullException(nameof(complexes));
            }
            if (maxIsotopePeaks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIsotopePeaks),
                    "maxIsotopePeaks must be at least 1");
            }

            ChargeStateEnvelopeOptions options = (envelope ?? new ChargeStateEnvelopeOptions
            {
                Mode = IonizationMode.Native
            }).Clone();

            var species = new List<IonSpecies>();
            foreach (ProteinComplex complex in complexes)
            {
                if (complex.Abundance <= 0)
                {
                    continue;
                }
                double mass = complex.MonoisotopicMass;
                (int first, int count) = IsotopeEnvelope.SuggestedWindow(mass, maxIsotopePeaks);

                foreach (ChargeStateAbundance state in ChargeStateEnvelope.Generate(mass, options))
                {
                    species.Add(new IonSpecies
                    {
                        MonoisotopicMass = mass,
                        Charge = state.Charge,
                        Abundance = complex.Abundance * state.Abundance,
                        IsotopePeaks = count,
                        FirstIsotopePeak = first,
                        DecayConstant = decayConstant
                    });
                }
            }
            return species;
        }

        /// <summary>
        /// m/z values where two stoichiometries coincide exactly, which is where a
        /// deconvolution has no positional information to work with.
        ///
        /// <para>
        /// Useful for building a test that exercises the degeneracy deliberately rather
        /// than hoping to stumble into it.
        /// </para>
        /// </summary>
        public static IReadOnlyList<(int ChargeA, int ChargeB, double Mz)> SharedMzValues(
            ProteinComplex a, ProteinComplex b, int maxCharge = 60, double tolerancePpm = 1)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }
            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }
            if (maxCharge < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCharge), "maxCharge must be at least 1");
            }

            var shared = new List<(int, int, double)>();
            for (int za = 1; za <= maxCharge; za++)
            {
                double mzA = a.MonoisotopicMass / za + IonSpecies.ProtonMass;
                for (int zb = 1; zb <= maxCharge; zb++)
                {
                    double mzB = b.MonoisotopicMass / zb + IonSpecies.ProtonMass;
                    if (Math.Abs(mzA - mzB) / mzA * 1e6 <= tolerancePpm)
                    {
                        shared.Add((za, zb, mzA));
                    }
                }
            }
            return shared;
        }

        /// <summary>
        /// Shares abundance across the ladder, then trims and renormalises. Abundances sum
        /// to 1 over what survives.
        /// </summary>
        private static IReadOnlyList<ProteinComplex> ApplyAbundances(List<ProteinComplex> ladder,
            int fullSize, AssemblyOptions options)
        {
            if (options.DecayPerSubunit <= 0 || options.DecayPerSubunit > 1)
            {
                throw new ArgumentException("DecayPerSubunit must lie in (0, 1]", nameof(options));
            }
            if (options.RelativeThreshold < 0 || options.RelativeThreshold >= 1)
            {
                throw new ArgumentException("RelativeThreshold must lie in [0, 1)", nameof(options));
            }
            if (options.MaxSpecies < 1)
            {
                throw new ArgumentException("MaxSpecies must be at least 1", nameof(options));
            }
            if (options.Model == AssemblyAbundance.Explicit)
            {
                if (options.Abundances == null || options.Abundances.Count != ladder.Count)
                {
                    throw new ArgumentException(
                        $"Explicit abundances must have one entry per stoichiometry ({ladder.Count})",
                        nameof(options));
                }
                if (options.Abundances.Any(a => a < 0))
                {
                    throw new ArgumentException("Abundances must not be negative", nameof(options));
                }
            }

            var weighted = new List<ProteinComplex>(ladder.Count);
            for (int i = 0; i < ladder.Count; i++)
            {
                ProteinComplex complex = ladder[i];
                double weight = options.Model switch
                {
                    AssemblyAbundance.Uniform => 1,
                    AssemblyAbundance.IntactDominant =>
                        Math.Pow(options.DecayPerSubunit, fullSize - complex.SubunitCount),
                    AssemblyAbundance.MonomerDominant =>
                        Math.Pow(options.DecayPerSubunit, complex.SubunitCount - 1),
                    _ => options.Abundances![i]
                };
                weighted.Add(new ProteinComplex(complex.Composition, weight));
            }

            double peak = weighted.Max(c => c.Abundance);
            if (peak <= 0)
            {
                throw new ArgumentException("Every stoichiometry was given zero abundance", nameof(options));
            }

            List<ProteinComplex> kept = weighted
                .Where(c => c.Abundance >= peak * options.RelativeThreshold)
                .OrderByDescending(c => c.Abundance)
                .Take(options.MaxSpecies)
                .OrderBy(c => c.SubunitCount)
                .ThenBy(c => c.MonoisotopicMass)
                .ToList();

            double total = kept.Sum(c => c.Abundance);
            return kept
                .Select(c => new ProteinComplex(c.Composition, c.Abundance / total))
                .ToList();
        }
    }
}
