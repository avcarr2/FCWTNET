using System;

namespace FCWTNET.Transform
{
    /// <summary>
    /// A continuous wavelet transform implementation.
    ///
    /// Implementations emit rows in ascending scale order (row 0 = smallest scale =
    /// highest frequency), matching the layout the native fCWT library produces.
    /// <see cref="CWTObject.PerformCWT"/> reverses the rows afterwards.
    /// </summary>
    public interface ICwtBackend
    {
        /// <summary>Human-readable backend name, used in diagnostics.</summary>
        string Name { get; }

        /// <summary>True when this backend can run on the current machine.</summary>
        bool IsAvailable { get; }

        /// <param name="input">Signal to transform.</param>
        /// <param name="psoctave">First octave of the scale grid.</param>
        /// <param name="pendoctave">Last octave of the scale grid, inclusive.</param>
        /// <param name="pnbvoice">Scales per octave.</param>
        /// <param name="c0">Morlet central angular frequency (sigma), radians per sample.</param>
        /// <param name="nthreads">Worker threads to use; values below 1 mean "choose automatically".</param>
        /// <param name="useOptimizationSchemes">Passed through to fCWT; ignored by the managed backend.</param>
        /// <param name="real">Real part, indexed [scale][sample].</param>
        /// <param name="imag">Imaginary part, indexed [scale][sample].</param>
        void Transform(double[] input, int psoctave, int pendoctave, int pnbvoice, float c0,
            int nthreads, bool useOptimizationSchemes, out double[][] real, out double[][] imag);

        /// <summary>
        /// Transform over an explicit scale grid rather than a whole number of octaves.
        /// This is what lets the analysed frequency range be bounded to the m/z actually of
        /// interest instead of being rounded out to octave boundaries.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown by backends that can only express octave-aligned grids.
        /// </exception>
        void Transform(double[] input, ScaleGrid grid, int nthreads,
            out double[][] real, out double[][] imag);
    }
}
