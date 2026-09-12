using System;
using System.Linq;
using FCWTNET;
using FCWTNET.Transform;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class ManagedMorletCwtTests
    {
        private const int Psoctave = 1;
        private const int Pendoctave = 6;
        private const int Pnbvoice = 32;
        private const float C0 = 6f;

        /// <summary>Index of the largest value in an array.</summary>
        private static int ArgMax(double[] values)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > values[best])
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Row-wise mean modulus of a CWT, i.e. energy per scale.</summary>
        private static double[] MeanModulusPerScale(double[][] real, double[][] imag)
        {
            double[] perScale = new double[real.Length];
            for (int j = 0; j < real.Length; j++)
            {
                double sum = 0;
                for (int i = 0; i < real[j].Length; i++)
                {
                    sum += Math.Sqrt(real[j][i] * real[j][i] + imag[j][i] * imag[j][i]);
                }
                perScale[j] = sum / real[j].Length;
            }
            return perScale;
        }

        [Test]
        public void ScaleGridMatchesOctaveVoiceSettings()
        {
            double[] scales = CwtScales.Scales(2, 4, 10);
            Assert.AreEqual(30, scales.Length);
            // First scale is 2^psoctave, and each octave doubles it.
            Assert.AreEqual(4.0, scales[0], 1e-12);
            Assert.AreEqual(8.0, scales[10], 1e-12);
            Assert.AreEqual(16.0, scales[20], 1e-12);
        }

        [Test]
        public void CentreFrequencyAxisIsAscendingAndWithinNyquist()
        {
            double[] frequencies = CwtScales.AscendingCentreFrequencies(Psoctave, Pendoctave, Pnbvoice, C0);
            Assert.AreEqual(CwtScales.ScaleCount(Psoctave, Pendoctave, Pnbvoice), frequencies.Length);
            for (int i = 1; i < frequencies.Length; i++)
            {
                Assert.Greater(frequencies[i], frequencies[i - 1], "frequency axis must ascend");
            }
            Assert.Greater(frequencies[0], 0);
            // c0 = 6 rad/sample at the smallest scale (2^1) gives 6/(2*pi*2) = 0.477 c/sample.
            Assert.LessOrEqual(frequencies[^1], 0.5, "axis must respect Nyquist");
        }

        [Test]
        public void FrequencyAndScaleConversionsRoundTrip()
        {
            double scale = CwtScales.ScaleForNormalizedFrequency(0.1, C0);
            Assert.AreEqual(0.1, CwtScales.NormalizedCentreFrequency(scale, C0), 1e-12);
        }

        [TestCase(0.05)]
        [TestCase(0.1)]
        [TestCase(0.2)]
        public void RecoversFrequencyOfAPureSinusoid(double normalizedFrequency)
        {
            int n = 4096;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Cos(2 * Math.PI * normalizedFrequency * i);
            }

            var backend = new ManagedMorletCwt();
            backend.Transform(signal, Psoctave, Pendoctave, Pnbvoice, C0, 0, false,
                out double[][] real, out double[][] imag);

            double[] perScale = MeanModulusPerScale(real, imag);
            int peakRow = ArgMax(perScale);

            // Rows come back in ascending scale order, so convert the winning row's scale
            // straight back to a frequency.
            double[] scales = CwtScales.Scales(Psoctave, Pendoctave, Pnbvoice);
            double recovered = CwtScales.NormalizedCentreFrequency(scales[peakRow], C0);

            // One voice step is 2^(1/32) = 2.2%, so allow a couple of voices of slack.
            Assert.AreEqual(normalizedFrequency, recovered, normalizedFrequency * 0.05,
                $"expected the ridge at {normalizedFrequency} c/sample, found {recovered}");
        }

        [Test]
        public void ModulusIsFlatForAStationarySinusoid()
        {
            int n = 4096;
            double frequency = 0.1;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Cos(2 * Math.PI * frequency * i);
            }

            var backend = new ManagedMorletCwt();
            backend.Transform(signal, Psoctave, Pendoctave, Pnbvoice, C0, 0, false,
                out double[][] real, out double[][] imag);

            double[] perScale = MeanModulusPerScale(real, imag);
            int peakRow = ArgMax(perScale);

            // Away from the edges the analytic envelope of a constant-amplitude tone
            // should itself be constant.
            double[] row = new double[n];
            for (int i = 0; i < n; i++)
            {
                row[i] = Math.Sqrt(real[peakRow][i] * real[peakRow][i] + imag[peakRow][i] * imag[peakRow][i]);
            }
            double[] interior = row.Skip(n / 4).Take(n / 2).ToArray();
            double mean = interior.Average();
            double maxDeviation = interior.Max(v => Math.Abs(v - mean));
            Assert.Less(maxDeviation / mean, 0.02, "envelope of a stationary tone should be flat");
        }

        /// <summary>
        /// Amplitude normalisation means the modulus at the ridge equals the amplitude of
        /// the tone, and the ridge sits at the tone's own frequency with no bias. An
        /// energy-normalised (sqrt(a)) wavelet would peak where a*w0 = (c0+sqrt(c0^2+2))/2,
        /// which at c0 = 6 is 1.4% off in scale and 2.8% off once converted to m/z.
        /// </summary>
        [TestCase(1.0)]
        [TestCase(3.5)]
        public void ModulusAtTheRidgeEqualsTheToneAmplitude(double amplitude)
        {
            int n = 8192;
            double frequency = 0.1;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = amplitude * Math.Cos(2 * Math.PI * frequency * i);
            }

            var backend = new ManagedMorletCwt();
            backend.Transform(signal, Psoctave, Pendoctave, Pnbvoice, C0, 0, false,
                out double[][] real, out double[][] imag);

            double[] perScale = MeanModulusPerScale(real, imag);
            int peakRow = ArgMax(perScale);

            // Measured mid-signal, away from edge effects.
            int centre = n / 2;
            double modulus = Math.Sqrt(
                real[peakRow][centre] * real[peakRow][centre] +
                imag[peakRow][centre] * imag[peakRow][centre]);
            Assert.AreEqual(amplitude, modulus, amplitude * 0.02);

            // And the ridge is where the tone actually is.
            double[] scales = CwtScales.Scales(Psoctave, Pendoctave, Pnbvoice);
            double recovered = CwtScales.NormalizedCentreFrequency(scales[peakRow], C0);
            Assert.AreEqual(frequency, recovered, frequency * 0.02);
        }

        [Test]
        public void TransformIsLinear()
        {
            int n = 1024;
            double[] a = new double[n];
            double[] b = new double[n];
            double[] sum = new double[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = Math.Cos(2 * Math.PI * 0.08 * i);
                b[i] = 0.5 * Math.Cos(2 * Math.PI * 0.17 * i + 1.1);
                sum[i] = a[i] + b[i];
            }

            var backend = new ManagedMorletCwt();
            backend.Transform(a, Psoctave, Pendoctave, Pnbvoice, C0, 0, false, out double[][] ra, out double[][] ia);
            backend.Transform(b, Psoctave, Pendoctave, Pnbvoice, C0, 0, false, out double[][] rb, out double[][] ib);
            backend.Transform(sum, Psoctave, Pendoctave, Pnbvoice, C0, 0, false, out double[][] rs, out double[][] iss);

            for (int j = 0; j < ra.Length; j += 17)
            {
                for (int i = 0; i < n; i += 97)
                {
                    Assert.AreEqual(ra[j][i] + rb[j][i], rs[j][i], 1e-9);
                    Assert.AreEqual(ia[j][i] + ib[j][i], iss[j][i], 1e-9);
                }
            }
        }

        [Test]
        public void ResolvesTwoSimultaneousTones()
        {
            int n = 8192;
            double lowFrequency = 0.05;
            double highFrequency = 0.2;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Cos(2 * Math.PI * lowFrequency * i)
                          + Math.Cos(2 * Math.PI * highFrequency * i);
            }

            var backend = new ManagedMorletCwt();
            backend.Transform(signal, Psoctave, Pendoctave, Pnbvoice, C0, 0, false,
                out double[][] real, out double[][] imag);

            double[] perScale = MeanModulusPerScale(real, imag);
            double[] scales = CwtScales.Scales(Psoctave, Pendoctave, Pnbvoice);

            // Both tones should appear as local maxima at their own scales.
            foreach (double expected in new[] { lowFrequency, highFrequency })
            {
                double targetScale = CwtScales.ScaleForNormalizedFrequency(expected, C0);
                int nearest = 0;
                for (int j = 1; j < scales.Length; j++)
                {
                    if (Math.Abs(scales[j] - targetScale) < Math.Abs(scales[nearest] - targetScale))
                    {
                        nearest = j;
                    }
                }
                // Within a two-voice window the expected row should dominate its neighbourhood.
                int lo = Math.Max(0, nearest - 2);
                int hi = Math.Min(scales.Length - 1, nearest + 2);
                double local = perScale.Skip(lo).Take(hi - lo + 1).Max();
                Assert.AreEqual(perScale[nearest], local, perScale[nearest] * 0.25,
                    $"expected a ridge near {expected} c/sample");
            }
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var backend = new ManagedMorletCwt();
            Assert.Throws<ArgumentNullException>(() => backend.Transform(
                null!, 1, 2, 4, C0, 0, false, out _, out _));
            Assert.Throws<ArgumentException>(() => backend.Transform(
                Array.Empty<double>(), 1, 2, 4, C0, 0, false, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => backend.Transform(
                new double[16], 1, 2, 4, 0f, 0, false, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => CwtScales.ScaleCount(1, 2, 0));
            Assert.Throws<ArgumentException>(() => CwtScales.ScaleCount(4, 2, 10));
        }

        [Test]
        public void CwtObjectUsesManagedBackendAndProducesMatchingAxisLength()
        {
            int n = 2048;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Cos(2 * Math.PI * 0.1 * i);
            }

            var cwt = new CWTObject(signal, Psoctave, Pendoctave, Pnbvoice, C0, 0, false, samplingRate: 1000)
            {
                Backend = CwtBackends.Managed
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();
            cwt.CalculateTimeAxis();

            Assert.AreEqual(cwt.OutputCWT!.Rows, cwt.FrequencyAxis!.Length);
            Assert.AreEqual(cwt.OutputCWT.Columns, cwt.TimeAxis!.Length);

            // PerformCWT reverses the rows, so the stored CWT runs low frequency first;
            // the ridge for a 0.1 c/sample tone must land where the axis says 0.1.
            double[] perScale = new double[cwt.OutputCWT.Rows];
            double[,] modulus = cwt.OutputCWT.ModulusCalculation();
            for (int j = 0; j < cwt.OutputCWT.Rows; j++)
            {
                double sum = 0;
                for (int i = 0; i < cwt.OutputCWT.Columns; i++)
                {
                    sum += modulus[j, i];
                }
                perScale[j] = sum / cwt.OutputCWT.Columns;
            }
            int peakRow = ArgMax(perScale);
            Assert.AreEqual(0.1, cwt.FrequencyAxis.WaveletCenterFrequencies![peakRow], 0.1 * 0.05);
        }
    }
}
