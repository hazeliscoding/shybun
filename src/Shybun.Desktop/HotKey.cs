using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Shybun.Desktop;

/// <summary>
/// The mute key, Win+Shift+Q, through <c>RegisterHotKey</c> on a message-only window. Windows
/// delivers it to whichever app has focus without moving focus, and a message-only window can
/// never come to the front.
/// </summary>
internal sealed unsafe class HotKey : IDisposable
{
    public const string Name = "Win+Shift+Q";

    private const string WindowClass = "Shybun.HotKey";
    private const int Id = 1;

    // One key per process, and the window procedure can't capture state.
    private static Action? _pressed;

    private readonly HWND _hwnd;

    /// <param name="pressed">Runs on the UI thread, which owns the window.</param>
    public HotKey(Action pressed)
    {
        _pressed = pressed;
        fixed (char* name = WindowClass)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                lpszClassName = name,
            };
            PInvoke.RegisterClassEx(in windowClass);
        }

        _hwnd = PInvoke.CreateWindowEx(0, WindowClass, "Shybun key", 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, null, null, null);
        // MOD_NOREPEAT: holding the key down must not flip mute over and over.
        Registered = PInvoke.RegisterHotKey(
            _hwnd,
            Id,
            HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_SHIFT | HOT_KEY_MODIFIERS.MOD_NOREPEAT,
            (uint)VIRTUAL_KEY.VK_Q);
    }

    /// <summary>False when another app already holds the key, such as PowerToys 0.87's Video Conference Mute.</summary>
    public bool Registered { get; }

    public void Dispose()
    {
        PInvoke.UnregisterHotKey(_hwnd, Id);
        PInvoke.DestroyWindow(_hwnd);
        _pressed = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_HOTKEY)
        {
            _pressed?.Invoke();
            return default;
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
