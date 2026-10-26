using Avalonia;
using Avalonia.Controls;

namespace Shybun.Desktop;

internal static class Program
{
    // Shybun has no window, so only Quit in the tray menu ends it. Started elevated for the camera's install step, it
    // does that step and exits, with no tray, key or badge.
    [STAThread]
    public static int Main(string[] args) => args switch
    {
        [CameraSetup.InstallArgument] => CameraSetup.Install(),
        [CameraSetup.UninstallArgument] => CameraSetup.Uninstall(),
        _ => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown),
    };

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
