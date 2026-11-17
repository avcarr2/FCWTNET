using System;

namespace FCWTNET.Transform.Wavelets
{
    /// <summary>
    /// An analysing wavelet, defined by its frequency-domain response.
    ///
    /// <para>
    /// The CWT here is computed in the frequency domain, so a wavelet only needs to say
    /// what it looks like there. Every wavelet used for this is a function of the product
    /// <c>a*w</c> (scale times angular frequency) alone, which is what makes the transform
    /// a simple per-scale multiply.
    /// </para>
    ///
    /// <para>
    /// Implementations are <b>analytic</b>: the response is zero for negative frequencies.
    /// That is what makes the CWT complex and its modulus an envelope, which is what ridge
    /// extraction and m/z assignment need. A real-valued wavelet would give an oscillating
    /// transform with no envelope to follow.
    /// </para>
    ///
    /// <para>
    /// The response is normalised to a peak of 1, so the backend's amplitude scaling makes
    /// the modulus at a ridge equal the amplitude of the component, whichever wavelet is in
    /// use.
    /// </para>
    /// </summary>
    public interface IWavelet
    {
        /// <summary>Short name, as used on the command line and in the UI.</summary>
        string Name { get; }

        /// <summary>Human-readable description of the wavelet and its trade-offs.</summary>
        string Description { get; }

        /// <summary>
        /// Where the response peaks, in <c>a*w</c> units. A wavelet at scale <c>a</c> is
        /// therefore centred on normalised frequency
        /// <c>CentreAngularFrequency / (2*pi*a)</c>. Getting this right per wavelet is what
        /// keeps the frequency and m/z axes meaningful when the wavelet is swapped.
        /// </summary>
        double CentreAngularFrequency { get; }

        /// <summary>
        /// Frequency-domain response at <paramref name="scaleTimesOmega"/>, normalised to a
        /// peak of 1. Must return 0 for non-positive arguments.
        /// </summary>
        double Response(double scaleTimesOmega);

        /// <summary>
        /// Fractional frequency resolution: the FWHM of the response divided by its centre
        /// frequency. Two components closer together than this merge into one ridge.
        /// </summary>
        double RelativeFrequencyResolution { get; }
    }
}
