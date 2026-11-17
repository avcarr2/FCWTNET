using System;

namespace FCWTNET.Transform.Wavelets
{
    /// <summary>
    /// Shared machinery for analytic wavelets: derives the frequency resolution from the
    /// response itself rather than making each wavelet hand-derive a FWHM formula.
    ///
    /// The FWHM is found by bisecting for the half-maximum crossings either side of the
    /// known peak. This is exact to the tolerance used and, more importantly, cannot drift
    /// out of step with the response the transform actually applies.
    /// </summary>
    public abstract class WaveletBase : IWavelet
    {
        private const double Tolerance = 1e-10;
        private const int MaxIterations = 200;

        private double? _relativeResolution;

        public abstract string Name { get; }

        public abstract string Description { get; }

        public abstract double CentreAngularFrequency { get; }

        public abstract double Response(double scaleTimesOmega);

        public double RelativeFrequencyResolution =>
            _relativeResolution ??= ComputeFullWidthHalfMaximum() / CentreAngularFrequency;

        private double ComputeFullWidthHalfMaximum()
        {
            double peak = CentreAngularFrequency;
            if (peak <= 0)
            {
                throw new InvalidOperationException($"{Name}: CentreAngularFrequency must be positive");
            }
            double peakValue = Response(peak);
            if (peakValue <= 0)
            {
                throw new InvalidOperationException($"{Name}: response at the centre frequency must be positive");
            }
            double half = peakValue / 2;

            double left = BisectBelow(0, peak, half);
            double right = BisectAbove(peak, half);
            return right - left;
        }

        /// <summary>Crossing of the half-maximum on the rising side, between 0 and the peak.</summary>
        private double BisectBelow(double low, double high, double target)
        {
            // Response(0) is zero for every analytic wavelet here, so the crossing is bracketed.
            for (int i = 0; i < MaxIterations && high - low > Tolerance; i++)
            {
                double middle = 0.5 * (low + high);
                if (Response(middle) < target)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return 0.5 * (low + high);
        }

        /// <summary>Crossing of the half-maximum on the falling side, above the peak.</summary>
        private double BisectAbove(double peak, double target)
        {
            // Walk outward until the response has definitely fallen below half.
            double high = peak * 2;
            int expansions = 0;
            while (Response(high) >= target)
            {
                high *= 2;
                if (++expansions > 64)
                {
                    throw new InvalidOperationException($"{Name}: response does not decay above the centre frequency");
                }
            }

            double low = peak;
            for (int i = 0; i < MaxIterations && high - low > Tolerance; i++)
            {
                double middle = 0.5 * (low + high);
                if (Response(middle) >= target)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return 0.5 * (low + high);
        }
    }
}
