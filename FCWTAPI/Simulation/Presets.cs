using System;
using System.Collections.Generic;
using System.Linq;

namespace FCWTNET.Simulation
{
    /// <summary>
    /// A protein plus the envelope settings that turn it into charge states. Presets that
    /// carry one can have their envelope regenerated -- switching between native and
    /// denatured, or moving the centroid -- without rebuilding the preset.
    /// </summary>
    public class ChargeEnvelopeTemplate
    {
        public ChargeEnvelopeTemplate(double monoisotopicMass, ChargeStateEnvelopeOptions options,
            int? isotopePeaks = null)
        {
            MonoisotopicMass = monoisotopicMass;
            Options = options;
            IsotopePeaks = isotopePeaks;
        }

        public double MonoisotopicMass { get; }
        public ChargeStateEnvelopeOptions Options { get; }
        public int? IsotopePeaks { get; }

        /// <summary>Expand into one species per charge state.</summary>
        public IReadOnlyList<IonSpecies> CreateSpecies(ChargeStateEnvelopeOptions? overrideOptions = null)
        {
            return ChargeStateEnvelope.CreateSpecies(
                MonoisotopicMass, overrideOptions ?? Options, IsotopePeaks);
        }

        /// <summary>The charge states and abundances this template produces.</summary>
        public IReadOnlyList<ChargeStateAbundance> Envelope(ChargeStateEnvelopeOptions? overrideOptions = null)
        {
            return ChargeStateEnvelope.Generate(MonoisotopicMass, overrideOptions ?? Options);
        }
    }

    /// <summary>A named simulation scenario: a configuration plus the species to simulate.</summary>
    public class SimulationPreset
    {
        public SimulationPreset(string name, string description,
            OrbitrapSimulatorConfig config, IReadOnlyList<IonSpecies> species,
            ChargeEnvelopeTemplate? chargeEnvelope = null, float suggestedC0 = 6f)
        {
            Name = name;
            Description = description;
            Config = config;
            Species = species;
            ChargeEnvelope = chargeEnvelope;
            SuggestedC0 = suggestedC0;
        }

        /// <summary>
        /// Morlet c0 that suits this scenario. A Morlet resolves components about
        /// 2*sqrt(2*ln2)/c0 apart in relative frequency, so a single isolated peak is fine
        /// at c0 = 6 (~39%) while a charge state envelope whose neighbours differ by ~3%
        /// needs roughly 80. See <see cref="Transform.CwtScales.RelativeFrequencyResolution"/>.
        /// </summary>
        public float SuggestedC0 { get; }

        public string Name { get; }
        public string Description { get; }
        public OrbitrapSimulatorConfig Config { get; }
        public IReadOnlyList<IonSpecies> Species { get; }

        /// <summary>
        /// Set when this preset's species come from a charge state envelope, so callers
        /// can regenerate it with different ionisation settings. Null for presets whose
        /// species are specified directly.
        /// </summary>
        public ChargeEnvelopeTemplate? ChargeEnvelope { get; }

        public SimulationResult Run() => new OrbitrapSimulator(Config).Simulate(Species);
    }

    /// <summary>
    /// Ready-made scenarios covering the cases worth looking at with a CWT: a clean
    /// single peak, an isotope cluster, overlapping species, fast decay, and frequency
    /// drift.
    ///
    /// Defaults follow the bundled sample transients (A = 2.1175e14, 4 MHz), but with
    /// shorter acquisitions: a full second at 4 MHz is 4 million samples, and a CWT of
    /// that across many voices is a very large matrix.
    /// </summary>
    public static class Presets
    {
        /// <summary>Calibration of the bundled SampleTransient_Myo_z13.csv.</summary>
        public const double SampleCalibrationCoefficient = 2.11751487582899e14;

        public const int SampleSamplingRate = 4_000_000;

        /// <summary>Approximate monoisotopic mass of apo-myoglobin, in Da.</summary>
        private const double MyoglobinMass = 16940.0;

        /// <summary>A mid-size peptide, around 2 kDa.</summary>
        private const double PeptideMass = 2093.1;

        private static OrbitrapSimulatorConfig BaseConfig(double durationSeconds = 0.016, int seed = 20240101)
        {
            return new OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = SampleCalibrationCoefficient,
                SamplingRate = SampleSamplingRate,
                DurationSeconds = durationSeconds,
                Decay = DecayModel.Exponential,
                DefaultDecayConstant = 0.5,
                SignalToNoise = 100,
                PinkNoiseFraction = 0.3,
                Seed = seed
            };
        }

        /// <summary>Every preset, keyed by name.</summary>
        public static IReadOnlyDictionary<string, SimulationPreset> All { get; } =
            new[]
            {
                SinglePeak(),
                PeptideIsotopes(),
                MyoglobinChargeState(),
                MyoglobinDenatured(),
                MyoglobinNative(),
                Mixture(),
                FastDecay(),
                FrequencyDrift(),
                LowSignalToNoise()
            }.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        public static SimulationPreset Get(string name)
        {
            if (All.TryGetValue(name, out SimulationPreset? preset))
            {
                return preset;
            }
            throw new ArgumentException(
                $"Unknown preset '{name}'. Available: {string.Join(", ", All.Keys)}", nameof(name));
        }

        /// <summary>One monoisotopic peak, no decay, no noise: the cleanest possible check.</summary>
        public static SimulationPreset SinglePeak()
        {
            OrbitrapSimulatorConfig config = BaseConfig();
            config.Decay = DecayModel.None;
            config.SignalToNoise = null;
            return new SimulationPreset(
                "single-peak",
                "One ion at m/z 1046.5, no decay and no noise. The ridge should sit exactly on the ground-truth m/z.",
                config,
                new[]
                {
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass,
                        Charge = 2,
                        Abundance = 1.0,
                        IsotopePeaks = 1,
                        InitialPhase = 0
                    }
                });
        }

        /// <summary>A 2+ peptide with its full isotope cluster.</summary>
        public static SimulationPreset PeptideIsotopes()
        {
            return new SimulationPreset(
                "peptide-isotopes",
                "A 2+ peptide with an averagine isotope cluster; adjacent peaks are 0.5 Th apart.",
                BaseConfig(),
                new[]
                {
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass,
                        Charge = 2,
                        Abundance = 1.0,
                        IsotopePeaks = IsotopeEnvelope.SuggestedPeakCount(PeptideMass)
                    }
                });
        }

        /// <summary>Myoglobin at 13+, matching the bundled sample transient's setup.</summary>
        public static SimulationPreset MyoglobinChargeState()
        {
            return new SimulationPreset(
                "myoglobin-13plus",
                "Apo-myoglobin at 13+ (m/z ~1304), the charge state in the bundled sample transient.",
                BaseConfig(),
                new[]
                {
                    new IonSpecies
                    {
                        MonoisotopicMass = MyoglobinMass,
                        Charge = 13,
                        Abundance = 1.0,
                        // A 17 kDa protein has a wide cluster; cap it to keep the
                        // simulation quick while still overlapping neighbours.
                        IsotopePeaks = 12
                    }
                });
        }

        /// <summary>
        /// Myoglobin from denaturing electrospray: a broad envelope running roughly 8+ to
        /// 26+, centred near 17+. Each charge state lands at its own frequency, so the
        /// scalogram shows a ladder of ridges spanning about an octave.
        /// </summary>
        public static SimulationPreset MyoglobinDenatured()
        {
            var template = new ChargeEnvelopeTemplate(
                MyoglobinMass,
                new ChargeStateEnvelopeOptions { Mode = IonizationMode.Denatured },
                // A 17 kDa protein has a wide isotope cluster; the full count is used so
                // each charge state's centroid m/z is not skewed by truncating the tail.
                isotopePeaks: IsotopeEnvelope.SuggestedPeakCount(MyoglobinMass));

            return new SimulationPreset(
                "myoglobin-denatured",
                "Apo-myoglobin under denaturing ESI: a broad charge envelope (~8+ to 26+, centred near 17+), one ridge per charge state.",
                BaseConfig(),
                template.CreateSpecies(),
                template,
                // Adjacent charge states near 17+ differ by ~3% in frequency.
                suggestedC0: 80f);
        }

        /// <summary>
        /// Myoglobin from native electrospray: a narrow envelope near the Rayleigh limit,
        /// roughly 8+ to 12+. Far fewer ridges, packed much more closely in frequency.
        /// </summary>
        public static SimulationPreset MyoglobinNative()
        {
            var template = new ChargeEnvelopeTemplate(
                MyoglobinMass,
                new ChargeStateEnvelopeOptions { Mode = IonizationMode.Native },
                isotopePeaks: IsotopeEnvelope.SuggestedPeakCount(MyoglobinMass));

            return new SimulationPreset(
                "myoglobin-native",
                "Apo-myoglobin under native ESI: a narrow charge envelope near the Rayleigh limit (~8+ to 12+), tightly spaced ridges.",
                BaseConfig(),
                template.CreateSpecies(),
                template,
                suggestedC0: 80f);
        }

        /// <summary>Two species whose isotope clusters interleave.</summary>
        public static SimulationPreset Mixture()
        {
            return new SimulationPreset(
                "mixture",
                "Two peptides of similar m/z at different abundances, with interleaving isotope clusters.",
                BaseConfig(),
                new[]
                {
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass,
                        Charge = 2,
                        Abundance = 1.0,
                        IsotopePeaks = 5
                    },
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass + 1.6,
                        Charge = 2,
                        Abundance = 0.35,
                        IsotopePeaks = 5
                    }
                });
        }

        /// <summary>
        /// Decay fast enough to matter over the acquisition, so the scalogram shows the
        /// ridge fading with time. An FFT over the whole transient cannot show this.
        /// </summary>
        public static SimulationPreset FastDecay()
        {
            OrbitrapSimulatorConfig config = BaseConfig();
            config.DefaultDecayConstant = 0.004;
            return new SimulationPreset(
                "fast-decay",
                "Decay constant of 4 ms against a 16 ms acquisition: the ridge fades to 2% by the end.",
                config,
                new[]
                {
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass,
                        Charge = 2,
                        Abundance = 1.0,
                        IsotopePeaks = 3
                    }
                });
        }

        /// <summary>
        /// A single drifting ion: the clearest demonstration of what a CWT sees that an
        /// FFT over the whole transient averages away.
        ///
        /// Uses a slower acquisition (200 kHz over 250 ms) rather than the 4 MHz default.
        /// Drift only becomes visible once the frequency moves by more than the voice
        /// spacing, which needs a long observation; at 4 MHz a transient that long is
        /// millions of samples. Two ions close enough to drift past each other would beat
        /// rather than resolve, which obscures the very effect being shown.
        /// </summary>
        public static SimulationPreset FrequencyDrift()
        {
            var config = new OrbitrapSimulatorConfig
            {
                // Puts m/z ~1047 near 20 kHz, well inside a 100 kHz Nyquist limit.
                CalibrationCoefficient = 4.188e11,
                SamplingRate = 200_000,
                DurationSeconds = 0.25,
                Decay = DecayModel.None,
                SignalToNoise = 200,
                PinkNoiseFraction = 0.3,
                Seed = 20240101
            };
            return new SimulationPreset(
                "frequency-drift",
                "One ion drifting at 20%/s over 250 ms: the ridge slopes upward across the scalogram.",
                config,
                new[]
                {
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass,
                        Charge = 2,
                        Abundance = 1.0,
                        IsotopePeaks = 1,
                        InitialPhase = 0,
                        FrequencyDriftPerSecond = 0.2
                    }
                });
        }

        /// <summary>Noise-dominated acquisition, digitised on a 12-bit converter.</summary>
        public static SimulationPreset LowSignalToNoise()
        {
            OrbitrapSimulatorConfig config = BaseConfig();
            config.SignalToNoise = 3;
            config.AdcBits = 12;
            return new SimulationPreset(
                "low-snr",
                "SNR of 3 with 12-bit digitisation: tests whether the ridge survives noise and quantisation.",
                config,
                new[]
                {
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass,
                        Charge = 2,
                        Abundance = 1.0,
                        IsotopePeaks = 3
                    }
                });
        }
    }
}
