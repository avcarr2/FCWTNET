using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FCWTNET.Analysis;
using FCWTNET.Simulation;
using FCWTNET.Transform.Wavelets;
using FCWTNET.Visualization;

namespace FCWTNET.Gui
{
    public partial class MainWindow : Window
    {
        private const int ScalogramWidth = 1024;
        private const int ScalogramHeight = 512;
        private const int TransientWidth = 512;
        private const int TransientHeight = 160;

        private readonly CwtWorkflow _workflow = new();
        private CwtWorkflow.Result? _lastResult;
        private bool _initialised;

        public MainWindow()
        {
            InitializeComponent();
            PopulateChoices();
            _initialised = true;
            ApplyPreset(Presets.Get("single-peak"));

            // The overlay is positioned in canvas pixels, so it has to be redrawn
            // whenever the canvas is laid out or resized -- otherwise the markers keep
            // the geometry of whatever size the window happened to be at run time.
            OverlayCanvas.SizeChanged += (_, _) =>
            {
                if (_lastResult != null)
                {
                    DrawTruthOverlay(_lastResult);
                }
            };
        }

        // No hand-written InitializeComponent here: the XAML compiler generates one that
        // also assigns the x:Name fields. Declaring our own shadows it, leaving every
        // named control null.

        private void PopulateChoices()
        {
            PresetBox.ItemsSource = Presets.All.Values.Select(p => p.Name).OrderBy(n => n).ToList();
            PresetBox.SelectedItem = "single-peak";

            ColormapBox.ItemsSource = Colormaps.All.Keys.ToList();
            ColormapBox.SelectedItem = Colormaps.Default.Name;

            ScaleBox.ItemsSource = Enum.GetNames<IntensityScale>();
            ScaleBox.SelectedIndex = 0;

            AxisBox.ItemsSource = new[] { "m/z", "Frequency (Hz)" };
            AxisBox.SelectedIndex = 0;

            IonizationBox.ItemsSource = Enum.GetNames<IonizationMode>();
            IonizationBox.SelectedIndex = 0;

            WaveletBox.ItemsSource = WaveletCatalog.Names.ToList();
            WaveletBox.SelectedItem = WaveletCatalog.Default.Name;
            UpdateWaveletControls();
        }

        private void OnWaveletChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_initialised)
            {
                return;
            }
            // Parameters are per-wavelet, so a previous wavelet's values must not carry over.
            WaveletParameterBox.Text = string.Empty;
            UpdateWaveletControls();
        }

        /// <summary>
        /// Shows the parameter entry that applies to the selected wavelet, and reports what
        /// it can resolve -- which is the number that decides whether close components
        /// separate at all.
        /// </summary>
        private void UpdateWaveletControls()
        {
            string name = (string?)WaveletBox.SelectedItem ?? "morlet";
            bool isMorlet = name == "morlet";

            // The Morlet's shape parameter already has its own control.
            C0Panel.IsVisible = isMorlet;
            WaveletParameterPanel.IsVisible = !isMorlet;
            WaveletParameterLabel.Text = name switch
            {
                "paul" => "Order (integer, default 4)",
                "bump" => "Centre, width (default 5, 0.6)",
                "log-gabor" => "Centre, bandwidth ratio (default 6, 1.15)",
                "dog" => "Order (integer, default 2)",
                _ => "Parameters"
            };

            try
            {
                IWavelet wavelet = BuildWavelet();
                WaveletSummary.Text =
                    $"{wavelet.Description}\n" +
                    $"resolves components >{wavelet.RelativeFrequencyResolution * 100:F1}% apart";
            }
            catch (Exception exception)
            {
                WaveletSummary.Text = exception.Message;
            }
        }

        /// <summary>The wavelet described by the current controls.</summary>
        private IWavelet BuildWavelet()
        {
            string name = (string?)WaveletBox.SelectedItem ?? "morlet";
            if (name == "morlet")
            {
                return new MorletWavelet((double)(C0Box.Value ?? 6));
            }
            string parameters = WaveletParameterBox.Text?.Trim() ?? string.Empty;
            return WaveletCatalog.Parse(
                parameters.Length > 0 ? $"{name}:{parameters}" : name);
        }

        private void OnPresetChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Fires while the ComboBox is being populated in the constructor.
            if (!_initialised || PresetBox.SelectedItem is not string name)
            {
                return;
            }
            ApplyPreset(Presets.Get(name));
        }

        /// <summary>Copies a preset's settings into the controls so they can be tweaked.</summary>
        private void ApplyPreset(SimulationPreset preset)
        {
            PresetDescription.Text = preset.Description;
            DurationBox.Value = (decimal)(preset.Config.DurationSeconds * 1000);
            DecayBox.Value = (decimal)(preset.Config.DefaultDecayConstant * 1000);
            SnrBox.Value = (decimal)(preset.Config.SignalToNoise ?? 0);
            AdcBox.Value = preset.Config.AdcBits ?? 0;
            DriftBox.Value = (decimal)(preset.Species.Count > 0
                ? preset.Species[0].FrequencyDriftPerSecond * 100
                : 0);

            // Charge envelope controls only apply to presets built from one.
            ChargeEnvelopeTemplate? template = preset.ChargeEnvelope;
            ChargePanel.IsVisible = template != null;
            if (template != null)
            {
                IonizationBox.SelectedItem = template.Options.Mode.ToString();
                // 0 means "use the model", so clear any previous override.
                CentroidBox.Value = (decimal)(template.Options.CentroidCharge ?? 0);
                EnvelopeWidthBox.Value = (decimal)(template.Options.Width ?? 0);
                IsotopePeaksBox.Value = template.IsotopePeaks
                    ?? IsotopeEnvelope.SuggestedPeakCount(template.MonoisotopicMass);
                UpdateEnvelopeSummary();
            }
        }

        private void OnIonizationChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_initialised)
            {
                return;
            }
            // Switching mode changes the model defaults, so drop stale overrides.
            CentroidBox.Value = 0;
            EnvelopeWidthBox.Value = 0;
            UpdateEnvelopeSummary();
        }

        /// <summary>
        /// Shows which charge states the current settings produce, so the envelope can be
        /// tuned without running the whole transform.
        /// </summary>
        private void UpdateEnvelopeSummary()
        {
            ChargeEnvelopeTemplate? template = CurrentPreset()?.ChargeEnvelope;
            if (template == null)
            {
                EnvelopeSummary.Text = string.Empty;
                return;
            }

            try
            {
                IReadOnlyList<ChargeStateAbundance> envelope = template.Envelope(BuildEnvelopeOptions(template));
                ChargeStateAbundance top = envelope.OrderByDescending(c => c.Abundance).First();
                EnvelopeSummary.Text =
                    $"{envelope.Count} charge states, {envelope.Min(c => c.Charge)}+ to {envelope.Max(c => c.Charge)}+\n" +
                    $"base peak {top.Charge}+ at m/z {top.Mz:F1}\n" +
                    $"m/z {envelope.Min(c => c.Mz):F0} to {envelope.Max(c => c.Mz):F0}";
            }
            catch (Exception exception)
            {
                EnvelopeSummary.Text = exception.Message;
            }
        }

        private SimulationPreset? CurrentPreset()
        {
            return PresetBox.SelectedItem is string name ? Presets.Get(name) : null;
        }

        /// <summary>Envelope options from the controls; 0 means "use the model".</summary>
        private ChargeStateEnvelopeOptions BuildEnvelopeOptions(ChargeEnvelopeTemplate template)
        {
            ChargeStateEnvelopeOptions options = template.Options.Clone();
            if (IonizationBox.SelectedItem is string mode)
            {
                options.Mode = Enum.Parse<IonizationMode>(mode);
            }
            double centroid = (double)(CentroidBox.Value ?? 0);
            double width = (double)(EnvelopeWidthBox.Value ?? 0);
            options.CentroidCharge = centroid > 0 ? centroid : null;
            options.Width = width > 0 ? width : null;
            return options;
        }

        /// <summary>
        /// Colormap, intensity scale and axis choices only affect presentation, so they
        /// re-render the existing result rather than re-running the transform.
        /// </summary>
        private void OnDisplayChanged(object? sender, RoutedEventArgs e)
        {
            if (!_initialised || _lastResult == null)
            {
                return;
            }
            Render(_lastResult);
        }

        private void OnMzRangeToggled(object? sender, RoutedEventArgs e)
        {
            if (!_initialised)
            {
                return;
            }
            MzRangePanel.IsVisible = MzRangeBox.IsChecked == true;
        }

        private void OnRefineToggled(object? sender, RoutedEventArgs e)
        {
            if (!_initialised)
            {
                return;
            }
            // Discovery is a refinement of the fit, so it cannot be asked for on its own.
            bool refining = RefineBox.IsChecked == true;
            DiscoverBox.IsEnabled = refining;
            if (!refining)
            {
                DiscoverBox.IsChecked = false;
            }
        }

        private async void OnRun(object? sender, RoutedEventArgs e)
        {
            if (PresetBox.SelectedItem is not string presetName)
            {
                return;
            }

            RunButton.IsEnabled = false;
            StatusText.Text = "Simulating and transforming...";
            try
            {
                SimulationPreset preset = Presets.Get(presetName);
                (OrbitrapSimulatorConfig config, List<IonSpecies> species) = BuildFromControls(preset);
                CwtSettings settings = BuildSettings(config, species);

                // The CWT of a long transient takes seconds; keep the UI responsive.
                CwtWorkflow.Result result = await Task.Run(
                    () => _workflow.RunSimulation(config, species, settings));

                _lastResult = result;
                Render(result);
                StatusText.Text = Describe(result, settings);
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Error: {exception.Message}";
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }

        private (OrbitrapSimulatorConfig, List<IonSpecies>) BuildFromControls(SimulationPreset preset)
        {
            OrbitrapSimulatorConfig baseConfig = preset.Config;
            double snr = (double)(SnrBox.Value ?? 0);
            int adcBits = (int)(AdcBox.Value ?? 0);

            var config = new OrbitrapSimulatorConfig
            {
                CalibrationCoefficient = baseConfig.CalibrationCoefficient,
                SamplingRate = baseConfig.SamplingRate,
                DurationSeconds = (double)(DurationBox.Value ?? 16) / 1000.0,
                Decay = baseConfig.Decay,
                DefaultDecayConstant = (double)(DecayBox.Value ?? 500) / 1000.0,
                SignalToNoise = snr > 0 ? snr : null,
                PinkNoiseFraction = baseConfig.PinkNoiseFraction,
                AdcBits = adcBits > 0 ? adcBits : null,
                AdcFullScale = baseConfig.AdcFullScale,
                Seed = baseConfig.Seed
            };

            double drift = (double)(DriftBox.Value ?? 0) / 100.0;

            // When the preset is built from a charge envelope, rebuild it from the
            // controls so ionisation mode, centroid and width take effect. Otherwise copy
            // the preset's species, overriding drift.
            List<IonSpecies> species;
            if (preset.ChargeEnvelope is { } template)
            {
                species = ChargeStateEnvelope.CreateSpecies(
                    template.MonoisotopicMass,
                    BuildEnvelopeOptions(template),
                    isotopePeaks: (int)(IsotopePeaksBox.Value ?? 24),
                    frequencyDriftPerSecond: drift).ToList();
            }
            else
            {
                species = preset.Species.Select(s => new IonSpecies
                {
                    MonoisotopicMass = s.MonoisotopicMass,
                    Charge = s.Charge,
                    Abundance = s.Abundance,
                    IsotopePeaks = s.IsotopePeaks,
                    DecayConstant = s.DecayConstant,
                    InitialPhase = s.InitialPhase,
                    FrequencyDriftPerSecond = drift
                }).ToList();
            }

            return (config, species);
        }

        private CwtSettings BuildSettings(OrbitrapSimulatorConfig config, List<IonSpecies> species)
        {
            IWavelet wavelet = BuildWavelet();
            var settings = new CwtSettings
            {
                Voices = (int)(VoicesBox.Value ?? 48),
                C0 = (float)(double)(C0Box.Value ?? 6),
                MaxSamples = (int)(MaxSamplesBox.Value ?? 65536),
                Wavelet = wavelet,
                RefinePeaks = RefineBox.IsChecked == true,
                DiscoverComponents = DiscoverBox.IsChecked == true,
                Deconvolve = DeconvolveBox.IsChecked == true
            };

            if (MzRangeBox.IsChecked == true)
            {
                settings.MinMz = (double)(MinMzBox.Value ?? 600);
                settings.MaxMz = (double)(MaxMzBox.Value ?? 2300);
            }

            // Bracket the octaves around where the ions actually are. Running a throwaway
            // simulation would be wasteful, so derive the frequencies directly.
            var simulator = new OrbitrapSimulator(config);
            var truth = new List<IsotopologueTruth>();
            foreach (IonSpecies ion in species)
            {
                for (int k = 0; k < Math.Max(1, ion.IsotopePeaks); k++)
                {
                    truth.Add(new IsotopologueTruth
                    {
                        Mz = ion.MzForIsotope(k),
                        Frequency = simulator.FrequencyForMz(ion.MzForIsotope(k)),
                        FrequencyDriftPerSecond = ion.FrequencyDriftPerSecond
                    });
                }
            }

            int sampleCount = (int)Math.Round(config.DurationSeconds * config.SamplingRate);
            double windowSeconds = Math.Min(sampleCount, settings.MaxSamples) / (double)config.SamplingRate;
            (settings.StartOctave, settings.EndOctave) =
                CwtWorkflow.BracketOctaves(truth, config.SamplingRate, wavelet, windowSeconds);
            return settings;
        }

        private void Render(CwtWorkflow.Result result)
        {
            PlaceholderText.IsVisible = false;

            Colormap colormap = Colormaps.Get((string)(ColormapBox.SelectedItem ?? "viridis"));
            IntensityScale scale = Enum.Parse<IntensityScale>((string)(ScaleBox.SelectedItem ?? "Linear"));

            ScalogramImage.Source = ToBitmap(
                result.Render(ScalogramWidth, ScalogramHeight, colormap, scale),
                ScalogramWidth, ScalogramHeight);

            TransientImage.Source = ToBitmap(
                WaveformRenderer.ToRgba(result.Samples, TransientWidth, TransientHeight),
                TransientWidth, TransientHeight);

            PeakText.Text = FormatPeaks(result);
            PeakHeading.Text = result.PeaksWereRefined ? "Fitted components" : "Detected peaks";
            SpeciesText.Text = FormatSpecies(result);
            DrawTruthOverlay(result);
        }

        /// <summary>
        /// Marks the true m/z of each simulated isotopologue along the right edge, so the
        /// recovered ridge can be compared against ground truth by eye.
        /// </summary>
        private void DrawTruthOverlay(CwtWorkflow.Result result)
        {
            OverlayCanvas.Children.Clear();
            if (TruthOverlayBox.IsChecked != true || result.GroundTruth == null)
            {
                return;
            }

            double[] frequencies = result.FrequencyAxis;
            double height = OverlayCanvas.Bounds.Height;
            if (height <= 0)
            {
                height = ScalogramHeight;
            }

            foreach (IsotopologueTruth truth in result.GroundTruth)
            {
                int row = NearestRow(frequencies, truth.Frequency);
                if (row < 0)
                {
                    continue;
                }
                // The image puts high frequencies at the top, so invert the row.
                double y = (1.0 - (double)row / (frequencies.Length - 1)) * height;
                var marker = new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = new Avalonia.Point(0, y),
                    EndPoint = new Avalonia.Point(18, y),
                    Stroke = Brushes.OrangeRed,
                    StrokeThickness = 1.5
                };
                OverlayCanvas.Children.Add(marker);
            }
        }

        private static int NearestRow(double[] axis, double value)
        {
            if (axis.Length == 0)
            {
                return -1;
            }
            int best = 0;
            double bestDistance = Math.Abs(axis[0] - value);
            for (int i = 1; i < axis.Length; i++)
            {
                double distance = Math.Abs(axis[i] - value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private string FormatPeaks(CwtWorkflow.Result result)
        {
            bool useMz = (string?)AxisBox.SelectedItem != "Frequency (Hz)";
            // Which species claimed each peak, so the table reads as an assignment rather
            // than a list of unrelated numbers.
            var assignment = new Dictionary<double, (int Species, int Charge)>();
            for (int i = 0; i < result.Species.Count; i++)
            {
                foreach (AssignedPeak assigned in result.Species[i].Group.Peaks)
                {
                    assignment[assigned.Mz] = (i + 1, assigned.Charge);
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine(useMz
                ? "  #      m/z    magnitude     ppm   z  resid"
                : "  #   freq (Hz)  magnitude          z  resid");

            int rank = 1;
            foreach (DetectedPeak peak in result.Peaks.Take(15))
            {
                double value = peak.Position;
                if (!useMz)
                {
                    // Peaks were found against the m/z axis; convert back for display.
                    value = result.FrequencyAxis[Math.Clamp(peak.RowIndex, 0, result.FrequencyAxis.Length - 1)];
                }

                string ppm = string.Empty;
                if (useMz && result.GroundTruth is { Count: > 0 })
                {
                    IsotopologueTruth nearest = result.GroundTruth
                        .OrderBy(t => Math.Abs(t.Mz - peak.Position)).First();
                    ppm = ((peak.Position - nearest.Mz) / nearest.Mz * 1e6).ToString("F0", CultureInfo.InvariantCulture);
                }

                string charge = assignment.TryGetValue(peak.Position, out (int Species, int Charge) found)
                    ? $"{found.Charge}+"
                    : "-";
                // How well one wavelet response explained this ridge. A value far above its
                // neighbours' is a blend, and its position is the one to distrust.
                string residual = double.IsNaN(peak.FitResidual)
                    ? "-"
                    : peak.FitResidual.ToString("0.0E+0", CultureInfo.InvariantCulture);

                builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,3}  {1,10:F4}  {2,10:E2}  {3,6} {4,4} {5,6}",
                    rank, value, peak.Magnitude, ppm, charge, residual));
                rank++;
            }

            if (result.Peaks.Count == 0)
            {
                builder.AppendLine("  (no ridges above threshold)");
            }
            return builder.ToString();
        }

        /// <summary>
        /// The deconvolved species. A charge envelope is a dozen unrelated m/z values until
        /// this runs and one protein afterwards, which is the only form the answer is
        /// useful in once more than one species is present.
        /// </summary>
        private static string FormatSpecies(CwtWorkflow.Result result)
        {
            if (result.Species.Count == 0)
            {
                return "Enable \"Group charge states into species\" to deconvolve.";
            }

            var builder = new StringBuilder();
            int index = 1;
            foreach (SpeciesResult species in result.Species)
            {
                ChargeStateGroup group = species.Group;
                builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}. {1:F2} Da  +/-{2:F0} ppm", index, group.MonoisotopicMass,
                    group.MassUncertaintyPpm));
                builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0} states {1}+ to {2}+, score {3:F3}",
                    group.Peaks.Count, group.MinCharge, group.MaxCharge, group.Score));
                builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   contiguity {0:F2} ({1})", group.Contiguity, group.Method));
                if (species.Envelope != null)
                {
                    builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "   envelope {0}, fit {1:F2}",
                        species.Envelope.Mode, species.Envelope.Similarity));
                }
                builder.AppendLine();
                index++;
            }
            return builder.ToString();
        }

        private static string Describe(CwtWorkflow.Result result, CwtSettings settings)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{result.Samples.Length} samples, {result.WindowSeconds * 1000:F2} ms");
            if (settings.MinMz.HasValue || settings.MaxMz.HasValue)
            {
                builder.AppendLine($"m/z {settings.MinMz ?? 200:F0}-{settings.MaxMz ?? 4000:F0}, " +
                                   $"{result.ScaleGrid.Count} scales over " +
                                   $"{result.ScaleGrid.Octaves:F2} octaves, {settings.Voices} voices");
            }
            else
            {
                builder.AppendLine($"octaves {settings.StartOctave}:{settings.EndOctave}, " +
                                   $"{settings.Voices} voices, {result.Modulus.GetLength(0)} rows");
            }
            // Nyquist and signal-length clamps are reported rather than applied silently.
            foreach (string clamp in result.ScaleGrid.Clamps.Describe())
            {
                builder.AppendLine(clamp);
            }
            // The wavelet's own bandwidth decides what can be told apart at all, so it is
            // worth seeing next to the result.
            IWavelet wavelet = settings.EffectiveWavelet;
            builder.AppendLine($"{wavelet.Name}: resolves components " +
                               $">{wavelet.RelativeFrequencyResolution * 100:F1}% apart");
            builder.AppendLine($"backend {(settings.Backend ?? Transform.CwtBackends.Default).Name}");
            builder.AppendLine(result.PeaksWereRefined
                ? $"{result.Peaks.Count} component(s) fitted"
                : $"{result.Peaks.Count} ridge(s) detected");
            builder.Append(result.Species.Count > 0
                ? $"{result.Species.Count} species deconvolved"
                : "not deconvolved");
            return builder.ToString();
        }

        /// <summary>Wraps an RGBA buffer in an Avalonia bitmap.</summary>
        private static WriteableBitmap ToBitmap(byte[] rgba, int width, int height)
        {
            var bitmap = new WriteableBitmap(
                new Avalonia.PixelSize(width, height),
                new Avalonia.Vector(96, 96),
                PixelFormat.Rgba8888,
                AlphaFormat.Unpremul);

            using (ILockedFramebuffer buffer = bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(rgba, 0, buffer.Address, rgba.Length);
            }
            return bitmap;
        }
    }
}
