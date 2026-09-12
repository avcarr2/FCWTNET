using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FCWTNET.Simulation;

namespace FCWTNET.Analysis
{
    /// <summary>How well a candidate mass and ionisation mode explain a peak list.</summary>
    public class EnvelopeMatch
    {
        public double MonoisotopicMass { get; init; }

        /// <summary>Ionisation regime whose envelope shape fitted best.</summary>
        public IonizationMode Mode { get; init; }

        /// <summary>Centroid charge of the fitted envelope.</summary>
        public double CentroidCharge { get; init; }

        /// <summary>
        /// Cosine similarity between the predicted and observed intensity patterns, in
        /// [0, 1]. 1 means the observed peaks have exactly the abundance profile the model
        /// predicts for this mass.
        /// </summary>
        public double Similarity { get; init; }

        /// <summary>Fraction of the predicted charge states that were actually observed.</summary>
        public double Coverage { get; init; }

        /// <summary>Combined score: similarity weighted by how much of the envelope was seen.</summary>
        public double Score => Similarity * Coverage;

        public IReadOnlyList<AssignedPeak> Peaks { get; init; } = Array.Empty<AssignedPeak>();

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0:F1} Da as {1}, centroid {2:F1}+, similarity {3:F3}, coverage {4:F2}",
                MonoisotopicMass, Mode, CentroidCharge, Similarity, Coverage);
        }
    }

    /// <summary>
    /// Scores candidate masses by how well the <em>shape</em> of their charge envelope
    /// matches the observed peak intensities.
    ///
    /// <para>
    /// The comb and voting methods use only peak positions. This adds the other half of the
    /// information: electrospray does not populate charge states arbitrarily, it produces a
    /// smooth distribution whose centroid and width follow from the protein's mass and
    /// whether it is folded. <see cref="ChargeStateEnvelope"/> already models that for the
    /// simulator, so the same model can be run backwards as a matched filter.
    /// </para>
    ///
    /// <para>
    /// Two things come out of it that positions alone cannot give. Intensities corroborate
    /// a mass when peaks are sparse or shared between species, and the better-fitting
    /// ionisation mode is itself a result -- a native-looking envelope on a protein you
    /// expected to be denatured says something about the sample.
    /// </para>
    /// </summary>
    public static class EnvelopeTemplateMatching
    {
        /// <summary>
        /// Scores one candidate mass under one ionisation mode against the observed peaks.
        /// </summary>
        /// <param name="mzValues">Observed m/z values.</param>
        /// <param name="magnitudes">Observed intensities.</param>
        /// <param name="mass">Candidate neutral monoisotopic mass.</param>
        /// <param name="mode">Ionisation regime whose envelope model to use.</param>
        /// <param name="options">
        /// Envelope shape overrides; the mode's model is used for anything left null.
        /// </param>
        /// <param name="mzTolerancePpm">How close an observed peak must be to a predicted one.</param>
        public static EnvelopeMatch? Match(IReadOnlyList<double> mzValues, IReadOnlyList<double> magnitudes,
            double mass, IonizationMode mode, ChargeStateEnvelopeOptions? options = null,
            double mzTolerancePpm = 2000)
        {
            if (mzValues == null)
            {
                throw new ArgumentNullException(nameof(mzValues));
            }
            if (magnitudes == null)
            {
                throw new ArgumentNullException(nameof(magnitudes));
            }
            if (mzValues.Count != magnitudes.Count)
            {
                throw new ArgumentException("mzValues and magnitudes must be the same length", nameof(magnitudes));
            }
            if (mass <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(mass), "mass must be positive");
            }

            ChargeStateEnvelopeOptions envelopeOptions = (options ?? new ChargeStateEnvelopeOptions()).Clone();
            envelopeOptions.Mode = mode;
            IReadOnlyList<ChargeStateAbundance> predicted =
                ChargeStateEnvelope.Generate(mass, envelopeOptions);

            var matched = new List<AssignedPeak>();
            var predictedIntensities = new List<double>();
            var observedIntensities = new List<double>();
            int observedStates = 0;

            foreach (ChargeStateAbundance state in predicted)
            {
                // Nearest observed peak to where this charge state should be.
                int best = -1;
                double bestError = double.MaxValue;
                for (int i = 0; i < mzValues.Count; i++)
                {
                    double errorPpm = Math.Abs(mzValues[i] - state.Mz) / state.Mz * 1e6;
                    if (errorPpm < bestError)
                    {
                        bestError = errorPpm;
                        best = i;
                    }
                }

                predictedIntensities.Add(state.Abundance);
                if (best >= 0 && bestError <= mzTolerancePpm)
                {
                    observedIntensities.Add(magnitudes[best]);
                    observedStates++;
                    double impliedMass = ChargeStateDeconvolution.MassForCharge(mzValues[best], state.Charge);
                    matched.Add(new AssignedPeak
                    {
                        Mz = mzValues[best],
                        Magnitude = magnitudes[best],
                        Charge = state.Charge,
                        ImpliedMass = impliedMass,
                        MassErrorPpm = (impliedMass - mass) / mass * 1e6
                    });
                }
                else
                {
                    // A predicted state with nothing observed still counts against the fit.
                    observedIntensities.Add(0);
                }
            }

            if (matched.Count == 0)
            {
                return null;
            }

            return new EnvelopeMatch
            {
                MonoisotopicMass = mass,
                Mode = mode,
                CentroidCharge = envelopeOptions.CentroidCharge
                                 ?? ChargeStateEnvelope.CentroidCharge(mass, mode),
                Similarity = CosineSimilarity(predictedIntensities, observedIntensities),
                Coverage = (double)observedStates / predicted.Count,
                Peaks = matched
            };
        }

        /// <summary>
        /// Tries both ionisation modes and returns whichever explains the peaks better.
        /// The winning mode is informative in itself.
        /// </summary>
        public static EnvelopeMatch? BestMatch(IReadOnlyList<double> mzValues, IReadOnlyList<double> magnitudes,
            double mass, ChargeStateEnvelopeOptions? options = null, double mzTolerancePpm = 2000)
        {
            EnvelopeMatch? best = null;
            foreach (IonizationMode mode in Enum.GetValues<IonizationMode>())
            {
                EnvelopeMatch? match = Match(mzValues, magnitudes, mass, mode, options, mzTolerancePpm);
                if (match != null && (best == null || match.Score > best.Score))
                {
                    best = match;
                }
            }
            return best;
        }

        /// <summary>
        /// Adds envelope-shape evidence to groups already found by
        /// <see cref="ChargeStateDeconvolution"/>, without changing their masses.
        /// </summary>
        public static IReadOnlyList<(ChargeStateGroup Group, EnvelopeMatch? Match)> Annotate(
            IReadOnlyList<ChargeStateGroup> groups, IReadOnlyList<double> mzValues,
            IReadOnlyList<double> magnitudes, double mzTolerancePpm = 2000)
        {
            if (groups == null)
            {
                throw new ArgumentNullException(nameof(groups));
            }
            return groups
                .Select(g => (g, BestMatch(mzValues, magnitudes, g.MonoisotopicMass, null, mzTolerancePpm)))
                .ToList();
        }

        /// <summary>
        /// Cosine similarity, which compares the shape of two intensity patterns while
        /// ignoring their overall scale -- the right measure here, since absolute ion
        /// abundance is not predicted by the envelope model.
        /// </summary>
        private static double CosineSimilarity(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Count; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            if (normA <= 0 || normB <= 0)
            {
                return 0;
            }
            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}
