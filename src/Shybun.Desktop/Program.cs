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
        // The elevated step comes before Velopack, which may apply a downloaded update as it starts: never with admin.
        switch (args)
        {
            case [CameraSetup.InstallArgument]:
                return CameraSetup.Install();
            case [CameraSetup.UninstallArgument]:
                return CameraSetup.Uninstall();
        }

        // Setup and the uninstaller start an installed copy with their own arguments; Velopack handles those and
        // exits. A copy that isn't installed carries on.
        VelopackApp.Build().Run();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
