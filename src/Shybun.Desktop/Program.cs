using Avalonia;
using Avalonia.Controls;

namespace Shybun.Desktop;

internal static class Program
{
    // Shybun has no window, so only Quit in the tray menu ends it.
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
