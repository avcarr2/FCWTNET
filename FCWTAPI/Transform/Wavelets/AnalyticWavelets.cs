using System;

namespace FCWTNET.Transform.Wavelets
{
    /// <summary>
    /// Complex Morlet: a Gaussian in frequency centred on c0.
    ///
    ///     psi_hat(x) = exp(-(x - c0)^2 / 2),   x = a*w
    ///
    /// The default and the right choice for mass spectrometry. Its response is Gaussian in
    /// both domains, so it sits exactly on the time-frequency uncertainty bound: no other
    /// wavelet gives better frequency resolution for the same time resolution. c0 is the
    /// number of radians of carrier under the envelope, and the resolution is
    /// 2*sqrt(2*ln2)/c0 -- raise it to separate close components, lower it to follow fast
    /// changes.
    /// </summary>
    public class MorletWavelet : WaveletBase
    {
        public const double DefaultC0 = 6.0;

        public MorletWavelet(double c0 = DefaultC0)
        {
            if (c0 <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(c0), "c0 must be positive");
            }
            C0 = c0;
        }

        /// <summary>Central angular frequency of the carrier, in radians per sample.</summary>
        public double C0 { get; }

        public override string Name => "morlet";

        public override string Description =>
            "Gaussian in frequency; optimal time-frequency trade-off. The default.";

        public override double CentreAngularFrequency => C0;

        public override double Response(double scaleTimesOmega)
        {
            if (scaleTimesOmega <= 0)
            {
                return 0;
            }
            double shifted = scaleTimesOmega - C0;
            return Math.Exp(-0.5 * shifted * shifted);
        }

        /// <summary>
        /// Closed form, which the numeric base would otherwise rediscover:
        /// the response is a unit-variance Gaussian, so its FWHM is 2*sqrt(2*ln2).
        /// </summary>
        public double ExactRelativeFrequencyResolution => 2 * Math.Sqrt(2 * Math.Log(2)) / C0;
    }

    /// <summary>
    /// Paul wavelet of order m:
    ///
    ///     psi_hat(x) proportional to x^m * exp(-x),   x = a*w
    ///
    /// Sharply localised in time and correspondingly poor in frequency -- its resolution
    /// improves only as 1/sqrt(m), so matching a Morlet with c0 = 80 would need order
    /// ~6400. Useful when the question is "when exactly did this change?" rather than
    /// "what two frequencies are these?": transients that start, stop or step abruptly.
    /// </summary>
    public class PaulWavelet : WaveletBase
    {
        public const int DefaultOrder = 4;

        public PaulWavelet(int order = DefaultOrder)
        {
            if (order < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(order), "order must be at least 1");
            }
            Order = order;
        }

        public int Order { get; }

        public override string Name => "paul";

        public override string Description =>
            "Sharp in time, broad in frequency. For abrupt onsets rather than close peaks.";

        // d/dx [m ln x - x] = 0 at x = m.
        public override double CentreAngularFrequency => Order;

        public override double Response(double scaleTimesOmega)
        {
            if (scaleTimesOmega <= 0)
            {
                return 0;
            }
            // Normalised to a peak of 1 at x = m, computed in logs so large orders do not
            // overflow x^m before the exponential brings it back down.
            double logResponse = Order * (Math.Log(scaleTimesOmega) - Math.Log(Order))
                                 + Order - scaleTimesOmega;
            return Math.Exp(logResponse);
        }
    }

    /// <summary>
    /// Bump wavelet: a smooth window with compact support in frequency.
    ///
    ///     psi_hat(x) = exp(1 - 1 / (1 - ((x - mu)/sigma)^2))   for |x - mu| &lt; sigma
    ///                = 0                                        otherwise
    ///
    /// Being exactly zero outside a finite band, it cannot leak energy from a distant
    /// strong peak into a weak one nearby -- useful when a base peak is orders of magnitude
    /// larger than the isotopes or adducts around it, where a Gaussian's tails would still
    /// contribute. The price is a less compact time response than the Morlet.
    /// </summary>
    public class BumpWavelet : WaveletBase
    {
        public const double DefaultCentre = 5.0;
        public const double DefaultWidth = 0.6;

        public BumpWavelet(double centre = DefaultCentre, double width = DefaultWidth)
        {
            if (centre <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(centre), "centre must be positive");
            }
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "width must be positive");
            }
            if (width >= centre)
            {
                throw new ArgumentException(
                    "width must be smaller than centre, or the band reaches zero frequency", nameof(width));
            }
            Centre = centre;
            Width = width;
        }

        /// <summary>Centre of the band in a*w units.</summary>
        public double Centre { get; }

        /// <summary>Half-width of the band in a*w units; the response is zero outside it.</summary>
        public double Width { get; }

        public override string Name => "bump";

        public override string Description =>
            "Compact support in frequency: no spectral leakage from distant strong peaks.";

        public override double CentreAngularFrequency => Centre;

        public override double Response(double scaleTimesOmega)
        {
            if (scaleTimesOmega <= 0)
            {
                return 0;
            }
            double normalised = (scaleTimesOmega - Centre) / Width;
            if (Math.Abs(normalised) >= 1)
            {
                return 0;
            }
            return Math.Exp(1 - 1 / (1 - normalised * normalised));
        }
    }

    /// <summary>
    /// Log-Gabor: a Gaussian in log-frequency.
    ///
    ///     psi_hat(x) = exp( -(ln(x/mu))^2 / (2 * (ln sigma)^2) ),   x = a*w
    ///
    /// Because it is symmetric on a logarithmic axis it has the same fractional bandwidth
    /// at every scale, which is exactly how a geometric scale grid is laid out, and it
    /// carries no DC component by construction. A reasonable alternative to the Morlet
    /// when the signal spans a wide frequency range, as a charge state envelope does.
    /// </summary>
    public class LogGaborWavelet : WaveletBase
    {
        public const double DefaultCentre = 6.0;
        public const double DefaultBandwidth = 1.15;

        public LogGaborWavelet(double centre = DefaultCentre, double bandwidth = DefaultBandwidth)
        {
            if (centre <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(centre), "centre must be positive");
            }
            if (bandwidth <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bandwidth),
                    "bandwidth is a ratio and must be greater than 1");
            }
            Centre = centre;
            Bandwidth = bandwidth;
        }

        /// <summary>Centre of the band in a*w units.</summary>
        public double Centre { get; }

        /// <summary>
        /// Width as a frequency ratio: the standard deviation in log space is ln(Bandwidth),
        /// so 1.15 is roughly a 15% relative width. Values closer to 1 are narrower.
        /// </summary>
        public double Bandwidth { get; }

        public override string Name => "log-gabor";

        public override string Description =>
            "Gaussian in log-frequency: constant fractional bandwidth across all scales.";

        public override double CentreAngularFrequency => Centre;

        public override double Response(double scaleTimesOmega)
        {
            if (scaleTimesOmega <= 0)
            {
                return 0;
            }
            double logRatio = Math.Log(scaleTimesOmega / Centre);
            double sigma = Math.Log(Bandwidth);
            return Math.Exp(-0.5 * (logRatio * logRatio) / (sigma * sigma));
        }
    }

    /// <summary>
    /// Analytic derivative-of-Gaussian of order m (m = 2 is the Mexican hat):
    ///
    ///     psi_hat(x) proportional to x^m * exp(-x^2 / 2),   x = a*w
    ///
    /// <para>
    /// Note this is the <b>analytic</b> form -- the response is taken over positive
    /// frequencies only. The textbook DOG is real-valued, which makes its CWT oscillate
    /// rather than produce an envelope, and an oscillating transform has no ridge for peak
    /// detection to follow. The one-sided version keeps the wavelet's shape while giving a
    /// modulus that behaves like the other wavelets here.
    /// </para>
    ///
    /// Broad in frequency at low orders, so it is a poor choice for separating charge
    /// states; included mainly for comparison.
    /// </summary>
    public class DerivativeOfGaussianWavelet : WaveletBase
    {
        public const int DefaultOrder = 2;

        public DerivativeOfGaussianWavelet(int order = DefaultOrder)
        {
            if (order < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(order), "order must be at least 1");
            }
            Order = order;
        }

        public int Order { get; }

        public override string Name => "dog";

        public override string Description =>
            "Analytic derivative of Gaussian (order 2 is the Mexican hat). Broad in frequency.";

        // d/dx [m ln x - x^2/2] = 0 at x = sqrt(m).
        public override double CentreAngularFrequency => Math.Sqrt(Order);

        public override double Response(double scaleTimesOmega)
        {
            if (scaleTimesOmega <= 0)
            {
                return 0;
            }
            double peak = CentreAngularFrequency;
            // Normalised to 1 at the peak, in logs for numerical stability.
            double logResponse = Order * (Math.Log(scaleTimesOmega) - Math.Log(peak))
                                 - 0.5 * (scaleTimesOmega * scaleTimesOmega - peak * peak);
            return Math.Exp(logResponse);
        }
    }
}
