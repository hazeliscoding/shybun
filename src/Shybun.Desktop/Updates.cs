using System.Security;
using Microsoft.Win32;
using Shybun.Core;
using Velopack;
using Velopack.Sources;

namespace Shybun.Desktop;

/// <summary>
/// Velopack's check against GitHub Releases, the only network call Shybun makes: a minute after it starts, then every
/// 4 hours, while updates are on. An update downloads in the background, and <see cref="Ready"/> comes once no app is
/// using a mic or a camera, because the key does nothing while Shybun restarts.
/// </summary>
public sealed class Updates : IDisposable
{
    /// <summary>A folder with a Velopack release feed, used instead of GitHub to try an update locally.</summary>
    public const string SourceVariable = "SHYBUN_UPDATE_SOURCE";

    private const string SettingsKeyPath = @"Software\Shybun";
    private const string AutomaticValueName = "UpdateAutomatically";
    private const string ConsentStorePath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    private static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Every = TimeSpan.FromHours(4);

    private readonly UpdateManager _manager;
    private readonly CancellationTokenSource _stop = new();
    private UpdateInfo? _ready;

    public Updates()
    {
        var source = Environment.GetEnvironmentVariable(SourceVariable);
        _manager = string.IsNullOrWhiteSpace(source)
            ? new UpdateManager(new GithubSource("https://github.com/hazeliscoding/shybun", accessToken: null, prerelease: false))
            : new UpdateManager(source);
    }

    /// <summary>On a background thread, when a downloaded update can restart Shybun.</summary>
    public event Action? Ready;

    /// <summary>False for a build from source, which never checks.</summary>
    public bool Installed => _manager.IsInstalled;

    /// <summary>The tray menu's "Update automatically" tick. Without a value, it's on.</summary>
    public static bool Automatic
    {
        get
        {
            using var settings = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
            return settings?.GetValue(AutomaticValueName) is not 0;
        }
        set
        {
            using var settings = Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
            settings.SetValue(AutomaticValueName, value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    /// <summary>
    /// Whether an app is using a mic or a camera, as Windows' privacy settings record it. Shybun only reads them. When
    /// they can't be read, the answer is yes, so an update waits for the next start instead.
    /// </summary>
    public static bool DevicesInUse()
    {
        try
        {
            return DeviceUse.InUse(Uses());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return true;
        }
    }

    public void Start() => _ = Task.Run(WatchAsync);

    /// <summary>
    /// Starts Velopack's updater, which waits for Shybun to exit, installs the update and starts the new version. The
    /// caller then shuts down as usual, so the tray icon goes and no Shybun is left behind.
    /// </summary>
    public void InstallAfterExit() =>
        _manager.WaitExitThenApplyUpdates(_ready?.TargetFullRelease ?? throw new InvalidOperationException("No update is downloaded."), silent: true, restart: true);

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task WatchAsync()
    {
        try
        {
            await Task.Delay(FirstCheck, _stop.Token);
            using var timer = new PeriodicTimer(Every);
            while (_ready is null)
            {
                if (Automatic)
                {
                    _ready = await DownloadAsync();
                }

                if (_ready is null)
                {
                    await timer.WaitForNextTickAsync(_stop.Token);
                }
            }

            // Turning updates off also holds back one already downloaded; Velopack then installs it at the next start.
            while (!Automatic || DevicesInUse())
            {
                await Task.Delay(FirstCheck, _stop.Token);
            }

            Ready?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<UpdateInfo?> DownloadAsync()
    {
        try
        {
            if (await _manager.CheckForUpdatesAsync() is not { } update)
            {
                return null;
            }

            await _manager.DownloadUpdatesAsync(update, null, _stop.Token);
            return update;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Offline, or GitHub out of reach: the next check tries again.
            return null;
        }
    }

    // Each app's last start and stop of a mic or a camera. Apps from the Store have their own key, and the others sit
    // under NonPackaged, one key per exe path.
    private static IEnumerable<(long Start, long Stop)> Uses()
    {
        foreach (var device in (string[])["microphone", "webcam"])
        {
            using var store = Registry.CurrentUser.OpenSubKey($@"{ConsentStorePath}\{device}");
            using var unpackaged = store?.OpenSubKey("NonPackaged");
            foreach (var apps in (RegistryKey?[])[store, unpackaged])
            {
                foreach (var name in apps?.GetSubKeyNames() ?? [])
                {
                    using var app = apps!.OpenSubKey(name);
                    if (app?.GetValue("LastUsedTimeStart") is long start && app.GetValue("LastUsedTimeStop") is long stop)
                    {
                        yield return (start, stop);
                    }
                }
            }
        }
    }
}
