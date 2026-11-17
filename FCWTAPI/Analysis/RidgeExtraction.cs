using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET.Transform;
using FCWTNET.Transform.Wavelets;

namespace FCWTNET.Analysis
{
    /// <summary>A peak located in a CWT scalogram.</summary>
    public class DetectedPeak
    {
        /// <summary>Nearest frequency-axis row.</summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// Sub-row peak position. Refined against the analysing wavelet's own response
        /// where a <see cref="ScaleGrid"/> was supplied, and by a plain parabola otherwise.
        /// </summary>
        public double InterpolatedRow { get; set; }

        /// <summary>
        /// Interpolated position on whichever axis was handed to
        /// <see cref="RidgeExtraction.FindPeaks(double[], double[], double, int)"/>. That is
        /// usually an m/z axis, so this is usually an m/z -- the name is deliberately unit
        /// agnostic rather than misleadingly called a frequency.
        /// </summary>
        public double Position { get; set; }

        /// <summary>Time-averaged modulus at the peak.</summary>
        public double Magnitude { get; set; }

        /// <summary>
        /// How well one wavelet response explains the rows around this peak: the RMS
        /// deviation of the log magnitudes from the fitted shape, in nats. NaN where no
        /// wavelet fit was attempted.
        ///
        /// <para>
        /// Reported rather than acted on, because it answers a narrower question than it
        /// first appears to. An isolated pure tone fits to numerical precision, and a ridge
        /// blended with a neighbour the wavelet cannot resolve fits far worse -- on
        /// myoglobin's denatured envelope at c0 = 80 the clean low-charge ridges came in
        /// three orders of magnitude below the crowded high-charge ones. But an isotope
        /// cluster is not a pure tone either, and a narrower wavelet resolves more of its
        /// width, so the residual climbs with c0 even where the position stays good. Read
        /// it as a relative signal across ridges of one transform, not against a fixed
        /// number: a ridge far above its neighbours is one whose position to distrust, and
        /// the remedy is to raise c0 until the components separate.
        /// </para>
        /// </summary>
        public double FitResidual { get; set; } = double.NaN;
    }

    /// <summary>
    /// Turns a scalogram back into numbers: where the ridges are, and what m/z they
    /// correspond to. This is what lets a simulated run be scored against its own ground
    /// truth instead of judged by eye.
    /// </summary>
    public static class RidgeExtraction
    {
        /// <summary>
        /// Fraction of the time axis discarded at each end before averaging.
        ///
        /// <para>
        /// The transient is truncated at both ends, and the resulting spectral leakage is
        /// scale-dependent, so averaging the modulus over the whole window shifts the
        /// ridge. Measured on a tone sitting halfway between two FFT bins -- the worst
        /// case -- averaging everything biased the mass by -30 ppm at c0 = 6 and +16 ppm at
        /// c0 = 80; discarding 1% of each end took it to zero at every c0 tried. The
        /// default is that 1%, which for a 64k transient is 655 samples at each end against
        /// a wavelet support of tens of samples.
        /// </para>
        /// </summary>
        public const double DefaultEdgeFraction = 0.01;


        /// <summary>
        /// Mean modulus of each frequency row across time. Rows are the first dimension,
        /// time points the second, matching <see cref="CWTOutput.ModulusCalculation"/>.
        /// </summary>
        /// <param name="modulus">CWT magnitudes, [frequency row, time].</param>
        /// <param name="edgeFraction">
        /// Fraction of the time axis dropped at each end; see
        /// <see cref="DefaultEdgeFraction"/> for why the default is not zero. Pass 0 to
        /// average the whole window.
        /// </param>
        public static double[] TimeAveragedMagnitude(double[,] modulus,
            double edgeFraction = DefaultEdgeFraction)
        {
            if (modulus == null)
            {
                throw new ArgumentNullException(nameof(modulus));
            }
            if (edgeFraction < 0 || edgeFraction >= 0.5)
            {
                throw new ArgumentOutOfRangeException(nameof(edgeFraction),
                    "edgeFraction must lie in [0, 0.5)");
            }
            int rows = modulus.GetLength(0);
            int columns = modulus.GetLength(1);

            int margin = (int)(columns * edgeFraction);
            int count = columns - 2 * margin;
            if (count < 1)
            {
                // Too few columns to trim; averaging nothing would be worse than averaging
                // a contaminated window.
                margin = 0;
                count = columns;
            }

            double[] averaged = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double sum = 0;
                for (int j = 0; j < count; j++)
                {
                    sum += modulus[i, margin + j];
                }
                averaged[i] = sum / count;
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
        /// <param name="scaleGrid">
        /// The grid the transform was computed on. When supplied, sub-row positions are
        /// refined against the analysing wavelet's own response instead of a parabola,
        /// which is worth one to two orders of magnitude in mass accuracy -- see
        /// <see cref="RefineAgainstWavelet"/>. Null falls back to the parabola.
        /// </param>
        public static IReadOnlyList<DetectedPeak> FindPeaks(double[] magnitude, double[] frequencyAxis,
            double relativeThreshold = 0.05, int minimumRowSeparation = 1, ScaleGrid? scaleGrid = null)
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
            if (scaleGrid != null && scaleGrid.Count != magnitude.Length)
            {
                throw new ArgumentException(
                    $"scaleGrid has {scaleGrid.Count} scales but the magnitude profile has " +
                    $"{magnitude.Length} rows", nameof(scaleGrid));
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

                double residual = double.NaN;
                double offset = scaleGrid != null
                    ? RefineAgainstWavelet(magnitude, i, scaleGrid, out residual)
                    : ParabolicOffset(magnitude[i - 1], magnitude[i], magnitude[i + 1]);
                candidates.Add(new DetectedPeak
                {
                    RowIndex = i,
                    InterpolatedRow = i + offset,
                    Position = InterpolateFrequency(frequencyAxis, i, offset),
                    Magnitude = magnitude[i],
                    FitResidual = residual
                });
            }

            return SuppressNeighbours(candidates, minimumRowSeparation);
        }

        /// <summary>
        /// Convenience overload that goes straight from a CWT to detected peaks.
        /// </summary>
        public static IReadOnlyList<DetectedPeak> FindPeaks(double[,] modulus, double[] frequencyAxis,
            double relativeThreshold = 0.05, int minimumRowSeparation = 1, ScaleGrid? scaleGrid = null,
            double edgeFraction = DefaultEdgeFraction)
        {
            return FindPeaks(TimeAveragedMagnitude(modulus, edgeFraction), frequencyAxis,
                relativeThreshold, minimumRowSeparation, scaleGrid);
        }

        /// <summary>
        /// Sub-row peak offset found by fitting the analysing wavelet's <em>own</em>
        /// response to three samples, rather than assuming a parabola.
        ///
        /// <para>
        /// A parabola through three magnitudes at consecutive row indices is the wrong
        /// model twice over. The ordinate is wrong -- for a Morlet the response is
        /// <c>exp(-(a*w - c0)^2/2)</c>, so it is the <em>log</em> magnitude that is
        /// quadratic -- and so is the abscissa, because rows are geometric in scale while
        /// that quadratic is in scale itself. The two errors do not cancel, and what is
        /// left is a bias proportional to the row spacing: on a clean simulated peak it ran
        /// to 1477 ppm at 16 voices per octave and was still 70 ppm at 256.
        /// </para>
        ///
        /// <para>
        /// Fitting the response directly removes it. Since an overall scale factor is a
        /// free additive constant in log space, the only unknown left is the centre
        /// frequency, and a one-dimensional search over it fits all three samples. On the
        /// same peak this gives 60 ppm at 16 voices and does not improve with more -- it is
        /// already at the floor set by other effects, so it reaches at 16 voices what the
        /// parabola needs 256 to reach.
        /// </para>
        ///
        /// <para>
        /// This works for every wavelet in the catalogue rather than just the Morlet,
        /// because it asks the wavelet for its response instead of assuming a shape.
        /// </para>
        /// </summary>
        /// <returns>Offset from <paramref name="row"/> in rows, within [-0.5, 0.5].</returns>
        public static double RefineAgainstWavelet(double[] magnitude, int row, ScaleGrid scaleGrid)
        {
            return RefineAgainstWavelet(magnitude, row, scaleGrid, out _);
        }

        /// <summary>
        /// As <see cref="RefineAgainstWavelet(double[], int, ScaleGrid)"/>, also reporting
        /// how well one response fitted -- see <see cref="DetectedPeak.FitResidual"/>.
        /// </summary>
        public static double RefineAgainstWavelet(double[] magnitude, int row, ScaleGrid scaleGrid,
            out double residual)
        {
            residual = double.NaN;
            if (magnitude == null)
            {
                throw new ArgumentNullException(nameof(magnitude));
            }
            if (scaleGrid == null)
            {
                throw new ArgumentNullException(nameof(scaleGrid));
            }
            if (row < 1 || row > magnitude.Length - 2)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "row must have a neighbour on each side");
            }
            if (magnitude[row - 1] <= 0 || magnitude[row] <= 0 || magnitude[row + 1] <= 0)
            {
                // A log fit needs positive samples; a parabola is the honest fallback.
                return ParabolicOffset(magnitude[row - 1], magnitude[row], magnitude[row + 1]);
            }

            // Rows ascend in frequency, scales descend, so row r sits at Scales[n-1-r].
            // Three rows, not more: a wider window reaches into a neighbouring component
            // and the fit then describes the blend rather than the ion. Widening it to
            // five cost the denatured myoglobin envelope its whole deconvolution.
            int n = scaleGrid.Count;
            double[] scales =
            {
                scaleGrid.Scales[n - row],
                scaleGrid.Scales[n - 1 - row],
                scaleGrid.Scales[n - 2 - row]
            };
            double[] logs =
            {
                Math.Log(magnitude[row - 1]),
                Math.Log(magnitude[row]),
                Math.Log(magnitude[row + 1])
            };

            IWavelet wavelet = scaleGrid.Wavelet;
            // The component lies within one row of the maximum, so bracket the search by
            // the neighbouring rows' centre frequencies.
            double omegaLow = wavelet.CentreAngularFrequency / scales[0];
            double omegaHigh = wavelet.CentreAngularFrequency / scales[2];

            double Objective(double omega)
            {
                // An overall amplitude is a free additive constant in log space, so fit it
                // out and score only the shape.
                double total = 0;
                var deviations = new double[3];
                for (int k = 0; k < 3; k++)
                {
                    double response = wavelet.Response(scales[k] * omega);
                    if (response <= 0)
                    {
                        return double.MaxValue;
                    }
                    deviations[k] = logs[k] - Math.Log(response);
                    total += deviations[k];
                }
                double mean = total / 3;
                double sum = 0;
                for (int k = 0; k < 3; k++)
                {
                    double d = deviations[k] - mean;
                    sum += d * d;
                }
                return sum;
            }

            // Coarse scan then golden-section refine: the objective is unimodal in the
            // bracket for the wavelets here, and the scan guards against it not being so.
            const int ScanPoints = 33;
            double best = omegaLow;
            double bestObjective = double.MaxValue;
            for (int k = 0; k < ScanPoints; k++)
            {
                double omega = omegaLow + (omegaHigh - omegaLow) * k / (ScanPoints - 1);
                double value = Objective(omega);
                if (value < bestObjective)
                {
                    bestObjective = value;
                    best = omega;
                }
            }

            double step = (omegaHigh - omegaLow) / (ScanPoints - 1);
            double low = Math.Max(omegaLow, best - step);
            double high = Math.Min(omegaHigh, best + step);
            const double GoldenRatio = 0.6180339887498949;
            for (int iteration = 0; iteration < 60 && high - low > 1e-15 * high; iteration++)
            {
                double m1 = high - (high - low) * GoldenRatio;
                double m2 = low + (high - low) * GoldenRatio;
                if (Objective(m1) < Objective(m2))
                {
                    high = m2;
                }
                else
                {
                    low = m1;
                }
            }

            double omegaStar = 0.5 * (low + high);
            double finalObjective = Objective(omegaStar);
            if (omegaStar <= 0 || finalObjective == double.MaxValue)
            {
                return ParabolicOffset(magnitude[row - 1], magnitude[row], magnitude[row + 1]);
            }
            residual = Math.Sqrt(finalObjective / 3);

            // Back to a fractional row. Scales are geometric, so this is exact.
            double scaleStar = wavelet.CentreAngularFrequency / omegaStar;
            double ratio = scales[2] / scales[1];
            double offset = ratio == 1 ? 0 : Math.Log(scaleStar / scales[1]) / Math.Log(ratio);
            return Math.Clamp(offset, -0.5, 0.5);
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
