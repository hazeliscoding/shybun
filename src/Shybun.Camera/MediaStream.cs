using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using EventType = Windows.Win32.Media.MediaFoundation.__MIDL___MIDL_itf_mfobjects_0000_0013_0001;

namespace Shybun.Camera;

/// <summary>
/// One of the real camera's streams. Every frame it sends passes through here on its way to the app.
/// Ported from HWMediaStream in Microsoft's Windows-Camera samples (MIT); see THIRD-PARTY-NOTICES.md.
/// </summary>
[GeneratedComClass]
internal sealed unsafe partial class MediaStream : IMFMediaStream2
{
    private readonly Lock _gate = new();
    private readonly MediaSource _source;
    private readonly IMFStreamDescriptor _descriptor;
    private readonly IMFMediaEventQueue _events;
    private readonly AsyncCallback _onDeviceEvent;
    private readonly MuteSwitch _mute;
    private IMFMediaStream? _device;
    private bool _shutdown;

    public MediaStream(MediaSource source, IMFStreamDescriptor descriptor, uint queue, MuteSwitch mute)
    {
        _source = source;
        _descriptor = descriptor;
        _mute = mute;
        descriptor.GetStreamIdentifier(out var id);
        Id = id;
        PInvoke.MFCreateEventQueue(out _events).ThrowOnFailure();
        _onDeviceEvent = new AsyncCallback(queue, OnDeviceEvent);
    }

    public uint Id { get; }

    /// <summary>Follows the real camera's stream, which it announces each time it starts.</summary>
    public void Attach(IMFMediaStream device)
    {
        lock (_gate)
        {
            // An updated stream is usually the same one, which is still being listened to.
            if (_shutdown || ReferenceEquals(device, _device))
            {
                return;
            }

            _device = device;
            device.BeginGetEvent(_onDeviceEvent, device).ThrowOnFailure();
        }
    }

    public void Shutdown()
    {
        lock (_gate)
        {
            _shutdown = true;
            _events.Shutdown();
            _device = null;
        }
    }

    public HRESULT GetEvent(MEDIA_EVENT_GENERATOR_GET_EVENT_FLAGS dwFlags, out IMFMediaEvent ppEvent)
    {
        // GetEvent can wait for an event, so it runs without the lock.
        IMFMediaEventQueue events;
        lock (_gate)
        {
            if (_shutdown)
            {
                ppEvent = null!;
                return HRESULT.MF_E_SHUTDOWN;
            }

            events = _events;
        }

        return events.GetEvent((uint)dwFlags, out ppEvent);
    }

    public HRESULT BeginGetEvent(IMFAsyncCallback pCallback, object punkState)
    {
        lock (_gate)
        {
            return _shutdown ? HRESULT.MF_E_SHUTDOWN : _events.BeginGetEvent(pCallback, punkState);
        }
    }

    public HRESULT EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
    {
        lock (_gate)
        {
            ppEvent = null!;
            return _shutdown ? HRESULT.MF_E_SHUTDOWN : _events.EndGetEvent(pResult, out ppEvent);
        }
    }

    public HRESULT QueueEvent(uint met, Guid* guidExtendedType, HRESULT hrStatus, in PROPVARIANT pvValue)
    {
        lock (_gate)
        {
            return _shutdown ? HRESULT.MF_E_SHUTDOWN : _events.QueueEventParamVar(met, guidExtendedType, hrStatus, in pvValue);
        }
    }

    public HRESULT GetMediaSource(out IMFMediaSource ppMediaSource)
    {
        lock (_gate)
        {
            ppMediaSource = _source;
            return _shutdown ? HRESULT.MF_E_SHUTDOWN : HRESULT.S_OK;
        }
    }

    public HRESULT GetStreamDescriptor(out IMFStreamDescriptor ppStreamDescriptor)
    {
        lock (_gate)
        {
            ppStreamDescriptor = _descriptor;
            return _shutdown ? HRESULT.MF_E_SHUTDOWN : HRESULT.S_OK;
        }
    }

    public HRESULT RequestSample(object pToken)
    {
        lock (_gate)
        {
            return _shutdown ? HRESULT.MF_E_SHUTDOWN : _device?.RequestSample(pToken) ?? HRESULT.MF_E_UNEXPECTED;
        }
    }

    public HRESULT SetStreamState(MF_STREAM_STATE value)
    {
        lock (_gate)
        {
            return _shutdown ? HRESULT.MF_E_SHUTDOWN : (_device as IMFMediaStream2)?.SetStreamState(value) ?? HRESULT.MF_E_UNEXPECTED;
        }
    }

    public HRESULT GetStreamState(MF_STREAM_STATE* value)
    {
        lock (_gate)
        {
            return _shutdown ? HRESULT.MF_E_SHUTDOWN : (_device as IMFMediaStream2)?.GetStreamState(value) ?? HRESULT.MF_E_UNEXPECTED;
        }
    }

    private void OnDeviceEvent(IMFAsyncResult result)
    {
        result.GetState(out var state);
        var device = (IMFMediaStream)state;
        HRESULT failure;
        lock (_gate)
        {
            if (_shutdown)
            {
                return;
            }

            failure = device.EndGetEvent(result, out var e);
            if (failure.Succeeded)
            {
                e.GetType(out var type);
                // After an error the real camera sends nothing more.
                if ((EventType)type != EventType.MEError)
                {
                    device.BeginGetEvent(_onDeviceEvent, device);
                }

                if ((EventType)type == EventType.MEMediaSample && _mute.IsOn)
                {
                    SendCameraOff(e, device);
                }
                else
                {
                    _events.QueueEvent(e);
                }
            }
        }

        // Outside the lock: the source takes its own, and holds it while it calls into streams.
        if (failure.Failed)
        {
            PROPVARIANT empty = default;
            _source.QueueEvent((uint)EventType.MEError, null, failure, in empty);
        }
    }

    // The real frame stops here, in the Frame Server, and never reaches the app. When the camera-off image can't be
    // made, nothing is sent in its place.
    private void SendCameraOff(IMFMediaEvent e, IMFMediaStream device)
    {
        e.GetValue(out var value);
        try
        {
            if (value.vt != VARENUM.VT_UNKNOWN || value.punkVal is null || Frame(device) is not { } frame)
            {
                return;
            }

            var real = Com.Get<IMFSample>(value.punkVal);
            PInvoke.MFCreateMemoryBuffer((uint)frame.Length, out var buffer).ThrowOnFailure();
            byte* data;
            buffer.Lock(&data, null, null);
            frame.CopyTo(new Span<byte>(data, frame.Length));
            buffer.Unlock();
            buffer.SetCurrentLength((uint)frame.Length);

            PInvoke.MFCreateSample(out var sample).ThrowOnFailure();
            // Only the token that matches the frame to the app's request, and the stream flags. The real frame's
            // other attributes describe the room: capture metadata can carry face regions, a histogram and exposure.
            foreach (var kept in (ReadOnlySpan<Guid>)[PInvoke.MFSampleExtension_Token, PInvoke.MFSampleExtension_CleanPoint, PInvoke.MFSampleExtension_Discontinuity])
            {
                var key = kept;
                PROPVARIANT item = default;
                if (real.GetItem(&key, (PROPVARIANT_unmanaged*)&item).Succeeded)
                {
                    sample.SetItem(&key, in item).ThrowOnFailure();
                }

                PInvoke.PropVariantClear(ref item);
            }

            real.GetSampleTime(out var time);
            sample.SetSampleTime(time);
            real.GetSampleDuration(out var duration);
            sample.SetSampleDuration(duration);
            sample.AddBuffer(buffer);
            _events.QueueEventParamUnk((uint)EventType.MEMediaSample, null, HRESULT.S_OK, sample).ThrowOnFailure();
        }
        catch (Exception)
        {
        }
        finally
        {
            PInvoke.PropVariantClear(ref value);
        }
    }

    // The camera-off image in the format the real camera is streaming.
    private static byte[]? Frame(IMFMediaStream device)
    {
        device.GetStreamDescriptor(out var descriptor).ThrowOnFailure();
        descriptor.GetMediaTypeHandler(out var handler);
        handler.GetCurrentMediaType(out var type);
        Guid key = PInvoke.MF_MT_SUBTYPE, subtype;
        type.GetGUID(&key, &subtype).ThrowOnFailure();
        key = PInvoke.MF_MT_FRAME_SIZE;
        type.GetUINT64(&key, out var size).ThrowOnFailure();
        key = PInvoke.MF_MT_DEFAULT_STRIDE;
        var stride = type.GetUINT32(&key, out var defaultStride).Succeeded ? (int)defaultStride : 0;
        return CameraOff.Frame(subtype, (int)(size >> 32), (int)(size & 0xFFFFFFFF), stride);
    }
}
