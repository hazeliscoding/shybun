using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;
using Windows.Win32.System.Com.StructuredStorage;
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
    private IMFMediaStream? _device;
    private bool _shutdown;

    public MediaStream(MediaSource source, IMFStreamDescriptor descriptor, uint queue)
    {
        _source = source;
        _descriptor = descriptor;
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
                _events.QueueEvent(e);
                // After an error the real camera sends nothing more.
                if ((EventType)type != EventType.MEError)
                {
                    device.BeginGetEvent(_onDeviceEvent, device);
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
}
