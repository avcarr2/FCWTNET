using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FCWTNET.Transform;
using FCWTNET.Transform.Wavelets;

namespace FCWTNET.Analysis
{
    /// <summary>One component recovered by <see cref="MultiComponentFit"/>.</summary>
    public class FittedComponent
    {
        /// <summary>Centre frequency in cycles per sample.</summary>
        public double NormalisedFrequency { get; init; }

        /// <summary>Fitted power, in the units of the profile that was fitted.</summary>
        public double Power { get; init; }

        /// <summary>Amplitude, the square root of <see cref="Power"/>.</summary>
        public double Amplitude => Math.Sqrt(Math.Max(0, Power));

        /// <summary>Frequency in Hz for a given sampling rate.</summary>
        public double FrequencyHz(int samplingRate) => NormalisedFrequency * samplingRate;

        /// <summary>m/z under the Orbitrap calibration m/z = A/f^2.</summary>
        public double Mz(double calibrationCoefficient, int samplingRate)
        {
            double f = FrequencyHz(samplingRate);
            return f > 0 ? calibrationCoefficient / (f * f) : double.NaN;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "f {0:E6} cycles/sample, amplitude {1:E3}", NormalisedFrequency, Amplitude);
        }
    }

    /// <summary>Outcome of a multi-component fit.</summary>
    public class MultiComponentFitResult
    {
        public IReadOnlyList<FittedComponent> Components { get; init; } = Array.Empty<FittedComponent>();

        /// <summary>
        /// Residual sum of squares divided by the total power in the profile, so 0 is a
        /// perfect fit and 1 explains nothing.
        /// </summary>
        public double RelativeResidual { get; init; }

        /// <summary>Coordinate-descent sweeps actually run.</summary>
        public int Sweeps { get; init; }

        /// <summary>True when the sweeps stopped improving rather than running out.</summary>
        public bool Converged { get; init; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} components, residual {1:E2}, {2} sweeps{3}",
                Components.Count, RelativeResidual, Sweeps, Converged ? "" : " (not converged)");
        }
    }

    /// <summary>Knobs for <see cref="MultiComponentFit"/>.</summary>
    public class MultiComponentFitOptions
    {
        /// <summary>Coordinate-descent sweeps over the components.</summary>
        public int MaxSweeps { get; set; } = 30;

        /// <summary>Stop when a sweep improves the residual by less than this, relatively.</summary>
        public double Tolerance { get; set; } = 1e-12;

        /// <summary>
        /// How far a component may move from where it started, as a fraction of the
        /// wavelet's relative frequency resolution. The default lets a peak shift by half a
        /// resolution width, which is the scale on which a blend displaces a local maximum;
        /// components are additionally never allowed past the midpoint to their neighbours,
        /// so two cannot collapse onto one.
        /// </summary>
        public double SearchWidthInResolutions { get; set; } = 1.0;

        /// <summary>
        /// Ridge term added to the normal equations, relative to their trace. Two components
        /// the wavelet barely separates give a nearly singular design matrix, and a small
        /// ridge keeps the amplitudes finite without moving a well-posed fit.
        /// </summary>
        public double Ridge { get; set; } = 1e-10;

        /// <summary>Most components <see cref="MultiComponentFit.FitWithSplitting"/> will introduce.</summary>
        public int MaxComponents { get; set; } = 64;

        /// <summary>
        /// A split is kept only if it cuts the residual to below this fraction of what one
        /// component managed. Splitting always reduces the residual -- two free parameters
        /// where there was one -- so the threshold is what stops it running away, and it is
        /// deliberately strict: a genuine second ion typically drops the residual by orders
        /// of magnitude, while fitting noise improves it by a few percent.
        /// </summary>
        public double SplitImprovementFactor { get; set; } = 0.25;

        /// <summary>
        /// Initial separation of a trial split, as a fraction of the wavelet's resolution.
        /// Wide enough that the two halves do not start on top of each other, narrow enough
        /// that the search can still pull them apart or back together.
        /// </summary>
        public double SplitSeparationInResolutions { get; set; } = 0.5;
    }

    /// <summary>
    /// Fits a sum of wavelet responses to a scalogram's scale profile, so that components
    /// closer together than the wavelet resolves can still be located.
    ///
    /// <para>
    /// Reading ridges as local maxima assumes each maximum belongs to one ion. Where two
    /// charge states sit closer than the wavelet's resolution they merge into a single
    /// broadened bump whose peak lies between them and belongs to neither, and no amount of
    /// interpolation recovers what the maximum is not. Fitting the sum does, because the
    /// blend is still exactly the superposition of two known shapes.
    /// </para>
    ///
    /// <para><b>Fit power, not magnitude.</b> The CWT is linear, so
    /// <c>W(a,t) = sum_k A_k exp(i w_k t) psi(a w_k)</c>. The modulus of that sum depends on
    /// the relative phases and therefore beats in time, so the time-averaged <em>modulus</em>
    /// is not the sum of the individual moduli and cannot be fitted as one. The time
    /// averaged <em>squared</em> modulus can: the cross terms oscillate at the difference
    /// frequencies and average away, leaving
    /// <c>mean|W(a)|^2 = sum_k A_k^2 psi(a w_k)^2</c> exactly. Charge states of a protein
    /// differ by a few percent of several hundred kHz, so a 16 ms transient covers hundreds
    /// of beat periods and the cancellation is complete.
    /// </para>
    ///
    /// <para>
    /// Given the frequencies the powers are a linear least-squares problem, so only the
    /// frequencies are searched -- the separable, or variable-projection, form. The search
    /// is coordinate descent, one component at a time, each confined between the midpoints
    /// to its neighbours so that components cannot swap or collapse together.
    /// </para>
    /// </summary>
    public static class MultiComponentFit
    {
        /// <summary>
        /// Time-averaged <em>squared</em> modulus per row -- the quantity the fit models.
        /// </summary>
        /// <param name="modulus">CWT magnitudes, [frequency row, time].</param>
        /// <param name="edgeFraction">
        /// Fraction of the time axis dropped at each end, for the reason given in
        /// <see cref="RidgeExtraction.DefaultEdgeFraction"/>.
        /// </param>
        public static double[] TimeAveragedPower(double[,] modulus,
            double edgeFraction = RidgeExtraction.DefaultEdgeFraction)
        {
            if (modulus == null)
            {
                throw new ArgumentNullException(nameof(modulus));
            }
            if (edgeFraction < 0 || edgeFraction >= 0.5)
            {
                throw new ArgumentOutOfRangeException(nameof(edgeFraction),
                    "edgeFraction must lie in [0, 0.5)");
            }

            int rows = modulus.GetLength(0);
            int columns = modulus.GetLength(1);
            int margin = (int)(columns * edgeFraction);
            int count = columns - 2 * margin;
            if (count < 1)
            {
                margin = 0;
                count = columns;
            }

            var power = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double sum = 0;
                for (int j = 0; j < count; j++)
                {
                    double v = modulus[i, margin + j];
                    sum += v * v;
                }
                power[i] = sum / count;
            }
            return power;
        }

        /// <summary>
        /// Fits one response per starting frequency to a scale profile of squared modulus.
        /// </summary>
        /// <param name="meanPower">Time-averaged squared modulus, one entry per grid row.</param>
        /// <param name="scaleGrid">The grid the transform was computed on.</param>
        /// <param name="initialFrequencies">
        /// Starting normalised frequencies, typically the local maxima from
        /// <see cref="RidgeExtraction.FindPeaks(double[], double[], double, int, ScaleGrid)"/>.
        /// Order does not matter; they are sorted internally.
        /// </param>
        public static MultiComponentFitResult Fit(double[] meanPower, ScaleGrid scaleGrid,
            IReadOnlyList<double> initialFrequencies, MultiComponentFitOptions? options = null)
        {
            options ??= new MultiComponentFitOptions();
            if (meanPower == null)
            {
                throw new ArgumentNullException(nameof(meanPower));
            }
            if (scaleGrid == null)
            {
                throw new ArgumentNullException(nameof(scaleGrid));
            }
            if (initialFrequencies == null)
            {
                throw new ArgumentNullException(nameof(initialFrequencies));
            }
            if (meanPower.Length != scaleGrid.Count)
            {
                throw new ArgumentException(
                    $"meanPower has {meanPower.Length} rows but the grid has {scaleGrid.Count}",
                    nameof(meanPower));
            }
            if (options.MaxSweeps < 1)
            {
                throw new ArgumentException("MaxSweeps must be at least 1", nameof(options));
            }
            if (options.SearchWidthInResolutions <= 0)
            {
                throw new ArgumentException("SearchWidthInResolutions must be positive", nameof(options));
            }
            if (initialFrequencies.Any(f => f <= 0))
            {
                throw new ArgumentException("frequencies must be positive", nameof(initialFrequencies));
            }
            if (initialFrequencies.Count == 0)
            {
                return new MultiComponentFitResult { Converged = true };
            }

            IWavelet wavelet = scaleGrid.Wavelet;
            double[] scales = scaleGrid.Scales;
            // Angular frequency is what the response is a function of.
            double[] omega = initialFrequencies.Select(f => 2 * Math.PI * f).OrderBy(w => w).ToArray();
            int k = omega.Length;

            // Each component is confined between the midpoints to its neighbours, in log
            // frequency, and within the requested multiple of the wavelet's resolution.
            // Both together stop a component wandering onto its neighbour's peak.
            double halfWidth = 0.5 * options.SearchWidthInResolutions * wavelet.RelativeFrequencyResolution;
            var lower = new double[k];
            var upper = new double[k];
            for (int c = 0; c < k; c++)
            {
                double low = omega[c] * (1 - halfWidth);
                double high = omega[c] * (1 + halfWidth);
                if (c > 0)
                {
                    low = Math.Max(low, Math.Sqrt(omega[c - 1] * omega[c]));
                }
                if (c < k - 1)
                {
                    high = Math.Min(high, Math.Sqrt(omega[c] * omega[c + 1]));
                }
                lower[c] = low;
                upper[c] = high;
            }

            double totalPower = meanPower.Sum(p => p * p);
            double[] powers = SolvePowers(meanPower, scales, wavelet, omega, options.Ridge);
            double residual = ResidualSumOfSquares(meanPower, scales, wavelet, omega, powers);

            int sweep = 0;
            bool converged = false;
            for (; sweep < options.MaxSweeps; sweep++)
            {
                double before = residual;
                for (int c = 0; c < k; c++)
                {
                    if (upper[c] <= lower[c])
                    {
                        continue;
                    }
                    double best = MinimiseOver(lower[c], upper[c], candidate =>
                    {
                        double original = omega[c];
                        omega[c] = candidate;
                        double[] fitted = SolvePowers(meanPower, scales, wavelet, omega, options.Ridge);
                        double value = ResidualSumOfSquares(meanPower, scales, wavelet, omega, fitted);
                        omega[c] = original;
                        return value;
                    });
                    omega[c] = best;
                }
                powers = SolvePowers(meanPower, scales, wavelet, omega, options.Ridge);
                residual = ResidualSumOfSquares(meanPower, scales, wavelet, omega, powers);

                if (before - residual <= options.Tolerance * Math.Max(before, double.Epsilon))
                {
                    converged = true;
                    sweep++;
                    break;
                }
            }

            var components = new List<FittedComponent>(k);
            for (int c = 0; c < k; c++)
            {
                components.Add(new FittedComponent
                {
                    NormalisedFrequency = omega[c] / (2 * Math.PI),
                    Power = powers[c]
                });
            }

            return new MultiComponentFitResult
            {
                Components = components,
                RelativeResidual = totalPower > 0 ? residual / totalPower : 0,
                Sweeps = sweep,
                Converged = converged
            };
        }

        /// <summary>
        /// As <see cref="Fit"/>, but also discovers components that never produced a local
        /// maximum of their own.
        ///
        /// <para>
        /// <see cref="Fit"/> refines the positions of components it is told about; it cannot
        /// invent one. Two ions well inside the wavelet's resolution merge into a single
        /// bump, so peak detection offers a single seed and the fit dutifully returns a
        /// single wrong answer. This repeatedly takes the component whose neighbourhood the
        /// model fits worst, tries it as two, and keeps the split only when it cuts the
        /// residual by a factor -- which a real second ion does by orders of magnitude and
        /// noise does not.
        /// </para>
        ///
        /// <para>
        /// This is the step that lets <c>c0</c> come back down, and it is not free: it is a
        /// model-selection decision made from one profile, so a low signal-to-noise
        /// transient can talk it into a component that is not there. The residual is
        /// reported so the decision can be second-guessed.
        /// </para>
        /// </summary>
        public static MultiComponentFitResult FitWithSplitting(double[] meanPower, ScaleGrid scaleGrid,
            IReadOnlyList<double> initialFrequencies, MultiComponentFitOptions? options = null)
        {
            options ??= new MultiComponentFitOptions();
            if (options.SplitImprovementFactor <= 0 || options.SplitImprovementFactor >= 1)
            {
                throw new ArgumentException("SplitImprovementFactor must lie in (0, 1)", nameof(options));
            }
            if (options.MaxComponents < 1)
            {
                throw new ArgumentException("MaxComponents must be at least 1", nameof(options));
            }

            MultiComponentFitResult best = Fit(meanPower, scaleGrid, initialFrequencies, options);
            if (best.Components.Count == 0)
            {
                return best;
            }

            double separation = options.SplitSeparationInResolutions
                                * scaleGrid.Wavelet.RelativeFrequencyResolution;

            while (best.Components.Count < options.MaxComponents)
            {
                int worst = WorstFittingComponent(meanPower, scaleGrid, best);
                if (worst < 0)
                {
                    break;
                }

                var seeds = best.Components.Select(c => c.NormalisedFrequency).ToList();
                double centre = seeds[worst];
                seeds.RemoveAt(worst);
                seeds.Add(centre * (1 - 0.5 * separation));
                seeds.Add(centre * (1 + 0.5 * separation));

                MultiComponentFitResult split = Fit(meanPower, scaleGrid, seeds, options);
                if (split.RelativeResidual > best.RelativeResidual * options.SplitImprovementFactor)
                {
                    break;
                }
                best = split;
            }
            return best;
        }

        /// <summary>
        /// Index of the component whose own neighbourhood the model explains worst, which is
        /// where an unmodelled second ion would show up. -1 when there is nothing to choose.
        /// </summary>
        private static int WorstFittingComponent(double[] meanPower, ScaleGrid scaleGrid,
            MultiComponentFitResult fit)
        {
            IWavelet wavelet = scaleGrid.Wavelet;
            double[] scales = scaleGrid.Scales;
            int rows = meanPower.Length;
            double[] omega = fit.Components.Select(c => 2 * Math.PI * c.NormalisedFrequency).ToArray();
            double[] powers = fit.Components.Select(c => c.Power).ToArray();

            var localResidual = new double[omega.Length];
            for (int i = 0; i < rows; i++)
            {
                double scale = scales[rows - 1 - i];
                double model = 0;
                for (int c = 0; c < omega.Length; c++)
                {
                    double r = wavelet.Response(scale * omega[c]);
                    model += powers[c] * r * r;
                }
                double difference = meanPower[i] - model;
                double squared = difference * difference;

                // Attribute this row's error to whichever component is responsible for most
                // of the model there; an unmodelled neighbour shows up as error around the
                // component it is hiding behind.
                int owner = -1;
                double strongest = 0;
                for (int c = 0; c < omega.Length; c++)
                {
                    double r = wavelet.Response(scale * omega[c]);
                    double contribution = powers[c] * r * r;
                    if (contribution > strongest)
                    {
                        strongest = contribution;
                        owner = c;
                    }
                }
                if (owner >= 0)
                {
                    localResidual[owner] += squared;
                }
            }

            int worst = -1;
            double worstValue = 0;
            for (int c = 0; c < localResidual.Length; c++)
            {
                if (powers[c] > 0 && localResidual[c] > worstValue)
                {
                    worstValue = localResidual[c];
                    worst = c;
                }
            }
            return worst;
        }

        /// <summary>
        /// Convenience wrapper: refines already-detected peaks in place, returning new peaks
        /// whose <see cref="DetectedPeak.Position"/> has been moved onto the fitted
        /// frequency. The position axis is assumed geometric in row, which holds for both
        /// the frequency and m/z axes a <see cref="ScaleGrid"/> produces.
        /// </summary>
        /// <param name="discoverComponents">
        /// When true, uses <see cref="FitWithSplitting"/> so that ions which never produced
        /// a local maximum of their own can still be recovered. More peaks can come back
        /// than went in.
        /// </param>
        public static IReadOnlyList<DetectedPeak> RefinePeaks(IReadOnlyList<DetectedPeak> peaks,
            double[,] modulus, ScaleGrid scaleGrid, double[] positionAxis,
            MultiComponentFitOptions? options = null,
            double edgeFraction = RidgeExtraction.DefaultEdgeFraction,
            bool discoverComponents = false)
        {
            if (peaks == null)
            {
                throw new ArgumentNullException(nameof(peaks));
            }
            if (positionAxis == null)
            {
                throw new ArgumentNullException(nameof(positionAxis));
            }
            if (scaleGrid == null)
            {
                throw new ArgumentNullException(nameof(scaleGrid));
            }
            if (positionAxis.Length != scaleGrid.Count)
            {
                throw new ArgumentException("positionAxis must have one entry per grid row",
                    nameof(positionAxis));
            }
            if (peaks.Count == 0)
            {
                return peaks;
            }

            double[] frequencies = scaleGrid.AscendingCentreFrequencies;
            // Interpolate each peak's starting frequency from its fractional row.
            var starts = peaks.Select(p => FrequencyAtRow(frequencies, p.InterpolatedRow)).ToList();

            double[] power = TimeAveragedPower(modulus, edgeFraction);
            MultiComponentFitResult fit = discoverComponents
                ? FitWithSplitting(power, scaleGrid, starts, options)
                : Fit(power, scaleGrid, starts, options);

            // Every fitted component becomes a peak, including any the splitting introduced
            // that had no local maximum to begin with.
            var refined = new List<DetectedPeak>(fit.Components.Count);
            foreach (FittedComponent component in fit.Components)
            {
                if (component.Power <= 0)
                {
                    // The fit pushed this component's amplitude to zero, which is its way of
                    // saying the ion is not there.
                    continue;
                }
                double row = RowAtFrequency(frequencies, component.NormalisedFrequency);
                refined.Add(new DetectedPeak
                {
                    RowIndex = (int)Math.Round(row),
                    InterpolatedRow = row,
                    Position = PositionAtRow(positionAxis, row),
                    Magnitude = component.Amplitude,
                    FitResidual = fit.RelativeResidual
                });
            }
            return refined.OrderByDescending(p => p.Magnitude).ToList();
        }

        /// <summary>Geometric interpolation of an axis at a fractional row.</summary>
        private static double PositionAtRow(double[] axis, double row)
        {
            int low = (int)Math.Floor(row);
            low = Math.Clamp(low, 0, axis.Length - 2);
            double fraction = row - low;
            double here = axis[low];
            double there = axis[low + 1];
            return here > 0 && there > 0
                ? here * Math.Pow(there / here, fraction)
                : here + (there - here) * fraction;
        }

        private static double FrequencyAtRow(double[] frequencies, double row)
            => PositionAtRow(frequencies, row);

        /// <summary>Inverse of <see cref="FrequencyAtRow"/> on a geometric axis.</summary>
        private static double RowAtFrequency(double[] frequencies, double frequency)
        {
            // The axis ascends, so find the bracketing rows and invert geometrically.
            if (frequency <= frequencies[0])
            {
                return 0;
            }
            if (frequency >= frequencies[^1])
            {
                return frequencies.Length - 1;
            }
            int low = 0;
            int high = frequencies.Length - 1;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (frequencies[mid] <= frequency)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }
            double ratio = frequencies[high] / frequencies[low];
            return ratio == 1 ? low : low + Math.Log(frequency / frequencies[low]) / Math.Log(ratio);
        }

        /// <summary>
        /// Non-negative least squares for the component powers, given the frequencies. The
        /// design matrix is the squared response of each component at each scale.
        /// </summary>
        private static double[] SolvePowers(double[] meanPower, double[] scales, IWavelet wavelet,
            double[] omega, double ridge)
        {
            int rows = meanPower.Length;
            int k = omega.Length;

            // Rows ascend in frequency, scales descend.
            var design = new double[rows, k];
            for (int i = 0; i < rows; i++)
            {
                double scale = scales[rows - 1 - i];
                for (int c = 0; c < k; c++)
                {
                    double response = wavelet.Response(scale * omega[c]);
                    design[i, c] = response * response;
                }
            }

            var active = Enumerable.Range(0, k).ToList();
            var powers = new double[k];
            // At most k passes: each one either accepts the solution or removes a component
            // that wanted negative power, which cannot repeat forever.
            for (int pass = 0; pass < k && active.Count > 0; pass++)
            {
                double[] solution = SolveNormalEquations(design, meanPower, active, ridge);
                int worst = -1;
                double mostNegative = 0;
                for (int a = 0; a < active.Count; a++)
                {
                    if (solution[a] < mostNegative)
                    {
                        mostNegative = solution[a];
                        worst = a;
                    }
                }
                if (worst < 0)
                {
                    Array.Clear(powers);
                    for (int a = 0; a < active.Count; a++)
                    {
                        powers[active[a]] = solution[a];
                    }
                    return powers;
                }
                // A negative power is not physical; drop that component and re-solve.
                active.RemoveAt(worst);
            }
            return powers;
        }

        /// <summary>Normal equations with a relative ridge, solved by Gaussian elimination.</summary>
        private static double[] SolveNormalEquations(double[,] design, double[] target,
            List<int> active, double ridge)
        {
            int rows = target.Length;
            int n = active.Count;
            var normal = new double[n, n + 1];

            double trace = 0;
            for (int a = 0; a < n; a++)
            {
                for (int b = a; b < n; b++)
                {
                    double sum = 0;
                    for (int i = 0; i < rows; i++)
                    {
                        sum += design[i, active[a]] * design[i, active[b]];
                    }
                    normal[a, b] = sum;
                    normal[b, a] = sum;
                }
                double rhs = 0;
                for (int i = 0; i < rows; i++)
                {
                    rhs += design[i, active[a]] * target[i];
                }
                normal[a, n] = rhs;
                trace += normal[a, a];
            }

            double lambda = ridge * (trace / Math.Max(1, n));
            for (int a = 0; a < n; a++)
            {
                normal[a, a] += lambda;
            }

            // Gaussian elimination with partial pivoting.
            for (int column = 0; column < n; column++)
            {
                int pivot = column;
                for (int r = column + 1; r < n; r++)
                {
                    if (Math.Abs(normal[r, column]) > Math.Abs(normal[pivot, column]))
                    {
                        pivot = r;
                    }
                }
                if (Math.Abs(normal[pivot, column]) < double.Epsilon)
                {
                    continue;
                }
                if (pivot != column)
                {
                    for (int c = column; c <= n; c++)
                    {
                        (normal[column, c], normal[pivot, c]) = (normal[pivot, c], normal[column, c]);
                    }
                }
                for (int r = 0; r < n; r++)
                {
                    if (r == column)
                    {
                        continue;
                    }
                    double factor = normal[r, column] / normal[column, column];
                    if (factor == 0)
                    {
                        continue;
                    }
                    for (int c = column; c <= n; c++)
                    {
                        normal[r, c] -= factor * normal[column, c];
                    }
                }
            }

            var solution = new double[n];
            for (int a = 0; a < n; a++)
            {
                solution[a] = Math.Abs(normal[a, a]) < double.Epsilon ? 0 : normal[a, n] / normal[a, a];
            }
            return solution;
        }

        private static double ResidualSumOfSquares(double[] meanPower, double[] scales,
            IWavelet wavelet, double[] omega, double[] powers)
        {
            int rows = meanPower.Length;
            double total = 0;
            for (int i = 0; i < rows; i++)
            {
                double scale = scales[rows - 1 - i];
                double model = 0;
                for (int c = 0; c < omega.Length; c++)
                {
                    if (powers[c] == 0)
                    {
                        continue;
                    }
                    double response = wavelet.Response(scale * omega[c]);
                    model += powers[c] * response * response;
                }
                double difference = meanPower[i] - model;
                total += difference * difference;
            }
            return total;
        }

        /// <summary>Golden-section minimisation of a unimodal objective on a bracket.</summary>
        private static double MinimiseOver(double low, double high, Func<double, double> objective)
        {
            const double GoldenRatio = 0.6180339887498949;
            double m1 = high - (high - low) * GoldenRatio;
            double m2 = low + (high - low) * GoldenRatio;
            double f1 = objective(m1);
            double f2 = objective(m2);

            for (int iteration = 0; iteration < 80 && high - low > 1e-14 * Math.Abs(high); iteration++)
            {
                if (f1 < f2)
                {
                    high = m2;
                    m2 = m1;
                    f2 = f1;
                    m1 = high - (high - low) * GoldenRatio;
                    f1 = objective(m1);
                }
                else
                {
                    low = m1;
                    m1 = m2;
                    f1 = f2;
                    m2 = low + (high - low) * GoldenRatio;
                    f2 = objective(m2);
                }
            }
            return 0.5 * (low + high);
        }
    }
}
