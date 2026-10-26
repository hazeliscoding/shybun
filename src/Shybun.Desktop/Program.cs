using Avalonia;
using Avalonia.Controls;
using Velopack;

namespace Shybun.Desktop;

internal static class Program
{
    // Shybun has no window, so only Quit in the tray menu ends it. Started elevated for the camera's install step, it
    // does that step and exits, with no tray, key or badge.
    [STAThread]
    public static int Main(string[] args)
    {
        // Setup and the uninstaller start an installed copy with their own arguments; Velopack handles those and
        // exits. A copy that isn't installed carries on.
        VelopackApp.Build().Run();
        return args switch
        {
            [CameraSetup.InstallArgument] => CameraSetup.Install(),
            [CameraSetup.UninstallArgument] => CameraSetup.Uninstall(),
            _ => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown),
        };
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
