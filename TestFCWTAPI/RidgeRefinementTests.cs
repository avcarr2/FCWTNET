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
    /// How accurately a ridge's position can be read off the scale axis.
    ///
    /// <para>
    /// These use a synthetic tone rather than the simulator, deliberately: the quantity
    /// under test is a property of the transform and the peak finder, and anything the
    /// simulator adds -- decay, noise, isotopes -- would only obscure it. m/z goes as
    /// 1/f^2, so every frequency error here is doubled into the mass error that matters.
    /// </para>
    /// </summary>
    public class RidgeRefinementTests
    {
        private const int Samples = 65536;

        /// <summary>
        /// A tone halfway between two FFT bins: the worst case for leakage, and the one
        /// that exposes both of the biases these tests are about.
        /// </summary>
        private const double OffBinFrequency = (7194.0 + 0.5) / Samples;

        /// <summary>Exactly on a bin, where leakage vanishes.</summary>
        private const double OnBinFrequency = 7194.0 / Samples;

        private static (double[,] Modulus, ScaleGrid Grid, double[] FrequencyAxis) Transform(
            double normalisedFrequency, IWavelet wavelet, int voices)
        {
            var samples = new double[Samples];
            for (int i = 0; i < Samples; i++)
            {
                samples[i] = Math.Cos(2 * Math.PI * normalisedFrequency * i);
            }

            ScaleGrid grid = ScaleGrid.ForFrequencyRange(
                normalisedFrequency * 0.8, normalisedFrequency * 1.25, voices, wavelet, Samples);
            var cwt = new CWTObject(samples, 0, 1, voices, 6f, 0, false, 1, 1.0)
            {
                ScaleGrid = grid,
                Wavelet = wavelet
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.FrequencyAxis!.CalculateTrueFrequencies();
            return (cwt.OutputCWT!.ModulusCalculation(), grid, cwt.FrequencyAxis.TrueFrequencies!);
        }

        /// <summary>Mass error in ppm implied by the recovered frequency.</summary>
        private static double MassErrorPpm(double[,] modulus, double[] frequencyAxis,
            double trueFrequency, ScaleGrid? grid, double edgeFraction)
        {
            IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                modulus, frequencyAxis, relativeThreshold: 0.3, minimumRowSeparation: 1,
                scaleGrid: grid, edgeFraction: edgeFraction);
            Assert.IsNotEmpty(peaks);
            double found = peaks.OrderByDescending(p => p.Magnitude).First().Position;
            // m/z = A/f^2, so a fractional frequency error doubles into the mass.
            return -2 * (found - trueFrequency) / trueFrequency * 1e6;
        }

        /// <summary>
        /// The claim that motivated the change: fitting the wavelet's own response makes
        /// the answer independent of how finely the scale axis was sampled, where a
        /// parabola's error is proportional to the row spacing.
        /// </summary>
        [TestCase(16)]
        [TestCase(48)]
        [TestCase(128)]
        public void RefinedPositionDoesNotDependOnVoicesPerOctave(int voices)
        {
            var wavelet = new MorletWavelet(6);
            (double[,] modulus, ScaleGrid grid, double[] axis) =
                Transform(OffBinFrequency, wavelet, voices);

            double refined = MassErrorPpm(modulus, axis, OffBinFrequency, grid,
                RidgeExtraction.DefaultEdgeFraction);

            Assert.AreEqual(0, refined, 1.0,
                $"{voices} voices/octave should make no difference; got {refined:F2} ppm");
        }

        /// <summary>
        /// And the comparison that says the change was worth making. At a coarse grid the
        /// parabola is out by a part in a thousand, which on a 17 kDa protein is 25 Da.
        /// </summary>
        [Test]
        public void ParabolaIsFarWorseThanTheWaveletFitOnACoarseGrid()
        {
            var wavelet = new MorletWavelet(6);
            (double[,] modulus, ScaleGrid grid, double[] axis) = Transform(OffBinFrequency, wavelet, 16);

            double parabola = Math.Abs(MassErrorPpm(modulus, axis, OffBinFrequency, null,
                RidgeExtraction.DefaultEdgeFraction));
            double fitted = Math.Abs(MassErrorPpm(modulus, axis, OffBinFrequency, grid,
                RidgeExtraction.DefaultEdgeFraction));

            TestContext.WriteLine($"parabola {parabola:F1} ppm, wavelet fit {fitted:F2} ppm");
            Assert.Greater(parabola, 500);
            Assert.Less(fitted, 1.0);
        }

        /// <summary>
        /// The fit asks the wavelet for its response rather than assuming a Gaussian, so it
        /// has to hold for wavelets whose response is nothing like one -- the Paul's is
        /// strongly asymmetric, and a parabola has no chance on it either.
        /// </summary>
        [Test]
        public void RefinementHoldsForEveryWaveletInTheCatalogue()
        {
            foreach (IWavelet wavelet in WaveletCatalog.Defaults)
            {
                (double[,] modulus, ScaleGrid grid, double[] axis) =
                    Transform(OffBinFrequency, wavelet, 16);
                double error = MassErrorPpm(modulus, axis, OffBinFrequency, grid,
                    RidgeExtraction.DefaultEdgeFraction);
                TestContext.WriteLine($"{wavelet.Name}: {error:F2} ppm");
                Assert.AreEqual(0, error, 1.0, wavelet.Name);
            }
        }

        /// <summary>
        /// The second bias, isolated. A tone sitting exactly on an FFT bin has no leakage
        /// and is unaffected by averaging; one between bins is not, and the difference is
        /// what the edge trim exists to remove.
        /// </summary>
        [TestCase(6.0)]
        [TestCase(20.0)]
        [TestCase(80.0)]
        public void TrimmingTheEndsRemovesTheLeakageBias(double c0)
        {
            var wavelet = new MorletWavelet(c0);
            (double[,] onBin, ScaleGrid onGrid, double[] onAxis) = Transform(OnBinFrequency, wavelet, 32);
            (double[,] offBin, ScaleGrid offGrid, double[] offAxis) = Transform(OffBinFrequency, wavelet, 32);

            // On a bin there is nothing to remove, trimmed or not.
            Assert.AreEqual(0, MassErrorPpm(onBin, onAxis, OnBinFrequency, onGrid, 0), 1.0);

            double untrimmed = MassErrorPpm(offBin, offAxis, OffBinFrequency, offGrid, 0);
            double trimmed = MassErrorPpm(offBin, offAxis, OffBinFrequency, offGrid,
                RidgeExtraction.DefaultEdgeFraction);

            TestContext.WriteLine($"c0 {c0}: untrimmed {untrimmed:F2} ppm, trimmed {trimmed:F2} ppm");
            Assert.Greater(Math.Abs(untrimmed), 5, "there should be a bias to remove");
            Assert.AreEqual(0, trimmed, 1.0);
        }

        /// <summary>Both fixes together, which is the number that matters.</summary>
        [Test]
        public void AnIsolatedToneIsRecoveredExactly()
        {
            foreach (double c0 in new[] { 6.0, 20.0, 80.0 })
            {
                foreach (int voices in new[] { 16, 64 })
                {
                    (double[,] modulus, ScaleGrid grid, double[] axis) =
                        Transform(OffBinFrequency, new MorletWavelet(c0), voices);
                    double error = MassErrorPpm(modulus, axis, OffBinFrequency, grid,
                        RidgeExtraction.DefaultEdgeFraction);
                    Assert.AreEqual(0, error, 1.0, $"c0 {c0}, {voices} voices: {error:F2} ppm");
                }
            }
        }

        /// <summary>
        /// The residual is what says whether the position can be trusted. An isolated tone
        /// fits its wavelet to numerical precision; two tones the wavelet cannot resolve
        /// fit far worse, because a blend is not the shape of one response.
        /// </summary>
        [Test]
        public void FitResidualSeparatesAnIsolatedToneFromABlendedPair()
        {
            var wavelet = new MorletWavelet(20);   // resolves about 12% apart
            ScaleGrid grid = ScaleGrid.ForFrequencyRange(
                OffBinFrequency * 0.8, OffBinFrequency * 1.25, 32, wavelet, Samples);

            double Residual(Func<int, double> signal)
            {
                var samples = new double[Samples];
                for (int i = 0; i < Samples; i++)
                {
                    samples[i] = signal(i);
                }
                var cwt = new CWTObject(samples, 0, 1, 32, 20f, 0, false, 1, 1.0)
                {
                    ScaleGrid = grid,
                    Wavelet = wavelet
                };
                cwt.PerformCWT();
                cwt.CalculateFrequencyAxis();
                cwt.FrequencyAxis!.CalculateTrueFrequencies();
                IReadOnlyList<DetectedPeak> peaks = RidgeExtraction.FindPeaks(
                    cwt.OutputCWT!.ModulusCalculation(), cwt.FrequencyAxis.TrueFrequencies!,
                    relativeThreshold: 0.3, minimumRowSeparation: 1, scaleGrid: grid);
                return peaks.OrderByDescending(p => p.Magnitude).First().FitResidual;
            }

            double alone = Residual(i => Math.Cos(2 * Math.PI * OffBinFrequency * i));
            // A second tone 4% away, well inside this wavelet's 12% resolution, so the two
            // merge into a single ridge that belongs to neither.
            double blended = Residual(i => Math.Cos(2 * Math.PI * OffBinFrequency * i)
                                           + 0.8 * Math.Cos(2 * Math.PI * OffBinFrequency * 1.04 * i));

            TestContext.WriteLine($"isolated {alone:E2}, blended {blended:E2}");
            Assert.Less(alone, 1e-6, "an isolated tone is exactly the wavelet's own shape");
            Assert.Greater(blended, alone * 100, "a blend should be visibly worse");
        }

        [Test]
        public void FitResidualIsNotSetWithoutAScaleGrid()
        {
            var axis = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            IReadOnlyList<DetectedPeak> peaks =
                RidgeExtraction.FindPeaks(new[] { 0.0, 1.0, 2.0, 1.0, 0.0 }, axis);
            Assert.IsNaN(peaks[0].FitResidual);
        }

        [Test]
        public void EdgeFractionOfZeroAveragesTheWholeWindow()
        {
            var modulus = new double[2, 100];
            for (int t = 0; t < 100; t++)
            {
                modulus[0, t] = t;
                modulus[1, t] = 1;
            }

            double[] all = RidgeExtraction.TimeAveragedMagnitude(modulus, 0);
            Assert.AreEqual(49.5, all[0], 1e-12);

            // Trimming 10% of each end drops t < 10 and t >= 90, leaving 10..89.
            double[] trimmed = RidgeExtraction.TimeAveragedMagnitude(modulus, 0.1);
            Assert.AreEqual(49.5, trimmed[0], 1e-12, "a symmetric ramp has the same mean either way");
            Assert.AreEqual(1.0, trimmed[1], 1e-12);
        }

        [Test]
        public void TooFewColumnsToTrimFallsBackToTheWholeWindow()
        {
            var modulus = new double[1, 3];
            modulus[0, 0] = 1;
            modulus[0, 1] = 2;
            modulus[0, 2] = 3;

            // 40% of 3 columns is 1 at each end, leaving 1 -- fine. Push it further and
            // there is nothing left, at which point averaging everything beats averaging
            // nothing.
            Assert.AreEqual(2.0, RidgeExtraction.TimeAveragedMagnitude(modulus, 0.49)[0], 1e-12);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var modulus = new double[2, 8];
            var wavelet = new MorletWavelet(6);
            ScaleGrid grid = ScaleGrid.ForOctaves(1, 2, 4, wavelet);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RidgeExtraction.TimeAveragedMagnitude(modulus, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RidgeExtraction.TimeAveragedMagnitude(modulus, 0.5));
            Assert.Throws<ArgumentNullException>(() =>
                RidgeExtraction.RefineAgainstWavelet(null!, 1, grid));
            Assert.Throws<ArgumentNullException>(() =>
                RidgeExtraction.RefineAgainstWavelet(new double[5], 1, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RidgeExtraction.RefineAgainstWavelet(new double[5], 0, grid));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RidgeExtraction.RefineAgainstWavelet(new double[5], 4, grid));

            // A grid that does not describe this profile is a caller error, not something
            // to silently index past.
            Assert.Throws<ArgumentException>(() =>
                RidgeExtraction.FindPeaks(new double[5], new double[5], 0.05, 1, grid));
        }

        /// <summary>
        /// A log fit needs positive samples. Where they are not positive the parabola is
        /// still available, and falling back to it beats returning nothing.
        /// </summary>
        [Test]
        public void FallsBackToTheParabolaWhenMagnitudesAreNotPositive()
        {
            var wavelet = new MorletWavelet(6);
            ScaleGrid grid = ScaleGrid.ForOctaves(1, 2, 4, wavelet);
            var magnitude = new double[grid.Count];
            int peak = grid.Count / 2;
            magnitude[peak - 1] = 0;
            magnitude[peak] = 1;
            magnitude[peak + 1] = 0;

            double offset = RidgeExtraction.RefineAgainstWavelet(magnitude, peak, grid);
            Assert.AreEqual(0, offset, 1e-12, "a symmetric triple has its peak on the sample");
        }
    }
}
