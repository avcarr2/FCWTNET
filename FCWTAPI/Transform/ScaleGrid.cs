using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FCWTNET.Transform.Wavelets;

namespace FCWTNET.Transform
{
    /// <summary>
    /// Records where a requested frequency range had to be narrowed, and why. Reported
    /// rather than applied silently: a grid that quietly analyses a different range than
    /// asked for is how axis bugs start.
    /// </summary>
    public class ScaleGridClamps
    {
        /// <summary>Highest normalised frequency asked for.</summary>
        public double RequestedMaxFrequency { get; init; }

        /// <summary>Highest normalised frequency actually used.</summary>
        public double AppliedMaxFrequency { get; init; }

        /// <summary>Lowest normalised frequency asked for.</summary>
        public double RequestedMinFrequency { get; init; }

        /// <summary>Lowest normalised frequency actually used.</summary>
        public double AppliedMinFrequency { get; init; }

        /// <summary>True when the top of the range was pulled down to Nyquist.</summary>
        public bool NyquistClamped { get; init; }

        /// <summary>
        /// True when the bottom of the range was pulled up because the wavelet at that
        /// scale would not fit inside the transient.
        /// </summary>
        public bool SignalLengthClamped { get; init; }

        public bool Any => NyquistClamped || SignalLengthClamped;

        /// <summary>One line per clamp, or empty when nothing was limited.</summary>
        public IReadOnlyList<string> Describe()
        {
            var lines = new List<string>();
            if (NyquistClamped)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "upper frequency limited by Nyquist: {0:F4} -> {1:F4} cycles/sample",
                    RequestedMaxFrequency, AppliedMaxFrequency));
            }
            if (SignalLengthClamped)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "lower frequency limited by transient length: {0:G4} -> {1:G4} cycles/sample " +
                    "(the wavelet would not fit inside the signal)",
                    RequestedMinFrequency, AppliedMinFrequency));
            }
            return lines;
        }
    }

    /// <summary>
    /// The scale axis of a CWT: which scales are computed, and what frequency each one
    /// sits at.
    ///
    /// <para>
    /// Scales are geometric, <c>a_k = a_min * 2^(k / voices)</c>, but unlike the older
    /// octave-based grid the endpoints are set by the frequencies actually of interest
    /// rather than rounded out to whole octaves. For mass spectrometry that matters: a
    /// native myoglobin charge envelope occupies 0.29 octaves, so a three-octave grid
    /// spends roughly ten times more work than the data needs.
    /// </para>
    ///
    /// <para>
    /// Two limits are always applied. The top of the range cannot exceed Nyquist. The
    /// bottom cannot ask for a scale so large that the wavelet is no longer short compared
    /// with the transient — past that point the FFT's circular convolution wraps the end of
    /// the signal into its start and the edges of the scalogram are simply wrong.
    /// </para>
    /// </summary>
    public class ScaleGrid
    {
        /// <summary>
        /// Largest scale allowed, as a fraction of the signal length. A Morlet's
        /// time-domain envelope has a standard deviation of about <c>a</c> samples, so at
        /// 0.1 the wavelet's bulk occupies at most a tenth of the transient and wrap-around
        /// is negligible. Raising it recovers lower frequencies at the cost of edge
        /// accuracy.
        /// </summary>
        public const double DefaultMaxScaleFraction = 0.1;

        /// <summary>Highest normalised frequency any wavelet can meaningfully sit at.</summary>
        public const double NyquistNormalizedFrequency = 0.5;

        private ScaleGrid(double[] scales, int voicesPerOctave, IWavelet wavelet, ScaleGridClamps clamps)
        {
            Scales = scales;
            VoicesPerOctave = voicesPerOctave;
            Wavelet = wavelet;
            Clamps = clamps;

            // Ascending in frequency is descending in scale.
            AscendingCentreFrequencies = new double[scales.Length];
            for (int i = 0; i < scales.Length; i++)
            {
                AscendingCentreFrequencies[i] =
                    CwtScales.NormalizedCentreFrequency(scales[scales.Length - 1 - i], wavelet);
            }
        }

        /// <summary>
        /// Scales in samples, ascending. Index 0 is the smallest scale and therefore the
        /// highest frequency; this is the row order transform backends emit, which
        /// <see cref="CWTObject.PerformCWT"/> then reverses.
        /// </summary>
        public double[] Scales { get; }

        /// <summary>
        /// Normalised centre frequency of each row after that reversal, ascending. This is
        /// the axis <see cref="CWTFrequencies"/> consumes.
        /// </summary>
        public double[] AscendingCentreFrequencies { get; }

        public int VoicesPerOctave { get; }

        public IWavelet Wavelet { get; }

        /// <summary>What, if anything, was narrowed from the request.</summary>
        public ScaleGridClamps Clamps { get; }

        public int Count => Scales.Length;

        /// <summary>Span of the grid in octaves.</summary>
        public double Octaves => Count <= 1 ? 0 : Math.Log2(Scales[^1] / Scales[0]);

        /// <summary>
        /// Reproduces the historical integer-octave grid exactly, so existing callers keep
        /// the behaviour they had.
        /// </summary>
        public static ScaleGrid ForOctaves(int psoctave, int pendoctave, int voicesPerOctave, IWavelet wavelet)
        {
            if (wavelet == null)
            {
                throw new ArgumentNullException(nameof(wavelet));
            }
            double[] scales = CwtScales.Scales(psoctave, pendoctave, voicesPerOctave);
            return new ScaleGrid(scales, voicesPerOctave, wavelet, new ScaleGridClamps
            {
                RequestedMaxFrequency = CwtScales.NormalizedCentreFrequency(scales[0], wavelet),
                AppliedMaxFrequency = CwtScales.NormalizedCentreFrequency(scales[0], wavelet),
                RequestedMinFrequency = CwtScales.NormalizedCentreFrequency(scales[^1], wavelet),
                AppliedMinFrequency = CwtScales.NormalizedCentreFrequency(scales[^1], wavelet)
            });
        }

        /// <summary>
        /// Grid covering a normalised frequency range (cycles per sample).
        /// </summary>
        /// <param name="minFrequency">Lowest frequency of interest.</param>
        /// <param name="maxFrequency">Highest frequency of interest.</param>
        /// <param name="voicesPerOctave">Scales per octave.</param>
        /// <param name="wavelet">Analysing wavelet; sets where each scale lands.</param>
        /// <param name="signalLength">
        /// Number of samples to be transformed, used for the wrap-around guard. Pass 0 to
        /// skip that check.
        /// </param>
        /// <param name="maxScaleFraction">See <see cref="DefaultMaxScaleFraction"/>.</param>
        public static ScaleGrid ForFrequencyRange(double minFrequency, double maxFrequency,
            int voicesPerOctave, IWavelet wavelet, int signalLength = 0,
            double maxScaleFraction = DefaultMaxScaleFraction)
        {
            if (wavelet == null)
            {
                throw new ArgumentNullException(nameof(wavelet));
            }
            if (voicesPerOctave <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(voicesPerOctave), "voicesPerOctave must be positive");
            }
            if (minFrequency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minFrequency), "minFrequency must be positive");
            }
            if (maxFrequency <= minFrequency)
            {
                throw new ArgumentException("maxFrequency must be greater than minFrequency", nameof(maxFrequency));
            }
            if (maxScaleFraction <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxScaleFraction), "maxScaleFraction must be positive");
            }

            double appliedMax = maxFrequency;
            bool nyquistClamped = false;
            if (appliedMax > NyquistNormalizedFrequency)
            {
                appliedMax = NyquistNormalizedFrequency;
                nyquistClamped = true;
            }

            double appliedMin = minFrequency;
            bool lengthClamped = false;
            if (signalLength > 0)
            {
                double largestUsableScale = maxScaleFraction * signalLength;
                double lowestUsableFrequency =
                    CwtScales.NormalizedCentreFrequency(largestUsableScale, wavelet);
                if (appliedMin < lowestUsableFrequency)
                {
                    appliedMin = lowestUsableFrequency;
                    lengthClamped = true;
                }
            }

            if (appliedMin >= appliedMax)
            {
                throw new ArgumentException(
                    $"No usable scales remain: the requested range {minFrequency:G4}-{maxFrequency:G4} " +
                    $"cycles/sample collapses to {appliedMin:G4}-{appliedMax:G4} after applying the Nyquist " +
                    "and transient-length limits. Use a longer transient, a smaller c0, or a narrower m/z range.",
                    nameof(minFrequency));
            }

            // Smallest scale is the highest frequency.
            double smallestScale = CwtScales.ScaleForNormalizedFrequency(appliedMax, wavelet);
            double largestScale = CwtScales.ScaleForNormalizedFrequency(appliedMin, wavelet);

            // Round the count up so the requested range is covered rather than trimmed.
            int steps = (int)Math.Ceiling(voicesPerOctave * Math.Log2(largestScale / smallestScale));
            steps = Math.Max(0, steps);

            var generated = new List<double>(steps + 1);
            for (int k = 0; k <= steps; k++)
            {
                generated.Add(smallestScale * Math.Pow(2, (double)k / voicesPerOctave));
            }

            // The step count is rounded up so the requested range is covered rather than
            // trimmed, which can push the last scale just past the end. That is harmless for
            // a range the caller asked for, but a safety clamp is a hard limit: a scale
            // beyond it is the wrap-around this guard exists to prevent.
            if (lengthClamped)
            {
                double hardLimit = maxScaleFraction * signalLength;
                while (generated.Count > 1 && generated[^1] > hardLimit)
                {
                    generated.RemoveAt(generated.Count - 1);
                }
            }
            double[] scales = generated.ToArray();

            return new ScaleGrid(scales, voicesPerOctave, wavelet, new ScaleGridClamps
            {
                RequestedMaxFrequency = maxFrequency,
                AppliedMaxFrequency = appliedMax,
                RequestedMinFrequency = minFrequency,
                AppliedMinFrequency = appliedMin,
                NyquistClamped = nyquistClamped,
                SignalLengthClamped = lengthClamped
            });
        }

        /// <summary>
        /// Grid covering an m/z range, using the Orbitrap calibration f = sqrt(A / (m/z)).
        ///
        /// Note the inversion: a <em>low</em> m/z oscillates at a <em>high</em> frequency,
        /// so <paramref name="minMz"/> sets the top of the frequency range.
        /// </summary>
        public static ScaleGrid ForMzRange(double minMz, double maxMz, double calibrationCoefficient,
            int samplingRate, int voicesPerOctave, IWavelet wavelet, int signalLength = 0,
            double maxScaleFraction = DefaultMaxScaleFraction)
        {
            if (minMz <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minMz), "minMz must be positive");
            }
            if (maxMz <= minMz)
            {
                throw new ArgumentException("maxMz must be greater than minMz", nameof(maxMz));
            }
            if (calibrationCoefficient <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(calibrationCoefficient),
                    "calibrationCoefficient must be positive");
            }
            if (samplingRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplingRate), "samplingRate must be positive");
            }

            double maxFrequency = Math.Sqrt(calibrationCoefficient / minMz) / samplingRate;
            double minFrequency = Math.Sqrt(calibrationCoefficient / maxMz) / samplingRate;
            return ForFrequencyRange(minFrequency, maxFrequency, voicesPerOctave, wavelet,
                signalLength, maxScaleFraction);
        }

        /// <summary>m/z corresponding to each row, ascending in frequency and so descending in m/z.</summary>
        public double[] MzAxis(double calibrationCoefficient, int samplingRate)
        {
            return AscendingCentreFrequencies
                .Select(f =>
                {
                    double hz = f * samplingRate;
                    return calibrationCoefficient / (hz * hz);
                })
                .ToArray();
        }

        /// <summary>One-line summary for logs and status lines.</summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} scales, {1:F2} octaves, {2} voices/octave, wavelet {3}",
                Count, Octaves, VoicesPerOctave, Wavelet.Name);
        }
    }
}
