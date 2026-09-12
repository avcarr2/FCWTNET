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
  enough to support it. They broaden a ridge; they do not split it.
- **The time-averaged peak of a drifting component sits at its mean frequency**, not its
  starting frequency, so the `frequency-drift` preset reports a large apparent m/z error
  by design. The ridge itself carries the information there;
  `RidgeExtraction.RidgePerTimePoint` follows it.

The peak finder interpolates parabolically, so a single well-separated ridge is still
located to tens of ppm.

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
