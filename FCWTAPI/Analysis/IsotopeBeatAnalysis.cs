using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using FCWTNET.Simulation;

namespace FCWTNET.Analysis
{
    /// <summary>Charge read directly off the beat in a ridge's envelope.</summary>
    public class IsotopeBeatResult
    {
        /// <summary>Carrier frequency of the ridge, in Hz.</summary>
        public double CarrierFrequencyHz { get; init; }

        /// <summary>Measured envelope modulation frequency, in Hz.</summary>
        public double BeatFrequencyHz { get; init; }

        /// <summary>Beat period in seconds.</summary>
        public double BeatPeriodSeconds => BeatFrequencyHz > 0 ? 1 / BeatFrequencyHz : double.PositiveInfinity;

        /// <summary>Charge implied by the beat, before rounding.</summary>
        public double FractionalCharge { get; init; }

        /// <summary>Nearest whole charge.</summary>
        public int Charge => (int)Math.Round(FractionalCharge);

        /// <summary>
        /// How far the fractional charge sits from a whole number, in [0, 0.5]. Small is
        /// good: a real charge state lands near an integer.
        /// </summary>
        public double ChargeResidual => Math.Abs(FractionalCharge - Charge);

        /// <summary>
        /// Height of the beat peak relative to the mean of the envelope spectrum. Values
        /// near 1 mean there is no beat to speak of; a clear modulation gives tens.
        /// </summary>
        public double Prominence { get; init; }

        /// <summary>How many full beat cycles the analysed window contains.</summary>
        public double ObservedBeats { get; init; }

        /// <summary>RMS modulation of the detrended envelope, as a fraction of the envelope.</summary>
        public double ModulationDepth { get; init; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "z = {0} (fractional {1:F2}), beat {2:F2} Hz / {3:F1} ms, {4:F1} cycles observed, prominence {5:F1}",
                Charge, FractionalCharge, BeatFrequencyHz, BeatPeriodSeconds * 1000,
                ObservedBeats, Prominence);
        }
    }

    /// <summary>Knobs for <see cref="IsotopeBeatAnalysis"/>.</summary>
    public class IsotopeBeatOptions
    {
        /// <summary>Charge states considered plausible; bounds the beat frequency search.</summary>
        public int MinCharge { get; set; } = 1;
        public int MaxCharge { get; set; } = 60;

        /// <summary>
        /// At least this many beat cycles must fit in the analysed window. Below about two
        /// the "beat" is indistinguishable from the decay envelope.
        /// </summary>
        public double MinimumObservedBeats { get; set; } = 1.5;

        /// <summary>Reject results whose spectral peak is not at least this prominent.</summary>
        public double MinimumProminence { get; set; } = 4.0;

        /// <summary>
        /// Minimum RMS modulation of the detrended envelope, as a fraction of the envelope
        /// itself. Prominence alone is a ratio, so it stays high even when the residual is
        /// numerical dust -- a perfectly exponential decay detrends to nothing and its
        /// spectrum is still peaked somewhere. This sets an absolute floor on there being a
        /// modulation at all.
        /// </summary>
        public double MinimumModulationDepth { get; set; } = 1e-3;

        /// <summary>Spacing between adjacent isotope peaks, in Da.</summary>
        public double IsotopeSpacing { get; set; } = IonSpecies.IsotopeSpacing;
    }

    /// <summary>
    /// Determines charge from the beat an unresolved isotope cluster leaves in a ridge's
    /// envelope.
    ///
    /// <para>
    /// Adjacent isotopes of a species at charge z sit <c>spacing/z</c> Th apart. Since
    /// <c>m/z = A/f^2</c>, a fractional step in m/z is half that in frequency and opposite
    /// in sign, so the two isotopes differ in frequency by
    /// </para>
    /// <code>
    ///     df = f * (spacing/z) / (2 * m/z) = spacing * f^3 / (2 * A * z)
    /// </code>
    /// <para>
    /// Two tones that close together are not resolved by any practical wavelet -- doing so
    /// would need a c0 in the tens of thousands -- but they still <em>interfere</em>, and the
    /// interference modulates the envelope at exactly <c>df</c>. Measuring that modulation
    /// therefore yields the charge:
    /// </para>
    /// <code>
    ///     z = spacing * f^3 / (2 * A * df)
    /// </code>
    ///
    /// <para>
    /// This is the one measurement here an FFT of the whole transient cannot make: the
    /// modulation lives in how the amplitude at one frequency changes over time, which is
    /// precisely what a CWT preserves and a single spectrum averages away. It also fixes the
    /// charge of each ridge independently, without assuming anything about which peaks
    /// belong together -- so it can break ties that comb and voting methods cannot, such as a
    /// peak list containing only every third charge state.
    /// </para>
    ///
    /// <para>
    /// The cost is observation time. For myoglobin near 17+ on a 4 MHz instrument the beat
    /// is about 14 Hz, a 73 ms period, so a transient must run for a good fraction of a
    /// second before the modulation is visible at all.
    /// </para>
    /// </summary>
    public static class IsotopeBeatAnalysis
    {
        /// <summary>Beat frequency in Hz expected for a given carrier and charge.</summary>
        public static double BeatFrequencyForCharge(double carrierHz, double charge,
            double calibrationCoefficient, double isotopeSpacing = IonSpecies.IsotopeSpacing)
        {
            if (carrierHz <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(carrierHz), "carrier frequency must be positive");
            }
            if (charge <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(charge), "charge must be positive");
            }
            if (calibrationCoefficient <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(calibrationCoefficient),
                    "calibrationCoefficient must be positive");
            }
            return isotopeSpacing * carrierHz * carrierHz * carrierHz
                   / (2 * calibrationCoefficient * charge);
        }

        /// <summary>Charge implied by an observed beat frequency. The inverse of the above.</summary>
        public static double ChargeFromBeatFrequency(double carrierHz, double beatHz,
            double calibrationCoefficient, double isotopeSpacing = IonSpecies.IsotopeSpacing)
        {
            if (beatHz <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beatHz), "beat frequency must be positive");
            }
            return BeatFrequencyForCharge(carrierHz, 1, calibrationCoefficient, isotopeSpacing) / beatHz;
        }

        /// <summary>
        /// Shortest transient, in seconds, that contains enough beat cycles for the charge
        /// to be measurable. Worth checking before running an acquisition.
        /// </summary>
        public static double RequiredTransientSeconds(double carrierHz, double charge,
            double calibrationCoefficient, double minimumBeats = 1.5,
            double isotopeSpacing = IonSpecies.IsotopeSpacing)
        {
            return minimumBeats / BeatFrequencyForCharge(carrierHz, charge, calibrationCoefficient, isotopeSpacing);
        }

        /// <summary>
        /// Measures the beat in an envelope and converts it to a charge.
        /// </summary>
        /// <param name="envelope">
        /// CWT modulus along time at the ridge: one value per sample of the transient.
        /// </param>
        /// <param name="samplingRate">Sampling rate of the transient, in Hz.</param>
        /// <param name="carrierHz">Frequency of the ridge, in Hz.</param>
        /// <param name="calibrationCoefficient">A, where m/z = A/f^2.</param>
        /// <param name="options">Search bounds and acceptance thresholds.</param>
        /// <returns>The measurement, or null when no acceptable beat was found.</returns>
        public static IsotopeBeatResult? Analyse(double[] envelope, int samplingRate, double carrierHz,
            double calibrationCoefficient, IsotopeBeatOptions? options = null)
        {
            options ??= new IsotopeBeatOptions();
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }
            if (envelope.Length < 16)
            {
                throw new ArgumentException("Envelope is too short to contain a beat", nameof(envelope));
            }
            if (samplingRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplingRate), "samplingRate must be positive");
            }
            if (options.MinCharge < 1 || options.MaxCharge < options.MinCharge)
            {
                throw new ArgumentException("MaxCharge must be at least MinCharge, and MinCharge at least 1",
                    nameof(options));
            }

            double[] residual = RemoveDecay(envelope);

            double modulationDepth = Math.Sqrt(residual.Sum(v => v * v) / residual.Length);
            if (modulationDepth < options.MinimumModulationDepth)
            {
                // Nothing is modulating the envelope; whatever decay it had has been removed
                // exactly, and there is no beat to report.
                return null;
            }

            double[] spectrum = MagnitudeSpectrum(residual);

            double windowSeconds = (double)envelope.Length / samplingRate;
            double binHz = samplingRate / (double)envelope.Length;

            // A higher charge gives a slower beat, so MaxCharge sets the low end of the
            // search and MinCharge the high end.
            double slowestBeat = BeatFrequencyForCharge(carrierHz, options.MaxCharge,
                calibrationCoefficient, options.IsotopeSpacing);
            double fastestBeat = BeatFrequencyForCharge(carrierHz, options.MinCharge,
                calibrationCoefficient, options.IsotopeSpacing);

            // Never look below the frequency at which the required number of cycles fits in
            // the window: anything slower is indistinguishable from residual trend.
            double lowestUsable = Math.Max(slowestBeat, options.MinimumObservedBeats / windowSeconds);
            int lowBin = Math.Max(1, (int)Math.Floor(lowestUsable / binHz));
            int highBin = Math.Min(spectrum.Length - 1, (int)Math.Ceiling(fastestBeat / binHz));
            if (highBin <= lowBin)
            {
                return null;
            }

            int peakBin = lowBin;
            for (int k = lowBin; k <= highBin; k++)
            {
                if (spectrum[k] > spectrum[peakBin])
                {
                    peakBin = k;
                }
            }

            double mean = 0;
            for (int k = lowBin; k <= highBin; k++)
            {
                mean += spectrum[k];
            }
            mean /= highBin - lowBin + 1;
            double prominence = mean > 0 ? spectrum[peakBin] / mean : 0;
            if (prominence < options.MinimumProminence)
            {
                return null;
            }

            // Parabolic interpolation for sub-bin precision, as used for ridge positions.
            double beatHz = InterpolatedBin(spectrum, peakBin) * binHz;
            if (beatHz <= 0)
            {
                return null;
            }

            double observedBeats = beatHz * windowSeconds;
            if (observedBeats < options.MinimumObservedBeats)
            {
                return null;
            }

            double charge = ChargeFromBeatFrequency(carrierHz, beatHz, calibrationCoefficient,
                options.IsotopeSpacing);
            if (charge < options.MinCharge - 0.5 || charge > options.MaxCharge + 0.5)
            {
                return null;
            }

            return new IsotopeBeatResult
            {
                CarrierFrequencyHz = carrierHz,
                BeatFrequencyHz = beatHz,
                FractionalCharge = charge,
                Prominence = prominence,
                ObservedBeats = observedBeats,
                ModulationDepth = modulationDepth
            };
        }

        /// <summary>
        /// Divides out the transient decay, leaving the modulation.
        ///
        /// Ion signals decay roughly exponentially, which puts a large slow component in the
        /// envelope spectrum that would otherwise swamp the beat. Fitting a straight line to
        /// the log envelope removes an exponential exactly, and any non-exponential residue
        /// is left behind for the search bounds to reject.
        /// </summary>
        private static double[] RemoveDecay(double[] envelope)
        {
            int n = envelope.Length;
            // Guard against zeros and negatives before taking logs.
            double floor = envelope.Where(v => v > 0).DefaultIfEmpty(1).Min() * 1e-6;

            double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = Math.Log(Math.Max(envelope[i], floor));
                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumXY += x * y;
            }
            double denominator = n * sumXX - sumX * sumX;
            double slope = denominator == 0 ? 0 : (n * sumXY - sumX * sumY) / denominator;
            double intercept = (sumY - slope * sumX) / n;

            double[] residual = new double[n];
            for (int i = 0; i < n; i++)
            {
                double fitted = Math.Exp(intercept + slope * i);
                residual[i] = fitted > 0 ? envelope[i] / fitted - 1 : 0;
            }
            return residual;
        }

        /// <summary>Magnitude spectrum of the positive-frequency half.</summary>
        private static double[] MagnitudeSpectrum(double[] signal)
        {
            int n = signal.Length;
            Complex[] buffer = new Complex[n];
            for (int i = 0; i < n; i++)
            {
                // A Hann window keeps a strong beat from smearing across neighbouring bins.
                double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
                buffer[i] = new Complex(signal[i] * window, 0);
            }
            Fourier.Forward(buffer, FourierOptions.NoScaling);

            int half = n / 2 + 1;
            double[] magnitude = new double[half];
            for (int k = 0; k < half; k++)
            {
                magnitude[k] = buffer[k].Magnitude;
            }
            return magnitude;
        }

        private static double InterpolatedBin(double[] spectrum, int bin)
        {
            if (bin <= 0 || bin >= spectrum.Length - 1)
            {
                return bin;
            }
            double left = spectrum[bin - 1];
            double centre = spectrum[bin];
            double right = spectrum[bin + 1];
            double denominator = left - 2 * centre + right;
            if (Math.Abs(denominator) < double.Epsilon)
            {
                return bin;
            }
            return bin + Math.Clamp(0.5 * (left - right) / denominator, -0.5, 0.5);
        }
    }
}
