using System;

namespace FCWTNET.Transform
{
    /// <summary>Backend selection for <see cref="CWTObject"/>.</summary>
    public static class CwtBackends
    {
        private static readonly Lazy<ICwtBackend> LazyDefault = new Lazy<ICwtBackend>(SelectDefault);

        /// <summary>
        /// Native fCWT where it is available (Windows with fCWT.dll present), managed
        /// Morlet everywhere else.
        /// </summary>
        public static ICwtBackend Default => LazyDefault.Value;

        public static ICwtBackend Native { get; } = new NativeFcwtBackend();

        public static ICwtBackend Managed { get; } = new ManagedCwt();

        /// <summary>A managed backend using the given analysing wavelet.</summary>
        public static ICwtBackend ManagedWith(Wavelets.IWavelet wavelet) => new ManagedCwt(wavelet);

        private static ICwtBackend SelectDefault()
        {
            return Native.IsAvailable ? Native : Managed;
        }
    }
}
