using System;
using System.Linq;
using FCWTNET;
using FCWTNET.Transform;
using FCWTNET.Transform.Wavelets;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class ScaleGridTests
    {
        private const double SampleCalibration = 2.11751487582899e14;
        private const int SampleRate = 4_000_000;

        private static IWavelet Morlet(double c0 = 80) => new MorletWavelet(c0);

        [Test]
        public void ForOctavesReproducesTheHistoricalGridExactly()
        {
            // The octave path must not drift from what CwtScales produced before, or every
            // existing result silently changes.
            var grid = ScaleGrid.ForOctaves(2, 5, 32, Morlet());
            double[] expected = CwtScales.Scales(2, 5, 32);
            Assert.AreEqual(expected.Length, grid.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], grid.Scales[i], 1e-12);
            }
        }

        [Test]
        public void FrequencyAxisIsAscendingAndMatchesTheScales()
        {
            var grid = ScaleGrid.ForOctaves(2, 5, 16, Morlet());
            Assert.AreEqual(grid.Count, grid.AscendingCentreFrequencies.Length);
            for (int i = 1; i < grid.Count; i++)
            {
                Assert.Greater(grid.AscendingCentreFrequencies[i], grid.AscendingCentreFrequencies[i - 1]);
            }
            // Index 0 of the axis is the largest scale.
            Assert.AreEqual(CwtScales.NormalizedCentreFrequency(grid.Scales[^1], grid.Wavelet),
                grid.AscendingCentreFrequencies[0], 1e-15);
        }

        [Test]
        public void FrequencyRangeGridCoversTheRequestedRange()
        {
            const double low = 0.02;
            const double high = 0.2;
            var grid = ScaleGrid.ForFrequencyRange(low, high, 64, Morlet());

            double lowest = grid.AscendingCentreFrequencies[0];
            double highest = grid.AscendingCentreFrequencies[^1];
            // Covered, not trimmed: the ends reach at least as far as asked.
            Assert.LessOrEqual(lowest, low * (1 + 1e-9));
            Assert.GreaterOrEqual(highest, high * (1 - 1e-9));
            Assert.IsFalse(grid.Clamps.Any);
        }

        [Test]
        public void FrequencyRangeGridDoesNotOvershootByMoreThanOneVoice()
        {
            const int voices = 64;
            var grid = ScaleGrid.ForFrequencyRange(0.02, 0.2, voices, Morlet());
            double voiceRatio = Math.Pow(2, 1.0 / voices);

            // The whole point is not paying for a wasted octave at each end.
            Assert.GreaterOrEqual(grid.AscendingCentreFrequencies[0], 0.02 / voiceRatio);
            Assert.LessOrEqual(grid.AscendingCentreFrequencies[^1], 0.2 * voiceRatio);
        }

        /// <summary>
        /// A native myoglobin envelope spans 0.29 octaves. The old integer-octave grid spent
        /// three; this is the saving the whole change exists for.
        /// </summary>
        [Test]
        public void BoundedGridIsFarCheaperThanWholeOctaves()
        {
            var bounded = ScaleGrid.ForMzRange(1413, 2119, SampleCalibration, SampleRate, 64, Morlet());
            var octaves = ScaleGrid.ForOctaves(6, 8, 64, Morlet());

            Assert.Less(bounded.Octaves, 0.4);
            Assert.Less(bounded.Count, octaves.Count / 5,
                $"expected a large saving; bounded {bounded.Count} vs {octaves.Count} scales");
        }

        [Test]
        public void MzRangeMapsLowMassToHighFrequency()
        {
            var grid = ScaleGrid.ForMzRange(500, 3000, SampleCalibration, SampleRate, 32, Morlet());

            double highestHz = grid.AscendingCentreFrequencies[^1] * SampleRate;
            double lowestHz = grid.AscendingCentreFrequencies[0] * SampleRate;
            // f = sqrt(A/(m/z)), so the *low* m/z is the *high* frequency.
            Assert.AreEqual(Math.Sqrt(SampleCalibration / 500), highestHz, highestHz * 0.02);
            Assert.AreEqual(Math.Sqrt(SampleCalibration / 3000), lowestHz, lowestHz * 0.02);
        }

        [Test]
        public void MzAxisRoundTripsThroughTheCalibration()
        {
            var grid = ScaleGrid.ForMzRange(800, 2000, SampleCalibration, SampleRate, 32, Morlet());
            double[] mz = grid.MzAxis(SampleCalibration, SampleRate);

            Assert.AreEqual(grid.Count, mz.Length);
            // m/z descends as frequency ascends.
            for (int i = 1; i < mz.Length; i++)
            {
                Assert.Less(mz[i], mz[i - 1]);
            }
            // The grid rounds out to whole voices so the requested range is covered, so
            // the ends can sit up to one voice beyond it. One voice is 2^(1/32) in
            // frequency, which is twice that in m/z since m/z goes as 1/f^2.
            double voiceInMz = Math.Pow(2, 2.0 / 32) - 1;
            Assert.AreEqual(2000, mz[0], 2000 * (voiceInMz + 0.001));
            Assert.AreEqual(800, mz[^1], 800 * (voiceInMz + 0.001));
            Assert.GreaterOrEqual(mz[0], 2000, "the requested range must be covered, not trimmed");
            Assert.LessOrEqual(mz[^1], 800);
        }

        [Test]
        public void NyquistClampIsAppliedAndReported()
        {
            // Ask for more than half the sampling rate.
            var grid = ScaleGrid.ForFrequencyRange(0.1, 0.9, 32, Morlet());

            Assert.IsTrue(grid.Clamps.NyquistClamped);
            Assert.AreEqual(ScaleGrid.NyquistNormalizedFrequency, grid.Clamps.AppliedMaxFrequency, 1e-12);
            Assert.LessOrEqual(grid.AscendingCentreFrequencies[^1],
                ScaleGrid.NyquistNormalizedFrequency * (1 + 1e-9));
            Assert.IsNotEmpty(grid.Clamps.Describe());
            StringAssert.Contains("Nyquist", grid.Clamps.Describe()[0]);
        }

        /// <summary>
        /// The guard that would have prevented the wrap-around seen at morlet:2000 and
        /// above: a wavelet longer than the transient cannot be computed honestly, because
        /// the FFT's circular convolution folds the end of the signal into its start.
        /// </summary>
        [Test]
        public void SignalLengthClampPreventsScalesLongerThanTheTransient()
        {
            const int signalLength = 32768;
            var grid = ScaleGrid.ForFrequencyRange(1e-6, 0.2, 32, Morlet(), signalLength);

            Assert.IsTrue(grid.Clamps.SignalLengthClamped);
            Assert.LessOrEqual(grid.Scales[^1],
                ScaleGrid.DefaultMaxScaleFraction * signalLength * (1 + 1e-9));
            StringAssert.Contains("transient length", string.Join(" ", grid.Clamps.Describe()));
        }

        [Test]
        public void SignalLengthClampIsConfigurable()
        {
            const int signalLength = 32768;
            var strict = ScaleGrid.ForFrequencyRange(1e-6, 0.2, 32, Morlet(), signalLength, 0.02);
            var loose = ScaleGrid.ForFrequencyRange(1e-6, 0.2, 32, Morlet(), signalLength, 0.25);
            Assert.Less(strict.Scales[^1], loose.Scales[^1]);
        }

        [Test]
        public void NoClampWhenTheRangeIsAlreadySafe()
        {
            var grid = ScaleGrid.ForFrequencyRange(0.05, 0.2, 32, Morlet(), 65536);
            Assert.IsFalse(grid.Clamps.Any);
            Assert.IsEmpty(grid.Clamps.Describe());
        }

        [Test]
        public void ThrowsWhenTheClampsLeaveNothingUsable()
        {
            // A very long wavelet over a very short signal has no room at all.
            var exception = Assert.Throws<ArgumentException>(() =>
                ScaleGrid.ForFrequencyRange(0.001, 0.002, 32, Morlet(8000), 512));
            StringAssert.Contains("No usable scales", exception!.Message);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ScaleGrid.ForFrequencyRange(0.01, 0.1, 32, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ScaleGrid.ForFrequencyRange(0, 0.1, 32, Morlet()));
            Assert.Throws<ArgumentException>(() =>
                ScaleGrid.ForFrequencyRange(0.2, 0.1, 32, Morlet()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ScaleGrid.ForFrequencyRange(0.01, 0.1, 0, Morlet()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ScaleGrid.ForMzRange(0, 2000, SampleCalibration, SampleRate, 32, Morlet()));
            Assert.Throws<ArgumentException>(() =>
                ScaleGrid.ForMzRange(2000, 1000, SampleCalibration, SampleRate, 32, Morlet()));
        }

        /// <summary>
        /// Each wavelet peaks at its own point in a*w, so the same frequency range needs a
        /// different band of scales for each. The grid must follow the wavelet, exactly as
        /// the frequency axis does.
        /// </summary>
        [TestCaseSource(typeof(ScaleGridTests), nameof(Wavelets))]
        public void GridFollowsTheWaveletsOwnCentreFrequency(IWavelet wavelet)
        {
            var grid = ScaleGrid.ForFrequencyRange(0.05, 0.2, 32, wavelet);
            Assert.AreEqual(CwtScales.ScaleForNormalizedFrequency(0.2, wavelet), grid.Scales[0],
                grid.Scales[0] * 1e-9, wavelet.Name);
            foreach (double frequency in grid.AscendingCentreFrequencies)
            {
                Assert.Greater(frequency, 0, wavelet.Name);
            }
        }

        public static System.Collections.IEnumerable Wavelets() => WaveletCatalog.Defaults;

        [Test]
        public void CwtObjectUsesTheGridForBothTransformAndAxis()
        {
            int n = 4096;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Cos(2 * Math.PI * 0.1 * i);
            }

            var grid = ScaleGrid.ForFrequencyRange(0.05, 0.2, 32, Morlet(20), n);
            var cwt = new CWTObject(signal, 2, 5, 32, 6f, 0, false, samplingRate: 1000)
            {
                ScaleGrid = grid
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();

            // Rows computed and rows labelled must agree, and both must come from the grid
            // rather than the octave arguments still sitting on the constructor.
            Assert.AreEqual(grid.Count, cwt.OutputCWT!.Rows);
            Assert.AreEqual(grid.Count, cwt.FrequencyAxis!.Length);
            Assert.AreEqual(grid.AscendingCentreFrequencies[0],
                cwt.FrequencyAxis.WaveletCenterFrequencies![0], 1e-15);
        }

        [Test]
        public void CwtObjectWithoutAGridStillUsesItsOctaves()
        {
            var cwt = new CWTObject(new double[256], 2, 4, 16, 6f, 0, false);
            Assert.AreEqual(CwtScales.ScaleCount(2, 4, 16), cwt.EffectiveScaleGrid.Count);
        }

        [Test]
        public void NativeBackendRefusesAnArbitraryGridRatherThanSilentlyChangingIt()
        {
            var grid = ScaleGrid.ForFrequencyRange(0.05, 0.2, 16, Morlet());
            Assert.Throws<NotSupportedException>(() =>
                CwtBackends.Native.Transform(new double[64], grid, 0, out _, out _));
        }

        [Test]
        public void ManagedBackendHonoursTheGridRowCount()
        {
            int n = 1024;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Cos(2 * Math.PI * 0.1 * i);
            }
            var grid = ScaleGrid.ForFrequencyRange(0.05, 0.2, 16, Morlet(20), n);
            CwtBackends.Managed.Transform(signal, grid, 0, out double[][] real, out double[][] imag);

            Assert.AreEqual(grid.Count, real.Length);
            Assert.AreEqual(grid.Count, imag.Length);
            Assert.AreEqual(n, real[0].Length);
        }
    }
}
