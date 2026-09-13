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
                IsotopeBeat(),
                Homotetramer(),
                HeteroComplex(),
                OligomerOverlap(),
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

        /// <summary>
        /// Two peptides whose isotopes beat fast enough to watch.
        ///
        /// <para>
        /// Unresolved isotopes of one ion do not simply blur together: they are two
        /// frequencies a little apart, so their sum beats, and the beat rate
        /// <c>spacing * f / (2 * (m/z) * z)</c> depends on the charge. That makes charge
        /// readable from the time axis alone, which is the one grouping signal that needs
        /// no assumption about which peaks belong together.
        /// </para>
        ///
        /// <para>
        /// What it takes to see one is the point of the preset. The beat is only worth
        /// measuring when many cycles fit in the window, and the cycles available for a
        /// given number of samples work out at <c>spacing * N / (4 * z * (m/z))</c> at the
        /// Nyquist limit -- so low m/z and low charge are what make it cheap, and a long
        /// transient is what makes it possible at all. These two peptides beat at 326 Hz
        /// (2+ at m/z 500) and 131 Hz (3+ at m/z 700), giving 26 and 10 cycles across the
        /// interior of a 100 ms acquisition. Real proteins sit near m/z 1000 at charge 17+
        /// and beat at around 14 Hz, needing a comparable window for only a couple of
        /// cycles; that is the physics, not a limit of the method, and
        /// <see cref="Analysis.IsotopeBeatAnalysis.RequiredTransientSeconds"/> says so
        /// before you acquire rather than after.
        /// </para>
        ///
        /// <para>
        /// 100 ms at 2 MHz is 200k samples, more than the CLI's default truncation, so
        /// analysing this one wants <c>--max-samples 200000</c>. The scale grid is small
        /// enough that this stays cheap.
        /// </para>
        /// </summary>
        public static SimulationPreset IsotopeBeat()
        {
            OrbitrapSimulatorConfig config = BaseConfig(durationSeconds: 0.100);
            config.SamplingRate = 2_000_000;

            return new SimulationPreset(
                "isotope-beat",
                "Two peptides (2+ at m/z 500, 3+ at m/z 700) over 100 ms: long enough for " +
                "their unresolved isotopes to beat 26 and 10 times, so charge can be read " +
                "off each ridge's envelope without grouping the peaks first.",
                config,
                new[]
                {
                    new IonSpecies
                    {
                        // m/z 500.0 at 2+.
                        MonoisotopicMass = 997.985,
                        Charge = 2,
                        Abundance = 1.0,
                        // A+1 is 56% of A at this mass, so the beat is deep.
                        IsotopePeaks = 3,
                        InitialPhase = 0
                    },
                    new IonSpecies
                    {
                        // m/z 700.0 at 3+.
                        MonoisotopicMass = 2096.978,
                        Charge = 3,
                        Abundance = 0.8,
                        IsotopePeaks = 4,
                        InitialPhase = 0
                    }
                },
                // The two ridges are 18% apart in frequency, so c0 must resolve better than
                // that; the isotopes are 0.05% apart, so it must not come close to resolving
                // them. c0 = 20 sits at 11.8%, comfortably between.
                suggestedC0: 20f);
        }

        /// <summary>A streptavidin-like subunit; the classic native-MS homotetramer.</summary>
        private static ProteinSubunit StreptavidinSubunit => new("S", 13215.0);

        /// <summary>
        /// Haemoglobin's two chains, at their approximate holo masses. The haem is folded
        /// into the subunit rather than modelled as a ligand, because nothing downstream
        /// distinguishes a bound cofactor from more protein.
        /// </summary>
        private static ProteinSubunit HaemoglobinAlpha => new("a", 15126.4 + 616.5);

        private static ProteinSubunit HaemoglobinBeta => new("b", 15867.2 + 616.5);

        /// <summary>
        /// A homotetramer and its dissociation ladder, the shape of a native spray of a
        /// self-assembling protein.
        ///
        /// <para>
        /// Charge in native electrospray follows the Rayleigh limit, so an n-mer carries
        /// only sqrt(n) times the monomer's charge and lands at sqrt(n) times its m/z. All
        /// four oligomers therefore crowd into about 1200-3800 Th and less than an octave
        /// of frequency, interleaved. This is what a complex spectrum actually looks like,
        /// and none of the earlier presets produce it.
        /// </para>
        /// </summary>
        public static SimulationPreset Homotetramer()
        {
            IReadOnlyList<ProteinComplex> ladder =
                ComplexAssembly.Homooligomer(StreptavidinSubunit, 4);

            return new SimulationPreset(
                "homotetramer",
                "A 13.2 kDa subunit as monomer through tetramer under native ESI: four charge " +
                "envelopes marching to higher m/z as sqrt(n) and interleaving with each other.",
                BaseConfig(),
                ComplexAssembly.CreateSpecies(ladder).ToList(),
                // Adjacent charge states of the tetramer are ~2.3% apart in frequency at
                // the top of its envelope, so the wavelet has to beat that.
                suggestedC0: 140f);
        }

        /// <summary>
        /// A hetero-complex and every partial assembly of it: haemoglobin as alpha2beta2,
        /// down through alpha2beta, alphabeta, beta2 and the free chains.
        ///
        /// <para>
        /// The hard part is not the intact complex but the sub-complexes. alpha2, alphabeta
        /// and beta2 differ by only the 741 Da between the two chains -- 2.3% of their mass
        /// -- so their envelopes lie almost on top of one another, and telling them apart
        /// is telling apart three masses that differ by less than the width of a charge
        /// state.
        /// </para>
        /// </summary>
        public static SimulationPreset HeteroComplex()
        {
            IReadOnlyList<ProteinComplex> ladder = ComplexAssembly.Hetero(
                new[] { (HaemoglobinAlpha, 2), (HaemoglobinBeta, 2) });

            return new SimulationPreset(
                "hetero-complex",
                "Haemoglobin as alpha2beta2 (64.5 kDa) plus every sub-complex: alpha2, alphabeta " +
                "and beta2 differ by only 741 Da and their envelopes nearly coincide.",
                BaseConfig(),
                ComplexAssembly.CreateSpecies(ladder).ToList(),
                suggestedC0: 140f);
        }

        /// <summary>
        /// The degeneracy that makes oligomers genuinely ambiguous, isolated so it can be
        /// looked at.
        ///
        /// <para>
        /// An n-mer at charge n*z sits at m/z nM/(nz) = M/z, exactly where the monomer at
        /// charge z sits. Not nearly -- exactly, so no amount of resolution separates them.
        /// Here the monomer runs 6+ to 10+ and the dimer 12+ to 20+, so <em>every</em>
        /// monomer peak is also a dimer peak and the dimer's odd charge states fall between
        /// them. A charge-state comb sees one envelope at twice the spacing and can read it
        /// either way, which is why
        /// <see cref="Analysis.ChargeStateDeconvolution"/>'s contiguity prior -- built to
        /// reject 2M as a harmonic -- answers the wrong question here: 2M is real.
        /// </para>
        ///
        /// <para>
        /// What separates them is charge, not position. At one shared m/z the dimer carries
        /// twice the charge, so its isotope beat runs at half the rate.
        /// </para>
        /// </summary>
        public static SimulationPreset OligomerOverlap()
        {
            ProteinSubunit subunit = StreptavidinSubunit;
            var monomer = new ProteinComplex(new[] { (subunit, 1) }, 0.5);
            var dimer = new ProteinComplex(new[] { (subunit, 2) }, 0.5);

            // Charge ranges and envelope widths are forced rather than taken from the
            // Rayleigh model: the point of this preset is to put every monomer state on a
            // dimer state, and the model's own envelope is too narrow at the dimer's top
            // end to reach 20+. Everything else about the two ions is physical.
            var species = new List<IonSpecies>();
            species.AddRange(ComplexAssembly.CreateSpecies(new[] { monomer },
                new ChargeStateEnvelopeOptions
                {
                    Mode = IonizationMode.Native,
                    MinCharge = 6,
                    MaxCharge = 10,
                    CentroidCharge = 8,
                    Width = 2,
                    RelativeThreshold = 1e-6
                }));
            species.AddRange(ComplexAssembly.CreateSpecies(new[] { dimer },
                new ChargeStateEnvelopeOptions
                {
                    Mode = IonizationMode.Native,
                    MinCharge = 12,
                    MaxCharge = 20,
                    CentroidCharge = 16,
                    Width = 4,
                    RelativeThreshold = 1e-6
                }));

            return new SimulationPreset(
                "oligomer-overlap",
                "A monomer at 6+ to 10+ and its dimer at 12+ to 20+: every monomer peak sits " +
                "exactly on a dimer peak, so position alone cannot say which is there.",
                BaseConfig(),
                species,
                suggestedC0: 140f);
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
