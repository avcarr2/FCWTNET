using System;
using System.Numerics;
using System.Threading.Tasks;
using MathNet.Numerics.IntegralTransforms;

namespace FCWTNET.Transform
{
    /// <summary>
    /// Pure managed complex Morlet CWT, computed in the frequency domain.
    ///
    /// The signal is transformed once, then for each scale the spectrum is multiplied by
    /// the analytic Morlet transfer function and inverse-transformed. This is the standard
    /// Torrence &amp; Compo formulation:
    ///
    ///     psi_hat(a, w) = pi^(-1/4) * sqrt(2 * pi * a) * exp(-(a*w - c0)^2 / 2)   for w &gt; 0
    ///                   = 0                                                       for w &lt;= 0
    ///
    /// The sqrt(a) factor normalises every scale to unit energy, so magnitudes are
    /// comparable across rows. Because the wavelet is analytic (identically zero for
    /// negative frequencies), the result is complex and its modulus is the analytic
    /// envelope of the signal component at that scale.
    ///
    /// Unlike <see cref="NativeFcwtBackend"/> this runs on any platform, which is what
    /// makes the simulator, the CLI and CI usable off Windows.
    /// </summary>
    public class ManagedMorletCwt : ICwtBackend
    {
        public string Name => "managed-morlet";

        public bool IsAvailable => true;

        public void Transform(double[] input, int psoctave, int pendoctave, int pnbvoice, float c0,
            int nthreads, bool useOptimizationSchemes, out double[][] real, out double[][] imag)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (input.Length == 0)
            {
                throw new ArgumentException("Input signal must not be empty", nameof(input));
            }
            if (c0 <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(c0), "c0 must be positive");
            }

            double[] scales = CwtScales.Scales(psoctave, pendoctave, pnbvoice);
            int n = input.Length;

            // Forward transform of the signal, computed once and reused for every scale.
            Complex[] spectrum = new Complex[n];
            for (int i = 0; i < n; i++)
            {
                spectrum[i] = new Complex(input[i], 0);
            }
            Fourier.Forward(spectrum, FourierOptions.NoScaling);

            // Angular frequency of each FFT bin, in radians per sample. Bins above
            // Nyquist represent negative frequencies, where the analytic wavelet is zero,
            // so we only ever evaluate the wavelet on the non-negative half.
            int positiveBins = n / 2 + 1;
            double[] omega = new double[positiveBins];
            for (int k = 0; k < positiveBins; k++)
            {
                omega[k] = 2.0 * Math.PI * k / n;
            }

            double[][] realRows = new double[scales.Length][];
            double[][] imagRows = new double[scales.Length][];

            double normalisation = Math.Pow(Math.PI, -0.25);
            ParallelOptions options = new ParallelOptions();
            if (nthreads > 0)
            {
                options.MaxDegreeOfParallelism = nthreads;
            }

            Parallel.For(0, scales.Length, options, j =>
            {
                double scale = scales[j];
                double scaleNorm = normalisation * Math.Sqrt(2.0 * Math.PI * scale);

                Complex[] work = new Complex[n];
                for (int k = 0; k < positiveBins; k++)
                {
                    double shifted = scale * omega[k] - c0;
                    double daughter = scaleNorm * Math.Exp(-0.5 * shifted * shifted);
                    work[k] = spectrum[k] * daughter;
                }
                // Bins above Nyquist stay zero: that is what makes the result analytic.

                Fourier.Inverse(work, FourierOptions.NoScaling);

                double[] rowReal = new double[n];
                double[] rowImag = new double[n];
                for (int i = 0; i < n; i++)
                {
                    rowReal[i] = work[i].Real / n;
                    rowImag[i] = work[i].Imaginary / n;
                }
                realRows[j] = rowReal;
                imagRows[j] = rowImag;
            });

            real = realRows;
            imag = imagRows;
        }
    }
}
