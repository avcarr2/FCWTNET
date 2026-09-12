using System;
using System.Numerics;
using System.Threading.Tasks;
using MathNet.Numerics.IntegralTransforms;
using FCWTNET.Transform.Wavelets;

namespace FCWTNET.Transform
{
    /// <summary>
    /// Pure managed complex CWT, computed in the frequency domain, for any analytic
    /// wavelet.
    ///
    /// The signal is transformed once, then for each scale the spectrum is multiplied by
    /// the wavelet's frequency response and inverse-transformed. The wavelet supplies that
    /// response (see <see cref="IWavelet"/>); the default is a complex Morlet.
    ///
    /// Because the wavelet is analytic (identically zero for negative frequencies) the
    /// result is complex, and its modulus is the envelope of the signal component at that
    /// scale.
    ///
    /// <para>
    /// Normalisation is by amplitude, not energy. The usual Torrence &amp; Compo form
    /// carries an extra sqrt(2*pi*a), which normalises each scale to unit energy but
    /// biases frequency estimation: for a Morlet and a pure tone at w0 the modulus then
    /// peaks where a*w0 = (c0 + sqrt(c0^2 + 2)) / 2 rather than at a*w0 = c0. At c0 = 6
    /// that is a 1.4% error in scale, and since m/z goes as 1/f^2 it becomes a 2.8% error
    /// in mass assignment -- far too large to be useful here. Because every wavelet's
    /// response is normalised to a peak of 1, the same amplitude scaling works for all of
    /// them and the ridge stays at the component's own frequency.
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
    public class ManagedCwt : ICwtBackend
    {
        /// <summary>Uses a complex Morlet; c0 comes from the per-transform argument.</summary>
        public ManagedCwt()
        {
        }

        /// <summary>
        /// Uses the given wavelet for every transform, ignoring the c0 argument passed to
        /// <see cref="Transform"/>. c0 is a Morlet-specific parameter and other wavelets
        /// carry their own.
        /// </summary>
        public ManagedCwt(IWavelet wavelet)
        {
            Wavelet = wavelet ?? throw new ArgumentNullException(nameof(wavelet));
        }

        /// <summary>
        /// The wavelet in use, or null to build a Morlet from each call's c0.
        /// </summary>
        public IWavelet? Wavelet { get; }

        public string Name => Wavelet == null ? "managed-morlet" : $"managed-{Wavelet.Name}";

        public bool IsAvailable => true;

        /// <summary>The wavelet this backend will apply for the given c0.</summary>
        public IWavelet ResolveWavelet(double c0) => Wavelet ?? new MorletWavelet(c0);

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
            if (Wavelet == null && c0 <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(c0), "c0 must be positive");
            }

            IWavelet wavelet = ResolveWavelet(c0);
            Transform(input, ScaleGrid.ForOctaves(psoctave, pendoctave, pnbvoice, wavelet),
                nthreads, out real, out imag);
        }

        /// <inheritdoc />
        public void Transform(double[] input, ScaleGrid grid, int nthreads,
            out double[][] real, out double[][] imag)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (input.Length == 0)
            {
                throw new ArgumentException("Input signal must not be empty", nameof(input));
            }
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            IWavelet wavelet = grid.Wavelet;
            double[] scales = grid.Scales;
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
                    double daughter = AmplitudeNormalisation * wavelet.Response(scale * omega[k]);
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
