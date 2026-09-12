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
    /// the analytic Morlet transfer function and inverse-transformed:
    ///
    ///     psi_hat(a, w) = 2 * exp(-(a*w - c0)^2 / 2)   for w &gt; 0
    ///                   = 0                            for w &lt;= 0
    ///
    /// Because the wavelet is analytic (identically zero for negative frequencies) the
    /// result is complex, and its modulus is the envelope of the signal component at that
    /// scale.
    ///
    /// <para>
    /// Normalisation is by amplitude, not energy. The usual Torrence &amp; Compo form
    /// carries an extra sqrt(2*pi*a), which normalises each scale to unit energy but
    /// biases frequency estimation: for a pure tone at w0 the modulus then peaks where
    /// a*w0 = (c0 + sqrt(c0^2 + 2)) / 2 rather than at a*w0 = c0. At c0 = 6 that is a
    /// 1.4% error in scale, and since m/z goes as 1/f^2 it becomes a 2.8% error in mass
    /// assignment -- far too large to be useful here.
    /// </para>
    /// <para>
    /// With the factor of 2 used above, a tone of amplitude A produces a modulus of A at
    /// its own scale, so the scalogram reads directly as an amplitude envelope and peak
    /// heights track ion abundance. Note this differs from whatever scaling the native
    /// fCWT applies, so absolute magnitudes are not comparable between the two backends
    /// even though peak positions are.
    /// </para>
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

            // Chosen so a tone of amplitude A yields a modulus of A: the forward FFT puts
            // A*n/2 in the tone's bin, and only the positive-frequency half is kept.
            const double AmplitudeNormalisation = 2.0;
            ParallelOptions options = new ParallelOptions();
            if (nthreads > 0)
            {
                options.MaxDegreeOfParallelism = nthreads;
            }

            Parallel.For(0, scales.Length, options, j =>
            {
                double scale = scales[j];

                Complex[] work = new Complex[n];
                for (int k = 0; k < positiveBins; k++)
                {
                    double shifted = scale * omega[k] - c0;
                    double daughter = AmplitudeNormalisation * Math.Exp(-0.5 * shifted * shifted);
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
