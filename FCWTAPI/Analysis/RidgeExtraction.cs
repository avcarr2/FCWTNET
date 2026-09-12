using System;
using System.Collections.Generic;
using System.Linq;

namespace FCWTNET.Analysis
{
    /// <summary>A peak located in a CWT scalogram.</summary>
    public class DetectedPeak
    {
        /// <summary>Nearest frequency-axis row.</summary>
        public int RowIndex { get; set; }

        /// <summary>Sub-row peak position from parabolic interpolation.</summary>
        public double InterpolatedRow { get; set; }

        /// <summary>Interpolated wavelet centre frequency, in whatever units the axis carries.</summary>
        public double Frequency { get; set; }

        /// <summary>Time-averaged modulus at the peak.</summary>
        public double Magnitude { get; set; }
    }

    /// <summary>
    /// Turns a scalogram back into numbers: where the ridges are, and what m/z they
    /// correspond to. This is what lets a simulated run be scored against its own ground
    /// truth instead of judged by eye.
    /// </summary>
    public static class RidgeExtraction
    {
        /// <summary>
        /// Mean modulus of each frequency row across time. Rows are the first dimension,
        /// time points the second, matching <see cref="CWTOutput.ModulusCalculation"/>.
        /// </summary>
        public static double[] TimeAveragedMagnitude(double[,] modulus)
        {
            if (modulus == null)
            {
                throw new ArgumentNullException(nameof(modulus));
            }
            int rows = modulus.GetLength(0);
            int columns = modulus.GetLength(1);
            double[] averaged = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double sum = 0;
                for (int j = 0; j < columns; j++)
                {
                    sum += modulus[i, j];
                }
                averaged[i] = sum / columns;
            }
            return averaged;
        }

        /// <summary>
        /// Frequency row carrying the most energy at each time point: the instantaneous
        /// ridge. Useful for following a drifting component across the transient.
        /// </summary>
        public static int[] RidgePerTimePoint(double[,] modulus)
        {
            if (modulus == null)
            {
                throw new ArgumentNullException(nameof(modulus));
            }
            int rows = modulus.GetLength(0);
            int columns = modulus.GetLength(1);
            int[] ridge = new int[columns];
            for (int j = 0; j < columns; j++)
            {
                int best = 0;
                double bestValue = modulus[0, j];
                for (int i = 1; i < rows; i++)
                {
                    if (modulus[i, j] > bestValue)
                    {
                        bestValue = modulus[i, j];
                        best = i;
                    }
                }
                ridge[j] = best;
            }
            return ridge;
        }

        /// <summary>
        /// Local maxima of a time-averaged magnitude profile, refined to sub-row precision.
        /// </summary>
        /// <param name="magnitude">Time-averaged magnitude per frequency row.</param>
        /// <param name="frequencyAxis">Frequency for each row; must be the same length.</param>
        /// <param name="relativeThreshold">
        /// Ignore maxima below this fraction of the largest peak. Keeps noise ripple out
        /// of the result.
        /// </param>
        /// <param name="minimumRowSeparation">
        /// Suppress peaks closer than this many rows to a stronger one. With many voices
        /// per octave a single component spans several rows.
        /// </param>
        public static IReadOnlyList<DetectedPeak> FindPeaks(double[] magnitude, double[] frequencyAxis,
            double relativeThreshold = 0.05, int minimumRowSeparation = 1)
        {
            if (magnitude == null)
            {
                throw new ArgumentNullException(nameof(magnitude));
            }
            if (frequencyAxis == null)
            {
                throw new ArgumentNullException(nameof(frequencyAxis));
            }
            if (magnitude.Length != frequencyAxis.Length)
            {
                throw new ArgumentException("magnitude and frequencyAxis must be the same length", nameof(frequencyAxis));
            }
            if (magnitude.Length < 3)
            {
                return Array.Empty<DetectedPeak>();
            }

            double maximum = magnitude.Max();
            if (maximum <= 0)
            {
                return Array.Empty<DetectedPeak>();
            }
            double cutoff = maximum * relativeThreshold;

            var candidates = new List<DetectedPeak>();
            for (int i = 1; i < magnitude.Length - 1; i++)
            {
                if (magnitude[i] < cutoff)
                {
                    continue;
                }
                if (magnitude[i] < magnitude[i - 1] || magnitude[i] < magnitude[i + 1])
                {
                    continue;
                }
                // A flat top would otherwise register twice; keep the left-hand sample.
                if (magnitude[i] == magnitude[i - 1])
                {
                    continue;
                }

                double offset = ParabolicOffset(magnitude[i - 1], magnitude[i], magnitude[i + 1]);
                candidates.Add(new DetectedPeak
                {
                    RowIndex = i,
                    InterpolatedRow = i + offset,
                    Frequency = InterpolateFrequency(frequencyAxis, i, offset),
                    Magnitude = magnitude[i]
                });
            }

            return SuppressNeighbours(candidates, minimumRowSeparation);
        }

        /// <summary>
        /// Convenience overload that goes straight from a CWT to detected peaks.
        /// </summary>
        public static IReadOnlyList<DetectedPeak> FindPeaks(double[,] modulus, double[] frequencyAxis,
            double relativeThreshold = 0.05, int minimumRowSeparation = 1)
        {
            return FindPeaks(TimeAveragedMagnitude(modulus), frequencyAxis, relativeThreshold, minimumRowSeparation);
        }

        /// <summary>
        /// Sub-sample peak offset in [-0.5, 0.5] from a three-point parabola fit.
        /// </summary>
        private static double ParabolicOffset(double left, double centre, double right)
        {
            double denominator = left - 2 * centre + right;
            if (Math.Abs(denominator) < double.Epsilon)
            {
                return 0;
            }
            double offset = 0.5 * (left - right) / denominator;
            return Math.Clamp(offset, -0.5, 0.5);
        }

        /// <summary>
        /// Interpolate the frequency axis at a fractional row. The axis is geometric
        /// (constant ratio between adjacent voices), so interpolation happens in log space.
        /// </summary>
        private static double InterpolateFrequency(double[] frequencyAxis, int row, double offset)
        {
            if (offset == 0)
            {
                return frequencyAxis[row];
            }
            int neighbour = offset > 0 ? row + 1 : row - 1;
            if (neighbour < 0 || neighbour >= frequencyAxis.Length)
            {
                return frequencyAxis[row];
            }
            double here = frequencyAxis[row];
            double there = frequencyAxis[neighbour];
            if (here <= 0 || there <= 0)
            {
                return here + (there - here) * Math.Abs(offset);
            }
            return here * Math.Pow(there / here, Math.Abs(offset));
        }

        private static List<DetectedPeak> SuppressNeighbours(List<DetectedPeak> candidates, int minimumRowSeparation)
        {
            if (minimumRowSeparation <= 0)
            {
                return candidates.OrderByDescending(p => p.Magnitude).ToList();
            }

            var kept = new List<DetectedPeak>();
            foreach (DetectedPeak candidate in candidates.OrderByDescending(p => p.Magnitude))
            {
                bool tooClose = kept.Any(k => Math.Abs(k.RowIndex - candidate.RowIndex) < minimumRowSeparation);
                if (!tooClose)
                {
                    kept.Add(candidate);
                }
            }
            return kept;
        }
    }
}
