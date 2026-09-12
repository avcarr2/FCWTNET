using System;
using System.IO;
using System.Linq;
using FCWTNET;
using FCWTNET.Visualization;
using NUnit.Framework;
using OxyPlot;

namespace TestFCWTAPI
{
    public class VisualizationTests
    {
        private static double[,] SampleMagnitude(int rows = 16, int columns = 32)
        {
            var data = new double[rows, columns];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    // A ridge along the middle row that fades with time.
                    double distance = Math.Abs(i - rows / 2.0);
                    data[i, j] = Math.Exp(-distance) * (1.0 - 0.5 * j / columns);
                }
            }
            return data;
        }

        /// <summary>
        /// The defining property of a sequential ramp: lightness must increase
        /// monotonically from one end to the other. This is what keeps the map readable
        /// in greyscale and under colour vision deficiency, and it is precisely what a
        /// rainbow palette fails -- so it is checked by computation, not by eye.
        /// </summary>
        [TestCase("viridis")]
        [TestCase("magma")]
        [TestCase("greys")]
        public void SequentialRampsRiseMonotonicallyInLightness(string name)
        {
            Colormap map = Colormaps.Get(name);
            double[] luminance = map.Sample(64).Select(c => c.RelativeLuminance()).ToArray();
            for (int i = 1; i < luminance.Length; i++)
            {
                Assert.GreaterOrEqual(luminance[i], luminance[i - 1] - 1e-6,
                    $"{name} lightness dips between samples {i - 1} and {i}");
            }
            // And it should actually span a useful range, not sit flat.
            Assert.Greater(luminance[^1] - luminance[0], 0.5, $"{name} should span a wide lightness range");
        }

        [Test]
        public void BluesRampIsMonotonicFromDarkToLight()
        {
            double[] luminance = Colormaps.Blues.Sample(32).Select(c => c.RelativeLuminance()).ToArray();
            for (int i = 1; i < luminance.Length; i++)
            {
                Assert.GreaterOrEqual(luminance[i], luminance[i - 1] - 1e-6);
            }
        }

        /// <summary>
        /// A rainbow ramp is the counter-example: its lightness rises and falls, which is
        /// why it was replaced. If this ever stops failing the premise has changed.
        /// </summary>
        [Test]
        public void RainbowIsNotMonotonicWhichIsWhyItIsNotUsed()
        {
            OxyPalette rainbow = OxyPalettes.Rainbow(32);
            double[] luminance = rainbow.Colors
                .Select(c => new RgbColor(c.R, c.G, c.B).RelativeLuminance())
                .ToArray();
            bool dips = false;
            for (int i = 1; i < luminance.Length; i++)
            {
                if (luminance[i] < luminance[i - 1] - 1e-6)
                {
                    dips = true;
                    break;
                }
            }
            Assert.IsTrue(dips, "rainbow is expected to be non-monotonic in lightness");
        }

        [Test]
        public void ColormapInterpolatesBetweenAnchorsAndClamps()
        {
            var map = new Colormap("test", new[] { "#000000", "#FFFFFF" });
            Assert.AreEqual(0, map.Map(0).R);
            Assert.AreEqual(255, map.Map(1).R);
            Assert.AreEqual(128, map.Map(0.5).R, 1);
            // Out-of-range values clamp rather than wrap.
            Assert.AreEqual(0, map.Map(-5).R);
            Assert.AreEqual(255, map.Map(5).R);
            Assert.AreEqual(0, map.Map(double.NaN).R);
        }

        [Test]
        public void ColormapRejectsBadInput()
        {
            Assert.Throws<ArgumentException>(() => new Colormap("x", new[] { "#000000" }));
            Assert.Throws<ArgumentException>(() => new Colormap("x", new[] { "#000000", "nothex" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Colormaps.Viridis.Sample(1));
            var exception = Assert.Throws<ArgumentException>(() => Colormaps.Get("jet"));
            StringAssert.Contains("viridis", exception!.Message);
        }

        [Test]
        public void OxyPaletteHasTheRequestedNumberOfSteps()
        {
            OxyPalette palette = Colormaps.Viridis.ToOxyPalette(128);
            Assert.AreEqual(128, palette.Colors.Count);
        }

        [Test]
        public void RendererProducesOpaqueRgbaOfTheRequestedSize()
        {
            double[,] data = SampleMagnitude();
            byte[] pixels = ScalogramRenderer.ToRgba(data, 64, 48);
            Assert.AreEqual(64 * 48 * 4, pixels.Length);
            for (int i = 3; i < pixels.Length; i += 4)
            {
                Assert.AreEqual(255, pixels[i], "every pixel should be fully opaque");
            }
        }

        [Test]
        public void RendererPutsHighFrequenciesAtTheTop()
        {
            // A matrix whose magnitude rises with row index: row 0 is the lowest
            // frequency, so after the vertical flip the brightest pixels sit at the top.
            int rows = 8;
            var data = new double[rows, 4];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    data[i, j] = i;
                }
            }

            byte[] pixels = ScalogramRenderer.ToRgba(data, 4, rows, Colormaps.Greys);
            double TopLeftLuminance(int y)
            {
                int offset = (y * 4 + 0) * 4;
                return new RgbColor(pixels[offset], pixels[offset + 1], pixels[offset + 2]).RelativeLuminance();
            }
            Assert.Greater(TopLeftLuminance(0), TopLeftLuminance(rows - 1),
                "the highest-frequency row should render at the top of the image");
        }

        [Test]
        public void IntensityScalesCompressDifferently()
        {
            // At the midpoint, log lifts small values most, then sqrt, then linear.
            double linear = ScalogramRenderer.Normalise(0.5, 0, 1, IntensityScale.Linear);
            double sqrt = ScalogramRenderer.Normalise(0.5, 0, 1, IntensityScale.SquareRoot);
            double log = ScalogramRenderer.Normalise(0.5, 0, 1, IntensityScale.Logarithmic);
            Assert.AreEqual(0.5, linear, 1e-12);
            Assert.Greater(sqrt, linear);
            Assert.Greater(log, sqrt);

            // All of them pin the endpoints.
            foreach (IntensityScale scale in Enum.GetValues<IntensityScale>())
            {
                Assert.AreEqual(0, ScalogramRenderer.Normalise(0, 0, 1, scale), 1e-12);
                Assert.AreEqual(1, ScalogramRenderer.Normalise(1, 0, 1, scale), 1e-12);
            }
        }

        [Test]
        public void NormaliseClampsOutsideTheLimitsAndHandlesFlatInput()
        {
            Assert.AreEqual(0, ScalogramRenderer.Normalise(-10, 0, 1, IntensityScale.Linear), 1e-12);
            Assert.AreEqual(1, ScalogramRenderer.Normalise(10, 0, 1, IntensityScale.Linear), 1e-12);
            // Degenerate limits must not divide by zero.
            Assert.AreEqual(0, ScalogramRenderer.Normalise(5, 3, 3, IntensityScale.Linear), 1e-12);
        }

        [Test]
        public void ExtentIgnoresNonFiniteValues()
        {
            var data = new double[2, 2];
            data[0, 0] = double.NaN;
            data[0, 1] = 2;
            data[1, 0] = double.PositiveInfinity;
            data[1, 1] = -1;
            (double minimum, double maximum) = ScalogramRenderer.Extent(data);
            Assert.AreEqual(-1, minimum, 1e-12);
            Assert.AreEqual(2, maximum, 1e-12);
        }

        [Test]
        public void RendererRejectsEmptyOrBadDimensions()
        {
            Assert.Throws<ArgumentNullException>(() => ScalogramRenderer.ToRgba(null!, 4, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScalogramRenderer.ToRgba(SampleMagnitude(), 0, 4));
            Assert.Throws<ArgumentException>(() => ScalogramRenderer.ToRgba(new double[0, 0], 4, 4));
        }

        [Test]
        public void PngExportWritesAValidFile()
        {
            double[,] data = SampleMagnitude();
            double[] timeAxis = Enumerable.Range(0, data.GetLength(1)).Select(i => (double)i).ToArray();
            double[] freqAxis = Enumerable.Range(1, data.GetLength(0)).Select(i => (double)i).ToArray();
            // GenerateCWTHeatMap expects [time, frequency], so transpose.
            var transposed = new double[data.GetLength(1), data.GetLength(0)];
            for (int i = 0; i < data.GetLength(0); i++)
            {
                for (int j = 0; j < data.GetLength(1); j++)
                {
                    transposed[j, i] = data[i, j];
                }
            }

            PlotModel model = PlottingUtils.GenerateCWTHeatMap(transposed, "Test", timeAxis, freqAxis);
            string path = Path.Combine(Path.GetTempPath(), $"fcwtnet-plot-{Guid.NewGuid():N}.png");
            try
            {
                PlottingUtils.ExportPlotPNG(model, path, 400, 300);
                Assert.IsTrue(File.Exists(path));
                byte[] bytes = File.ReadAllBytes(path);
                Assert.Greater(bytes.Length, 1000, "a real PNG should not be trivially small");
                // PNG magic number.
                CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void ExportPlotDispatchesOnExtension()
        {
            PlotModel model = PlottingUtils.GenerateCWTHeatMap(new double[4, 4], "Test");
            string directory = Path.Combine(Path.GetTempPath(), $"fcwtnet-export-{Guid.NewGuid():N}");
            try
            {
                string png = Path.Combine(directory, "plot.png");
                string pdf = Path.Combine(directory, "plot.pdf");
                PlottingUtils.ExportPlot(model, png);
                PlottingUtils.ExportPlot(model, pdf);
                Assert.IsTrue(File.Exists(png));
                Assert.IsTrue(File.Exists(pdf));
                Assert.Throws<ArgumentException>(() => PlottingUtils.ExportPlot(model, Path.Combine(directory, "plot.gif")));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        [Test]
        public void SupportedExtensionsAreRecognised()
        {
            Assert.IsTrue(PlottingUtils.IsSupportedPlotExtension("a.png"));
            Assert.IsTrue(PlottingUtils.IsSupportedPlotExtension("a.PDF"));
            Assert.IsFalse(PlottingUtils.IsSupportedPlotExtension("a.xlsx"));
            Assert.IsFalse(PlottingUtils.IsSupportedPlotExtension("a"));
        }

        [Test]
        public void HeatMapUsesTheSequentialPaletteRatherThanRainbow()
        {
            PlotModel model = PlottingUtils.GenerateCWTHeatMap(new double[4, 4], "Test");
            var colorAxis = model.Axes.OfType<OxyPlot.Axes.LinearColorAxis>().Single();
            double[] luminance = colorAxis.Palette.Colors
                .Select(c => new RgbColor(c.R, c.G, c.B).RelativeLuminance())
                .ToArray();
            for (int i = 1; i < luminance.Length; i++)
            {
                Assert.GreaterOrEqual(luminance[i], luminance[i - 1] - 1e-6,
                    "the heat map palette must be monotonic in lightness");
            }
        }
    }
}
