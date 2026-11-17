using System;
using System.Collections.Generic;
using System.Linq;

namespace FCWTNET.Simulation
{
    /// <summary>
    /// How the protein was ionised, which is what sets the charge state distribution.
    /// </summary>
    public enum IonizationMode
    {
        /// <summary>
        /// Native (non-denaturing) electrospray. The protein stays folded, so it can only
        /// carry the charge its surface supports: a low centroid and a narrow envelope,
        /// typically three or four charge states.
        /// </summary>
        Native,

        /// <summary>
        /// Denaturing electrospray. The unfolded chain exposes far more basic sites, so
        /// the centroid moves to much higher charge and the envelope is broad -- a dozen
        /// or more charge states.
        /// </summary>
        Denatured
    }

    /// <summary>One charge state and its share of the total ion current.</summary>
    public class ChargeStateAbundance
    {
        public int Charge { get; set; }

        /// <summary>Relative abundance; abundances over an envelope sum to 1.</summary>
        public double Abundance { get; set; }

        /// <summary>m/z of this charge state's monoisotopic peak.</summary>
        public double Mz { get; set; }
    }

    /// <summary>Knobs for building a charge state envelope.</summary>
    public class ChargeStateEnvelopeOptions
    {
        public IonizationMode Mode { get; set; } = IonizationMode.Denatured;

        /// <summary>
        /// Centroid of the distribution. Null uses the model for <see cref="Mode"/>.
        /// </summary>
        public double? CentroidCharge { get; set; }

        /// <summary>
        /// Standard deviation of the distribution, in charge units. Null uses the model
        /// for <see cref="Mode"/>.
        /// </summary>
        public double? Width { get; set; }

        /// <summary>Lowest charge state to consider. Null lets the threshold decide.</summary>
        public int? MinCharge { get; set; }

        /// <summary>Highest charge state to consider. Null lets the threshold decide.</summary>
        public int? MaxCharge { get; set; }

        /// <summary>
        /// Drop charge states whose abundance is below this fraction of the most abundant
        /// one. Raising it narrows the envelope to its most intense members.
        /// </summary>
        public double RelativeThreshold { get; set; } = 0.05;

        /// <summary>Hard cap on how many charge states are produced.</summary>
        public int MaxChargeStates { get; set; } = 40;

        public ChargeStateEnvelopeOptions Clone() => new()
        {
            Mode = Mode,
            CentroidCharge = CentroidCharge,
            Width = Width,
            MinCharge = MinCharge,
            MaxCharge = MaxCharge,
            RelativeThreshold = RelativeThreshold,
            MaxChargeStates = MaxChargeStates
        };
    }

    /// <summary>
    /// Builds charge state envelopes for a protein.
    ///
    /// Two regimes, because they are what an Orbitrap user actually chooses between:
    ///
    /// <para><b>Native.</b> A folded protein cannot hold more charge than its surface
    /// supports. The Rayleigh limit for a spherical droplet of protein density gives
    /// z ~ 0.0778 * sqrt(M) (Fernandez de la Mora, 2000), which for myoglobin's ~16.9 kDa
    /// predicts a centroid near 10+ -- in line with the 8-10+ actually observed. The
    /// envelope is narrow.</para>
    ///
    /// <para><b>Denatured.</b> An unfolded chain exposes far more basic sites and charges
    /// well past the Rayleigh limit. Empirically the envelope centres near m/z 1000
    /// regardless of protein, so the centroid goes roughly as M/1000: myoglobin lands
    /// near 17+, again matching the observed 9-26+ spread. The envelope is much broader.</para>
    ///
    /// Both the centroid and the width can be overridden, so the model is a starting
    /// point for experimenting rather than a fixed answer.
    /// </summary>
    public static class ChargeStateEnvelope
    {
        /// <summary>
        /// Rayleigh-limit charge for a globular protein of the given mass:
        /// z = 0.0778 * sqrt(M).
        /// </summary>
        public static double RayleighCharge(double monoisotopicMass)
        {
            if (monoisotopicMass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monoisotopicMass), "mass must be positive");
            }
            return 0.0778 * Math.Sqrt(monoisotopicMass);
        }

        /// <summary>Model centroid charge for the given mass and ionisation mode.</summary>
        public static double CentroidCharge(double monoisotopicMass, IonizationMode mode)
        {
            if (monoisotopicMass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monoisotopicMass), "mass must be positive");
            }
            return mode switch
            {
                // Folded: capped by the droplet's Rayleigh limit.
                IonizationMode.Native => RayleighCharge(monoisotopicMass),
                // Unfolded: charges until it lands near m/z 1000.
                _ => monoisotopicMass / 1000.0
            };
        }

        /// <summary>
        /// Model envelope width (standard deviation in charge units). Expressed as a
        /// fraction of the centroid, since a heavier protein spreads over more charge
        /// states in absolute terms.
        /// </summary>
        public static double EnvelopeWidth(double monoisotopicMass, IonizationMode mode)
        {
            double centroid = CentroidCharge(monoisotopicMass, mode);
            double fraction = mode == IonizationMode.Native ? 0.10 : 0.22;
            // Never narrower than half a charge, or the envelope collapses to one state
            // for small proteins.
            return Math.Max(0.5, fraction * centroid);
        }

        /// <summary>
        /// The charge states and relative abundances for a protein, as a Gaussian in
        /// charge truncated at the threshold. Abundances sum to 1.
        /// </summary>
        public static IReadOnlyList<ChargeStateAbundance> Generate(double monoisotopicMass,
            ChargeStateEnvelopeOptions? options = null)
        {
            options ??= new ChargeStateEnvelopeOptions();
            if (monoisotopicMass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monoisotopicMass), "mass must be positive");
            }
            if (options.RelativeThreshold <= 0 || options.RelativeThreshold >= 1)
            {
                throw new ArgumentException("RelativeThreshold must lie in (0, 1)", nameof(options));
            }
            if (options.MaxChargeStates < 1)
            {
                throw new ArgumentException("MaxChargeStates must be at least 1", nameof(options));
            }

            double centroid = options.CentroidCharge ?? CentroidCharge(monoisotopicMass, options.Mode);
            double width = options.Width ?? EnvelopeWidth(monoisotopicMass, options.Mode);
            if (centroid <= 0)
            {
                throw new ArgumentException("CentroidCharge must be positive", nameof(options));
            }
            if (width <= 0)
            {
                throw new ArgumentException("Width must be positive", nameof(options));
            }

            // Four standard deviations either side covers the envelope; the threshold
            // then trims it. Charge is at least 1.
            int lower = options.MinCharge ?? Math.Max(1, (int)Math.Floor(centroid - 4 * width));
            int upper = options.MaxCharge ?? (int)Math.Ceiling(centroid + 4 * width);
            lower = Math.Max(1, lower);
            if (upper < lower)
            {
                throw new ArgumentException("MaxCharge must not be below MinCharge", nameof(options));
            }

            var candidates = new List<ChargeStateAbundance>();
            for (int z = lower; z <= upper; z++)
            {
                double deviation = (z - centroid) / width;
                double weight = Math.Exp(-0.5 * deviation * deviation);
                candidates.Add(new ChargeStateAbundance
                {
                    Charge = z,
                    Abundance = weight,
                    Mz = (monoisotopicMass + z * IonSpecies.ProtonMass) / z
                });
            }

            double peak = candidates.Max(c => c.Abundance);
            if (peak <= 0)
            {
                // The requested range misses the envelope entirely; keep the nearest state
                // rather than returning nothing.
                int nearest = (int)Math.Round(Math.Clamp(centroid, lower, upper));
                candidates = new List<ChargeStateAbundance>
                {
                    new()
                    {
                        Charge = nearest,
                        Abundance = 1,
                        Mz = (monoisotopicMass + nearest * IonSpecies.ProtonMass) / nearest
                    }
                };
                peak = 1;
            }

            List<ChargeStateAbundance> kept = candidates
                .Where(c => c.Abundance >= peak * options.RelativeThreshold)
                .OrderByDescending(c => c.Abundance)
                .Take(options.MaxChargeStates)
                .OrderBy(c => c.Charge)
                .ToList();

            double total = kept.Sum(c => c.Abundance);
            foreach (ChargeStateAbundance state in kept)
            {
                state.Abundance /= total;
            }
            return kept;
        }

        /// <summary>
        /// Expands a protein into one <see cref="IonSpecies"/> per charge state, ready to
        /// hand to <see cref="OrbitrapSimulator"/>.
        /// </summary>
        /// <param name="monoisotopicMass">Neutral monoisotopic mass in Da.</param>
        /// <param name="options">Envelope shape; null uses the denatured model.</param>
        /// <param name="isotopePeaks">
        /// Isotope peaks per charge state. Null uses a mass-appropriate count, which for a
        /// 17 kDa protein is large -- pass a smaller number to keep simulations quick.
        /// </param>
        /// <param name="totalAbundance">Total abundance shared across the envelope.</param>
        /// <param name="decayConstant">Optional per-species decay constant in seconds.</param>
        /// <param name="frequencyDriftPerSecond">Optional fractional drift per second.</param>
        public static IReadOnlyList<IonSpecies> CreateSpecies(double monoisotopicMass,
            ChargeStateEnvelopeOptions? options = null, int? isotopePeaks = null,
            double totalAbundance = 1.0, double? decayConstant = null,
            double frequencyDriftPerSecond = 0)
        {
            IReadOnlyList<ChargeStateAbundance> envelope = Generate(monoisotopicMass, options);
            int peaks = isotopePeaks ?? IsotopeEnvelope.SuggestedPeakCount(monoisotopicMass);

            return envelope.Select(state => new IonSpecies
            {
                MonoisotopicMass = monoisotopicMass,
                Charge = state.Charge,
                Abundance = totalAbundance * state.Abundance,
                IsotopePeaks = peaks,
                DecayConstant = decayConstant,
                FrequencyDriftPerSecond = frequencyDriftPerSecond
            }).ToList();
        }
    }
}
