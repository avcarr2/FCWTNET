using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FCWTNET.Simulation;

namespace FCWTNET.Analysis
{
    /// <summary>One detected peak, assigned to a charge state of a particular species.</summary>
    public class AssignedPeak
    {
        /// <summary>Mass-to-charge ratio of the peak.</summary>
        public double Mz { get; init; }

        /// <summary>Peak magnitude, used to weight the mass estimate.</summary>
        public double Magnitude { get; init; }

        /// <summary>Charge state assigned to this peak.</summary>
        public int Charge { get; init; }

        /// <summary>Neutral mass this peak alone implies: z * (m/z - proton).</summary>
        public double ImpliedMass { get; init; }

        /// <summary>Deviation of <see cref="ImpliedMass"/> from the group's mass, in ppm.</summary>
        public double MassErrorPpm { get; init; }
    }

    /// <summary>How a group was found.</summary>
    [Flags]
    public enum DeconvolutionMethod
    {
        None = 0,

        /// <summary>Comb coherence in reciprocal-frequency space.</summary>
        Comb = 1,

        /// <summary>Pairwise charge-difference voting.</summary>
        Voting = 2,

        /// <summary>Found independently by both, which is the strongest evidence.</summary>
        Both = Comb | Voting
    }

    /// <summary>A set of peaks explained as one species at several charge states.</summary>
    public class ChargeStateGroup
    {
        /// <summary>Intensity-weighted neutral monoisotopic mass.</summary>
        public double MonoisotopicMass { get; init; }

        /// <summary>Spread of the per-peak mass estimates, in ppm (1 sigma).</summary>
        public double MassUncertaintyPpm { get; init; }

        /// <summary>Comb coherence in [0, 1]; 1 means every peak lands exactly on the comb.</summary>
        public double Score { get; init; }

        /// <summary>Which method(s) found this group.</summary>
        public DeconvolutionMethod Method { get; init; }

        /// <summary>Summed magnitude of the member peaks.</summary>
        public double TotalMagnitude { get; init; }

        public IReadOnlyList<AssignedPeak> Peaks { get; init; } = Array.Empty<AssignedPeak>();

        public int MinCharge => Peaks.Count == 0 ? 0 : Peaks.Min(p => p.Charge);

        public int MaxCharge => Peaks.Count == 0 ? 0 : Peaks.Max(p => p.Charge);

        /// <summary>
        /// Fraction of the charge states between <see cref="MinCharge"/> and
        /// <see cref="MaxCharge"/> that are actually present, in (0, 1].
        ///
        /// Electrospray produces a contiguous envelope -- a distribution over consecutive
        /// charge states -- so a genuine species scores near 1. This is what separates a
        /// real mass from its harmonics: the alias at 2M explains the same peaks but assigns
        /// only even charges, leaving every other state empty and halving contiguity.
        /// </summary>
        public double Contiguity =>
            Peaks.Count == 0 ? 0 : (double)Peaks.Count / (MaxCharge - MinCharge + 1);

        /// <summary>
        /// How well this explanation holds together: coherence times contiguity. Used to
        /// rank competing masses that claim overlapping peaks.
        /// </summary>
        public double Quality => Score * Contiguity;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0:F2} Da +/- {1:F0} ppm, {2} charge states {3}+ to {4}+, score {5:F3} ({6})",
                MonoisotopicMass, MassUncertaintyPpm, Peaks.Count, MinCharge, MaxCharge, Score, Method);
        }
    }

    /// <summary>Knobs for <see cref="ChargeStateDeconvolution"/>.</summary>
    public class ChargeDeconvolutionOptions
    {
        public double MinMass { get; set; } = 500;
        public double MaxMass { get; set; } = 200_000;

        /// <summary>Charge states considered plausible.</summary>
        public int MinCharge { get; set; } = 1;
        public int MaxCharge { get; set; } = 60;

        /// <summary>Largest gap in charge the voting method will bridge over missing peaks.</summary>
        public int MaxChargeGap { get; set; } = 5;

        /// <summary>
        /// How close a peak's implied charge must be to a whole number to count. Peak
        /// positions here are good to a few hundred ppm, so this is generous by design.
        /// </summary>
        public double ChargeTolerance { get; set; } = 0.15;

        /// <summary>Masses within this tolerance are treated as the same species.</summary>
        public double MassTolerancePpm { get; set; } = 2000;

        /// <summary>Minimum comb coherence for a group to be reported.</summary>
        public double MinScore { get; set; } = 0.7;

        /// <summary>A species must explain at least this many peaks.</summary>
        public int MinPeaksPerGroup { get; set; } = 3;

        /// <summary>
        /// Minimum fraction of the charge states spanned by a group that must be present.
        /// Electrospray envelopes are contiguous, so a low value here is the signature of a
        /// harmonic (which only claims every second or third state) rather than a species.
        /// </summary>
        public double MinContiguity { get; set; } = 0.5;

        /// <summary>Candidate masses evaluated per octave of mass when scanning the comb.</summary>
        public int MassGridPointsPerOctave { get; set; } = 4000;

        /// <summary>Stop after this many species, so a pathological list cannot loop forever.</summary>
        public int MaxSpecies { get; set; } = 20;
    }

    /// <summary>
    /// Groups detected peaks into species, assigning a charge state to each peak.
    ///
    /// <para>
    /// Both methods rest on the same algebra. With the Orbitrap calibration m/z = A/f^2 and
    /// m/z = M/z + m_p for a neutral mass M at charge z:
    /// </para>
    /// <code>
    ///     1/f^2 = M/(A*z) + m_p/A
    /// </code>
    /// <para>
    /// so 1/f^2 is exactly linear in 1/z. Defining
    /// </para>
    /// <code>
    ///     w = 1 / (1/f^2 - m_p/A)   =>   w = (A/M) * z
    /// </code>
    /// <para>
    /// the charge states of a single species fall on a uniform comb in w with step A/M.
    /// Equivalently, and more usefully here, w*M/A is exactly the integer z.
    /// </para>
    ///
    /// <para>
    /// The two methods fail in different ways -- the comb is exact but dislikes sparse
    /// peak lists, voting is robust to missing charge states but noisier -- so agreement
    /// between them is itself evidence.
    /// </para>
    /// </summary>
    public static class ChargeStateDeconvolution
    {
        /// <summary>Mass of the charge-carrying proton.</summary>
        public const double ProtonMass = IonSpecies.ProtonMass;

        /// <summary>
        /// Group peaks into species. <paramref name="mzValues"/> and
        /// <paramref name="magnitudes"/> must be the same length.
        /// </summary>
        public static IReadOnlyList<ChargeStateGroup> Deconvolve(
            IReadOnlyList<double> mzValues, IReadOnlyList<double> magnitudes,
            ChargeDeconvolutionOptions? options = null)
        {
            options ??= new ChargeDeconvolutionOptions();
            Validate(mzValues, magnitudes, options);

            if (mzValues.Count < options.MinPeaksPerGroup)
            {
                return Array.Empty<ChargeStateGroup>();
            }

            // Greedy peeling. In a mixture the true mass of one species only explains its
            // own peaks, so its coherence over the whole list is roughly its share of the
            // intensity -- too low to separate from noise by threshold alone. Taking the
            // strongest species, removing its peaks and repeating lets each species be
            // found against a progressively cleaner background.
            var remaining = Enumerable.Range(0, mzValues.Count).ToList();
            var found = new List<ChargeStateGroup>();

            while (remaining.Count >= options.MinPeaksPerGroup && found.Count < options.MaxSpecies)
            {
                double[] mz = remaining.Select(i => mzValues[i]).ToArray();
                double[] magnitude = remaining.Select(i => magnitudes[i]).ToArray();

                ChargeStateGroup? best = null;
                foreach ((double mass, DeconvolutionMethod method) in Candidates(mz, magnitude, options))
                {
                    ChargeStateGroup? group = BuildGroup(mass, mz, magnitude, method, options);
                    if (group == null || group.Contiguity < options.MinContiguity)
                    {
                        continue;
                    }
                    // Intensity explained, discounted by how well it is explained. Without
                    // the quality factor a harmonic wins simply by sweeping up a couple of
                    // extra peaks from another species that happen to fall on its comb.
                    if (best == null ||
                        group.TotalMagnitude * group.Quality > best.TotalMagnitude * best.Quality)
                    {
                        best = group;
                    }
                }

                if (best == null)
                {
                    break;
                }

                found.Add(best);
                // Drop the peaks this species accounted for.
                var claimed = new HashSet<double>(best.Peaks.Select(p => p.Mz));
                remaining = remaining.Where(i => !claimed.Contains(mzValues[i])).ToList();
            }

            return MergeAndRank(RefineAssignments(found, mzValues, magnitudes, options), options);
        }

        /// <summary>
        /// Lets every peak choose between the species found, then re-estimates each mass.
        ///
        /// Peeling is greedy: the first species claims any peak that happens to fit its comb,
        /// including ones that really belong to a weaker species found later. A wrongly
        /// claimed peak drags the mass by a few hundred ppm. Here each peak is given to
        /// whichever group explains it best -- smallest deviation of its implied charge from
        /// a whole number -- and the masses are recomputed from the result.
        /// </summary>
        private static List<ChargeStateGroup> RefineAssignments(List<ChargeStateGroup> groups,
            IReadOnlyList<double> mzValues, IReadOnlyList<double> magnitudes,
            ChargeDeconvolutionOptions options)
        {
            if (groups.Count < 2)
            {
                return groups;
            }

            var masses = groups.Select(g => g.MonoisotopicMass).ToList();

            // Two passes is enough: the first fixes the assignments, the second settles the
            // masses those assignments imply.
            for (int pass = 0; pass < 2; pass++)
            {
                var claims = masses.Select(_ => new List<int>()).ToList();

                for (int peak = 0; peak < mzValues.Count; peak++)
                {
                    int bestGroup = -1;
                    double bestResidual = options.ChargeTolerance;
                    for (int g = 0; g < masses.Count; g++)
                    {
                        double impliedCharge = ImpliedCharge(mzValues[peak], masses[g]);
                        int charge = (int)Math.Round(impliedCharge);
                        if (charge < options.MinCharge || charge > options.MaxCharge)
                        {
                            continue;
                        }
                        double residual = Math.Abs(impliedCharge - charge);
                        if (residual < bestResidual)
                        {
                            bestResidual = residual;
                            bestGroup = g;
                        }
                    }
                    if (bestGroup >= 0)
                    {
                        claims[bestGroup].Add(peak);
                    }
                }

                for (int g = 0; g < masses.Count; g++)
                {
                    if (claims[g].Count < options.MinPeaksPerGroup)
                    {
                        continue;
                    }
                    double weight = claims[g].Sum(i => magnitudes[i]);
                    masses[g] = claims[g].Sum(i =>
                        magnitudes[i] * MassForCharge(mzValues[i],
                            (int)Math.Round(ImpliedCharge(mzValues[i], masses[g])))) / weight;
                }
            }

            // Rebuild the groups from the settled masses, each over the peaks it won.
            var refined = new List<ChargeStateGroup>();
            var taken = new HashSet<int>();
            for (int g = 0; g < masses.Count; g++)
            {
                var owned = Enumerable.Range(0, mzValues.Count)
                    .Where(i => !taken.Contains(i))
                    .Where(i =>
                    {
                        double implied = ImpliedCharge(mzValues[i], masses[g]);
                        return Math.Abs(implied - Math.Round(implied)) <= options.ChargeTolerance;
                    })
                    .ToList();

                ChargeStateGroup? group = BuildGroup(masses[g],
                    owned.Select(i => mzValues[i]).ToArray(),
                    owned.Select(i => magnitudes[i]).ToArray(),
                    groups[g].Method, options);
                if (group == null)
                {
                    continue;
                }
                refined.Add(group);
                foreach (AssignedPeak assigned in group.Peaks)
                {
                    int index = owned.First(i => mzValues[i] == assigned.Mz);
                    taken.Add(index);
                }
            }
            return refined.Count >= groups.Count ? refined : groups;
        }

        /// <summary>
        /// Candidate masses from both methods, de-duplicated. Each is tagged with where it
        /// came from so corroboration can be reported.
        /// </summary>
        private static IEnumerable<(double Mass, DeconvolutionMethod Method)> Candidates(
            IReadOnlyList<double> mzValues, IReadOnlyList<double> magnitudes,
            ChargeDeconvolutionOptions options)
        {
            var comb = CombCandidateMasses(mzValues, magnitudes, options);
            var voting = VotingCandidateMasses(mzValues, options);

            foreach (double mass in comb)
            {
                bool alsoVoted = voting.Any(v => Math.Abs(v - mass) / mass * 1e6 <= options.MassTolerancePpm);
                yield return (mass, alsoVoted ? DeconvolutionMethod.Both : DeconvolutionMethod.Comb);
            }
            foreach (double mass in voting)
            {
                bool alreadyCombed = comb.Any(c => Math.Abs(c - mass) / mass * 1e6 <= options.MassTolerancePpm);
                if (!alreadyCombed)
                {
                    yield return (mass, DeconvolutionMethod.Voting);
                }
            }
        }

        /// <summary>
        /// Convenience overload taking peaks straight from
        /// <see cref="RidgeExtraction.FindPeaks(double[,], double[], double, int)"/> run
        /// against an m/z axis.
        /// </summary>
        public static IReadOnlyList<ChargeStateGroup> Deconvolve(
            IReadOnlyList<DetectedPeak> peaks, ChargeDeconvolutionOptions? options = null)
        {
            if (peaks == null)
            {
                throw new ArgumentNullException(nameof(peaks));
            }
            return Deconvolve(
                peaks.Select(p => p.Position).ToList(),
                peaks.Select(p => p.Magnitude).ToList(),
                options);
        }

        /// <summary>
        /// Comb coherence of a peak list against a candidate mass, in [0, 1].
        ///
        /// Each peak contributes exp(2*pi*i * z_i) where z_i = M/(m/z - m_p) is its implied
        /// charge. At the true mass every z_i is an integer, every term is exp(2*pi*i*k) = 1,
        /// and the intensity-weighted mean has magnitude exactly 1. A wrong mass scatters
        /// the phases and the sum cancels.
        ///
        /// Note this is also exactly 1 at 2M, 3M and every other integer multiple, since
        /// those simply scale each z_i by a whole number. Distinguishing the real mass from
        /// its harmonics is the job of the contiguity prior, not of this score.
        /// </summary>
        public static double CombScore(IReadOnlyList<double> mzValues, IReadOnlyList<double> magnitudes,
            double mass)
        {
            if (mass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(mass), "mass must be positive");
            }

            double realPart = 0;
            double imaginaryPart = 0;
            double weight = 0;
            for (int i = 0; i < mzValues.Count; i++)
            {
                double impliedCharge = ImpliedCharge(mzValues[i], mass);
                if (impliedCharge <= 0)
                {
                    continue;
                }
                double w = magnitudes[i];
                double phase = 2 * Math.PI * impliedCharge;
                realPart += w * Math.Cos(phase);
                imaginaryPart += w * Math.Sin(phase);
                weight += w;
            }
            if (weight <= 0)
            {
                return 0;
            }
            return Math.Sqrt(realPart * realPart + imaginaryPart * imaginaryPart) / weight;
        }

        /// <summary>
        /// Charge a peak would carry if it belonged to a species of the given mass. Real
        /// valued: how close it lands to a whole number is the evidence.
        /// </summary>
        public static double ImpliedCharge(double mz, double mass)
        {
            double excess = mz - ProtonMass;
            if (excess <= 0)
            {
                return 0;
            }
            return mass / excess;
        }

        /// <summary>Neutral mass implied by a peak at a given charge.</summary>
        public static double MassForCharge(double mz, int charge) => charge * (mz - ProtonMass);

        private static void Validate(IReadOnlyList<double> mzValues, IReadOnlyList<double> magnitudes,
            ChargeDeconvolutionOptions options)
        {
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
                throw new ArgumentException("mzValues and magnitudes must be the same length", nameof(magnitudes));
            }
            if (options.MinMass <= 0 || options.MaxMass <= options.MinMass)
            {
                throw new ArgumentException("MaxMass must exceed MinMass, and both must be positive", nameof(options));
            }
            if (options.MinCharge < 1 || options.MaxCharge < options.MinCharge)
            {
                throw new ArgumentException("MaxCharge must be at least MinCharge, and MinCharge at least 1",
                    nameof(options));
            }
            if (options.MaxChargeGap < 1)
            {
                throw new ArgumentException("MaxChargeGap must be at least 1", nameof(options));
            }
            if (options.MinPeaksPerGroup < 2)
            {
                throw new ArgumentException("MinPeaksPerGroup must be at least 2", nameof(options));
            }
            if (options.MassGridPointsPerOctave < 1)
            {
                throw new ArgumentException("MassGridPointsPerOctave must be positive", nameof(options));
            }
            foreach (double mz in mzValues)
            {
                if (mz <= ProtonMass)
                {
                    throw new ArgumentException($"m/z values must exceed the proton mass; got {mz}", nameof(mzValues));
                }
            }
        }

        /// <summary>
        /// Scans mass on a logarithmic grid and keeps local maxima of the comb score.
        /// Logarithmic because the comb's sensitivity to mass is multiplicative.
        /// </summary>
        private static List<double> CombCandidateMasses(IReadOnlyList<double> mzValues,
            IReadOnlyList<double> magnitudes, ChargeDeconvolutionOptions options)
        {
            double logLow = Math.Log2(options.MinMass);
            double logHigh = Math.Log2(options.MaxMass);
            int steps = Math.Max(2, (int)Math.Ceiling((logHigh - logLow) * options.MassGridPointsPerOctave));

            double[] scores = new double[steps + 1];
            double[] masses = new double[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                masses[i] = Math.Pow(2, logLow + (logHigh - logLow) * i / steps);
                scores[i] = CombScore(mzValues, magnitudes, masses[i]);
            }

            var candidates = new List<double>();
            for (int i = 1; i < steps; i++)
            {
                if (scores[i] < options.MinScore)
                {
                    continue;
                }
                if (scores[i] >= scores[i - 1] && scores[i] >= scores[i + 1])
                {
                    // Refine by golden-section-ish local search on the bracketing interval.
                    candidates.Add(RefineCombPeak(mzValues, magnitudes, masses[i - 1], masses[i + 1]));
                }
            }
            return candidates;
        }

        private static double RefineCombPeak(IReadOnlyList<double> mzValues, IReadOnlyList<double> magnitudes,
            double low, double high)
        {
            // Ternary search on a unimodal bump; 60 iterations is far past double precision
            // on any realistic mass range.
            for (int i = 0; i < 60 && high - low > 1e-9; i++)
            {
                double a = low + (high - low) / 3;
                double b = high - (high - low) / 3;
                if (CombScore(mzValues, magnitudes, a) < CombScore(mzValues, magnitudes, b))
                {
                    low = a;
                }
                else
                {
                    high = b;
                }
            }
            return 0.5 * (low + high);
        }

        /// <summary>
        /// Every pair of peaks, under every plausible charge gap, votes for the mass it
        /// implies. Real species accumulate votes from many pairs; chance coincidences do not.
        /// </summary>
        private static List<double> VotingCandidateMasses(IReadOnlyList<double> mzValues,
            ChargeDeconvolutionOptions options)
        {
            var votes = new List<double>();
            for (int i = 0; i < mzValues.Count; i++)
            {
                for (int j = 0; j < mzValues.Count; j++)
                {
                    double low = mzValues[i];
                    double high = mzValues[j];
                    if (high <= low)
                    {
                        continue;
                    }
                    for (int gap = 1; gap <= options.MaxChargeGap; gap++)
                    {
                        // z_low = gap * (high - m_p) / (high - low), from equating the two
                        // implied masses with charges differing by `gap`.
                        double chargeOfLower = gap * (high - ProtonMass) / (high - low);
                        int rounded = (int)Math.Round(chargeOfLower);
                        if (rounded < options.MinCharge || rounded > options.MaxCharge)
                        {
                            continue;
                        }
                        if (Math.Abs(chargeOfLower - rounded) > options.ChargeTolerance)
                        {
                            continue;
                        }
                        double mass = MassForCharge(low, rounded);
                        if (mass >= options.MinMass && mass <= options.MaxMass)
                        {
                            votes.Add(mass);
                        }
                    }
                }
            }

            // Cluster the votes; a cluster's centre is a candidate mass.
            votes.Sort();
            var candidates = new List<double>();
            int index = 0;
            while (index < votes.Count)
            {
                double start = votes[index];
                double sum = 0;
                int count = 0;
                while (index < votes.Count &&
                       (votes[index] - start) / start * 1e6 <= options.MassTolerancePpm)
                {
                    sum += votes[index];
                    count++;
                    index++;
                }
                if (count >= options.MinPeaksPerGroup)
                {
                    candidates.Add(sum / count);
                }
            }
            return candidates;
        }

        /// <summary>
        /// Assigns whichever peaks are consistent with the candidate mass, then re-estimates
        /// the mass from those peaks.
        /// </summary>
        private static ChargeStateGroup? BuildGroup(double mass, IReadOnlyList<double> mzValues,
            IReadOnlyList<double> magnitudes, DeconvolutionMethod method,
            ChargeDeconvolutionOptions options)
        {
            var members = new List<(double Mz, double Magnitude, int Charge, double ImpliedMass)>();
            var usedCharges = new HashSet<int>();

            // Strongest peaks claim their charge state first, so a weak coincidence cannot
            // displace a real assignment.
            foreach (int i in Enumerable.Range(0, mzValues.Count)
                                        .OrderByDescending(i => magnitudes[i]))
            {
                double impliedCharge = ImpliedCharge(mzValues[i], mass);
                int charge = (int)Math.Round(impliedCharge);
                if (charge < options.MinCharge || charge > options.MaxCharge)
                {
                    continue;
                }
                if (Math.Abs(impliedCharge - charge) > options.ChargeTolerance)
                {
                    continue;
                }
                if (!usedCharges.Add(charge))
                {
                    continue;
                }
                members.Add((mzValues[i], magnitudes[i], charge, MassForCharge(mzValues[i], charge)));
            }

            if (members.Count < options.MinPeaksPerGroup)
            {
                return null;
            }

            // Reject peaks that round to a plausible charge but then disagree with the rest
            // of the group about the mass. Rounding to a whole number is a weak test taken
            // one peak at a time -- a peak from a different species can pass it -- but the
            // real members of an envelope all imply the same mass to within the peak
            // position error, while an interloper is off by thousands of ppm. Compared
            // against the median so a single outlier cannot move the reference.
            members.Sort((a, b) => a.ImpliedMass.CompareTo(b.ImpliedMass));
            double medianMass = members[members.Count / 2].ImpliedMass;
            members = members
                .Where(m => Math.Abs(m.ImpliedMass - medianMass) / medianMass * 1e6 <= options.MassTolerancePpm)
                .ToList();

            if (members.Count < options.MinPeaksPerGroup)
            {
                return null;
            }

            double totalMagnitude = members.Sum(m => m.Magnitude);
            double refinedMass = members.Sum(m => m.Magnitude * m.ImpliedMass) / totalMagnitude;

            double variance = members.Sum(m =>
            {
                double ppm = (m.ImpliedMass - refinedMass) / refinedMass * 1e6;
                return m.Magnitude * ppm * ppm;
            }) / totalMagnitude;

            // Coherence of the peaks actually claimed. This is close to 1 by construction,
            // so it is a goodness-of-fit measure for the assignment rather than evidence
            // that the species exists -- peeling and the peak count carry that.
            double[] memberMz = members.Select(m => m.Mz).ToArray();
            double[] memberMagnitudes = members.Select(m => m.Magnitude).ToArray();
            double score = CombScore(memberMz, memberMagnitudes, refinedMass);
            if (score < options.MinScore)
            {
                return null;
            }

            return new ChargeStateGroup
            {
                MonoisotopicMass = refinedMass,
                MassUncertaintyPpm = Math.Sqrt(variance),
                Score = score,
                Method = method,
                TotalMagnitude = totalMagnitude,
                Peaks = members
                    .OrderBy(m => m.Charge)
                    .Select(m => new AssignedPeak
                    {
                        Mz = m.Mz,
                        Magnitude = m.Magnitude,
                        Charge = m.Charge,
                        ImpliedMass = m.ImpliedMass,
                        MassErrorPpm = (m.ImpliedMass - refinedMass) / refinedMass * 1e6
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Collapses candidates that describe the same species and orders by intensity.
        /// Harmonics are already suppressed during peeling by the contiguity prior.
        /// </summary>
        private static IReadOnlyList<ChargeStateGroup> MergeAndRank(List<ChargeStateGroup> groups,
            ChargeDeconvolutionOptions options)
        {
            var merged = new List<ChargeStateGroup>();
            foreach (ChargeStateGroup group in groups.OrderByDescending(g => g.TotalMagnitude))
            {
                ChargeStateGroup? existing = merged.FirstOrDefault(m =>
                    Math.Abs(m.MonoisotopicMass - group.MonoisotopicMass) / m.MonoisotopicMass * 1e6
                        <= options.MassTolerancePpm);
                if (existing == null)
                {
                    merged.Add(group);
                    continue;
                }
                int at = merged.IndexOf(existing);
                merged[at] = new ChargeStateGroup
                {
                    MonoisotopicMass = existing.MonoisotopicMass,
                    MassUncertaintyPpm = existing.MassUncertaintyPpm,
                    Score = Math.Max(existing.Score, group.Score),
                    Method = existing.Method | group.Method,
                    TotalMagnitude = existing.TotalMagnitude,
                    Peaks = existing.Peaks.Count >= group.Peaks.Count ? existing.Peaks : group.Peaks
                };
            }
            return merged.OrderByDescending(g => g.TotalMagnitude).ToList();
        }
    }
}
