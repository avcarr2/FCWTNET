using System;

namespace FCWTNET.Simulation
{
    /// <summary>
    /// One ion population in a simulated Orbitrap transient: a molecule at a given charge
    /// state, together with the transient behaviour of its image current.
    /// </summary>
    public class IonSpecies
    {
        /// <summary>Mass of the proton in Da, used to build m/z from a neutral mass.</summary>
        public const double ProtonMass = 1.00727646688;

        /// <summary>Spacing between adjacent isotope peaks in Da (roughly the C13-C12 gap).</summary>
        public const double IsotopeSpacing = 1.00235;

        /// <summary>Neutral monoisotopic mass in Da.</summary>
        public double MonoisotopicMass { get; set; }

        /// <summary>Charge state. Positive mode is assumed, so m/z = (M + z*proton)/z.</summary>
        public int Charge { get; set; } = 1;

        /// <summary>Relative abundance; scales the amplitude of the whole isotope cluster.</summary>
        public double Abundance { get; set; } = 1.0;

        /// <summary>
        /// Number of isotope peaks to simulate. 1 gives the monoisotopic peak only.
        /// Use <see cref="IsotopeEnvelope.SuggestedPeakCount"/> for a mass-appropriate value.
        /// </summary>
        public int IsotopePeaks { get; set; } = 1;

        /// <summary>
        /// Transient decay constant in seconds. When null the simulator's default is used.
        /// Shorter values mean faster dephasing and therefore a broader peak.
        /// </summary>
        public double? DecayConstant { get; set; }

        /// <summary>
        /// Initial phase in radians. When null the simulator draws one from its seeded RNG,
        /// which keeps runs reproducible while still avoiding every ion starting in phase.
        /// </summary>
        public double? InitialPhase { get; set; }

        /// <summary>
        /// Fractional change in frequency over one second, modelling space-charge drift.
        /// 0 is a perfectly stationary tone; 1e-4 means the frequency rises by 0.01% per
        /// second. This is the non-stationary behaviour a CWT can resolve and an FFT
        /// over the whole transient cannot.
        /// </summary>
        public double FrequencyDriftPerSecond { get; set; }

        /// <summary>Mass-to-charge ratio of the monoisotopic peak.</summary>
        public double MonoisotopicMz => MzForIsotope(0);

        /// <summary>Mass-to-charge ratio of the given isotope peak (0 = monoisotopic).</summary>
        public double MzForIsotope(int isotopeIndex)
        {
            if (Charge <= 0)
            {
                throw new InvalidOperationException("Charge must be a positive integer");
            }
            double mass = MonoisotopicMass + isotopeIndex * IsotopeSpacing;
            return (mass + Charge * ProtonMass) / Charge;
        }
    }
}
