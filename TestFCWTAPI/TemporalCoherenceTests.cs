using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET.Analysis;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class TemporalCoherenceTests
    {
        private const int SamplingRate = 10_000;
        private const int Columns = 4096;

        /// <summary>
        /// Builds a synthetic modulus array. Each row decays at its species' rate and
        /// carries that species' shared amplitude wobble -- the common-mode fluctuation
        /// that ions born in the same packet actually share -- plus a little independent
        /// noise, which is what stops the test from being trivially correlated.
        /// </summary>
        private static double[,] BuildRidges(IReadOnlyList<(double Tau, double WobbleHz, double Phase)> species,
            int rowsPerSpecies, int seed = 4242)
        {
            var random = new Random(seed);
            int rows = species.Count * rowsPerSpecies;
            var modulus = new double[rows, Columns];

            for (int s = 0; s < species.Count; s++)
            {
                (double tau, double wobbleHz, double phase) = species[s];
                for (int r = 0; r < rowsPerSpecies; r++)
                {
                    int row = s * rowsPerSpecies + r;
                    double amplitude = 1 + 0.3 * r;
                    for (int t = 0; t < Columns; t++)
                    {
                        double seconds = (double)t / SamplingRate;
                        double shared = 0.15 * Math.Sin(2 * Math.PI * wobbleHz * seconds + phase);
                        double noise = 0.01 * (random.NextDouble() - 0.5);
                        modulus[row, t] = amplitude * Math.Exp(-seconds / tau) * (1 + shared + noise);
                    }
                }
            }
            return modulus;
        }

        /// <summary>
        /// The constant comes back across two decades, but the fit quality does not: the
        /// window here is 0.41 s, so a 1 s time constant only takes the envelope down by
        /// 0.4 nats while the amplitude wobble moves it by 0.15 either way. That is why
        /// <see cref="RidgeDynamics.DecayFitQuality"/> is reported alongside the constant
        /// rather than folded into it.
        /// </summary>
        [TestCase(0.05, 0.99)]
        [TestCase(0.2, 0.9)]
        [TestCase(1.0, 0.3)]
        public void RecoversTheDecayConstant(double tau, double minimumFitQuality)
        {
            double[,] modulus = BuildRidges(new[] { (tau, 13.0, 0.0) }, 1);

            RidgeDynamics dynamics = TemporalCoherence.Describe(modulus, 0, SamplingRate);

            Assert.AreEqual(tau, dynamics.DecayConstantSeconds, tau * 0.1);
            Assert.Greater(dynamics.DecayFitQuality, minimumFitQuality);
        }

        /// <summary>
        /// A time constant far longer than the window is not measurable, and the fit must
        /// say so rather than returning the slope of the noise.
        /// </summary>
        [Test]
        public void ReportsNoDecayWhenTheWindowIsFarShorterThanTheDecay()
        {
            // 0.41 s of a 500 s decay: the envelope falls by 0.08%, under the threshold.
            double[,] modulus = BuildRidges(new[] { (500.0, 0.0, 0.0) }, 1, seed: 7);

            RidgeDynamics dynamics = TemporalCoherence.Describe(modulus, 0, SamplingRate);

            Assert.IsTrue(double.IsPositiveInfinity(dynamics.DecayConstantSeconds));
        }

        [Test]
        public void ReportsNoDecayForAFlatRidge()
        {
            var modulus = new double[1, Columns];
            for (int t = 0; t < Columns; t++)
            {
                modulus[0, t] = 5.0;
            }

            RidgeDynamics dynamics = TemporalCoherence.Describe(modulus, 0, SamplingRate);

            // A flat ridge has no decay to report, and saying "infinite" is honest where
            // reporting some large finite number would not be.
            Assert.IsTrue(double.IsPositiveInfinity(dynamics.DecayConstantSeconds));
            Assert.AreEqual(5.0, dynamics.MeanMagnitude, 1e-12);
        }

        [Test]
        public void RidgesOfOneSpeciesAreMoreSimilarThanRidgesOfDifferentSpecies()
        {
            double[,] modulus = BuildRidges(new[] { (0.20, 7.0, 0.0), (0.05, 23.0, 1.0) }, 3);
            var ridges = Enumerable.Range(0, 6)
                .Select(r => TemporalCoherence.Describe(modulus, r, SamplingRate, r))
                .ToList();

            double within = TemporalCoherence.Similarity(ridges[0], ridges[1]);
            double across = TemporalCoherence.Similarity(ridges[0], ridges[4]);

            TestContext.WriteLine($"within {within:F3}, across {across:F3}");
            Assert.Greater(within, 0.8);
            Assert.Less(across, within);
        }

        [Test]
        public void SimilarityIsSymmetricAndBounded()
        {
            double[,] modulus = BuildRidges(new[] { (0.20, 7.0, 0.0), (0.05, 23.0, 1.0) }, 2);
            var ridges = Enumerable.Range(0, 4)
                .Select(r => TemporalCoherence.Describe(modulus, r, SamplingRate))
                .ToList();

            foreach (RidgeDynamics a in ridges)
            {
                foreach (RidgeDynamics b in ridges)
                {
                    double similarity = TemporalCoherence.Similarity(a, b);
                    Assert.GreaterOrEqual(similarity, 0);
                    Assert.LessOrEqual(similarity, 1 + 1e-12);
                    Assert.AreEqual(similarity, TemporalCoherence.Similarity(b, a), 1e-12);
                }
            }
        }

        [Test]
        public void ClustersRidgesBySharedHistory()
        {
            double[,] modulus = BuildRidges(new[] { (0.20, 7.0, 0.0), (0.05, 23.0, 1.0) }, 3);
            var ridges = Enumerable.Range(0, 6)
                .Select(r => TemporalCoherence.Describe(modulus, r, SamplingRate, r))
                .ToList();

            IReadOnlyList<CoherenceCluster> clusters = TemporalCoherence.Cluster(ridges, 0.5);

            foreach (CoherenceCluster cluster in clusters)
            {
                TestContext.WriteLine(cluster.ToString());
            }
            Assert.AreEqual(2, clusters.Count);
            foreach (CoherenceCluster cluster in clusters)
            {
                Assert.AreEqual(3, cluster.Members.Count);
                // Rows 0-2 are one species and 3-5 the other; a cluster must not mix them.
                var rows = cluster.Members.Select(m => m.RowIndex).ToList();
                Assert.AreEqual(1, rows.Select(r => r / 3).Distinct().Count(),
                    "cluster mixed two species: rows " + string.Join(",", rows));
                Assert.Greater(cluster.Cohesion, 0.5);
            }
            Assert.AreEqual(0.20, clusters.Max(c => c.MeanDecayConstant), 0.03);
            Assert.AreEqual(0.05, clusters.Min(c => c.MeanDecayConstant), 0.01);
        }

        [Test]
        public void EveryRidgeLandsInExactlyOneCluster()
        {
            double[,] modulus = BuildRidges(new[] { (0.20, 7.0, 0.0), (0.05, 23.0, 1.0) }, 3);
            var ridges = Enumerable.Range(0, 6)
                .Select(r => TemporalCoherence.Describe(modulus, r, SamplingRate, r))
                .ToList();

            IReadOnlyList<CoherenceCluster> clusters = TemporalCoherence.Cluster(ridges);

            var assigned = clusters.SelectMany(c => c.Members).Select(m => m.RowIndex).ToList();
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 6), assigned);
        }

        /// <summary>
        /// A threshold of 1 admits no pair, so every ridge is its own cluster. Pinned
        /// because it is the behaviour that says the clustering is driven by the
        /// similarity and nothing else.
        /// </summary>
        [Test]
        public void AThresholdOfOneLeavesEveryRidgeAlone()
        {
            double[,] modulus = BuildRidges(new[] { (0.20, 7.0, 0.0) }, 3);
            var ridges = Enumerable.Range(0, 3)
                .Select(r => TemporalCoherence.Describe(modulus, r, SamplingRate))
                .ToList();

            IReadOnlyList<CoherenceCluster> clusters = TemporalCoherence.Cluster(ridges, 1.0);

            Assert.AreEqual(3, clusters.Count);
            Assert.IsTrue(clusters.All(c => c.Cohesion == 1), "a lone ridge is trivially cohesive");
        }

        [Test]
        public void ClusteringAnEmptyListGivesNoClusters()
        {
            Assert.IsEmpty(TemporalCoherence.Cluster(Array.Empty<RidgeDynamics>()));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            double[,] modulus = BuildRidges(new[] { (0.2, 7.0, 0.0) }, 1);

            Assert.Throws<ArgumentNullException>(() =>
                TemporalCoherence.Describe(null!, 0, SamplingRate));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TemporalCoherence.Describe(modulus, -1, SamplingRate));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TemporalCoherence.Describe(modulus, 1, SamplingRate));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TemporalCoherence.Describe(modulus, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TemporalCoherence.Describe(modulus, 0, SamplingRate, 0, 0.5));
            Assert.Throws<ArgumentException>(() =>
                TemporalCoherence.Describe(new double[1, 4], 0, SamplingRate));
            Assert.Throws<ArgumentNullException>(() => TemporalCoherence.Cluster(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TemporalCoherence.Cluster(Array.Empty<RidgeDynamics>(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TemporalCoherence.Cluster(Array.Empty<RidgeDynamics>(), 1.5));
            Assert.Throws<ArgumentNullException>(() => TemporalCoherence.Similarity(null!, null!));
        }
    }
}
