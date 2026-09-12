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
    public class WaveletTests
    {
        /// <summary>Every wavelet at its default settings.</summary>
        private static IEnumerable<IWavelet> AllWavelets() => WaveletCatalog.Defaults;

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

        [TestCaseSource(nameof(AllWavelets))]
        public void ResponseIsZeroAtAndBelowZeroFrequency(IWavelet wavelet)
        {
            // Analytic wavelets must have no negative-frequency content; that is what makes
            // the transform complex and its modulus an envelope.
            Assert.AreEqual(0, wavelet.Response(0), 1e-12, wavelet.Name);
            Assert.AreEqual(0, wavelet.Response(-1), 1e-12, wavelet.Name);
            Assert.AreEqual(0, wavelet.Response(-100), 1e-12, wavelet.Name);
        }

        [TestCaseSource(nameof(AllWavelets))]
        public void ResponsePeaksAtTheDeclaredCentreFrequencyWithValueOne(IWavelet wavelet)
        {
            double centre = wavelet.CentreAngularFrequency;
            Assert.Greater(centre, 0, wavelet.Name);

            // Normalised to 1 at the peak, which is what lets the backend's amplitude
            // scaling be wavelet-independent.
            Assert.AreEqual(1.0, wavelet.Response(centre), 1e-9, wavelet.Name);

            // And it really is the maximum, not just a point where the value is 1.
            for (double x = centre * 0.05; x < centre * 5; x += centre * 0.01)
            {
                Assert.LessOrEqual(wavelet.Response(x), 1.0 + 1e-9,
                    $"{wavelet.Name}: response exceeds 1 at {x}");
            }
        }

        [TestCaseSource(nameof(AllWavelets))]
        public void ResponseDecaysAwayFromTheCentre(IWavelet wavelet)
        {
            double centre = wavelet.CentreAngularFrequency;
            Assert.Less(wavelet.Response(centre * 0.2), wavelet.Response(centre), wavelet.Name);
            Assert.Less(wavelet.Response(centre * 4), wavelet.Response(centre), wavelet.Name);
        }

        [TestCaseSource(nameof(AllWavelets))]
        public void RelativeFrequencyResolutionIsPositiveAndFinite(IWavelet wavelet)
        {
            double resolution = wavelet.RelativeFrequencyResolution;
            Assert.Greater(resolution, 0, wavelet.Name);
            Assert.Less(resolution, 100, wavelet.Name);
        }

        /// <summary>
        /// The numerically derived FWHM must agree with the Morlet's closed form, which is
        /// the check that the base class's bisection is right for all the others too.
        /// </summary>
        [TestCase(6.0)]
        [TestCase(20.0)]
        [TestCase(80.0)]
        public void MorletResolutionMatchesItsClosedForm(double c0)
        {
            var morlet = new MorletWavelet(c0);
            Assert.AreEqual(morlet.ExactRelativeFrequencyResolution,
                morlet.RelativeFrequencyResolution, 1e-6);
            // And the old CwtScales helper still agrees.
            Assert.AreEqual(CwtScales.RelativeFrequencyResolution(c0),
                morlet.RelativeFrequencyResolution, 1e-6);
        }

        [Test]
        public void RaisingMorletC0SharpensResolution()
        {
            Assert.Greater(new MorletWavelet(6).RelativeFrequencyResolution,
                new MorletWavelet(80).RelativeFrequencyResolution);
        }

        /// <summary>
        /// Paul's resolution improves only as 1/sqrt(order), which is why it is a poor
        /// choice for separating charge states.
        /// </summary>
        [Test]
        public void PaulResolutionImprovesSlowlyWithOrder()
        {
            double low = new PaulWavelet(4).RelativeFrequencyResolution;
            double high = new PaulWavelet(16).RelativeFrequencyResolution;
            Assert.Less(high, low);
            // Quadrupling the order should roughly halve the width.
            Assert.AreEqual(0.5, high / low, 0.15);
        }

        [Test]
        public void BumpHasCompactSupport()
        {
            var bump = new BumpWavelet(centre: 5, width: 0.6);
            // Exactly zero outside the band, which is the point of it.
            Assert.AreEqual(0, bump.Response(5 - 0.6), 1e-12);
            Assert.AreEqual(0, bump.Response(5 + 0.6), 1e-12);
            Assert.AreEqual(0, bump.Response(20), 1e-12);
            Assert.Greater(bump.Response(5), 0);
        }

        [Test]
        public void LogGaborIsSymmetricInLogFrequency()
        {
            var wavelet = new LogGaborWavelet(centre: 6, bandwidth: 1.2);
            // Equal ratios either side of the centre give equal response.
            Assert.AreEqual(wavelet.Response(6 * 1.3), wavelet.Response(6 / 1.3), 1e-12);
            Assert.AreEqual(wavelet.Response(6 * 2.0), wavelet.Response(6 / 2.0), 1e-12);
        }

        [Test]
        public void DogCentreMatchesTheSquareRootOfItsOrder()
        {
            Assert.AreEqual(Math.Sqrt(2), new DerivativeOfGaussianWavelet(2).CentreAngularFrequency, 1e-12);
            Assert.AreEqual(4, new DerivativeOfGaussianWavelet(16).CentreAngularFrequency, 1e-12);
        }

        [Test]
        public void WaveletsRejectInvalidParameters()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MorletWavelet(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PaulWavelet(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BumpWavelet(centre: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BumpWavelet(width: 0));
            // A band wider than its centre would reach zero frequency.
            Assert.Throws<ArgumentException>(() => new BumpWavelet(centre: 2, width: 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogGaborWavelet(bandwidth: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DerivativeOfGaussianWavelet(0));
        }

        [Test]
        public void CatalogParsesNamesAndParameters()
        {
            Assert.IsInstanceOf<MorletWavelet>(WaveletCatalog.Parse("morlet"));
            Assert.AreEqual(80, ((MorletWavelet)WaveletCatalog.Parse("morlet:80")).C0, 1e-12);
            Assert.AreEqual(8, ((PaulWavelet)WaveletCatalog.Parse("paul:8")).Order);
            var bump = (BumpWavelet)WaveletCatalog.Parse("bump:5,0.4");
            Assert.AreEqual(5, bump.Centre, 1e-12);
            Assert.AreEqual(0.4, bump.Width, 1e-12);
            var gabor = (LogGaborWavelet)WaveletCatalog.Parse("log-gabor:6,1.1");
            Assert.AreEqual(1.1, gabor.Bandwidth, 1e-12);
            Assert.AreEqual(4, ((DerivativeOfGaussianWavelet)WaveletCatalog.Parse("dog:4")).Order);
            // Aliases and whitespace.
            Assert.IsInstanceOf<DerivativeOfGaussianWavelet>(WaveletCatalog.Parse("mexican-hat"));
            Assert.IsInstanceOf<LogGaborWavelet>(WaveletCatalog.Parse("loggabor"));
            Assert.IsInstanceOf<MorletWavelet>(WaveletCatalog.Parse("  MORLET  "));
            // Empty falls back to the default.
            Assert.IsInstanceOf<MorletWavelet>(WaveletCatalog.Parse(""));
        }

        [Test]
        public void CatalogRejectsBadSpecifications()
        {
            var unknown = Assert.Throws<ArgumentException>(() => WaveletCatalog.Parse("haar"));
            StringAssert.Contains("morlet", unknown!.Message);
            Assert.Throws<ArgumentException>(() => WaveletCatalog.Parse("morlet:abc"));
            Assert.Throws<ArgumentException>(() => WaveletCatalog.Parse("morlet:1,2"));
            Assert.Throws<ArgumentException>(() => WaveletCatalog.Parse("paul:2.5"));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveletCatalog.Parse("morlet:-1"));
        }

        [Test]
        public void CatalogNamesAndDefaultsAgree()
        {
            CollectionAssert.AreEquivalent(
                WaveletCatalog.Names,
                WaveletCatalog.Defaults.Select(w => w.Name).ToList());
            foreach (string name in WaveletCatalog.Names)
            {
                Assert.DoesNotThrow(() => WaveletCatalog.Parse(name), name);
            }
        }

        /// <summary>
        /// The axis has to follow the wavelet. Each wavelet peaks at its own point on the
        /// scale grid, so building the frequency axis from a hardcoded Morlet relation
        /// would silently mislabel every row for the others.
        /// </summary>
        [TestCaseSource(nameof(AllWavelets))]
        public void FrequencyAxisFollowsTheWaveletsOwnCentreFrequency(IWavelet wavelet)
        {
            double[] axis = CwtScales.AscendingCentreFrequencies(2, 5, 16, wavelet);
            double[] scales = CwtScales.Scales(2, 5, 16);

            Assert.AreEqual(scales.Length, axis.Length, wavelet.Name);
            // Index 0 is the largest scale, i.e. the lowest frequency.
            Assert.AreEqual(wavelet.CentreAngularFrequency / (2 * Math.PI * scales[^1]), axis[0], 1e-12,
                wavelet.Name);
            for (int i = 1; i < axis.Length; i++)
            {
                Assert.Greater(axis[i], axis[i - 1], wavelet.Name);
            }
        }

        [TestCaseSource(nameof(AllWavelets))]
        public void ScaleAndFrequencyRoundTripPerWavelet(IWavelet wavelet)
        {
            double scale = CwtScales.ScaleForNormalizedFrequency(0.1, wavelet);
            Assert.AreEqual(0.1, CwtScales.NormalizedCentreFrequency(scale, wavelet), 1e-12, wavelet.Name);
        }

        /// <summary>
        /// The end-to-end check: every wavelet must place the ridge of a known tone at that
        /// tone's own frequency on its own axis.
        /// </summary>
        [TestCaseSource(nameof(AllWavelets))]
        public void EveryWaveletRecoversAKnownFrequency(IWavelet wavelet)
        {
            const double frequency = 0.08;
            int n = 4096;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Cos(2 * Math.PI * frequency * i);
            }

            // Bracket the scale grid around where this wavelet puts that frequency.
            double targetScale = CwtScales.ScaleForNormalizedFrequency(frequency, wavelet);
            int centreOctave = (int)Math.Round(Math.Log2(targetScale));
            int start = Math.Max(0, centreOctave - 1);
            int end = centreOctave + 1;

            var backend = new ManagedCwt(wavelet);
            backend.Transform(signal, start, end, 64, 0f, 0, false,
                out double[][] real, out double[][] imag);

            double[] perScale = MeanModulusPerScale(real, imag);
            double[] scales = CwtScales.Scales(start, end, 64);
            double recovered = CwtScales.NormalizedCentreFrequency(scales[ArgMax(perScale)], wavelet);

            // One voice at 64 per octave is 1.1%; allow a few voices of slack, since the
            // broader wavelets have a flatter peak across scale.
            Assert.AreEqual(frequency, recovered, frequency * 0.06,
                $"{wavelet.Name}: expected {frequency}, recovered {recovered}");
        }

        /// <summary>
        /// Amplitude normalisation has to hold for every wavelet, not just the Morlet, or
        /// peak heights stop meaning anything when the wavelet is swapped.
        /// </summary>
        [TestCaseSource(nameof(AllWavelets))]
        public void ModulusAtTheRidgeTracksAmplitudeForEveryWavelet(IWavelet wavelet)
        {
            const double frequency = 0.08;
            const double amplitude = 2.5;
            int n = 8192;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = amplitude * Math.Cos(2 * Math.PI * frequency * i);
            }

            double targetScale = CwtScales.ScaleForNormalizedFrequency(frequency, wavelet);
            int centreOctave = (int)Math.Round(Math.Log2(targetScale));
            var backend = new ManagedCwt(wavelet);
            backend.Transform(signal, Math.Max(0, centreOctave - 1), centreOctave + 1, 64, 0f, 0, false,
                out double[][] real, out double[][] imag);

            double[] perScale = MeanModulusPerScale(real, imag);
            int peakRow = ArgMax(perScale);
            int centre = n / 2;
            double modulus = Math.Sqrt(
                real[peakRow][centre] * real[peakRow][centre] +
                imag[peakRow][centre] * imag[peakRow][centre]);

            // The scale grid is discrete, so the best row may sit slightly off the exact
            // peak; allow for that rather than demanding an exact amplitude match.
            Assert.AreEqual(amplitude, modulus, amplitude * 0.15,
                $"{wavelet.Name}: modulus {modulus} vs amplitude {amplitude}");
        }

        /// <summary>
        /// Different wavelets peak at wildly different points on the scale grid -- a Paul
        /// of order 6400 works at scales three orders of magnitude from a Morlet's -- yet
        /// each must report the same m/z for the same ion. This is the invariant that the
        /// wavelet-aware frequency axis exists to guarantee; if the axis were built from a
        /// hardcoded Morlet relation, everything but the Morlet would be silently wrong.
        /// </summary>
        [Test]
        public void DifferentWaveletsAgreeOnRecoveredMz()
        {
            var config = new FCWTNET.Simulation.OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = 1e11,
                SamplingRate = 100_000,
                DurationSeconds = 0.08,
                Decay = FCWTNET.Simulation.DecayModel.None,
                SignalToNoise = null,
                Seed = 11
            };
            var species = new[]
            {
                new FCWTNET.Simulation.IonSpecies
                {
                    MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 1, InitialPhase = 0
                }
            };

            var wavelets = new IWavelet[]
            {
                new MorletWavelet(30),
                new PaulWavelet(900),
                new BumpWavelet(5, 0.25),
                new LogGaborWavelet(6, 1.05)
            };

            double expectedMz = 0;
            var recovered = new List<double>();
            foreach (IWavelet wavelet in wavelets)
            {
                // Each wavelet needs its own octave range, because each peaks at its own
                // point in a*w. Deriving it from the wavelet is the whole point.
                double normalised = Math.Sqrt(config.CalibrationCoefficient / 1001.007) / config.SamplingRate;
                double scale = CwtScales.ScaleForNormalizedFrequency(normalised, wavelet);
                int centreOctave = (int)Math.Round(Math.Log2(scale));

                var settings = new CwtSettings
                {
                    StartOctave = Math.Max(0, centreOctave - 1),
                    EndOctave = centreOctave + 1,
                    Voices = 64,
                    Wavelet = wavelet,
                    Backend = CwtBackends.Managed
                };

                CwtWorkflow.Result result = new CwtWorkflow().RunSimulation(config, species, settings);
                expectedMz = result.GroundTruth![0].Mz;
                Assert.IsNotEmpty(result.Peaks, wavelet.Name);
                recovered.Add(result.Peaks.OrderByDescending(p => p.Magnitude).First().Position);
            }

            for (int i = 0; i < wavelets.Length; i++)
            {
                Assert.AreEqual(expectedMz, recovered[i], expectedMz * 0.05,
                    $"{wavelets[i].Name}: recovered {recovered[i]:F3}, expected {expectedMz:F3}");
            }
            // And they agree with each other, not just loosely with the truth.
            Assert.AreEqual(recovered.Min(), recovered.Max(), expectedMz * 0.05,
                "wavelets disagree with one another");
        }

        [Test]
        public void BackendNameReflectsTheWavelet()
        {
            Assert.AreEqual("managed-morlet", new ManagedCwt().Name);
            Assert.AreEqual("managed-paul", new ManagedCwt(new PaulWavelet()).Name);
            Assert.AreEqual("managed-bump", new ManagedCwt(new BumpWavelet()).Name);
        }

        [Test]
        public void BackendWithoutAWaveletBuildsAMorletFromC0()
        {
            var backend = new ManagedCwt();
            IWavelet wavelet = backend.ResolveWavelet(42);
            Assert.IsInstanceOf<MorletWavelet>(wavelet);
            Assert.AreEqual(42, ((MorletWavelet)wavelet).C0, 1e-12);

            // A pinned wavelet ignores c0 entirely.
            var pinned = new ManagedCwt(new PaulWavelet(6));
            Assert.IsInstanceOf<PaulWavelet>(pinned.ResolveWavelet(42));
        }

        [Test]
        public void BackendRejectsANullWavelet()
        {
            Assert.Throws<ArgumentNullException>(() => new ManagedCwt(null!));
        }

        /// <summary>
        /// Setting the wavelet on a CWTObject must change both the transform and the axis,
        /// together. If only one followed, the m/z labels would describe the wrong rows.
        /// </summary>
        [Test]
        public void CwtObjectWaveletDrivesBothTransformAndAxis()
        {
            int n = 2048;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Cos(2 * Math.PI * 0.1 * i);
            }

            var paul = new PaulWavelet(8);
            var cwt = new CWTObject(signal, 2, 5, 32, 6f, 0, false, samplingRate: 1000)
            {
                Wavelet = paul
            };
            cwt.PerformCWT();
            cwt.CalculateFrequencyAxis();

            Assert.AreSame(paul, cwt.EffectiveWavelet);
            // The axis must use Paul's centre frequency, not the c0 of 6 passed to the ctor.
            double[] scales = CwtScales.Scales(2, 5, 32);
            Assert.AreEqual(paul.CentreAngularFrequency / (2 * Math.PI * scales[^1]),
                cwt.FrequencyAxis!.WaveletCenterFrequencies![0], 1e-12);
            Assert.AreEqual(cwt.OutputCWT!.Rows, cwt.FrequencyAxis.Length);
        }

        [Test]
        public void CwtObjectDefaultsToMorletFromC0()
        {
            var cwt = new CWTObject(new double[64], 1, 2, 8, 12f, 0, false);
            Assert.IsInstanceOf<MorletWavelet>(cwt.EffectiveWavelet);
            Assert.AreEqual(12, cwt.EffectiveWavelet.CentreAngularFrequency, 1e-6);
        }

        [Test]
        public void WorkflowPeakSeparationFollowsTheWaveletResolution()
        {
            // A broad wavelet needs a wider suppression window than a sharp one, or a
            // single peak gets reported many times.
            var broad = new CwtSettings { Voices = 64, Wavelet = new MorletWavelet(6) };
            var sharp = new CwtSettings { Voices = 64, Wavelet = new MorletWavelet(80) };
            Assert.Greater(broad.EffectivePeakSeparation(), sharp.EffectivePeakSeparation());
        }

        [Test]
        public void WorkflowRunsWithANonDefaultWavelet()
        {
            var config = new FCWTNET.Simulation.OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = 1e11,
                SamplingRate = 100_000,
                DurationSeconds = 0.04,
                Decay = FCWTNET.Simulation.DecayModel.None,
                SignalToNoise = null,
                Seed = 7
            };
            var species = new[]
            {
                new FCWTNET.Simulation.IonSpecies
                {
                    MonoisotopicMass = 1000, Charge = 1, IsotopePeaks = 1, InitialPhase = 0
                }
            };
            var settings = new CwtSettings
            {
                StartOctave = 2,
                EndOctave = 5,
                Voices = 32,
                Wavelet = new BumpWavelet(),
                Backend = CwtBackends.Managed
            };

            CwtWorkflow.Result result = new CwtWorkflow().RunSimulation(config, species, settings);
            Assert.AreEqual(result.Modulus.GetLength(0), result.MzAxis.Length);
            Assert.IsNotEmpty(result.Peaks);

            double expectedMz = result.GroundTruth![0].Mz;
            double recovered = result.Peaks.OrderByDescending(p => p.Magnitude).First().Position;
            Assert.AreEqual(expectedMz, recovered, expectedMz * 0.1);
        }
    }
}
