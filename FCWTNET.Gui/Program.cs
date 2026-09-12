using Avalonia;

namespace FCWTNET.Gui
{
    public static class Program
    {
        // Avalonia needs to be initialised before any UI type is touched, so keep this
        // free of anything that might load a control.
        [System.STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
