using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FCWTNET.Simulation;

namespace FCWTNET.Analysis
{
    /// <summary>One peak explained as an oligomer of a subunit, at a charge state.</summary>
    public class OligomerAssignment
    {
        /// <summary>How many subunits: 1 is the monomer, 2 the dimer, and so on.</summary>
        public int Order { get; init; }

        public int Charge { get; init; }

        public double Mz { get; init; }

        public double Magnitude { get; init; }

        /// <summary>Mass of the oligomer this peak was assigned to.</summary>
        public double OligomerMass { get; init; }

        /// <summary>
        /// How believable this charge is for an ion of this mass, in (0, 1]: a Gaussian
        /// weight against the native charge envelope. This is the whole discriminator --
        /// see <see cref="OligomerAnalysis"/>.
        /// </summary>
        public double Plausibility { get; init; }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "m/z {0:F3} = {1}-mer at {2}+ ({3:F1} Da, plausibility {4:F2})",
            Mz, Order, Charge, OligomerMass, Plausibility);
    }

    /// <summary>One candidate reading of a peak list as an oligomer series.</summary>
    public class OligomerLadder
    {
        /// <summary>Mass of the repeating unit.</summary>
        public double SubunitMass { get; init; }

        public IReadOnlyList<OligomerAssignment> Peaks { get; init; } = Array.Empty<OligomerAssignment>();

        /// <summary>Fraction of the total peak magnitude this ladder explains at all.</summary>
        public double Coverage { get; init; }

        /// <summary>
        /// Magnitude-weighted mean plausibility of the assigned charges. A ladder built on
        /// too large a subunit has to give its peaks charges the Rayleigh limit does not
        /// support, and this is where that shows.
        /// </summary>
        public double Plausibility { get; init; }

        /// <summary>Oligomer orders actually observed, ascending.</summary>
        public IReadOnlyList<int> Orders =>
            Peaks.Select(p => p.Order).Distinct().OrderBy(o => o).ToList();

        /// <summary>
        /// Fraction of the orders from the monomer up to the largest observed that are
        /// actually present, in (0, 1].
        ///
        /// <para>
        /// Measured from one, not from the smallest order seen, and that is the whole point.
        /// An assembly ladder is built a subunit at a time, so it produces consecutive
        /// orders and shows its small members. A subunit guessed too small has more orders
        /// to choose from and cherry-picks whichever fit -- landing on 3-mer, 4-mer and
        /// 5-mer with no monomer or dimer anywhere, which is not what a self-assembling
        /// protein looks like. Counting the gap at the bottom is what stops a single clean
        /// tetramer being read as a trimer-to-pentamer ladder of a quarter-sized subunit.
        /// </para>
        ///
        /// <para>
        /// It is the same argument <see cref="ChargeStateGroup.Contiguity"/> makes about
        /// charge states, asked about oligomer order instead -- where, unlike there, it is
        /// the right question.
        /// </para>
        /// </summary>
        public double Completeness
        {
            get
            {
                IReadOnlyList<int> orders = Orders;
                return orders.Count == 0 ? 0 : (double)orders.Count / orders[^1];
            }
        }

        /// <summary>Combined score, in [0, 1].</summary>
        public double Score => Coverage * Plausibility * Completeness;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "subunit {0:F1} Da, orders {1}, coverage {2:F2}, plausibility {3:F2}, completeness {4:F2}",
            SubunitMass, Orders.Count == 0 ? "none" : string.Join("/", Orders), Coverage, Plausibility,
            Completeness);
    }

    /// <summary>Knobs for <see cref="OligomerAnalysis"/>.</summary>
    public class OligomerOptions
    {
        /// <summary>Largest oligomer considered.</summary>
        public int MaxOrder { get; set; } = 8;

        /// <summary>How close an implied charge must be to a whole number to count.</summary>
        public double ChargeTolerance { get; set; } = 0.15;

        public int MinCharge { get; set; } = 1;
        public int MaxCharge { get; set; } = 100;

        /// <summary>
        /// Ionisation regime used to judge whether a charge is believable. Complexes
        /// survive native spray, so that is the default; a denatured envelope would have
        /// taken them apart.
        /// </summary>
        public IonizationMode Mode { get; set; } = IonizationMode.Native;

        /// <summary>
        /// Charges beyond this many envelope widths from the centroid count as impossible
        /// rather than merely unlikely. Without a floor, an absurd charge still scores a
        /// tiny positive weight and enough of them can outvote a sensible reading.
        /// </summary>
        public double MaximumDeviations { get; set; } = 4;

        /// <summary>
        /// How much better a smaller subunit must score before
        /// <see cref="OligomerAnalysis.FindLadder"/> prefers it.
        ///
        /// <para>
        /// A ladder built on m/k can express everything a ladder on m can, since m is just
        /// its k-th order, so a smaller subunit can never fit worse and will usually fit
        /// slightly better by having more ways to place each peak. Halving the subunit is a
        /// claim, not a free improvement, and this is the evidence it has to bring.
        /// </para>
        /// </summary>
        public double SubunitImprovementFactor { get; set; } = 1.2;
    }

    /// <summary>
    /// Reads a peak list as an oligomer series, which is the one question a charge-state
    /// comb cannot answer.
    ///
    /// <para>
    /// An n-mer at charge <c>n*z</c> sits at m/z <c>nM/(nz) = M/z</c>, exactly where the
    /// monomer at charge z sits. So for a self-assembling protein the comb in
    /// <see cref="ChargeStateDeconvolution"/> is genuinely degenerate: on a simulated
    /// monomer-through-tetramer ladder it returns a single species at the dimer's mass,
    /// carrying charge states drawn from all four oligomers, and it is not wrong to -- that
    /// reading explains every peak. The contiguity prior does not help either, because it
    /// was built to reject 2M as a harmonic and here 2M is real.
    /// </para>
    ///
    /// <para>
    /// <b>What does discriminate is charge, not position.</b> Native electrospray charges to
    /// the Rayleigh limit, <c>z ~ 0.0778*sqrt(M)</c>, so the plausible charges for a mass
    /// are a narrow band. Explaining a monomer's peaks as a dimer forces them to twice the
    /// charge, which for the monomer's low-m/z states lands many envelope widths outside
    /// that band. Scoring every candidate subunit mass by how believable the charges it
    /// implies are is therefore enough to pick the ladder out, and it uses only the model
    /// <see cref="ChargeStateEnvelope"/> already encodes.
    /// </para>
    ///
    /// <para>
    /// Two caveats worth stating. This assumes the sample really is an oligomer series of
    /// one subunit, so it will happily fit a ladder to a mixture that is not one -- read
    /// <see cref="OligomerLadder.Score"/>, not just the answer. And it rests entirely on
    /// the Rayleigh model being right about the sample, which is a statement about how the
    /// protein was sprayed, not about the data.
    /// </para>
    /// </summary>
    public static class OligomerAnalysis
    {
        /// <summary>
        /// Scores one candidate subunit mass: assigns every peak to the oligomer and charge
        /// that best explains it, and reports how much was explained and how believably.
        /// </summary>
        public static OligomerLadder Explain(IReadOnlyList<double> mzValues,
            IReadOnlyList<double> magnitudes, double subunitMass, OligomerOptions? options = null)
        {
            options ??= new OligomerOptions();
            if (mzValues == null)
            {
                throw new ArgumentNullException(nameof(mzValues));
            }
            if (magnitudes == null)
            {
                throw new ArgumentNullException(nameof(magnitudes));
            }
            if (mzValues.Count != magnitudes.Count)
            {
                throw new ArgumentException("mzValues and magnitudes must be the same length",
                    nameof(magnitudes));
            }
            if (subunitMass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(subunitMass), "subunitMass must be positive");
            }
            if (options.MaxOrder < 1)
            {
                throw new ArgumentException("MaxOrder must be at least 1", nameof(options));
            }
            if (options.ChargeTolerance <= 0 || options.ChargeTolerance >= 0.5)
            {
                throw new ArgumentException("ChargeTolerance must lie in (0, 0.5)", nameof(options));
            }

            // Centroid and width of the plausible charge band for each oligomer.
            var centroids = new double[options.MaxOrder + 1];
            var widths = new double[options.MaxOrder + 1];
            for (int order = 1; order <= options.MaxOrder; order++)
            {
                double mass = subunitMass * order;
                centroids[order] = ChargeStateEnvelope.CentroidCharge(mass, options.Mode);
                widths[order] = ChargeStateEnvelope.EnvelopeWidth(mass, options.Mode);
            }

            var assignments = new List<OligomerAssignment>();
            double totalMagnitude = magnitudes.Sum();
            double explainedMagnitude = 0;
            double weightedPlausibility = 0;

            for (int i = 0; i < mzValues.Count; i++)
            {
                double neutralPerCharge = mzValues[i] - IonSpecies.ProtonMass;
                if (neutralPerCharge <= 0)
                {
                    continue;
                }

                OligomerAssignment? best = null;
                for (int order = 1; order <= options.MaxOrder; order++)
                {
                    double mass = subunitMass * order;
                    double fractional = mass / neutralPerCharge;
                    int charge = (int)Math.Round(fractional);
                    if (charge < options.MinCharge || charge > options.MaxCharge)
                    {
                        continue;
                    }
                    if (Math.Abs(fractional - charge) > options.ChargeTolerance)
                    {
                        continue;
                    }

                    double deviations = Math.Abs(charge - centroids[order]) / widths[order];
                    if (deviations > options.MaximumDeviations)
                    {
                        // Not merely unlikely: outside what this mass can carry.
                        continue;
                    }
                    double plausibility = Math.Exp(-0.5 * deviations * deviations);

                    if (best == null || plausibility > best.Plausibility)
                    {
                        best = new OligomerAssignment
                        {
                            Order = order,
                            Charge = charge,
                            Mz = mzValues[i],
                            Magnitude = magnitudes[i],
                            OligomerMass = mass,
                            Plausibility = plausibility
                        };
                    }
                }

                if (best != null)
                {
                    assignments.Add(best);
                    explainedMagnitude += magnitudes[i];
                    weightedPlausibility += magnitudes[i] * best.Plausibility;
                }
            }

            return new OligomerLadder
            {
                SubunitMass = subunitMass,
                Peaks = assignments,
                Coverage = totalMagnitude > 0 ? explainedMagnitude / totalMagnitude : 0,
                Plausibility = explainedMagnitude > 0 ? weightedPlausibility / explainedMagnitude : 0
            };
        }

        /// <summary>
        /// Searches for the subunit whose ladder best explains the peaks, trying the given
        /// mass and every integer fraction of it.
        ///
        /// <para>
        /// The fractions are what matter: a comb that has locked onto the dimer reports 2m,
        /// and the true subunit is that divided by two. Trying multiples as well would only
        /// re-find ladders already covered, since a ladder on m already contains 2m, 3m and
        /// the rest as its own higher orders.
        /// </para>
        /// </summary>
        /// <param name="candidateMass">
        /// A mass known to be present, typically from
        /// <see cref="ChargeStateDeconvolution"/>.
        /// </param>
        public static OligomerLadder? FindLadder(IReadOnlyList<double> mzValues,
            IReadOnlyList<double> magnitudes, double candidateMass, OligomerOptions? options = null)
        {
            options ??= new OligomerOptions();
            if (candidateMass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateMass), "candidateMass must be positive");
            }

            if (options.SubunitImprovementFactor < 1)
            {
                throw new ArgumentException("SubunitImprovementFactor must be at least 1", nameof(options));
            }

            // Largest subunit first, so a smaller one has to earn the switch.
            OligomerLadder? best = null;
            for (int divisor = 1; divisor <= options.MaxOrder; divisor++)
            {
                OligomerLadder ladder = Explain(mzValues, magnitudes, candidateMass / divisor, options);
                if (ladder.Peaks.Count == 0)
                {
                    continue;
                }
                if (best == null || ladder.Score > best.Score * options.SubunitImprovementFactor)
                {
                    best = ladder;
                }
            }
            return best;
        }

        /// <summary>
        /// Total abundance assigned to each oligomer order, which is what a stoichiometry
        /// argument is actually made from.
        /// </summary>
        public static IReadOnlyDictionary<int, double> AbundanceByOrder(OligomerLadder ladder)
        {
            if (ladder == null)
            {
                throw new ArgumentNullException(nameof(ladder));
            }
            return ladder.Peaks
                .GroupBy(p => p.Order)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Magnitude));
        }
    }
}
