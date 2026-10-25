using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.Media.Audio.Endpoints;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Shybun.Tests;

/// <summary>
/// M1's acceptance check on real devices. It mutes and unmutes every mic, so run it only when
/// nobody is on a call, and keep off the keyboard while it runs. It reads each mic's loudest
/// sample and keeps nothing else.
/// </summary>
public class LiveTests
{
    private static readonly Guid FloatFormat = new("00000003-0000-0010-8000-00aa00389b71");
    private static readonly Guid PcmFormat = new("00000001-0000-0010-8000-00aa00389b71");

    // Below one step of 16-bit audio. A muted mic measured 2e-10: float residue from Windows' audio
    // processing, not sound.
    private const float Silence = 1f / 32768;

    [Fact(Explicit = true)]
    public void The_key_silences_every_mic_and_leaves_the_app_in_front()
    {
        var mics = Mics();
        Assert.NotEmpty(mics);
        Assert.All(mics, mic => Assert.False(IsMuted(mic.Volume), $"Unmute {mic.Id} before running this test."));

        using var shybun = StartShybun();
        try
        {
            var front = PInvoke.GetForegroundWindow();
            Assert.NotEqual(HWND.Null, front);

            PressMuteKey();
            Thread.Sleep(1000);
            Assert.Equal(front, PInvoke.GetForegroundWindow());
            Assert.All(mics, mic => Assert.True(IsMuted(mic.Volume), $"{mic.Id} isn't muted"));
            Assert.All(mics, mic => Assert.True(LoudestSample(mic.Device) < Silence, $"{mic.Id} gave sound while muted"));

            PressMuteKey();
            Thread.Sleep(1000);
            Assert.Equal(front, PInvoke.GetForegroundWindow());
            Assert.All(mics, mic => Assert.False(IsMuted(mic.Volume), $"{mic.Id} is still muted"));
            // A wireless headset that's switched off, or muted on the headset itself, sends pure
            // silence while Windows reads it live, so only some mic has to give sound.
            var silent = mics.Where(mic => LoudestSample(mic.Device) < Silence).Select(mic => mic.Id).ToList();
            TestContext.Current.SendDiagnosticMessage($"Silent while live: {string.Join(", ", silent)}");
            Assert.True(silent.Count < mics.Count, "Every mic gave pure silence while live");
        }
        finally
        {
            shybun.Kill();
            Unmute(mics);
        }
    }

    [Fact(Explicit = true)]
    public void A_mic_unmuted_behind_Shybuns_back_stays_live_and_is_logged()
    {
        var mics = Mics();
        Assert.All(mics, mic => Assert.False(IsMuted(mic.Volume), $"Unmute {mic.Id} before running this test."));

        using var shybun = StartShybun();
        try
        {
            PressMuteKey();
            Thread.Sleep(1000);
            Assert.All(mics, mic => Assert.True(IsMuted(mic.Volume), $"{mic.Id} isn't muted"));

            mics[0].Volume.SetMute(false, Guid.Empty);
            Thread.Sleep(1000);

            Assert.False(IsMuted(mics[0].Volume), "Shybun muted it again");
            var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shybun", "mute-log.csv");
            Assert.EndsWith($",windows,on,{mics.Count},{mics.Count - 1},1,0", File.ReadLines(log).Last());
        }
        finally
        {
            shybun.Kill();
            Unmute(mics);
        }
    }

    // SHYBUN_EXE points the tests at a published native build; otherwise they run the one built beside them.
    private static Process StartShybun()
    {
        var exe = Environment.GetEnvironmentVariable("SHYBUN_EXE") ?? Path.Combine(AppContext.BaseDirectory, "Shybun.exe");
        var shybun = Process.Start(exe)!;
        Thread.Sleep(3000);
        return shybun;
    }

    private static void Unmute(List<(string Id, IMMDevice Device, IAudioEndpointVolume Volume)> mics)
    {
        foreach (var mic in mics)
        {
            mic.Volume.SetMute(false, Guid.Empty);
        }
    }

    private static unsafe List<(string Id, IMMDevice Device, IAudioEndpointVolume Volume)> Mics()
    {
        var enumerator = MMDeviceEnumerator.CreateInstance<IMMDeviceEnumerator>();
        enumerator.EnumAudioEndpoints(EDataFlow.eCapture, DEVICE_STATE.DEVICE_STATE_ACTIVE, out var devices);
        devices.GetCount(out var count);
        var mics = new List<(string, IMMDevice, IAudioEndpointVolume)>();
        for (uint i = 0; i < count; i++)
        {
            devices.Item(i, out var device);
            device.GetId(out PWSTR id);
            var name = id.ToString();
            Marshal.FreeCoTaskMem((nint)id.Value);
            device.Activate(typeof(IAudioEndpointVolume).GUID, CLSCTX.CLSCTX_ALL, null, out var volume);
            mics.Add((name, device, (IAudioEndpointVolume)volume));
        }

        return mics;
    }

    private static bool IsMuted(IAudioEndpointVolume volume)
    {
        volume.GetMute(out BOOL muted);
        return muted;
    }

    private static unsafe void PressMuteKey()
    {
        static INPUT Key(VIRTUAL_KEY key, bool up) => new()
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new() { ki = new() { wVk = key, dwFlags = up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0 } },
        };

        INPUT[] keys =
        [
            Key(VIRTUAL_KEY.VK_LWIN, false), Key(VIRTUAL_KEY.VK_SHIFT, false), Key(VIRTUAL_KEY.VK_Q, false),
            Key(VIRTUAL_KEY.VK_Q, true), Key(VIRTUAL_KEY.VK_SHIFT, true), Key(VIRTUAL_KEY.VK_LWIN, true),
        ];
        Assert.Equal((uint)keys.Length, PInvoke.SendInput(keys, sizeof(INPUT)));
    }

    // Reads a second of the mic in shared mode and returns only its loudest sample, so no sound is kept.
    private static unsafe float LoudestSample(IMMDevice device)
    {
        device.Activate(typeof(IAudioClient).GUID, CLSCTX.CLSCTX_ALL, null, out var activated);
        var client = (IAudioClient)activated;
        client.GetMixFormat(out var format);
        try
        {
            var isFloat = format->wFormatTag == 3
                || (format->wFormatTag == 0xFFFE && ((WAVEFORMATEXTENSIBLE*)format)->SubFormat == FloatFormat);
            var isPcm16 = format->wBitsPerSample == 16
                && (format->wFormatTag == 1 || (format->wFormatTag == 0xFFFE && ((WAVEFORMATEXTENSIBLE*)format)->SubFormat == PcmFormat));
            Assert.True((isFloat && format->wBitsPerSample == 32) || isPcm16, $"Unhandled mix format {format->wFormatTag}, {format->wBitsPerSample} bits");

            client.Initialize(AUDCLNT_SHAREMODE.AUDCLNT_SHAREMODE_SHARED, 0, 10_000_000, 0, *format);
            var captureId = typeof(IAudioCaptureClient).GUID;
            client.GetService(&captureId, out var raw);
            var capture = ComInterfaceMarshaller<IAudioCaptureClient>.ConvertToManaged(raw)!;
            Marshal.Release((nint)raw);

            var loudest = 0f;
            client.Start();
            var until = Stopwatch.StartNew();
            while (until.ElapsedMilliseconds < 1000)
            {
                Thread.Sleep(20);
                capture.GetNextPacketSize(out var frames);
                while (frames > 0)
                {
                    capture.GetBuffer(out var data, out var read, out var flags);
                    // AUDCLNT_BUFFERFLAGS_SILENT: Windows marked the packet as silence.
                    if ((flags & 2) == 0)
                    {
                        var samples = (int)read * format->nChannels;
                        for (var i = 0; i < samples; i++)
                        {
                            var sample = isFloat ? Math.Abs(((float*)data)[i]) : Math.Abs(((short*)data)[i] / 32768f);
                            loudest = Math.Max(loudest, sample);
                        }
                    }

                    capture.ReleaseBuffer(read);
                    capture.GetNextPacketSize(out frames);
                }
            }

            client.Stop();
            return loudest;
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)format);
        }
    }
}
