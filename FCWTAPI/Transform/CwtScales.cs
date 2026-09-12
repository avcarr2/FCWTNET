using System;

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
            double[] scales = Scales(psoctave, pendoctave, nvoices);
            double[] frequencies = new double[scales.Length];
            for (int j = 0; j < scales.Length; j++)
            {
                // Reversed: largest scale (lowest frequency) lands at index 0.
                frequencies[j] = NormalizedCentreFrequency(scales[scales.Length - 1 - j], c0);
            }
            return frequencies;
        }

        /// <summary>Normalised centre frequency (cycles per sample) of a Morlet wavelet at the given scale.</summary>
        public static double NormalizedCentreFrequency(double scale, double c0)
        {
            return c0 / (2 * Math.PI * scale);
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
    }
}
