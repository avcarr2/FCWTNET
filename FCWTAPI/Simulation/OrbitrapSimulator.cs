using System;
using System.Collections.Generic;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace FCWTNET.Simulation
{
    /// <summary>Shape of the transient decay envelope.</summary>
    public enum DecayModel
    {
        /// <summary>No decay; every component keeps its initial amplitude.</summary>
        None,

        /// <summary>exp(-t/tau). The usual first-order description of ion dephasing.</summary>
        Exponential,

        /// <summary>exp(-(t/tau)^2 / 2). A softer initial roll-off, sharper tail.</summary>
        Gaussian
    }

    /// <summary>Everything that controls a simulated acquisition.</summary>
    public class OrbitrapSimulatorConfig
    {
        /// <summary>
        /// Calibration coefficient A in m/z = A / f^2, so f = sqrt(A / (m/z)).
        /// The bundled sample transients use A = 2.1175e14 at 4 MHz.
        /// </summary>
        public double CalibrationCoefficient { get; set; } = 2.11751487582899e14;

        /// <summary>Sampling rate in Hz.</summary>
        public int SamplingRate { get; set; } = 4_000_000;

        /// <summary>Transient length in seconds.</summary>
        public double DurationSeconds { get; set; } = 0.256;

        /// <summary>Decay envelope shape.</summary>
        public DecayModel Decay { get; set; } = DecayModel.Exponential;

        /// <summary>Decay constant in seconds, used for species that do not set their own.</summary>
        public double DefaultDecayConstant { get; set; } = 0.5;

        /// <summary>
        /// Signal-to-noise ratio as an amplitude ratio against the RMS of the noiseless
        /// transient. Null or non-positive adds no noise.
        /// </summary>
        public double? SignalToNoise { get; set; } = 100;

        /// <summary>
        /// Share of the noise power that is 1/f (pink) rather than white, in [0, 1].
        /// Real Orbitrap preamplifiers contribute both.
        /// </summary>
        public double PinkNoiseFraction { get; set; } = 0.3;

        /// <summary>
        /// ADC resolution in bits. Null skips quantisation. Real Orbitrap digitisers are
        /// typically 16-bit or better.
        /// </summary>
        public int? AdcBits { get; set; }

        /// <summary>
        /// Full-scale amplitude of the ADC. Samples beyond +/- this value clip. Null
        /// auto-scales to the largest absolute sample, so nothing clips.
        /// </summary>
        public double? AdcFullScale { get; set; }

        /// <summary>Seed for every random draw, so a configuration reproduces exactly.</summary>
        public int Seed { get; set; } = 20240101;
    }

    /// <summary>
    /// Generates Orbitrap image-current transients from a list of ion species.
    ///
    /// In an Orbitrap, ions oscillate axially at a frequency set only by their m/z:
    ///
    ///     f = sqrt(A / (m/z))
    ///
    /// which is the inverse of the m/z = A/f^2 calibration the analysis side already uses
    /// (<see cref="CWTFrequencies.CalculateMZValues"/>), so simulated data and the
    /// analysis axes share one calibration constant.
    ///
    /// Each isotopologue contributes a decaying sinusoid, and the sum is what the
    /// detector would see:
    ///
    ///     s(t) = sum_i  a_i * env(t) * cos(phi_i(t) + phi_i0)   + noise
    ///
    /// Frequency drift is integrated properly: for a linear drift f(t) = f0 (1 + b t) the
    /// phase is phi(t) = 2*pi*f0*(t + b*t^2/2), not 2*pi*f(t)*t, which would double the
    /// chirp rate.
    /// </summary>
    public class OrbitrapSimulator
    {
        private readonly OrbitrapSimulatorConfig _config;

        public OrbitrapSimulator(OrbitrapSimulatorConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (_config.SamplingRate <= 0)
            {
                throw new ArgumentException("SamplingRate must be positive", nameof(config));
            }
            if (_config.DurationSeconds <= 0)
            {
                throw new ArgumentException("DurationSeconds must be positive", nameof(config));
            }
            if (_config.CalibrationCoefficient <= 0)
            {
                throw new ArgumentException("CalibrationCoefficient must be positive", nameof(config));
            }
            if (_config.PinkNoiseFraction < 0 || _config.PinkNoiseFraction > 1)
            {
                throw new ArgumentException("PinkNoiseFraction must lie in [0, 1]", nameof(config));
            }
        }

        /// <summary>Frequency in Hz of an ion at the given m/z, under this calibration.</summary>
        public double FrequencyForMz(double mz)
        {
            if (mz <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(mz), "m/z must be positive");
            }
            return Math.Sqrt(_config.CalibrationCoefficient / mz);
        }

        /// <summary>m/z of an ion oscillating at the given frequency, under this calibration.</summary>
        public double MzForFrequency(double frequency)
        {
            if (frequency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frequency), "frequency must be positive");
            }
            return _config.CalibrationCoefficient / (frequency * frequency);
        }

        /// <summary>Simulate a transient for the given species.</summary>
        public SimulationResult Simulate(IEnumerable<IonSpecies> species)
        {
            if (species == null)
            {
                throw new ArgumentNullException(nameof(species));
            }

            int sampleCount = (int)Math.Round(_config.DurationSeconds * _config.SamplingRate);
            if (sampleCount <= 0)
            {
                throw new ArgumentException("Configuration yields no samples", nameof(species));
            }

            var random = new Random(_config.Seed);
            var truth = BuildGroundTruth(species, random);

            double[] samples = new double[sampleCount];
            double timeStep = 1.0 / _config.SamplingRate;

            foreach (IsotopologueTruth component in truth)
            {
                double omega0 = 2 * Math.PI * component.Frequency;
                double drift = component.FrequencyDriftPerSecond;
                for (int i = 0; i < sampleCount; i++)
                {
                    double t = i * timeStep;
                    // Phase is the integral of the instantaneous frequency.
                    double phase = omega0 * (t + 0.5 * drift * t * t) + component.InitialPhase;
                    samples[i] += component.Amplitude * Envelope(t, component.DecayConstant) * Math.Cos(phase);
                }
            }

            AddNoise(samples, random);
            ApplyAdc(samples);

            return new SimulationResult(samples, truth, _config.SamplingRate, _config.CalibrationCoefficient);
        }

        private List<IsotopologueTruth> BuildGroundTruth(IEnumerable<IonSpecies> species, Random random)
        {
            var truth = new List<IsotopologueTruth>();
            double nyquist = _config.SamplingRate / 2.0;

            foreach (IonSpecies ion in species)
            {
                if (ion.IsotopePeaks < 1)
                {
                    throw new ArgumentException(
                        $"IsotopePeaks must be at least 1 (m/z {ion.MonoisotopicMz:F4})", nameof(species));
                }

                double[] intensities = IsotopeEnvelope.RelativeIntensities(ion.MonoisotopicMass, ion.IsotopePeaks);
                double tau = ion.DecayConstant ?? _config.DefaultDecayConstant;
                if (tau <= 0 && _config.Decay != DecayModel.None)
                {
                    throw new ArgumentException("Decay constant must be positive", nameof(species));
                }

                for (int k = 0; k < ion.IsotopePeaks; k++)
                {
                    double mz = ion.MzForIsotope(k);
                    double frequency = FrequencyForMz(mz);
                    if (frequency >= nyquist)
                    {
                        throw new ArgumentException(
                            $"m/z {mz:F4} oscillates at {frequency:F0} Hz, at or above the Nyquist " +
                            $"frequency of {nyquist:F0} Hz. Raise SamplingRate or simulate a heavier ion.",
                            nameof(species));
                    }

                    truth.Add(new IsotopologueTruth
                    {
                        Mz = mz,
                        Frequency = frequency,
                        Amplitude = ion.Abundance * intensities[k],
                        // A fresh draw per isotopologue: in a real acquisition ions are not
                        // excited perfectly in phase.
                        InitialPhase = ion.InitialPhase ?? random.NextDouble() * 2 * Math.PI,
                        DecayConstant = tau,
                        FrequencyDriftPerSecond = ion.FrequencyDriftPerSecond,
                        Charge = ion.Charge,
                        IsotopeIndex = k
                    });
                }
            }

            if (truth.Count == 0)
            {
                throw new ArgumentException("At least one species must be supplied", nameof(species));
            }
            return truth;
        }

        private double Envelope(double t, double tau)
        {
            switch (_config.Decay)
            {
                case DecayModel.None:
                    return 1.0;
                case DecayModel.Gaussian:
                    double ratio = t / tau;
                    return Math.Exp(-0.5 * ratio * ratio);
                default:
                    return Math.Exp(-t / tau);
            }
        }

        private void AddNoise(double[] samples, Random random)
        {
            if (!_config.SignalToNoise.HasValue || _config.SignalToNoise.Value <= 0)
            {
                return;
            }

            double signalRms = Rms(samples);
            if (signalRms <= 0)
            {
                return;
            }
            double noiseRms = signalRms / _config.SignalToNoise.Value;

            // Split the requested noise power between white and pink components.
            double pinkPower = _config.PinkNoiseFraction;
            double whitePower = 1 - pinkPower;

            double[] white = GaussianNoise(samples.Length, random);
            Normalise(white, noiseRms * Math.Sqrt(whitePower));

            double[] pink = Array.Empty<double>();
            if (pinkPower > 0)
            {
                pink = PinkNoise(samples.Length, random);
                Normalise(pink, noiseRms * Math.Sqrt(pinkPower));
            }

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] += white[i];
                if (pink.Length > 0)
                {
                    samples[i] += pink[i];
                }
            }
        }

        /// <summary>Standard normal samples via Box-Muller, so determinism depends only on Random.</summary>
        private static double[] GaussianNoise(int count, Random random)
        {
            double[] values = new double[count];
            for (int i = 0; i < count; i += 2)
            {
                // NextDouble() can return 0, which Log would send to -infinity.
                double u1 = 1.0 - random.NextDouble();
                double u2 = random.NextDouble();
                double magnitude = Math.Sqrt(-2.0 * Math.Log(u1));
                values[i] = magnitude * Math.Cos(2 * Math.PI * u2);
                if (i + 1 < count)
                {
                    values[i + 1] = magnitude * Math.Sin(2 * Math.PI * u2);
                }
            }
            return values;
        }

        /// <summary>
        /// 1/f noise, made by shaping white noise in the frequency domain so each bin is
        /// scaled by 1/sqrt(f) (power proportional to 1/f).
        /// </summary>
        private static double[] PinkNoise(int count, Random random)
        {
            double[] white = GaussianNoise(count, random);
            Complex[] spectrum = new Complex[count];
            for (int i = 0; i < count; i++)
            {
                spectrum[i] = new Complex(white[i], 0);
            }
            Fourier.Forward(spectrum, FourierOptions.NoScaling);

            int half = count / 2;
            for (int k = 0; k < count; k++)
            {
                // Bin index measured from DC, folding the negative-frequency half.
                int binFromDc = k <= half ? k : count - k;
                // DC is left at bin 1's weighting to avoid dividing by zero and to keep
                // the series zero-mean after normalisation.
                double scale = 1.0 / Math.Sqrt(Math.Max(1, binFromDc));
                spectrum[k] *= scale;
            }
            spectrum[0] = Complex.Zero;

            Fourier.Inverse(spectrum, FourierOptions.NoScaling);
            double[] pink = new double[count];
            for (int i = 0; i < count; i++)
            {
                pink[i] = spectrum[i].Real / count;
            }
            return pink;
        }

        private static double Rms(double[] values)
        {
            double sum = 0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i] * values[i];
            }
            return Math.Sqrt(sum / values.Length);
        }

        private static void Normalise(double[] values, double targetRms)
        {
            double current = Rms(values);
            if (current <= 0)
            {
                return;
            }
            double factor = targetRms / current;
            for (int i = 0; i < values.Length; i++)
            {
                values[i] *= factor;
            }
        }

        private void ApplyAdc(double[] samples)
        {
            if (!_config.AdcBits.HasValue)
            {
                return;
            }
            int bits = _config.AdcBits.Value;
            if (bits < 1 || bits > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(_config.AdcBits), "AdcBits must lie in [1, 32]");
            }

            double fullScale = _config.AdcFullScale ?? MaxAbsolute(samples);
            if (fullScale <= 0)
            {
                return;
            }

            // A signed converter of n bits has 2^(n-1) levels per side.
            double levels = Math.Pow(2, bits - 1);
            double step = fullScale / levels;

            for (int i = 0; i < samples.Length; i++)
            {
                double clipped = Math.Clamp(samples[i], -fullScale, fullScale);
                double code = Math.Round(clipped / step, MidpointRounding.AwayFromZero);
                // Clamp the code too: a sample exactly at full scale rounds to `levels`,
                // which is one past the largest representable code.
                code = Math.Clamp(code, -levels, levels - 1);
                samples[i] = code * step;
            }
        }

        private static double MaxAbsolute(double[] values)
        {
            double max = 0;
            for (int i = 0; i < values.Length; i++)
            {
                double magnitude = Math.Abs(values[i]);
                if (magnitude > max)
                {
                    max = magnitude;
                }
            }
            return max;
        }
    }
}
