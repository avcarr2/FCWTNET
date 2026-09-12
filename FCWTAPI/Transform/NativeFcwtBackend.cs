using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FCWTNET.Transform
{
    /// <summary>
    /// Backend delegating to the native fCWT library through <see cref="FCWTAPI"/>.
    ///
    /// Only usable on Windows x64: the repository ships fCWT.dll and fftw3f.dll as PE
    /// binaries and the P/Invoke targets a mangled MSVC symbol. <see cref="IsAvailable"/>
    /// reports false elsewhere so callers can fall back to <see cref="ManagedMorletCwt"/>
    /// rather than hitting a DllNotFoundException.
    /// </summary>
    public class NativeFcwtBackend : ICwtBackend
    {
        public string Name => "native-fcwt";

        public bool IsAvailable
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return false;
                }
                // The DllImport resolves relative to the running assembly; the csproj
                // copies fCWT.dll next to it.
                string probe = Path.Combine(AppContext.BaseDirectory, "fCWT.dll");
                return File.Exists(probe);
            }
        }

        public void Transform(double[] input, int psoctave, int pendoctave, int pnbvoice, float c0,
            int nthreads, bool useOptimizationSchemes, out double[][] real, out double[][] imag)
        {
            FCWTAPI.CWT(input, psoctave, pendoctave, pnbvoice, c0, nthreads, useOptimizationSchemes,
                out double[][] nativeReal, out double[][] nativeImag);
            real = nativeReal;
            imag = nativeImag;
        }
    }
}
