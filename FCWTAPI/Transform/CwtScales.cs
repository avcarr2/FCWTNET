using System;
using FCWTNET.Transform.Wavelets;

namespace FCWTNET.Transform
{
    /// <summary>
    /// Single source of truth for the mapping between CWT row index, wavelet scale and
    /// wavelet centre frequency.
    ///
    /// The scale grid is geometric, spanning <paramref name="noctaves"/> octaves with
    /// <c>nvoices</c> scales per octave:
    ///
    ///     a_j = 2 ^ (psoctave + j / nvoices),  j = 0 .. noctaves * nvoices - 1
    ///
    /// For a complex Morlet wavelet with central angular frequency c0 (sigma, in
    /// radians per sample), the wavelet at scale a peaks at angular frequency c0 / a,
    /// i.e. at a normalised frequency of
    ///
    ///     f_j = c0 / (2 * pi * a_j)     [cycles per sample]
    ///
    /// Normalised frequency is what <see cref="CWTFrequencies.CalculateTrueFrequencies"/>
    /// expects: it multiplies by the sampling rate to obtain Hz, so a valid axis lies in
    /// (0, 0.5] to respect Nyquist.
    /// </summary>
    public static class CwtScales
    {
        /// <summary>Number of scales (rows) produced for the given octave/voice settings.</summary>
        public static int ScaleCount(int psoctave, int pendoctave, int nvoices)
        {
            if (nvoices <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nvoices), "nvoices must be positive");
            }
            if (pendoctave < psoctave)
            {
                throw new ArgumentException("pendoctave must not be less than psoctave", nameof(pendoctave));
            }
            return (pendoctave - psoctave + 1) * nvoices;
        }

        /// <summary>
        /// Wavelet scales in samples, ascending. Index 0 is the smallest scale, which is
        /// the highest frequency. This is the row order the transform backends emit;
        /// <see cref="CWTObject.PerformCWT"/> reverses it so the stored CWT runs from low
        /// to high frequency.
        /// </summary>
        public static double[] Scales(int psoctave, int pendoctave, int nvoices)
        {
            int count = ScaleCount(psoctave, pendoctave, nvoices);
            double[] scales = new double[count];
            for (int j = 0; j < count; j++)
            {
                scales[j] = Math.Pow(2, psoctave + (double)j / nvoices);
            }
            return scales;
        }

        /// <summary>
        /// Wavelet centre frequencies in cycles per sample, ascending, matching the row
        /// order of the CWT <em>after</em> <see cref="CWTObject.PerformCWT"/> has
        /// reversed it. Index 0 is therefore the lowest frequency (largest scale), which
        /// is what <see cref="CWTFrequencies.CalculateIndicesForFrequencyRange"/> assumes
        /// when it binary-searches the axis.
        /// </summary>
        public static double[] AscendingCentreFrequencies(int psoctave, int pendoctave, int nvoices, double c0)
        {
            return AscendingCentreFrequencies(psoctave, pendoctave, nvoices, new MorletWavelet(c0));
        }

        /// <summary>
        /// As above, for an arbitrary wavelet. Each wavelet peaks at its own point in
        /// <c>a*w</c>, so the axis has to be built from the wavelet actually in use or the
        /// frequency and m/z labels silently describe the wrong rows.
        /// </summary>
        public static double[] AscendingCentreFrequencies(int psoctave, int pendoctave, int nvoices,
            IWavelet wavelet)
        {
            if (wavelet == null)
            {
                throw new ArgumentNullException(nameof(wavelet));
            }
            double[] scales = Scales(psoctave, pendoctave, nvoices);
            double[] frequencies = new double[scales.Length];
            for (int j = 0; j < scales.Length; j++)
            {
                // Reversed: largest scale (lowest frequency) lands at index 0.
                frequencies[j] = NormalizedCentreFrequency(scales[scales.Length - 1 - j], wavelet);
            }
            return frequencies;
        }

        /// <summary>Normalised centre frequency (cycles per sample) of a Morlet wavelet at the given scale.</summary>
        public static double NormalizedCentreFrequency(double scale, double c0)
        {
            return c0 / (2 * Math.PI * scale);
        }

        /// <summary>Normalised centre frequency (cycles per sample) of any wavelet at the given scale.</summary>
        public static double NormalizedCentreFrequency(double scale, IWavelet wavelet)
        {
            if (wavelet == null)
            {
                throw new ArgumentNullException(nameof(wavelet));
            }
            return wavelet.CentreAngularFrequency / (2 * Math.PI * scale);
        }

        /// <summary>
        /// Fractional frequency resolution of a Morlet wavelet with central frequency c0,
        /// as the FWHM of its frequency response divided by its centre frequency.
        ///
        /// In the frequency domain the wavelet is exp(-(a*w - c0)^2 / 2): a Gaussian of
        /// unit standard deviation in a*w, centred at c0. Its FWHM is therefore
        /// 2*sqrt(2*ln2) in those units, and relative to the centre frequency c0 that is
        ///
        ///     df/f = 2*sqrt(2*ln2) / c0
        ///
        /// Two components closer together than this merge into one ridge. This is the
        /// knob that decides whether a charge state envelope resolves: adjacent charge
        /// states of a 17 kDa protein near 17+ differ by about 3% in frequency, which
        /// needs c0 of roughly 80, not the 6 that suits a single well-separated peak.
        ///
        /// Raising c0 buys frequency resolution at the cost of time resolution -- the
        /// wavelet spans more cycles -- which is the trade the CWT exists to let you make.
        /// </summary>
        public static double RelativeFrequencyResolution(double c0)
        {
            if (c0 <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(c0), "c0 must be positive");
            }
            return 2 * Math.Sqrt(2 * Math.Log(2)) / c0;
        }

        /// <summary>
        /// Smallest c0 that can separate two components differing by the given fraction of
        /// their frequency. The inverse of <see cref="RelativeFrequencyResolution"/>.
        /// </summary>
        public static double C0ForRelativeResolution(double relativeSeparation)
        {
            if (relativeSeparation <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(relativeSeparation), "separation must be positive");
            }
            return 2 * Math.Sqrt(2 * Math.Log(2)) / relativeSeparation;
        }

        /// <summary>Scale whose Morlet wavelet is centred on the given normalised frequency.</summary>
        public static double ScaleForNormalizedFrequency(double normalizedFrequency, double c0)
        {
            if (normalizedFrequency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedFrequency), "normalised frequency must be positive");
            }
            return c0 / (2 * Math.PI * normalizedFrequency);
        }

        /// <summary>Scale at which any wavelet is centred on the given normalised frequency.</summary>
        public static double ScaleForNormalizedFrequency(double normalizedFrequency, IWavelet wavelet)
        {
            if (wavelet == null)
            {
                throw new ArgumentNullException(nameof(wavelet));
            }
            if (normalizedFrequency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedFrequency), "normalised frequency must be positive");
            }
            return wavelet.CentreAngularFrequency / (2 * Math.PI * normalizedFrequency);
        }
    }
}
