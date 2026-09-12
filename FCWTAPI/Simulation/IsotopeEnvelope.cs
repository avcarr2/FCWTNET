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
        /// A sensible number of isotope peaks to simulate for the given mass: enough to
        /// cover the bulk of the distribution without trailing off into noise.
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
        /// Relative intensities of the isotope cluster, index 0 being the monoisotopic
        /// peak. The returned values sum to 1, so scaling by a species' abundance
        /// preserves the total ion current of that species.
        /// </summary>
        public static double[] RelativeIntensities(double monoisotopicMass, int peakCount)
        {
            if (peakCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(peakCount), "peakCount must be at least 1");
            }

            double lambda = PoissonMean(monoisotopicMass);
            double[] intensities = new double[peakCount];

            // Poisson pmf built by recurrence (p_k = p_{k-1} * lambda / k) to avoid
            // computing factorials of large k.
            double term = Math.Exp(-lambda);
            intensities[0] = term;
            for (int k = 1; k < peakCount; k++)
            {
                term *= lambda / k;
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
