using System;
using System.Collections.Generic;
using System.Linq;

namespace FCWTNET.Simulation
{
    /// <summary>A named simulation scenario: a configuration plus the species to simulate.</summary>
    public class SimulationPreset
    {
        public SimulationPreset(string name, string description,
            OrbitrapSimulatorConfig config, IReadOnlyList<IonSpecies> species)
        {
            Name = name;
            Description = description;
            Config = config;
            Species = species;
        }

        public string Name { get; }
        public string Description { get; }
        public OrbitrapSimulatorConfig Config { get; }
        public IReadOnlyList<IonSpecies> Species { get; }

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

        private static OrbitrapSimulatorConfig BaseConfig(double durationSeconds = 0.064, int seed = 20240101)
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
            config.DefaultDecayConstant = 0.015;
            return new SimulationPreset(
                "fast-decay",
                "Short decay constant (15 ms) against a 64 ms acquisition: the ridge visibly fades.",
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
        /// Two ions at the same m/z-ish frequency, one drifting and one not: the clearest
        /// demonstration of what a CWT sees that a whole-transient FFT averages away.
        /// </summary>
        public static SimulationPreset FrequencyDrift()
        {
            return new SimulationPreset(
                "frequency-drift",
                "One stationary ion and one drifting at 0.2%/s: the drifting ridge slopes across the scalogram.",
                BaseConfig(),
                new[]
                {
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass,
                        Charge = 2,
                        Abundance = 1.0,
                        IsotopePeaks = 1,
                        InitialPhase = 0
                    },
                    new IonSpecies
                    {
                        MonoisotopicMass = PeptideMass + 6,
                        Charge = 2,
                        Abundance = 0.8,
                        IsotopePeaks = 1,
                        InitialPhase = 0,
                        FrequencyDriftPerSecond = 2e-3
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
