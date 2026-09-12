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

            var scalogram = Find<Image>(window, "ScalogramImage");
            RaiseClick(window, "RunButton");
            PumpUntil(() => scalogram.Source != null, TimeSpan.FromSeconds(60));

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
