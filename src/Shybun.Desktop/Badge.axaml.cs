using Avalonia;
using Avalonia.Controls;
using Shybun.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Shybun.Desktop;

/// <summary>
/// The badge: what Windows reports for the mics, on top of everything in the primary screen's top
/// right corner. It never takes focus, and clicks pass through it to whatever is underneath.
/// </summary>
public partial class Badge : Window
{
    public Badge()
    {
        InitializeComponent();
        Resized += (_, _) => PlaceTopRight();
    }

    public void Display(BadgeState state)
    {
        if (state == BadgeState.Hidden)
        {
            Hide();
            return;
        }

        Label.Text = state switch
        {
            BadgeState.Muted => "Muted",
            BadgeState.Live => "Mic is live",
            _ => "Can't confirm the mic",
        };
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
