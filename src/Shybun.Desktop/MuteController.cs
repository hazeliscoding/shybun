using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Shybun.Core;

namespace Shybun.Desktop;

/// <summary>Ties the key, the mics, the badge, the tray icon and the log to the mute state in Core.</summary>
internal sealed class MuteController : IDisposable
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shybun", "mute-log.csv");

    private static readonly WindowIcon PlainIcon = Icon("shybun.ico");
    private static readonly WindowIcon MutedIcon = Icon("shybun-muted.ico");
    private static readonly WindowIcon AlertIcon = Icon("shybun-alert.ico");

    private readonly TrayIcon _tray;
    private readonly Mics _mics;
    private readonly Mute _mute;
    private readonly Badge _badge = new();
    private readonly HotKey _key;
    private readonly NativeMenuItem _cameraItem = new() { IsEnabled = false };
    private readonly ShybunCamera _camera;

    public MuteController(TrayIcon tray)
    {
        _tray = tray;
        _mics = new Mics(action => Dispatcher.UIThread.Post(action));
        _mute = new Mute(_mics.Read());
        _mics.Added += OnAdded;
        _mics.Removed += OnRemoved;
        _mics.Changed += OnChanged;
        _key = new HotKey(Press);
        _camera = new ShybunCamera(_mute.On, status => Dispatcher.UIThread.Post(() => _cameraItem.Header = status));
        _cameraItem.Header = _camera.Status;
        _tray.Menu?.Items.Insert(0, new NativeMenuItem
        {
            Header = _key.Registered ? $"Mute key: {HotKey.Name}" : $"{HotKey.Name} is taken by another app",
            IsEnabled = false,
        });
        _tray.Menu?.Items.Insert(1, _cameraItem);
        _tray.Menu?.Items.Insert(2, new NativeMenuItemSeparator());
        Show();
    }

    public void Dispose()
    {
        _key.Dispose();
        _mics.Dispose();
        _camera.Dispose();
        _badge.Close();
    }

    private void Press()
    {
        var muted = _mute.Press();
        foreach (var id in _mute.Mics.Keys.ToList())
        {
            _mute.Read(id, _mics.Set(id, muted));
        }

        if (!_camera.WriteMute(_mute.On) && _camera.Exists)
        {
            _cameraItem.Header = "Shybun couldn't reach the Shybun camera";
        }

        Log(MuteCause.Key);
        Show();
    }

    private void OnAdded(string id, bool? muted)
    {
        if (_mute.Added(id, muted))
        {
            _mute.Read(id, _mics.Set(id, true));
            Log(MuteCause.NewMic);
        }

        Show();
    }

    private void OnRemoved(string id)
    {
        _mute.Removed(id);
        Show();
    }

    private void OnChanged(string id, bool muted, bool byShybun)
    {
        var before = _mute.Mics.GetValueOrDefault(id);
        _mute.Read(id, muted);
        // Windows also notifies volume changes; only a mute flipped by something else is logged.
        if (!byShybun && before != muted && _mute.Mics.ContainsKey(id))
        {
            Log(MuteCause.Windows);
        }

        Show();
    }

    private void Show()
    {
        var state = _mute.Badge;
        _badge.Display(state);
        _tray.Icon = state switch
        {
            BadgeState.Muted => MutedIcon,
            BadgeState.Hidden => PlainIcon,
            _ => AlertIcon,
        };
        _tray.ToolTipText = state switch
        {
            BadgeState.Muted => "Shybun: muted",
            BadgeState.Live => "Shybun: a mic is live",
            BadgeState.Unconfirmed => "Shybun: can't confirm a mic",
            _ => "Shybun: live",
        };
    }

    private void Log(MuteCause cause)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var header = File.Exists(LogPath) ? "" : MuteLog.Header + Environment.NewLine;
            File.AppendAllText(LogPath, header + MuteLog.Row(DateTimeOffset.Now, cause, _mute) + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The log is for dogfooding: muting must never fail because of it.
        }
    }

    private static WindowIcon Icon(string name) =>
        new(AssetLoader.Open(new Uri($"avares://Shybun/Assets/{name}")));
}
