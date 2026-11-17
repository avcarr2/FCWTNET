using System;

namespace FCWTNET.Simulation
{
    /// <summary>
    /// Approximate isotope distributions for peptides and proteins, without needing an
    /// elemental formula.
    ///
    /// The relative intensities of a peptide's isotope cluster are well approximated by a
    /// Poisson distribution whose mean grows linearly with mass (Breen et al., 2000):
    ///
    ///     lambda = 0.000594 * M - 0.03091
    ///
    /// This is the averagine idea in its cheapest useful form: accurate enough in shape
    /// and spacing to be worth simulating, and not a substitute for a real formula-based
    /// isotope calculation.
    /// </summary>
    public static class IsotopeEnvelope
    {
        /// <summary>Poisson mean for the isotope distribution of a peptide of the given mass.</summary>
        public static double PoissonMean(double monoisotopicMass)
        {
            if (monoisotopicMass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monoisotopicMass), "mass must be positive");
            }
            // Clamped at zero: below ~52 Da the linear fit goes negative, which is
            // meaningless and would produce a degenerate distribution.
            return Math.Max(0.0, 0.000594 * monoisotopicMass - 0.03091);
        }

        /// <summary>
        /// A sensible number of isotope peaks to simulate for the given mass, counting from
        /// the monoisotopic peak: enough to cover the bulk of the distribution without
        /// trailing off into noise.
        ///
        /// <para>
        /// Fine for peptides and small proteins. Above a few tens of kDa the cluster's mode
        /// moves far from the monoisotopic peak and counting from zero spends most of the
        /// peaks on a negligible tail -- use <see cref="SuggestedWindow"/> there.
        /// </para>
        /// </summary>
        public static int SuggestedPeakCount(double monoisotopicMass)
        {
            double lambda = PoissonMean(monoisotopicMass);
            // Mean plus four standard deviations of a Poisson, rounded up, with room for
            // the monoisotopic peak itself.
            int count = (int)Math.Ceiling(lambda + 4 * Math.Sqrt(lambda)) + 1;
            return Math.Max(1, count);
        }

        /// <summary>
        /// The isotope peaks actually worth simulating: the first peak and how many, as a
        /// window centred on the distribution rather than a count taken from zero.
        ///
        /// <para>
        /// For a peptide the two are the same, because the mode is at or near the
        /// monoisotopic peak. For anything large they are not, and the difference is a
        /// trap: a 64 kDa complex has a Poisson mean of 38, so its monoisotopic peak
        /// carries a weight of e^-38 and the first eight isotopes carry essentially none of
        /// the cluster. Asking for eight peaks from zero therefore does not simulate a
        /// truncated cluster, it simulates the wrong m/z -- the renormalisation puts the
        /// whole abundance onto a tail that should have been invisible, about 38 Da below
        /// where the ion really is.
        /// </para>
        /// </summary>
        /// <param name="monoisotopicMass">Neutral monoisotopic mass in Da.</param>
        /// <param name="maxPeaks">
        /// Cap on the number of peaks; 0 means no cap. When the four-sigma window is wider
        /// than this, the window is narrowed around the mode rather than truncated at the
        /// bottom, so the cluster stays where it belongs.
        /// </param>
        public static (int FirstPeak, int PeakCount) SuggestedWindow(double monoisotopicMass,
            int maxPeaks = 0)
        {
            double lambda = PoissonMean(monoisotopicMass);
            double spread = 4 * Math.Sqrt(lambda);
            int first = Math.Max(0, (int)Math.Floor(lambda - spread));
            int last = (int)Math.Ceiling(lambda + spread);
            int count = Math.Max(1, last - first + 1);

            if (maxPeaks > 0 && count > maxPeaks)
            {
                // Keep the mode, give up the tails.
                int mode = (int)Math.Round(lambda);
                first = Math.Max(0, mode - maxPeaks / 2);
                count = maxPeaks;
            }
            return (first, count);
        }

        /// <summary>
        /// Relative intensities of the isotope cluster, index 0 being the monoisotopic
        /// peak. The returned values sum to 1, so scaling by a species' abundance
        /// preserves the total ion current of that species.
        /// </summary>
        public static double[] RelativeIntensities(double monoisotopicMass, int peakCount)
            => RelativeIntensities(monoisotopicMass, peakCount, 0);

        /// <summary>
        /// Relative intensities of a window of the isotope cluster, starting at
        /// <paramref name="firstPeak"/> neutrons above the monoisotopic peak. Values are
        /// renormalised over the window, so a species keeps its full abundance.
        /// </summary>
        public static double[] RelativeIntensities(double monoisotopicMass, int peakCount,
            int firstPeak)
        {
            if (peakCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(peakCount), "peakCount must be at least 1");
            }
            if (firstPeak < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstPeak), "firstPeak must not be negative");
            }

            double lambda = PoissonMean(monoisotopicMass);
            double[] intensities = new double[peakCount];

            // Poisson pmf built by recurrence (p_k = p_{k-1} * lambda / k) to avoid
            // computing factorials of large k. Walking up to firstPeak in log space keeps
            // it from underflowing on a large cluster, where p_0 is around e^-38.
            double logTerm = -lambda;
            for (int k = 1; k <= firstPeak; k++)
            {
                logTerm += Math.Log(lambda) - Math.Log(k);
            }
            double term = Math.Exp(logTerm);
            intensities[0] = term;
            for (int k = 1; k < peakCount; k++)
            {
                term *= lambda / (firstPeak + k);
                intensities[k] = term;
            }

            // Renormalise over the truncated window so the cluster carries the full abundance.
            double total = 0;
            for (int k = 0; k < peakCount; k++)
            {
                total += intensities[k];
            }
            if (total <= 0)
            {
                // Degenerate case (lambda = 0 and peakCount = 1): all intensity is monoisotopic.
                intensities[0] = 1;
                return intensities;
            }
            for (int k = 0; k < peakCount; k++)
            {
                intensities[k] /= total;
            }
            return intensities;
        }
    }
}
