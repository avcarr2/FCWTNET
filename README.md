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

### Presets

| Preset | What it shows |
|---|---|
| `single-peak` | One ion, no decay or noise — the cleanest check |
| `peptide-isotopes` | A 2+ peptide with an averagine isotope cluster |
| `myoglobin-13plus` | Apo-myoglobin 13+, matching the bundled sample transient |
| `mixture` | Two species with interleaving isotope clusters |
| `fast-decay` | Decay short against the acquisition; the ridge fades |
| `frequency-drift` | A drifting ion; the ridge slopes across the scalogram |
| `low-snr` | SNR 3 with 12-bit digitisation |

## Transform backends

`fCWT.dll` is a Windows x64 binary and there is no Linux or macOS equivalent in the repo,
so the library also ships `ManagedMorletCwt`: a pure C# complex Morlet CWT computed in
the frequency domain. `CWTObject.Backend` defaults to native on Windows where the DLL
loads, and managed everywhere else.

The managed backend normalises by **amplitude**, so the modulus at a ridge equals the
amplitude of the component and peak heights track ion abundance. The more common
energy normalisation (Torrence & Compo's `sqrt(2*pi*a)`) biases frequency estimation:
for a tone at `w0` the modulus peaks where `a*w0 = (c0 + sqrt(c0^2 + 2))/2` rather than
at `c0`, and because `m/z` goes as `1/f^2` a 1.4% scale error becomes a 2.8% mass error.
Absolute magnitudes therefore differ between the two backends; peak positions do not.

## Resolution, and what to expect

Voices per octave set the frequency resolution. At 48 voices/octave adjacent scales differ
by about 1.5%, while adjacent isotope peaks of a 2+ peptide near m/z 1000 differ by about
0.02% — **isotope peaks are not resolved at typical voice densities**. Resolving them
needs both a much finer scale grid and a transient long enough to support it. The peak
finder interpolates parabolically, so a single ridge is still located to tens of ppm.

Note also that the time-averaged peak of a *drifting* component sits at its mean
frequency, not its starting frequency, so the `frequency-drift` preset reports a large
apparent m/z error by design. The ridge itself is what carries the information there;
`RidgeExtraction.RidgePerTimePoint` follows it.

## Visualization

`Colormaps` provides viridis, magma, a single-hue blues ramp and greys. CWT magnitude is
a sequential quantity, so the ramp rises monotonically in lightness — that is what keeps
it readable in greyscale, in print, and with colour vision deficiency, and it is what the
previous rainbow palette failed. Monotonicity is asserted by test.

`ScalogramRenderer` maps a magnitude matrix to RGBA with linear, log or sqrt intensity
scaling; `PlottingUtils` exports OxyPlot models to PDF or PNG.

## Known gaps

- The native and managed backends have not been compared numerically; that test exists
  (`ManagedMorletCwtTests`) but only runs on Windows, where `fCWT.dll` loads.
- Averagine is an approximation; isotope intensities are not formula-derived.
- `CWTOutput` holds real, imaginary and modulus as separate dense `double[,]`, so a long
  transient across many voices needs several GB. Keep an eye on `MaxSamples`.
- The `MinMaxIdentificationTest` fixture is `[Explicit]`: it is exploratory, slow, and
  operates on large real transients.

## Licence

fCWT is distributed under Apache 2.0. FCWTNET is distributed under the MIT Licence; see
LICENSE for details. The fCWT paper is at
https://www.nature.com/articles/s43588-021-00183-z.
