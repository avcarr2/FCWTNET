using System;
using System.Collections.Generic;
using System.Linq;
using FCWTNET;
using FCWTNET.Analysis;
using FCWTNET.Transform;
using FCWTNET.Transform.Wavelets;
using NUnit.Framework;

namespace TestFCWTAPI
{
    /// <summary>
    /// Locating components that are closer together than the analysing wavelet resolves.
    ///
    /// <para>
    /// Errors are quoted as mass ppm throughout, since m/z goes as 1/f^2 and a fractional
    /// frequency error doubles into the quantity anyone cares about.
    /// </para>
    /// </summary>
    public class MultiComponentFitTests
    {
        private const int Samples = 65536;
        private const double BaseFrequency = (7194.0 + 0.5) / Samples;

        private sealed class Scene
        {
            public double[,] Modulus = new double[0, 0];
            public ScaleGrid Grid = null!;
            public double[] FrequencyAxis = Array.Empty<double>();
            public double[] Power = Array.Empty<double>();
        }

        /// <summary>Transforms a sum of tones at the given fractional separations.</summary>
        /// <param name="span">
        /// Frequencies the scale grid must cover, when that has to match another scene's
        /// rather than follow from the tones present. Defaults to the tones themselves.
        /// </param>
        private static Scene Build(double c0, IReadOnlyList<double> frequencies,
            IReadOnlyList<double> amplitudes, double noiseAmplitude = 0, int seed = 7,
            IReadOnlyList<double>? span = null)
        {
            var random = new Random(seed);
            var samples = new double[Samples];
            for (int i = 0; i < Samples; i++)
            {
                double value = 0;
                for (int k = 0; k < frequencies.Count; k++)
                {
                    // A different phase per tone, so nothing depends on them starting aligned.
                    value += amplitudes[k] * Math.Cos(2 * Math.PI * frequencies[k] * i + 0.7 * k);
                }
                if (noiseAmplitude > 0)
                {
                    value += (random.NextDouble() * 2 - 1) * noiseAmplitude;
                }
                samples[i] = value;
            }

            var wavelet = new MorletWavelet(c0);
            IReadOnlyList<double> extent = span ?? frequencies;
            ScaleGrid grid = ScaleGrid.ForFrequencyRange(
                extent.Min() * 0.7, extent.Max() * 1.4, 64, wavelet, Samples);
            var cwt = new CWTObject(samples, 0, 1, 64, (float)c0, 0, false, 1, 1.0)
            {
                ScaleGrid = grid,
                Wavelet = wavelet
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();

            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            return new Scene
            {
                Modulus = modulus,
                Grid = grid,
                FrequencyAxis = cwt.FrequencyAxis.TrueFrequencies!,
                Power = MultiComponentFit.TimeAveragedPower(modulus)
            };
        }

        private static double MassPpm(double measured, double expected)
            => -2 * (measured - expected) / expected * 1e6;

        /// <summary>
        /// The point of the whole thing: two ions inside the wavelet's resolution produce
        /// one local maximum belonging to neither, and fitting the sum recovers both.
        /// </summary>
        [TestCase(20.0, 0.06)]   // resolves 11.8%, tones half that apart
        [TestCase(40.0, 0.03)]   // resolves 5.9%
        [TestCase(40.0, 0.015)]  // four times below the resolution
        public void RecoversTwoComponentsTheWaveletCannotResolve(double c0, double separation)
        {
            double f1 = BaseFrequency;
            double f2 = f1 * (1 + separation);
            Scene scene = Build(c0, new[] { f1, f2 }, new[] { 1.0, 0.8 });

            IReadOnlyList<DetectedPeak> maxima = RidgeExtraction.FindPeaks(
                scene.Modulus, scene.FrequencyAxis, 0.1, 1, scene.Grid);
            Assert.AreEqual(1, maxima.Count,
                "this test is only meaningful while the two tones merge into one maximum");

            MultiComponentFitResult fit = MultiComponentFit.FitWithSplitting(
                scene.Power, scene.Grid, maxima.Select(p => p.Position).ToList());
            List<FittedComponent> found = fit.Components.Where(c => c.Power > 0)
                .OrderBy(c => c.NormalisedFrequency).ToList();

            Assert.GreaterOrEqual(found.Count, 2, "expected the blend to be split");
            FittedComponent lower = found.OrderBy(c => Math.Abs(c.NormalisedFrequency - f1)).First();
            FittedComponent upper = found.OrderBy(c => Math.Abs(c.NormalisedFrequency - f2)).First();

            double lowerPpm = MassPpm(lower.NormalisedFrequency, f1);
            double upperPpm = MassPpm(upper.NormalisedFrequency, f2);
            TestContext.WriteLine($"c0 {c0}, {separation:P1} apart: {lowerPpm:F0} / {upperPpm:F0} ppm " +
                                  $"from one maximum out by " +
                                  $"{MassPpm(maxima[0].Position, f1):F0} ppm");

            Assert.AreEqual(0, lowerPpm, 100);
            Assert.AreEqual(0, upperPpm, 100);
            // The stronger tone should come back stronger.
            Assert.Greater(lower.Amplitude, upper.Amplitude);
        }

        /// <summary>
        /// The claim that this is what lets c0 come down: the single maximum is out by
        /// orders of magnitude more than the fit.
        /// </summary>
        [Test]
        public void FittingBeatsTheLocalMaximumByOrdersOfMagnitude()
        {
            double f1 = BaseFrequency;
            double f2 = f1 * 1.03;
            Scene scene = Build(40, new[] { f1, f2 }, new[] { 1.0, 0.8 });

            IReadOnlyList<DetectedPeak> maxima = RidgeExtraction.FindPeaks(
                scene.Modulus, scene.FrequencyAxis, 0.1, 1, scene.Grid);
            double maximumError = Math.Abs(MassPpm(maxima[0].Position, f1));

            MultiComponentFitResult fit = MultiComponentFit.Fit(
                scene.Power, scene.Grid, new[] { f1 * 0.995, f2 * 1.005 });
            double fitError = Math.Abs(MassPpm(
                fit.Components.OrderBy(c => c.NormalisedFrequency).First().NormalisedFrequency, f1));

            TestContext.WriteLine($"local maximum {maximumError:F0} ppm, fit {fitError:F0} ppm");
            Assert.Greater(maximumError, 5000);
            Assert.Less(fitError, 100);
        }

        /// <summary>
        /// Well-separated tones must not be made worse. The fit has to agree with the
        /// maxima where the maxima were right all along.
        /// </summary>
        [Test]
        public void LeavesWellSeparatedComponentsAlone()
        {
            double f1 = BaseFrequency;
            double f2 = f1 * 1.3;
            Scene scene = Build(40, new[] { f1, f2 }, new[] { 1.0, 0.6 });

            MultiComponentFitResult fit = MultiComponentFit.Fit(
                scene.Power, scene.Grid, new[] { f1, f2 });
            List<FittedComponent> found = fit.Components.OrderBy(c => c.NormalisedFrequency).ToList();

            Assert.AreEqual(0, MassPpm(found[0].NormalisedFrequency, f1), 5);
            Assert.AreEqual(0, MassPpm(found[1].NormalisedFrequency, f2), 5);
            Assert.Less(fit.RelativeResidual, 1e-6);
        }

        /// <summary>
        /// Splitting must not invent an ion. A single clean tone is already explained, so
        /// there is no residual for a second component to take and the improvement test
        /// should refuse it.
        /// </summary>
        [Test]
        public void DoesNotSplitASingleCleanTone()
        {
            Scene scene = Build(40, new[] { BaseFrequency }, new[] { 1.0 });

            MultiComponentFitResult fit = MultiComponentFit.FitWithSplitting(
                scene.Power, scene.Grid, new[] { BaseFrequency });

            TestContext.WriteLine(fit.ToString());
            Assert.AreEqual(1, fit.Components.Count(c => c.Power > 0));
            Assert.AreEqual(0, MassPpm(fit.Components[0].NormalisedFrequency, BaseFrequency), 5);
        }

        /// <summary>
        /// And it has to survive noise, since a real transient is not noiseless. The
        /// tolerance here is loose on purpose: this is the regime where the model-selection
        /// decision starts to be a judgement rather than a fact.
        /// </summary>
        [Test]
        public void StillSplitsABlendUnderNoise()
        {
            double f1 = BaseFrequency;
            double f2 = f1 * 1.03;
            Scene scene = Build(40, new[] { f1, f2 }, new[] { 1.0, 0.8 }, noiseAmplitude: 0.15);

            MultiComponentFitResult fit = MultiComponentFit.FitWithSplitting(
                scene.Power, scene.Grid, new[] { f1 * 1.01 });
            List<FittedComponent> found = fit.Components.Where(c => c.Power > 0)
                .OrderBy(c => c.NormalisedFrequency).ToList();

            Assert.GreaterOrEqual(found.Count, 2);
            Assert.AreEqual(0, MassPpm(found.OrderBy(c => Math.Abs(c.NormalisedFrequency - f1))
                .First().NormalisedFrequency, f1), 500);
        }

        /// <summary>
        /// Why the fit is on squared modulus. The CWT is linear, so the moduli of two
        /// components do not add -- their sum beats in time -- but the mean squared modulus
        /// does, because the cross terms oscillate at the difference frequency and average
        /// away. This is what makes the model exactly a sum of responses.
        /// </summary>
        [Test]
        public void MeanPowerAddsWhereMeanModulusDoesNot()
        {
            double f1 = BaseFrequency;
            double f2 = f1 * 1.3;
            // All three over the same frequency range, or the grids differ and the rows
            // are not comparable.
            var span = new[] { f1, f2 };
            Scene first = Build(40, new[] { f1 }, new[] { 1.0 }, span: span);
            Scene second = Build(40, new[] { f2 }, new[] { 1.0 }, span: span);
            Scene both = Build(40, new[] { f1, f2 }, new[] { 1.0, 1.0 }, span: span);

            // At the row carrying the lower tone, where the two overlap least.
            int row = Enumerable.Range(0, first.FrequencyAxis.Length)
                .OrderBy(i => Math.Abs(first.FrequencyAxis[i] - f1))
                .First();
            double sumOfPowers = first.Power[row] + second.Power[row];
            Assert.AreEqual(sumOfPowers, both.Power[row], sumOfPowers * 0.02,
                "mean power should be additive");
        }

        [Test]
        public void RefinePeaksReturnsOnePeakPerFittedComponent()
        {
            double f1 = BaseFrequency;
            double f2 = f1 * 1.3;
            Scene scene = Build(40, new[] { f1, f2 }, new[] { 1.0, 0.6 });

            IReadOnlyList<DetectedPeak> maxima = RidgeExtraction.FindPeaks(
                scene.Modulus, scene.FrequencyAxis, 0.1, 1, scene.Grid);
            IReadOnlyList<DetectedPeak> refined = MultiComponentFit.RefinePeaks(
                maxima, scene.Modulus, scene.Grid, scene.FrequencyAxis);

            Assert.AreEqual(2, refined.Count);
            var positions = refined.Select(p => p.Position).OrderBy(p => p).ToArray();
            Assert.AreEqual(0, MassPpm(positions[0], f1), 5);
            Assert.AreEqual(0, MassPpm(positions[1], f2), 5);
            Assert.IsFalse(refined.Any(p => double.IsNaN(p.FitResidual)));
        }

        /// <summary>
        /// The measurement that motivated the feature: on the denatured myoglobin envelope
        /// the charge states sit 3% apart against a 2.9% resolution, and refining collapses
        /// the spread of the per-peak mass estimates.
        /// </summary>
        [Test]
        public void TightensMyoglobinsChargeStatesAtAModestC0()
        {
            const double Calibration = FCWTNET.Simulation.Presets.SampleCalibrationCoefficient;
            FCWTNET.Simulation.SimulationResult simulated =
                FCWTNET.Simulation.Presets.Get("myoglobin-denatured").Run();

            var wavelet = new MorletWavelet(80);
            ScaleGrid grid = ScaleGrid.ForMzRange(600, 2300, Calibration, simulated.SamplingRate,
                64, wavelet, simulated.Samples.Length);
            var cwt = new CWTObject(simulated.Samples, 0, 1, 64, 80f, 0, false,
                simulated.SamplingRate, Calibration)
            {
                ScaleGrid = grid,
                Wavelet = wavelet
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();
            cwt.FrequencyAxis.CalculateMZValues();

            double[,] modulus = cwt.OutputCWT!.ModulusCalculation();
            int separation = new CwtSettings { Voices = 64, Wavelet = wavelet }.EffectivePeakSeparation();
            IReadOnlyList<DetectedPeak> maxima = RidgeExtraction.FindPeaks(
                modulus, cwt.FrequencyAxis.MZValues!, 0.1, separation, grid);
            IReadOnlyList<DetectedPeak> refined = MultiComponentFit.RefinePeaks(
                maxima, modulus, grid, cwt.FrequencyAxis.MZValues!);

            double Scatter(IReadOnlyList<DetectedPeak> peaks) =>
                ChargeStateDeconvolution.Deconvolve(
                        peaks.Select(p => p.Position).ToList(),
                        peaks.Select(p => p.Magnitude).ToList())
                    .OrderByDescending(g => g.Peaks.Count)
                    .First().MassUncertaintyPpm;

            double before = Scatter(maxima);
            double after = Scatter(refined);
            TestContext.WriteLine($"per-peak mass scatter {before:F0} ppm -> {after:F0} ppm");
            Assert.Less(after, before / 3, "refining should tighten the charge states substantially");
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var wavelet = new MorletWavelet(6);
            ScaleGrid grid = ScaleGrid.ForOctaves(1, 2, 4, wavelet);
            var power = new double[grid.Count];
            var seeds = new[] { 0.1 };

            Assert.Throws<ArgumentNullException>(() => MultiComponentFit.Fit(null!, grid, seeds));
            Assert.Throws<ArgumentNullException>(() => MultiComponentFit.Fit(power, null!, seeds));
            Assert.Throws<ArgumentNullException>(() => MultiComponentFit.Fit(power, grid, null!));
            Assert.Throws<ArgumentException>(() =>
                MultiComponentFit.Fit(new double[3], grid, seeds));
            Assert.Throws<ArgumentException>(() =>
                MultiComponentFit.Fit(power, grid, new[] { 0.0 }));
            Assert.Throws<ArgumentException>(() =>
                MultiComponentFit.Fit(power, grid, seeds, new MultiComponentFitOptions { MaxSweeps = 0 }));
            Assert.Throws<ArgumentException>(() => MultiComponentFit.FitWithSplitting(
                power, grid, seeds, new MultiComponentFitOptions { SplitImprovementFactor = 1 }));
            Assert.Throws<ArgumentNullException>(() =>
                MultiComponentFit.TimeAveragedPower(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MultiComponentFit.TimeAveragedPower(new double[2, 8], 0.7));

            // No components is not an error; it is an empty answer.
            Assert.IsEmpty(MultiComponentFit.Fit(power, grid, Array.Empty<double>()).Components);
        }
    }
}
