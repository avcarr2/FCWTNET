using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FCWTNET.Gui;
using NUnit.Framework;

[assembly: AvaloniaTestApplication(typeof(TestFCWTNET.Gui.TestAppBuilder))]

namespace TestFCWTNET.Gui
{
    /// <summary>
    /// Boots the real App on Avalonia's headless platform, so the window can be
    /// constructed and driven without a display.
    /// </summary>
    public static class TestAppBuilder
    {
        // UseHeadlessDrawing=false routes drawing through Skia instead of the no-op
        // headless drawing backend, which is what makes CaptureRenderedFrame produce a
        // real image rather than a blank one.
        public static AppBuilder BuildAvaloniaApp() => AppBuilder
            .Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>
    /// The desktop app cannot be looked at in CI, so these tests assert the things a
    /// glance would otherwise confirm: the window builds, its controls are populated,
    /// picking a preset updates the inputs, and pressing Run fills the scalogram, the
    /// transient preview and the peak table.
    /// </summary>
    public class MainWindowTests
    {
        private static T Find<T>(Control root, string name) where T : Control
        {
            T? found = root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
            Assert.IsNotNull(found, $"could not find {typeof(T).Name} named '{name}'");
            return found!;
        }

        [AvaloniaTest]
        public void WindowConstructsWithPopulatedControls()
        {
            var window = new MainWindow();
            window.Show();

            var presets = Find<ComboBox>(window, "PresetBox");
            Assert.IsNotNull(presets.ItemsSource);
            Assert.Greater(presets.ItemsSource!.Cast<object>().Count(), 1, "presets should be listed");
            Assert.AreEqual("single-peak", presets.SelectedItem);

            var colormaps = Find<ComboBox>(window, "ColormapBox");
            CollectionAssert.Contains(colormaps.ItemsSource!.Cast<object>().ToList(), "viridis");
            Assert.AreEqual("viridis", colormaps.SelectedItem);

            var scales = Find<ComboBox>(window, "ScaleBox");
            Assert.AreEqual(3, scales.ItemsSource!.Cast<object>().Count());

            // The description should reflect the initially selected preset.
            var description = Find<TextBlock>(window, "PresetDescription");
            Assert.IsNotEmpty(description.Text ?? string.Empty);
        }

        [AvaloniaTest]
        public void SelectingAPresetUpdatesTheInputs()
        {
            var window = new MainWindow();
            window.Show();

            var presets = Find<ComboBox>(window, "PresetBox");
            var drift = Find<NumericUpDown>(window, "DriftBox");
            var snr = Find<NumericUpDown>(window, "SnrBox");

            // single-peak is noiseless and has no drift.
            Assert.AreEqual(0m, drift.Value);
            Assert.AreEqual(0m, snr.Value);

            presets.SelectedItem = "frequency-drift";
            // frequency-drift runs one ion at 20%/s with SNR 200.
            Assert.AreEqual(20m, drift.Value);
            Assert.AreEqual(200m, snr.Value);

            var description = Find<TextBlock>(window, "PresetDescription");
            StringAssert.Contains("drift", description.Text!);
        }

        [AvaloniaTest]
        public void RunFillsTheScalogramTransientAndPeakTable()
        {
            var window = new MainWindow();
            window.Show();

            // Keep the run small so the test stays quick.
            Find<NumericUpDown>(window, "MaxSamplesBox").Value = 4096;
            Find<NumericUpDown>(window, "VoicesBox").Value = 16;
            Find<NumericUpDown>(window, "DurationBox").Value = 2;

            var scalogram = Find<Image>(window, "ScalogramImage");
            var transient = Find<Image>(window, "TransientImage");
            var peaks = Find<TextBlock>(window, "PeakText");
            var status = Find<TextBlock>(window, "StatusText");
            var placeholder = Find<TextBlock>(window, "PlaceholderText");

            Assert.IsNull(scalogram.Source, "nothing should be rendered before Run");
            Assert.IsTrue(placeholder.IsVisible);

            RaiseClick(window, "RunButton");
            PumpUntil(() => scalogram.Source != null, TimeSpan.FromSeconds(60));

            Assert.IsNotNull(scalogram.Source, "the scalogram should be rendered");
            Assert.IsNotNull(transient.Source, "the transient preview should be rendered");
            Assert.IsFalse(placeholder.IsVisible, "the placeholder should be hidden after a run");

            var bitmap = (Bitmap)scalogram.Source!;
            Assert.AreEqual(1024, bitmap.PixelSize.Width);
            Assert.AreEqual(512, bitmap.PixelSize.Height);

            StringAssert.Contains("m/z", peaks.Text!);
            StringAssert.Contains("samples", status.Text!);
            StringAssert.Contains("ridge", status.Text!);
        }

        /// <summary>
        /// Captures the rendered window to a PNG. Set FCWTNET_GUI_SCREENSHOT to a path to
        /// keep the image; it is the only way to actually look at the desktop UI on a
        /// machine with no display.
        /// </summary>
        [AvaloniaTest]
        public void RenderedWindowCanBeCaptured()
        {
            var window = new MainWindow();
            window.Show();

            Find<NumericUpDown>(window, "MaxSamplesBox").Value = 8192;
            Find<NumericUpDown>(window, "VoicesBox").Value = 32;
            Find<NumericUpDown>(window, "DurationBox").Value = 4;

            // A screenshot of the default single peak shows none of the analysis panels
            // doing anything, so let the capture be pointed at the full pipeline.
            if (Environment.GetEnvironmentVariable("FCWTNET_GUI_FULL") == "1")
            {
                Find<ComboBox>(window, "PresetBox").SelectedItem = "myoglobin-denatured";
                Find<NumericUpDown>(window, "MaxSamplesBox").Value = 32768;
                Find<NumericUpDown>(window, "VoicesBox").Value = 64;
                Find<NumericUpDown>(window, "DurationBox").Value = 8;
                Find<NumericUpDown>(window, "C0Box").Value = 80;
                Find<NumericUpDown>(window, "IsotopePeaksBox").Value = 8;
                Find<CheckBox>(window, "MzRangeBox").IsChecked = true;
                Find<NumericUpDown>(window, "MinMzBox").Value = 600;
                Find<NumericUpDown>(window, "MaxMzBox").Value = 2300;
                Find<CheckBox>(window, "RefineBox").IsChecked = true;
                Find<CheckBox>(window, "DeconvolveBox").IsChecked = true;
            }

            var scalogram = Find<Image>(window, "ScalogramImage");
            RaiseClick(window, "RunButton");
            PumpUntil(() => scalogram.Source != null, TimeSpan.FromSeconds(180));

            // Let layout settle so the captured frame includes the rendered image.
            for (int i = 0; i < 10; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }

            // Changing a display control must re-render the existing result rather than
            // requiring another Run.
            if (Environment.GetEnvironmentVariable("FCWTNET_GUI_ALT_STYLE") == "1")
            {
                Find<ComboBox>(window, "ColormapBox").SelectedItem = "magma";
                Find<ComboBox>(window, "ScaleBox").SelectedIndex = 1;
                for (int i = 0; i < 10; i++)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }
            }

            var frame = window.CaptureRenderedFrame();
            Assert.IsNotNull(frame, "headless Skia should produce a frame");
            Assert.Greater(frame!.PixelSize.Width, 100);
            Assert.Greater(frame.PixelSize.Height, 100);

            string? path = Environment.GetEnvironmentVariable("FCWTNET_GUI_SCREENSHOT");
            if (!string.IsNullOrEmpty(path))
            {
                frame.Save(path);
                TestContext.WriteLine($"saved screenshot to {path}");
            }
        }

        /// <summary>
        /// Renders a myoglobin charge state envelope. Each charge state sits at its own
        /// frequency, so the scalogram should show a ladder of ridges rather than one.
        /// Set FCWTNET_GUI_ENVELOPE to a path to keep the image, and FCWTNET_GUI_MODE to
        /// Native or Denatured to pick the ionisation mode.
        /// </summary>
        [AvaloniaTest]
        public void MyoglobinChargeEnvelopeRenders()
        {
            var window = new MainWindow();
            window.Show();

            string mode = Environment.GetEnvironmentVariable("FCWTNET_GUI_MODE") ?? "Denatured";
            string preset = mode.Equals("Native", StringComparison.OrdinalIgnoreCase)
                ? "myoglobin-native"
                : "myoglobin-denatured";

            Find<ComboBox>(window, "PresetBox").SelectedItem = preset;

            // The charge envelope controls should reveal themselves for these presets.
            var chargePanel = window.GetVisualDescendants().OfType<StackPanel>()
                .First(p => p.Name == "ChargePanel");
            Assert.IsTrue(chargePanel.IsVisible, "charge envelope controls should be shown");

            var summary = Find<TextBlock>(window, "EnvelopeSummary");
            Assert.IsNotEmpty(summary.Text ?? string.Empty, "the envelope summary should be populated");
            TestContext.WriteLine($"{preset}: {summary.Text}");

            // Keep the run manageable: a wide envelope with full isotope clusters is a lot
            // of components.
            Find<NumericUpDown>(window, "IsotopePeaksBox").Value = 8;
            Find<NumericUpDown>(window, "MaxSamplesBox").Value = 32768;
            Find<NumericUpDown>(window, "VoicesBox").Value = 64;
            Find<NumericUpDown>(window, "DurationBox").Value = 8;
            // Adjacent charge states near the base peak differ by only ~3% in frequency,
            // so the wavelet needs enough cycles to tell them apart; c0 = 6 (which suits a
            // single isolated peak) resolves nothing finer than ~39%.
            double c0 = (double)(Environment.GetEnvironmentVariable("FCWTNET_GUI_C0") is { } raw
                && double.TryParse(raw, out double parsed) ? parsed : 80.0);
            Find<NumericUpDown>(window, "C0Box").Value = (decimal)c0;

            var scalogram = Find<Image>(window, "ScalogramImage");
            RaiseClick(window, "RunButton");
            PumpUntil(() => scalogram.Source != null, TimeSpan.FromSeconds(180));

            var status = Find<TextBlock>(window, "StatusText");
            var peaks = Find<TextBlock>(window, "PeakText");
            TestContext.WriteLine(status.Text);
            TestContext.WriteLine(peaks.Text);

            StringAssert.Contains("ridge", status.Text!);

            for (int i = 0; i < 10; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }

            string? path = Environment.GetEnvironmentVariable("FCWTNET_GUI_ENVELOPE");
            if (!string.IsNullOrEmpty(path))
            {
                var frame = window.CaptureRenderedFrame();
                Assert.IsNotNull(frame);
                frame!.Save(path);
                TestContext.WriteLine($"saved {path}");
            }
        }

        [AvaloniaTest]
        public void WaveletPickerSwapsParameterControlsAndReportsResolution()
        {
            var window = new MainWindow();
            window.Show();

            var waveletBox = Find<ComboBox>(window, "WaveletBox");
            var summary = Find<TextBlock>(window, "WaveletSummary");
            var c0Panel = window.GetVisualDescendants().OfType<StackPanel>()
                .First(p => p.Name == "C0Panel");
            var parameterPanel = window.GetVisualDescendants().OfType<StackPanel>()
                .First(p => p.Name == "WaveletParameterPanel");

            // Morlet is the default and uses the dedicated c0 control.
            Assert.AreEqual("morlet", waveletBox.SelectedItem);
            Assert.IsTrue(c0Panel.IsVisible);
            Assert.IsFalse(parameterPanel.IsVisible);
            StringAssert.Contains("resolves components", summary.Text!);

            // Every other wavelet swaps to the free-form parameter box.
            waveletBox.SelectedItem = "paul";
            Assert.IsFalse(c0Panel.IsVisible);
            Assert.IsTrue(parameterPanel.IsVisible);
            StringAssert.Contains("Order", Find<TextBlock>(window, "WaveletParameterLabel").Text!);

            waveletBox.SelectedItem = "bump";
            StringAssert.Contains("Centre", Find<TextBlock>(window, "WaveletParameterLabel").Text!);

            // A bad parameter string is reported rather than thrown.
            Find<TextBox>(window, "WaveletParameterBox").Text = "not-a-number";
            Assert.DoesNotThrow(() => waveletBox.SelectedItem = "log-gabor");
        }

        [AvaloniaTest]
        public void RunHonoursTheSelectedWavelet()
        {
            var window = new MainWindow();
            window.Show();

            Find<ComboBox>(window, "WaveletBox").SelectedItem = "bump";
            Find<NumericUpDown>(window, "MaxSamplesBox").Value = 4096;
            Find<NumericUpDown>(window, "VoicesBox").Value = 16;
            Find<NumericUpDown>(window, "DurationBox").Value = 2;

            var scalogram = Find<Image>(window, "ScalogramImage");
            RaiseClick(window, "RunButton");
            PumpUntil(() => scalogram.Source != null, TimeSpan.FromSeconds(60));

            var status = Find<TextBlock>(window, "StatusText");
            TestContext.WriteLine(status.Text);
            StringAssert.Contains("bump", status.Text!);
        }

        [AvaloniaTest]
        public void ChangingColormapDoesNotThrowBeforeARun()
        {
            var window = new MainWindow();
            window.Show();
            // Selecting a colormap only takes effect on the next render; it must not
            // fault when there is no result yet.
            Assert.DoesNotThrow(() => Find<ComboBox>(window, "ColormapBox").SelectedItem = "magma");
            Assert.DoesNotThrow(() => Find<ComboBox>(window, "ScaleBox").SelectedIndex = 1);
        }

        /// <summary>
        /// The analysis toggles are not independent: discovery is a refinement of the fit,
        /// so it must not be reachable on its own.
        /// </summary>
        [AvaloniaTest]
        public void DiscoveryIsOnlyAvailableWhileFittingIsOn()
        {
            var window = new MainWindow();
            window.Show();

            var refine = Find<CheckBox>(window, "RefineBox");
            var discover = Find<CheckBox>(window, "DiscoverBox");

            Assert.IsFalse(discover.IsEnabled, "discovery should start unavailable");

            refine.IsChecked = true;
            Assert.IsTrue(discover.IsEnabled);

            discover.IsChecked = true;
            refine.IsChecked = false;
            Assert.IsFalse(discover.IsEnabled);
            Assert.AreNotEqual(true, discover.IsChecked,
                "turning the fit off must not leave discovery armed");
        }

        [AvaloniaTest]
        public void TheMzRangeInputsAppearOnlyWhenAskedFor()
        {
            var window = new MainWindow();
            window.Show();

            var toggle = Find<CheckBox>(window, "MzRangeBox");
            var panel = Find<StackPanel>(window, "MzRangePanel");

            Assert.IsFalse(panel.IsVisible);
            toggle.IsChecked = true;
            Assert.IsTrue(panel.IsVisible);
            toggle.IsChecked = false;
            Assert.IsFalse(panel.IsVisible);
        }

        /// <summary>
        /// The whole new back end through the UI: bound the transform by m/z, fit the
        /// overlapping ridges, and group the result into species.
        /// </summary>
        [AvaloniaTest]
        public void RunningWithFittingAndDeconvolutionFillsTheSpeciesPanel()
        {
            var window = new MainWindow();
            window.Show();

            Find<ComboBox>(window, "PresetBox").SelectedItem = "myoglobin-denatured";
            Find<NumericUpDown>(window, "MaxSamplesBox").Value = 16384;
            Find<NumericUpDown>(window, "VoicesBox").Value = 32;
            Find<NumericUpDown>(window, "DurationBox").Value = 4;
            Find<NumericUpDown>(window, "C0Box").Value = 80;
            Find<NumericUpDown>(window, "IsotopePeaksBox").Value = 4;

            Find<CheckBox>(window, "MzRangeBox").IsChecked = true;
            Find<NumericUpDown>(window, "MinMzBox").Value = 600;
            Find<NumericUpDown>(window, "MaxMzBox").Value = 2300;
            Find<CheckBox>(window, "RefineBox").IsChecked = true;
            Find<CheckBox>(window, "DeconvolveBox").IsChecked = true;

            var scalogram = Find<Image>(window, "ScalogramImage");
            var species = Find<TextBlock>(window, "SpeciesText");
            var peaks = Find<TextBlock>(window, "PeakText");
            var heading = Find<TextBlock>(window, "PeakHeading");
            var status = Find<TextBlock>(window, "StatusText");

            RaiseClick(window, "RunButton");
            PumpUntil(() => scalogram.Source != null, TimeSpan.FromSeconds(120));

            TestContext.WriteLine(status.Text);
            TestContext.WriteLine(species.Text);

            // The m/z bound should be reported instead of an octave bracket.
            StringAssert.Contains("m/z 600-2300", status.Text!);
            StringAssert.Contains("scales", status.Text!);
            Assert.AreEqual("Fitted components", heading.Text,
                "the heading should say where the positions came from");
            StringAssert.Contains("component(s) fitted", status.Text!);

            // A charge envelope must come back as species, not a list of m/z values.
            StringAssert.Contains("Da", species.Text!);
            StringAssert.Contains("states", species.Text!);
            StringAssert.DoesNotContain("Enable", species.Text!);
            StringAssert.Contains("species deconvolved", status.Text!);

            // And the peak table should carry the assignment and the fit quality.
            StringAssert.Contains("resid", peaks.Text!);
            StringAssert.Contains("+", peaks.Text!);
        }

        [AvaloniaTest]
        public void TheSpeciesPanelSaysHowToFillItWhenDeconvolutionIsOff()
        {
            var window = new MainWindow();
            window.Show();

            Find<NumericUpDown>(window, "MaxSamplesBox").Value = 4096;
            Find<NumericUpDown>(window, "VoicesBox").Value = 16;
            Find<NumericUpDown>(window, "DurationBox").Value = 2;

            var scalogram = Find<Image>(window, "ScalogramImage");
            var species = Find<TextBlock>(window, "SpeciesText");

            RaiseClick(window, "RunButton");
            PumpUntil(() => scalogram.Source != null, TimeSpan.FromSeconds(60));

            StringAssert.Contains("Enable", species.Text!);
            StringAssert.Contains("Detected peaks", Find<TextBlock>(window, "PeakHeading").Text!);
        }

        [AvaloniaTest]
        public void TheOligomerOrderInputAppearsOnlyWhenAskedFor()
        {
            var window = new MainWindow();
            window.Show();

            var toggle = Find<CheckBox>(window, "OligomerBox");
            var panel = Find<StackPanel>(window, "OligomerPanel");

            Assert.IsFalse(panel.IsVisible);
            toggle.IsChecked = true;
            Assert.IsTrue(panel.IsVisible);
            toggle.IsChecked = false;
            Assert.IsFalse(panel.IsVisible);
        }

        /// <summary>
        /// A homo-oligomer ladder through the UI. The charge-state comb collapses it into
        /// one species at a multiple of the subunit, so this checks the app can also say
        /// what the subunit actually is.
        /// </summary>
        [AvaloniaTest]
        public void ReadingAComplexAsAnOligomerSeriesFillsTheSpeciesPanel()
        {
            var window = new MainWindow();
            window.Show();

            Find<ComboBox>(window, "PresetBox").SelectedItem = "homotetramer";
            Find<NumericUpDown>(window, "MaxSamplesBox").Value = 32768;
            // 64 voices, not 48: at c0 = 140 a 48-voice grid puts only 1.1 rows inside the
            // wavelet's resolution width and the ladder comes back confidently wrong.
            Find<NumericUpDown>(window, "VoicesBox").Value = 64;
            Find<NumericUpDown>(window, "DurationBox").Value = 8;
            Find<NumericUpDown>(window, "C0Box").Value = 140;

            Find<CheckBox>(window, "MzRangeBox").IsChecked = true;
            Find<NumericUpDown>(window, "MinMzBox").Value = 1100;
            Find<NumericUpDown>(window, "MaxMzBox").Value = 4000;
            Find<CheckBox>(window, "RefineBox").IsChecked = true;
            Find<CheckBox>(window, "OligomerBox").IsChecked = true;
            Find<NumericUpDown>(window, "MaxOrderBox").Value = 4;

            var scalogram = Find<Image>(window, "ScalogramImage");
            var species = Find<TextBlock>(window, "SpeciesText");
            var status = Find<TextBlock>(window, "StatusText");

            RaiseClick(window, "RunButton");
            PumpUntil(() => scalogram.Source != null, TimeSpan.FromSeconds(180));

            TestContext.WriteLine(status.Text);
            TestContext.WriteLine(species.Text);

            StringAssert.Contains("subunit", species.Text!);
            StringAssert.Contains("-mer", species.Text!);
            StringAssert.Contains("oligomers:", status.Text!);
            // Deliberately no assertion on the subunit mass. The ladder search needs good
            // peak positions and this test runs a short, coarse transform for speed --
            // exactly the regime where it can return a confidently wrong divisor. That it
            // recovers the real subunit is asserted in OligomerAnalysisTests, at settings
            // that earn it.
        }

        private static void RaiseClick(Control root, string name)
        {
            var button = Find<Button>(root, name);
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        }

        /// <summary>
        /// Run is async, so drive the dispatcher until the work lands or time runs out.
        /// </summary>
        private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (condition())
                {
                    return;
                }
                System.Threading.Thread.Sleep(20);
            }
            Assert.Fail($"condition not met within {timeout.TotalSeconds:F0}s");
        }
    }
}
