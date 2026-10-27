using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Shybun.Desktop;

public partial class App : Application
{
    private MuteController? _controller;
    private Updates? _updates;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    // Only a real desktop run touches the mics: tests set the app up headless, with no lifetime.
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && TrayIcon.GetIcons(this) is [var tray, ..])
        {
            _controller = new MuteController(tray);
            _updates = StartUpdates(desktop, tray);
            desktop.Exit += (_, _) =>
            {
                _updates?.Dispose();
                _controller.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnQuit(object? sender, EventArgs e) =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();

    // An installed copy keeps itself up to date, and the tray menu's tick turns that off. A build from source never
    // checks.
    private static Updates? StartUpdates(IClassicDesktopStyleApplicationLifetime desktop, TrayIcon tray)
    {
        var updates = new Updates();
        if (!updates.Installed)
        {
            updates.Dispose();
            return null;
        }

        var automatic = new NativeMenuItem("Update automatically")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = Updates.Automatic,
        };
        automatic.Click += (_, _) => automatic.IsChecked = Updates.Automatic = !Updates.Automatic;
        // Above Quit.
        tray.Menu?.Items.Insert(tray.Menu.Items.Count - 1, automatic);

        updates.Ready += () => Dispatcher.UIThread.Post(() =>
        {
            try
            {
                updates.InstallAfterExit();
            }
            catch (Exception)
            {
                // The updater didn't start: keep running, and Velopack installs the update at the next start.
                return;
            }

            desktop.Shutdown();
        });
        updates.Start();
        return updates;
    }
}
