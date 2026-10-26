using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Shybun.Core;
using Windows.Win32;
using Windows.Win32.Media.MediaFoundation;

namespace Shybun.Desktop;

/// <summary>
/// Watches who streams each camera through Windows' sensor activity monitor, which needs no admin, and says whether
/// an app streams a real camera other than through the Shybun camera. It sees camera names and process IDs only.
/// </summary>
[SupportedOSPlatform("windows10.0.15063")]
internal sealed partial class CameraWatch : IDisposable
{
    private readonly IMFSensorActivityMonitor _monitor;
    private bool? _bypassed;

    /// <param name="changed">Gets the new answer, on a background thread, each time it changes.</param>
    public CameraWatch(Action<bool> changed)
    {
        PInvoke.MFStartup(PInvoke.MF_VERSION, 0).ThrowOnFailure();
        try
        {
            PInvoke.MFCreateSensorActivityMonitor(new Callback(report =>
            {
                var bypassed = CameraUse.Bypassed(Read(report), ShybunCamera.ListedName);
                if (bypassed != _bypassed)
                {
                    _bypassed = bypassed;
                    changed(bypassed);
                }
            }), out _monitor).ThrowOnFailure();
            _monitor.Start();
        }
        catch
        {
            PInvoke.MFShutdown();
            throw;
        }
    }

    public void Dispose()
    {
        _monitor.Stop();
        PInvoke.MFShutdown();
    }

    // Each camera's name, and the processes streaming it. Many apps hold a camera open without streaming, and
    // processes that have exited stay listed, so only streaming counts.
    private static unsafe List<(string Name, IReadOnlyCollection<uint> Streaming)> Read(IMFSensorActivitiesReport report)
    {
        var cameras = new List<(string, IReadOnlyCollection<uint>)>();
        var name = stackalloc char[256];
        report.GetCount(out var count);
        for (uint i = 0; i < count; i++)
        {
            report.GetActivityReport(i, out var camera);
            camera.GetFriendlyName(name, 256, out var written);
            camera.GetProcessCount(out var processes);
            var streaming = new HashSet<uint>();
            for (uint p = 0; p < processes; p++)
            {
                camera.GetProcessActivity(p, out var activity);
                activity.GetStreamingState(out var on);
                if (on)
                {
                    activity.GetProcessId(out var id);
                    streaming.Add(id);
                }
            }

            cameras.Add((new string(name, 0, (int)written).TrimEnd('\0'), streaming));
        }

        return cameras;
    }

    [GeneratedComClass]
    private sealed partial class Callback(Action<IMFSensorActivitiesReport> handler) : IMFSensorActivitiesReportCallback
    {
        public void OnActivitiesReport(IMFSensorActivitiesReport sensorActivitiesReport) => handler(sensorActivitiesReport);
    }
}
