using System;
using System.Collections.Generic;

namespace FCWTNET.Simulation
{
    /// <summary>
    /// One simulated isotopologue, recorded so analysis can be scored against the truth
    /// rather than eyeballed.
    /// </summary>
    public class IsotopologueTruth
    {
        /// <summary>Mass-to-charge ratio.</summary>
        public double Mz { get; set; }

        /// <summary>Initial frequency in Hz, before any drift.</summary>
        public double Frequency { get; set; }

        /// <summary>Amplitude of this component at t = 0.</summary>
        public double Amplitude { get; set; }

        /// <summary>Initial phase in radians.</summary>
        public double InitialPhase { get; set; }

        /// <summary>Decay constant in seconds.</summary>
        public double DecayConstant { get; set; }

        /// <summary>Fractional frequency drift per second.</summary>
        public double FrequencyDriftPerSecond { get; set; }

        /// <summary>Charge state of the parent species.</summary>
        public int Charge { get; set; }

        /// <summary>Isotope index within the cluster; 0 is monoisotopic.</summary>
        public int IsotopeIndex { get; set; }

        /// <summary>Instantaneous frequency at time t, in Hz.</summary>
        public double FrequencyAt(double timeSeconds)
        {
            return Frequency * (1 + FrequencyDriftPerSecond * timeSeconds);
        }
    }

    /// <summary>A simulated transient together with everything that went into it.</summary>
    public class SimulationResult
    {
        public SimulationResult(double[] samples, IReadOnlyList<IsotopologueTruth> groundTruth,
            int samplingRate, double calibrationCoefficient)
        {
            Samples = samples;
            GroundTruth = groundTruth;
            SamplingRate = samplingRate;
            CalibrationCoefficient = calibrationCoefficient;
        }

        /// <summary>The simulated image current, one sample per time point.</summary>
        public double[] Samples { get; }

        /// <summary>Every simulated isotopologue, in the order they were generated.</summary>
        public IReadOnlyList<IsotopologueTruth> GroundTruth { get; }

        /// <summary>Sampling rate in Hz.</summary>
        public int SamplingRate { get; }

        /// <summary>Calibration coefficient A, where m/z = A / f^2.</summary>
        public double CalibrationCoefficient { get; }

        /// <summary>Transient duration in seconds.</summary>
        public double DurationSeconds => (double)Samples.Length / SamplingRate;

        /// <summary>Highest frequency the sampling rate can represent, in Hz.</summary>
        public double NyquistFrequency => SamplingRate / 2.0;
    }
}
