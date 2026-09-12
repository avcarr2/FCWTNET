# FCWTNET

Exploring continuous wavelet transforms for analysing Orbitrap mass spectrometry
transients.

An Orbitrap measures mass by measuring frequency: ions oscillate axially at a rate set
only by their m/z, and the detector records their summed image current. The standard
analysis is an FFT over the whole transient, which assumes every component is stationary
for the entire acquisition. They are not — ions dephase, and space charge shifts their
frequencies as the acquisition proceeds. A CWT resolves time and frequency together, so
it can see what an FFT averages away.

This repository provides the pieces needed to test that idea:

- a **CWT** over transient data, either through the native
  [fCWT](https://github.com/fastlib/fCWT) library or a managed implementation
- an **Orbitrap simulator** that generates transients with known ground truth
- **visualization**: scalogram PNGs, plots, and a desktop app

## Layout

| Project | What it is |
|---|---|
| `FCWTAPI` | The library: transform, simulation, analysis, visualization |
| `FCWTNET.Cli` | `fcwtnet` command-line tool |
| `FCWTNET.Gui` | Avalonia desktop app |
| `TestFCWTAPI` | Library tests (NUnit) |
| `TestFCWTNET.Gui` | Headless GUI tests |

## Quick start

Requires the .NET 8 SDK.

```bash
dotnet build fCWT.NET.sln
dotnet test fCWT.NET.sln

# Simulate and analyse every preset; writes CSVs, ground truth, and PNGs
dotnet run --project FCWTNET.Cli -- demo --out ./demo-output

# Or one preset at a time
dotnet run --project FCWTNET.Cli -- presets
dotnet run --project FCWTNET.Cli -- simulate --preset myoglobin-13plus --out ./out
dotnet run --project FCWTNET.Cli -- analyze ./out/myoglobin-13plus.csv --out ./out

# Desktop app
dotnet run --project FCWTNET.Gui
```

## The simulator

`FCWTAPI/Simulation` builds an image-current transient from a list of ion species. Each
isotopologue contributes a decaying sinusoid:

```
s(t) = sum_i  a_i * env(t) * cos(phi_i(t) + phi_i0)  + noise
```

- **Frequency** comes from `f = sqrt(A / (m/z))`, the inverse of the `m/z = A/f^2`
  calibration the analysis side already uses, so simulated data and the analysis axes
  share one constant.
- **Isotope clusters** are spaced `1.00235/z` Th with averagine intensities (a Poisson
  model, `lambda = 0.000594*M - 0.03091`).
- **Decay** is exponential or Gaussian, per species.
- **Drift** is a linear fractional change in frequency. The phase integrates the
  instantaneous frequency, `phi(t) = 2*pi*f0*(t + b*t^2/2)`.
- **Noise** is white plus 1/f at a requested SNR, and the ADC can quantise and clip.
- Every random draw is seeded, so a configuration reproduces byte for byte.

`TransientWriter` emits the CSV layout `TransientData` already reads (calibration
coefficient, sampling rate, then one sample per line), so simulated transients flow
through the existing pipeline unchanged, with the ground truth written alongside as JSON.

### Charge state envelopes

`ChargeStateEnvelope` turns a protein mass into a distribution of charge states, in
either of the two regimes an Orbitrap user actually chooses between:

- **Native.** A folded protein cannot hold more charge than its surface supports. The
  Rayleigh limit for a droplet of protein density gives `z ≈ 0.0778·√M`
  (Fernández de la Mora, 2000), which puts myoglobin near 10+ and produces a narrow
  envelope — about five charge states.
- **Denatured.** An unfolded chain exposes far more basic sites and charges past the
  Rayleigh limit. Envelopes centre near m/z 1000 more or less regardless of protein, so
  the centroid goes as `M/1000` — myoglobin near 17+, spread over roughly 8+ to 26+.

The centroid, width, charge range, abundance threshold and state count are all
overridable, so the model is a starting point rather than a fixed answer:

```csharp
var species = ChargeStateEnvelope.CreateSpecies(
    monoisotopicMass: 16940,
    new ChargeStateEnvelopeOptions
    {
        Mode = IonizationMode.Denatured,
        CentroidCharge = 15,   // null uses the model
        Width = 2.5,           // null uses the model
        RelativeThreshold = 0.05
    },
    isotopePeaks: 8);
```

Presets built this way expose a `ChargeEnvelope` template, which is how the desktop app
regenerates the envelope live when you switch mode or move the centroid.

### Presets

| Preset | What it shows |
|---|---|
| `single-peak` | One ion, no decay or noise — the cleanest check |
| `peptide-isotopes` | A 2+ peptide with an averagine isotope cluster |
| `myoglobin-13plus` | Apo-myoglobin 13+, matching the bundled sample transient |
| `myoglobin-denatured` | A broad charge envelope, ~8+ to 26+, one ridge per charge state |
| `myoglobin-native` | A narrow envelope near the Rayleigh limit, ~8+ to 12+ |
| `mixture` | Two species with interleaving isotope clusters |
| `isotope-beat` | Two peptides over 100 ms; unresolved isotopes beat 26 and 10 times |
| `fast-decay` | Decay short against the acquisition; the ridge fades |
| `frequency-drift` | A drifting ion; the ridge slopes across the scalogram |
| `low-snr` | SNR 3 with 12-bit digitisation |

## Transform backends

`fCWT.dll` is a Windows x64 binary and there is no Linux or macOS equivalent in the repo,
so the library also ships `ManagedCwt`: a pure C# complex CWT computed in
the frequency domain. `CWTObject.Backend` defaults to native on Windows where the DLL
loads, and managed everywhere else.

The managed backend normalises by **amplitude**, so the modulus at a ridge equals the
amplitude of the component and peak heights track ion abundance. The more common
energy normalisation (Torrence & Compo's `sqrt(2*pi*a)`) biases frequency estimation:
for a tone at `w0` the modulus peaks where `a*w0 = (c0 + sqrt(c0^2 + 2))/2` rather than
at `c0`, and because `m/z` goes as `1/f^2` a 1.4% scale error becomes a 2.8% mass error.
Absolute magnitudes therefore differ between the two backends; peak positions do not.

## Choosing a wavelet

The analysing wavelet is selectable. All of them are **analytic** — zero response at
negative frequencies — so the transform is complex and its modulus is an envelope, which
is what ridge extraction needs; a real-valued wavelet gives an oscillating transform with
no ridge to follow.

| Wavelet | Spec | Shape | Resolution at defaults |
|---|---|---|---|
| Morlet | `morlet[:c0]` | Gaussian in frequency | ~39% (`c0` = 6) |
| Paul | `paul[:order]` | Sharp in time, broad in frequency | ~119% (order 4) |
| Bump | `bump[:centre,width]` | Compact support in frequency | ~15% |
| Log-Gabor | `log-gabor[:centre,bandwidth]` | Gaussian in log-frequency | ~33% |
| DOG | `dog[:order]` | Analytic derivative of Gaussian | ~116% (order 2) |

```bash
dotnet run --project FCWTNET.Cli -- wavelets
dotnet run --project FCWTNET.Cli -- analyze t.csv --out ./out --wavelet morlet:80
dotnet run --project FCWTNET.Cli -- analyze t.csv --out ./out --wavelet bump:5,0.09
```

**Morlet is the right default.** It is Gaussian in both domains, so it sits exactly on the
uncertainty bound: nothing else gives better frequency resolution for the same time
resolution. The others are worth reaching for when their particular property matters —
Paul for abrupt onsets, Bump when a huge neighbouring peak's tails would otherwise leak
into a small one, Log-Gabor for constant fractional bandwidth across a wide range.

Each wavelet peaks at its own point on the scale grid, so **the scale range that covers a
given frequency differs per wavelet** — a Paul of order 6400 does the same job at scales
three orders of magnitude from a Morlet's. The frequency and m/z axes are built from the
wavelet in use (`CwtScales.AscendingCentreFrequencies(…, IWavelet)`), so they stay correct
when you swap it; a test pins that four different wavelets recover the same m/z for the
same ion.

## Resolution, and what to expect

**Wavelet bandwidth is the knob that decides what can be told apart**, and for the Morlet
that knob is `c0`. Its frequency response is a Gaussian of width 1 around `c0`, so

```
df/f = 2*sqrt(2*ln2) / c0
```

Components closer together than that merge into one ridge no matter how many voices you
use — voices only sample the scale axis, they do not sharpen the wavelet. Concretely:

| `c0` | Resolves components apart by | Good for |
|---|---|---|
| 6 | ~39% | A single isolated peak |
| 40 | ~5.9% | Widely spaced charge states |
| 80 | ~2.9% | Adjacent charge states of a 17 kDa protein (~3% apart) |

Every wavelet reports its own `RelativeFrequencyResolution`, derived from the response it
actually applies rather than a hand-copied formula, so the number cannot drift out of step
with the transform. Raising it buys frequency resolution by spending time resolution — the
wavelet spans more cycles — which is exactly the trade a CWT exists to let you make. Each
preset declares a `SuggestedC0`; the CLI and the app use it unless you override `--c0` or
`--wavelet`.

Two consequences worth knowing before reading a scalogram:

- **Isotope peaks are not resolved.** Adjacent isotopes of a 2+ peptide near m/z 1000
  differ by ~0.02% in frequency, which would need `c0` above 10 000 and a transient long
  enough to support it. They broaden a ridge; they do not split it. They do, however, make
  it *pulse* — see [isotope beats](#isotope-beats), which turns that failure into the
  charge measurement.
- **The time-averaged peak of a drifting component sits at its mean frequency**, not its
  starting frequency, so the `frequency-drift` preset reports a large apparent m/z error
  by design. The ridge itself carries the information there;
  `RidgeExtraction.RidgePerTimePoint` follows it.

The peak finder interpolates parabolically, so a single well-separated ridge is still
located to tens of ppm.

## Bounding the transform by m/z

Integer octaves are an awkward way to ask for a frequency range, and nothing stops them
asking for scales the transient cannot support. `ScaleGrid` replaces them with an exact
geometric grid you can build from the bound you actually know:

```csharp
var grid = ScaleGrid.ForMzRange(minMz: 600, maxMz: 2300,
    calibrationCoefficient, samplingRate, voicesPerOctave: 64, wavelet,
    signalLength: samples.Length);

var cwt = new CWTObject(samples, 0, 1, 64, c0, 0, false, samplingRate, calibration)
{
    ScaleGrid = grid
};
```

Note the inversion: **low m/z is high frequency**, because `f = sqrt(A/(m/z))`. On the CLI
this is `--min-mz` / `--max-mz`.

Two guards are always on, and both are reported through `grid.Clamps.Describe()` rather
than applied silently:

- **Nyquist**, capping the centre frequency at 0.5 cycles/sample.
- **Signal length**, capping the largest scale at `MaxScaleFraction` (default 0.1) of the
  transient. This is what prevents the FFT wrap-around that `morlet:2000` and above used to
  produce — the wavelet grew longer than the signal and circular convolution folded the end
  of the transient into the start.

The saving is real: the denatured myoglobin envelope needs 64 scales bounded to m/z
600–2300 and the native one 28 scales bounded to 1300–2300, against hundreds for a naive
integer-octave range. `ScaleGrid.ForOctaves` reproduces the old grid exactly, so nothing
that already worked changed.

The native fCWT backend only accepts integer octaves and reports rather than silently
analysing a different grid.

## Grouping charge states into species

A scalogram of a protein gives one ridge per charge state, and `RidgeExtraction` returns
them as a flat list of m/z values. With one protein that is merely inconvenient; with
three it is unreadable. `ChargeStateDeconvolution` turns the list into species.

The algebra it rests on: with `m/z = A/f^2` and `m/z = M/z + m_p`,

```
1/f^2 = M/(A*z) + m_p/A
```

so `1/f^2` is exactly linear in `1/z`. Defining `w = 1/(1/f^2 - m_p/A)` gives `w = (A/M)*z`
— **the charge states of one species land on a uniform comb in `w` with step `A/M`**. Mass
recovery is then comb detection, and it works on frequencies directly without needing an
m/z axis.

```csharp
var groups = ChargeStateDeconvolution.Deconvolve(mzValues, magnitudes);
// 16940 Da +/- 300 ppm, 11 charge states 8+ to 20+, score 0.994
```

Two independent methods run and are cross-checked: comb coherence (an intensity-weighted
circular statistic that is exactly 1 at the true mass) and pairwise voting over assumed
charge gaps, which tolerates missing charge states. A group found by both is stronger
evidence than one found by either.

**The comb alone cannot tell `M` from `2M`.** Doubling the mass doubles every implied
charge and they all stay whole numbers, so the score is exactly 1 at both. What breaks the
tie is that electrospray envelopes are *contiguous*: the real mass assigns consecutive
charge states while the 2x alias assigns only even ones, halving `Contiguity`. That, not
the score, is what makes the answer unique.

Three further signals corroborate a grouping, each using information the comb ignores.

### Envelope shape

`EnvelopeTemplateMatching` runs the `ChargeStateEnvelope` model backwards as a matched
filter over intensities, scoring shape with cosine similarity — which ignores overall
scale, the right choice since the model predicts relative abundance and nothing about how
many ions were trapped. The better-fitting ionisation mode falls out as a result in its own
right: a native-looking envelope on a protein you expected to be denatured says something
about the sample.

Coverage is weighted by abundance rather than counting charge states. Counting hands a free
win to whichever model predicts fewest: a real denatured envelope whose weak tail falls
under the detection threshold was scored *native*, because the native model predicts only
the strong states that survived and claims perfect coverage.

### Isotope beats

Unresolved isotopes are not a blur — they are two frequencies a little apart, and their sum
beats:

```
beat = spacing * f / (2 * (m/z) * z)        equivalently   z = spacing * f^3 / (2*A*beat)
```

So **charge is measurable from the time axis**, and this is the one method that needs no
assumption about which peaks belong together. It settles what positions cannot: a peak list
holding only every third charge state is exactly as consistent with a third of the mass,
and the beat is not — the two hypotheses predict beats a factor of three apart.

An FFT over the whole transient integrates this away. A CWT keeps it, because it keeps
amplitude as a function of time. Run `analyze --beats` on the `isotope-beat` preset and
both charges come straight off the ridge envelopes, through a transform far too blunt to
resolve the isotopes themselves:

```
m/z 500.059: z = 2 (fractional 1.98), beat 325.65 Hz / 3.1 ms, 26.1 cycles observed
m/z 700.069: z = 3 (fractional 3.05), beat 131.14 Hz / 7.6 ms, 10.5 cycles observed
```

The cost is transient length. Myoglobin 17+ beats at about 14 Hz, so measuring it needs
roughly a tenth of a second; `IsotopeBeatAnalysis.RequiredTransientSeconds` tells you that
before you acquire. A window too short to hold the cycles returns null rather than a charge
inferred from a trend, and a pure exponential decay is rejected outright — prominence alone
is a ratio, so numerical dust in a noiseless decay scored 35.8 before a minimum modulation
depth was added.

### Temporal coherence

`TemporalCoherence` groups ridges by fitted decay constant and detrended-envelope
correlation. Charge states of one species share a history — created together, dephasing at
similar rates, pushed around by space charge together — and a CWT preserves all of it.

This is **the weakest of the four and is offered as corroboration only**. Decay rate varies
systematically with charge state within a single species, so "same species" implies similar
dynamics, not identical. A time constant much longer than the window is reported as no
decay rather than as the slope of the noise.

### On the CLI

```bash
dotnet run --project FCWTNET.Cli -- analyze ./out/myoglobin-denatured.csv --out ./out \
    --c0 80 --voices 64 --min-mz 600 --max-mz 2300 --peaks 30 --deconvolve

dotnet run --project FCWTNET.Cli -- analyze ./out/isotope-beat.csv --out ./out \
    --c0 20 --min-mz 400 --max-mz 900 --max-samples 200000 --beats
```

`--deconvolve` writes a `*.groups.csv` with one row per assigned peak, carrying the
species mass, assigned charge, per-peak mass error, envelope mode and similarity, and the
beat charge where `--beats` measured one. Where the comb and the beat disagree about a
peak's charge, the disagreement is printed rather than hidden.

## Visualization

`Colormaps` provides viridis, magma, a single-hue blues ramp and greys. CWT magnitude is
a sequential quantity, so the ramp rises monotonically in lightness — that is what keeps
it readable in greyscale, in print, and with colour vision deficiency, and it is what the
previous rainbow palette failed. Monotonicity is asserted by test.

`ScalogramRenderer` maps a magnitude matrix to RGBA with linear, log or sqrt intensity
scaling; `PlottingUtils` exports OxyPlot models to PDF or PNG.

## Known gaps

- The native and managed backends have not been compared numerically; that test exists
  (`ManagedCwtTests`) but only runs on Windows, where `fCWT.dll` loads.
- Averagine is an approximation; isotope intensities are not formula-derived.
- `CWTOutput` holds real, imaginary and modulus as separate dense `double[,]`, so a long
  transient across many voices needs several GB. Keep an eye on `MaxSamples`.
- The `MinMaxIdentificationTest` fixture is `[Explicit]`: it is exploratory, slow, and
  operates on large real transients.

## Licence

fCWT is distributed under Apache 2.0. FCWTNET is distributed under the MIT Licence; see
LICENSE for details. The fCWT paper is at
https://www.nature.com/articles/s43588-021-00183-z.
