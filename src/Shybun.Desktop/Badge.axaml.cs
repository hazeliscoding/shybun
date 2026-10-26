using Avalonia;
using Avalonia.Controls;
using Shybun.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Shybun.Desktop;

/// <summary>
/// The badge: what was read back for the mics and the Shybun camera, on top of everything in the primary screen's top
/// right corner. It never takes focus, and clicks pass through it to whatever is underneath.
/// </summary>
public partial class Badge : Window
{
    public Badge()
    {
        InitializeComponent();
        Resized += (_, _) => PlaceTopRight();
    }

    // "Muted" alone means the mics and the Shybun camera both: without a Shybun camera, it says which.
    public static string Describe(BadgeState state, BadgeDevices devices) => (state, devices) switch
    {
        (BadgeState.Muted, BadgeDevices.Mic) => "Mic muted",
        (BadgeState.Muted, BadgeDevices.Camera) => "Camera off",
        (BadgeState.Muted, _) => "Muted",
        (BadgeState.Live, BadgeDevices.Mic) => "Mic is live",
        (BadgeState.Live, BadgeDevices.Camera) => "Camera is live",
        (BadgeState.Live, _) => "Mic and camera are live",
        (BadgeState.Unconfirmed, BadgeDevices.Mic) => "Can't confirm the mic",
        (BadgeState.Unconfirmed, BadgeDevices.Camera) => "Can't confirm the camera",
        (BadgeState.Unconfirmed, _) => "Can't confirm the mic or camera",
        _ => "Live",
    };

    public void Display(BadgeState state, BadgeDevices devices)
    {
        if (state == BadgeState.Hidden)
        {
            Hide();
            return;
        }

        Label.Text = Describe(state, devices);
        Pill.Classes.Set("live", state == BadgeState.Live);
        Pill.Classes.Set("unconfirmed", state == BadgeState.Unconfirmed);
        if (!IsVisible)
        {
            Show();
        }

        PlaceTopRight();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        PassClicksThrough();
    }

    private void PlaceTopRight()
    {
        if (Screens.Primary is not { } screen)
        {
            return;
        }

        var area = screen.WorkingArea;
        var width = (int)Math.Ceiling(Bounds.Width * screen.Scaling);
        var inset = (int)(8 * screen.Scaling);
        Position = new PixelPoint(area.Right - width - inset, area.Y + inset);
    }

    // Avalonia has no click-through window. A layered, transparent window lets every click reach
    // the window underneath, and no-activate keeps it from ever taking focus.
    private void PassClicksThrough()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle)
        {
            return;
        }

        var hwnd = new HWND(handle);
        var style = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE)
            | WINDOW_EX_STYLE.WS_EX_LAYERED
            | WINDOW_EX_STYLE.WS_EX_TRANSPARENT
            | WINDOW_EX_STYLE.WS_EX_NOACTIVATE
            | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)style);
    }
}
