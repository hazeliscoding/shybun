using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Shybun.Desktop;

public partial class App : Application
{
    private MuteController? _controller;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    // Only a real desktop run touches the mics: tests set the app up headless, with no lifetime.
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && TrayIcon.GetIcons(this) is [var tray, ..])
        {
            _controller = new MuteController(tray);
            desktop.Exit += (_, _) => _controller.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnQuit(object? sender, EventArgs e) =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
}
