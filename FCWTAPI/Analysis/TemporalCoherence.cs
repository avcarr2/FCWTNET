using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FCWTNET.Analysis
{
    /// <summary>How one ridge behaves over the course of the transient.</summary>
    public class RidgeDynamics
    {
        /// <summary>Row of the CWT this describes.</summary>
        public int RowIndex { get; init; }

        /// <summary>Position on the frequency or m/z axis.</summary>
        public double Position { get; init; }

        /// <summary>
        /// Fitted exponential decay constant in seconds, from a straight-line fit to the log
        /// envelope. Positive means decaying; very large means effectively no decay.
        /// </summary>
        public double DecayConstantSeconds { get; init; }

        /// <summary>How well that exponential fits, as r-squared on the log envelope.</summary>
        public double DecayFitQuality { get; init; }

        /// <summary>Mean modulus over the window.</summary>
        public double MeanMagnitude { get; init; }

        /// <summary>Detrended envelope, normalised, for correlating against other ridges.</summary>
        public double[] NormalisedResidual { get; init; } = Array.Empty<double>();

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "row {0} at {1:F3}: tau {2:F4} s (r2 {3:F2}), mean {4:E2}",
                RowIndex, Position, DecayConstantSeconds, DecayFitQuality, MeanMagnitude);
        }
    }

    /// <summary>Ridges judged to share a physical history.</summary>
    public class CoherenceCluster
    {
        public IReadOnlyList<RidgeDynamics> Members { get; init; } = Array.Empty<RidgeDynamics>();

        /// <summary>Mean pairwise similarity within the cluster, in [0, 1].</summary>
        public double Cohesion { get; init; }

        public double MeanDecayConstant => Members.Count == 0 ? 0 : Members.Average(m => m.DecayConstantSeconds);

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} ridges, mean tau {1:F4} s, cohesion {2:F3}",
                Members.Count, MeanDecayConstant, Cohesion);
        }
    }

    /// <summary>
    /// Groups ridges by how they behave over time rather than by where they sit.
    ///
    /// <para>
    /// Charge states of one species share a physical history: they were created together,
    /// they dephase at similar rates, and space charge pushes their frequencies around
    /// together. A CWT preserves all of that, because it keeps the amplitude of each
    /// frequency as a function of time; an FFT over the whole transient integrates it away.
    /// This is the grouping evidence that is unavailable to a conventional workflow.
    /// </para>
    ///
    /// <para>
    /// It is also the weakest of the grouping methods and is offered as corroboration, not
    /// as a primary assignment. Decay rate varies systematically with charge state within a
    /// single species -- more highly charged ions dephase faster -- so "same species" implies
    /// only "similar dynamics", not identical. Treat a high cohesion as supporting a grouping
    /// that positions already suggest, and a low one as a reason to look closer.
    /// </para>
    /// </summary>
    public static class TemporalCoherence
    {
        /// <summary>
        /// Smallest total decline, in nats over the whole window, that counts as a decay.
        /// Below this the fitted slope is numerical dust -- a flat ridge fits a slope of
        /// -1e-18 and would otherwise be reported as decaying with a time constant of
        /// 1e18 seconds. The rule it encodes is simply that you cannot measure a decay
        /// constant much longer than about a thousand times your window.
        /// </summary>
        public const double MinimumMeasurableDecline = 1e-3;

        /// <summary>
        /// Characterises one ridge: how fast it decays, whether it drifts, and what is left
        /// once the decay is removed.
        /// </summary>
        /// <param name="modulus">CWT magnitudes, [frequency row, time].</param>
        /// <param name="row">Row to describe.</param>
        /// <param name="samplingRate">Sampling rate in Hz.</param>
        /// <param name="position">Frequency or m/z of this row, carried through for reporting.</param>
        /// <param name="edgeFraction">
        /// Fraction of the window trimmed at each end before fitting, to keep the wavelet's
        /// edge effects out of the dynamics.
        /// </param>
        public static RidgeDynamics Describe(double[,] modulus, int row, int samplingRate,
            double position = 0, double edgeFraction = 0.1)
        {
            if (modulus == null)
            {
                throw new ArgumentNullException(nameof(modulus));
            }
            if (row < 0 || row >= modulus.GetLength(0))
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            if (samplingRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplingRate), "samplingRate must be positive");
            }
            if (edgeFraction < 0 || edgeFraction >= 0.5)
            {
                throw new ArgumentOutOfRangeException(nameof(edgeFraction), "edgeFraction must lie in [0, 0.5)");
            }

            int columns = modulus.GetLength(1);
            int margin = (int)(columns * edgeFraction);
            int count = columns - 2 * margin;
            if (count < 8)
            {
                throw new ArgumentException("Too few time points remain after trimming the edges", nameof(modulus));
            }

            double[] envelope = new double[count];
            for (int i = 0; i < count; i++)
            {
                envelope[i] = modulus[row, margin + i];
            }

            (double slope, double intercept, double rSquared) = LogLinearFit(envelope, samplingRate);
            // envelope ~ exp(intercept + slope*t); decay constant is -1/slope, but only
            // where the fitted decline over the window is large enough to be real.
            double windowSeconds = (double)count / samplingRate;
            double tau = slope < 0 && -slope * windowSeconds >= MinimumMeasurableDecline
                ? -1 / slope
                : double.PositiveInfinity;

            double[] residual = new double[count];
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / samplingRate;
                double fitted = Math.Exp(intercept + slope * t);
                residual[i] = fitted > 0 ? envelope[i] / fitted - 1 : 0;
            }
            Normalise(residual);

            return new RidgeDynamics
            {
                RowIndex = row,
                Position = position,
                DecayConstantSeconds = tau,
                DecayFitQuality = rSquared,
                MeanMagnitude = envelope.Average(),
                NormalisedResidual = residual
            };
        }

        /// <summary>
        /// Similarity of two ridges' dynamics, in [0, 1]: the geometric mean of how closely
        /// their decay constants agree and how well their detrended envelopes correlate.
        /// </summary>
        public static double Similarity(RidgeDynamics a, RidgeDynamics b)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }
            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            // Decay agreement, compared in log space so a factor of two costs the same
            // either way round.
            double decayAgreement;
            if (double.IsInfinity(a.DecayConstantSeconds) && double.IsInfinity(b.DecayConstantSeconds))
            {
                decayAgreement = 1;
            }
            else if (double.IsInfinity(a.DecayConstantSeconds) || double.IsInfinity(b.DecayConstantSeconds)
                     || a.DecayConstantSeconds <= 0 || b.DecayConstantSeconds <= 0)
            {
                decayAgreement = 0;
            }
            else
            {
                double logRatio = Math.Abs(Math.Log(a.DecayConstantSeconds / b.DecayConstantSeconds));
                decayAgreement = Math.Exp(-logRatio);
            }

            double correlation = Math.Max(0, Correlation(a.NormalisedResidual, b.NormalisedResidual));
            return Math.Sqrt(decayAgreement * correlation);
        }

        /// <summary>
        /// Clusters ridges by pairwise similarity, using single-linkage agglomeration above
        /// a threshold. Deliberately simple: this is corroborating evidence, and an elaborate
        /// clustering scheme would overstate how much the signal supports.
        /// </summary>
        public static IReadOnlyList<CoherenceCluster> Cluster(IReadOnlyList<RidgeDynamics> ridges,
            double threshold = 0.5)
        {
            if (ridges == null)
            {
                throw new ArgumentNullException(nameof(ridges));
            }
            if (threshold <= 0 || threshold > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold), "threshold must lie in (0, 1]");
            }
            if (ridges.Count == 0)
            {
                return Array.Empty<CoherenceCluster>();
            }

            // Union-find over pairs above the threshold.
            int[] parent = Enumerable.Range(0, ridges.Count).ToArray();
            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            for (int i = 0; i < ridges.Count; i++)
            {
                for (int j = i + 1; j < ridges.Count; j++)
                {
                    if (Similarity(ridges[i], ridges[j]) >= threshold)
                    {
                        parent[Find(i)] = Find(j);
                    }
                }
            }

            return ridges
                .Select((ridge, index) => (ridge, root: Find(index)))
                .GroupBy(x => x.root)
                .Select(group =>
                {
                    var members = group.Select(x => x.ridge).ToList();
                    return new CoherenceCluster
                    {
                        Members = members,
                        Cohesion = MeanPairwiseSimilarity(members)
                    };
                })
                .OrderByDescending(c => c.Members.Count)
                .ToList();
        }

        private static double MeanPairwiseSimilarity(IReadOnlyList<RidgeDynamics> members)
        {
            if (members.Count < 2)
            {
                return 1;
            }
            double total = 0;
            int pairs = 0;
            for (int i = 0; i < members.Count; i++)
            {
                for (int j = i + 1; j < members.Count; j++)
                {
                    total += Similarity(members[i], members[j]);
                    pairs++;
                }
            }
            return total / pairs;
        }

        /// <summary>Least-squares fit of log(envelope) against time, with r-squared.</summary>
        private static (double Slope, double Intercept, double RSquared) LogLinearFit(
            double[] envelope, int samplingRate)
        {
            int n = envelope.Length;
            double floor = envelope.Where(v => v > 0).DefaultIfEmpty(1).Min() * 1e-6;

            double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
            var logs = new double[n];
            for (int i = 0; i < n; i++)
            {
                double x = (double)i / samplingRate;
                double y = Math.Log(Math.Max(envelope[i], floor));
                logs[i] = y;
                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumXY += x * y;
            }

            double denominator = n * sumXX - sumX * sumX;
            double slope = denominator == 0 ? 0 : (n * sumXY - sumX * sumY) / denominator;
            double intercept = (sumY - slope * sumX) / n;

            double meanY = sumY / n;
            double totalSumSquares = 0;
            double residualSumSquares = 0;
            for (int i = 0; i < n; i++)
            {
                double x = (double)i / samplingRate;
                double fitted = intercept + slope * x;
                totalSumSquares += (logs[i] - meanY) * (logs[i] - meanY);
                residualSumSquares += (logs[i] - fitted) * (logs[i] - fitted);
            }
            double rSquared = totalSumSquares > 0 ? 1 - residualSumSquares / totalSumSquares : 0;
            return (slope, intercept, rSquared);
        }

        private static void Normalise(double[] values)
        {
            double mean = values.Average();
            for (int i = 0; i < values.Length; i++)
            {
                values[i] -= mean;
            }
            double norm = Math.Sqrt(values.Sum(v => v * v));
            if (norm <= 0)
            {
                return;
            }
            for (int i = 0; i < values.Length; i++)
            {
                values[i] /= norm;
            }
        }

        /// <summary>Pearson correlation of two already-centred, unit-norm vectors.</summary>
        private static double Correlation(double[] a, double[] b)
        {
            if (a.Length == 0 || b.Length == 0)
            {
                return 0;
            }
            int n = Math.Min(a.Length, b.Length);
            double dot = 0;
            for (int i = 0; i < n; i++)
            {
                dot += a[i] * b[i];
            }
            return dot;
        }
    }
}
