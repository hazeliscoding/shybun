using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.KernelStreaming;
using Windows.Win32.Media.MediaFoundation;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using EventType = Windows.Win32.Media.MediaFoundation.__MIDL___MIDL_itf_mfobjects_0000_0013_0001;

namespace Shybun.Camera;

/// <summary>
/// The Shybun camera: the real camera's media source, wrapped so that its frames pass through <see cref="MediaStream"/>.
/// Ported from HWMediaSource in Microsoft's Windows-Camera samples (MIT); see THIRD-PARTY-NOTICES.md.
/// </summary>
[GeneratedComClass]
internal sealed unsafe partial class MediaSource : IMFMediaSourceEx, IMFGetService, IKsControl, IMFSampleAllocatorControl
{
    private enum State
    {
        Stopped,
        Started,
        Shutdown,
    }

    private static readonly HRESULT SetNotFound = (HRESULT)unchecked((int)(0x80070000u | (uint)WIN32_ERROR.ERROR_SET_NOT_FOUND));

    private readonly Lock _gate = new();
    private readonly IMFMediaSource _device;
    private readonly IMFAttributes _attributes;
    private readonly IMFMediaEventQueue _events;
    private readonly uint _queue;
    private readonly AsyncCallback _onDeviceEvent;
    private readonly MediaStream[] _streams;
    private State _state = State.Stopped;

    public MediaSource(IMFAttributes activateAttributes, IMFMediaSource device)
    {
        _device = device;
        _attributes = SourceAttributes(activateAttributes, device);
        PInvoke.MFCreateEventQueue(out _events).ThrowOnFailure();
        // The source and its streams share one serial queue, so their callbacks never run at the same time.
        PInvoke.MFAllocateSerialWorkQueue(PInvoke.MFASYNC_CALLBACK_QUEUE_MULTITHREADED, out _queue).ThrowOnFailure();
        _onDeviceEvent = new AsyncCallback(_queue, OnDeviceEvent);
        _streams = CreateStreams(new MuteSwitch(UserSid(activateAttributes)));
        _device.BeginGetEvent(_onDeviceEvent, _device).ThrowOnFailure();
    }

    public HRESULT GetEvent(MEDIA_EVENT_GENERATOR_GET_EVENT_FLAGS dwFlags, out IMFMediaEvent ppEvent)
    {
        // GetEvent can wait for an event, so it runs without the lock.
        IMFMediaEventQueue events;
        lock (_gate)
        {
            if (_state == State.Shutdown)
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
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : _events.BeginGetEvent(pCallback, punkState);
        }
    }

    public HRESULT EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
    {
        lock (_gate)
        {
            ppEvent = null!;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : _events.EndGetEvent(pResult, out ppEvent);
        }
    }

    public HRESULT QueueEvent(uint met, Guid* guidExtendedType, HRESULT hrStatus, in PROPVARIANT pvValue)
    {
        lock (_gate)
        {
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : _events.QueueEventParamVar(met, guidExtendedType, hrStatus, in pvValue);
        }
    }

    public HRESULT GetCharacteristics(out uint pdwCharacteristics)
    {
        lock (_gate)
        {
            pdwCharacteristics = 0;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : _device.GetCharacteristics(out pdwCharacteristics);
        }
    }

    public HRESULT CreatePresentationDescriptor(out IMFPresentationDescriptor ppPresentationDescriptor)
    {
        lock (_gate)
        {
            ppPresentationDescriptor = null!;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : _device.CreatePresentationDescriptor(out ppPresentationDescriptor);
        }
    }

    public HRESULT Start(IMFPresentationDescriptor pPresentationDescriptor, Guid* pguidTimeFormat, in PROPVARIANT pvarStartPosition)
    {
        if (pPresentationDescriptor is null || Unsafe.IsNullRef(in pvarStartPosition))
        {
            return HRESULT.E_INVALIDARG;
        }

        if (pguidTimeFormat is not null && *pguidTimeFormat != Guid.Empty)
        {
            return HRESULT.MF_E_UNSUPPORTED_TIME_FORMAT;
        }

        lock (_gate)
        {
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : _device.Start(pPresentationDescriptor, pguidTimeFormat, in pvarStartPosition);
        }
    }

    public HRESULT Stop()
    {
        lock (_gate)
        {
            if (_state != State.Started)
            {
                return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : HRESULT.MF_E_INVALID_STATE_TRANSITION;
            }

            _state = State.Stopped;
            return _device.Stop();
        }
    }

    public HRESULT Pause() => HRESULT.MF_E_INVALID_STATE_TRANSITION;

    public HRESULT Shutdown()
    {
        lock (_gate)
        {
            if (_state == State.Shutdown)
            {
                return HRESULT.S_OK;
            }

            _state = State.Shutdown;
            _events.Shutdown();
            foreach (var stream in _streams)
            {
                stream.Shutdown();
            }

            _device.Shutdown();
            PInvoke.MFUnlockWorkQueue(_queue);
            return HRESULT.S_OK;
        }
    }

    public HRESULT GetSourceAttributes(out IMFAttributes ppAttributes)
    {
        lock (_gate)
        {
            ppAttributes = _attributes;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : HRESULT.S_OK;
        }
    }

    public HRESULT GetStreamAttributes(uint dwStreamIdentifier, out IMFAttributes ppAttributes)
    {
        lock (_gate)
        {
            ppAttributes = null!;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : ((IMFMediaSourceEx)_device).GetStreamAttributes(dwStreamIdentifier, out ppAttributes);
        }
    }

    // The Frame Server ignores the result: the DXGI manager is a hint.
    public HRESULT SetD3DManager(object pManager)
    {
        lock (_gate)
        {
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN : ((IMFMediaSourceEx)_device).SetD3DManager(pManager);
        }
    }

    public HRESULT GetService(Guid* guidService, Guid* riid, out void* ppvObject)
    {
        lock (_gate)
        {
            ppvObject = null;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN
                : _device is IMFGetService service ? service.GetService(guidService, riid, out ppvObject)
                : HRESULT.MF_E_UNSUPPORTED_SERVICE;
        }
    }

    // ERROR_SET_NOT_FOUND is what a camera driver returns for a control it doesn't handle.
    public HRESULT KsProperty(KSIDENTIFIER* Property, uint PropertyLength, void* PropertyData, uint DataLength, out uint BytesReturned)
    {
        lock (_gate)
        {
            BytesReturned = 0;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN
                : _device is IKsControl control ? control.KsProperty(Property, PropertyLength, PropertyData, DataLength, out BytesReturned)
                : SetNotFound;
        }
    }

    public HRESULT KsMethod(KSIDENTIFIER* Method, uint MethodLength, void* MethodData, uint DataLength, out uint BytesReturned)
    {
        lock (_gate)
        {
            BytesReturned = 0;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN
                : _device is IKsControl control ? control.KsMethod(Method, MethodLength, MethodData, DataLength, out BytesReturned)
                : SetNotFound;
        }
    }

    public HRESULT KsEvent(KSIDENTIFIER* Event, uint EventLength, void* EventData, uint DataLength, out uint BytesReturned)
    {
        lock (_gate)
        {
            BytesReturned = 0;
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN
                : _device is IKsControl control ? control.KsEvent(Event, EventLength, EventData, DataLength, out BytesReturned)
                : SetNotFound;
        }
    }

    public HRESULT SetDefaultAllocator(uint dwOutputStreamID, object pAllocator)
    {
        lock (_gate)
        {
            return _state == State.Shutdown ? HRESULT.MF_E_SHUTDOWN
                : _device is IMFSampleAllocatorControl control ? control.SetDefaultAllocator(dwOutputStreamID, pAllocator)
                : HRESULT.E_NOINTERFACE;
        }
    }

    public HRESULT GetAllocatorUsage(uint dwOutputStreamID, out uint pdwInputStreamID, MFSampleAllocatorUsage* peUsage)
    {
        lock (_gate)
        {
            pdwInputStreamID = dwOutputStreamID;
            if (_state == State.Shutdown)
            {
                return HRESULT.MF_E_SHUTDOWN;
            }

            if (_device is IMFSampleAllocatorControl control)
            {
                return control.GetAllocatorUsage(dwOutputStreamID, out pdwInputStreamID, peUsage);
            }

            *peUsage = MFSampleAllocatorUsage.MFSampleAllocatorUsage_UsesCustomAllocator;
            return HRESULT.S_OK;
        }
    }

    private void OnDeviceEvent(IMFAsyncResult result)
    {
        lock (_gate)
        {
            if (_state == State.Shutdown)
            {
                return;
            }

            try
            {
                _device.EndGetEvent(result, out var e).ThrowOnFailure();
                e.GetType(out var type);
                switch ((EventType)type)
                {
                    case EventType.MENewStream or EventType.MEUpdatedStream:
                        OnNewStream(e, type);
                        break;
                    case EventType.MESourceStarted:
                        _state = State.Started;
                        _events.QueueEvent(e).ThrowOnFailure();
                        break;
                    case EventType.MESourceStopped:
                        _state = State.Stopped;
                        foreach (var stream in _streams)
                        {
                            stream.SetStreamState(MF_STREAM_STATE.MF_STREAM_STATE_STOPPED);
                        }

                        _events.QueueEvent(e).ThrowOnFailure();
                        break;
                    default:
                        _events.QueueEvent(e).ThrowOnFailure();
                        break;
                }
            }
            finally
            {
                _device.BeginGetEvent(_onDeviceEvent, _device);
            }
        }
    }

    // The real camera announces each stream it starts. The app gets the wrapping stream instead.
    private void OnNewStream(IMFMediaEvent e, uint type)
    {
        e.GetValue(out var value);
        try
        {
            if (value.vt != VARENUM.VT_UNKNOWN || value.punkVal is null)
            {
                throw new InvalidOperationException("A new-stream event without a stream.");
            }

            var device = Com.Get<IMFMediaStream>(value.punkVal);
            device.GetStreamDescriptor(out var descriptor).ThrowOnFailure();
            descriptor.GetStreamIdentifier(out var id);
            foreach (var stream in _streams.Where(stream => stream.Id == id))
            {
                stream.Attach(device);
                // The extended type is a reference in C++, so it must point somewhere, even when it says nothing.
                var none = Guid.Empty;
                _events.QueueEventParamUnk(type, &none, HRESULT.S_OK, stream).ThrowOnFailure();
            }
        }
        finally
        {
            PInvoke.PropVariantClear(ref value);
        }
    }

    // The streams share one switch: their callbacks run one at a time on the source's queue.
    private MediaStream[] CreateStreams(MuteSwitch mute)
    {
        _device.CreatePresentationDescriptor(out var presentation).ThrowOnFailure();
        presentation.GetStreamDescriptorCount(out var count);
        var streams = new MediaStream[count];
        for (uint i = 0; i < count; i++)
        {
            presentation.GetStreamDescriptorByIndex(i, out _, out var descriptor);
            streams[i] = new MediaStream(this, descriptor, _queue, mute);
        }

        return streams;
    }

    // Whoever made the virtual camera names the user it belongs to; without that, Mute can't be read.
    private static string? UserSid(IMFAttributes attributes)
    {
        var key = MuteSwitch.UserAttribute;
        PWSTR sid;
        if (attributes.GetAllocatedString(&key, &sid, out _).Failed)
        {
            return null;
        }

        try
        {
            return sid.ToString();
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)sid.Value);
        }
    }

    // The real camera's attributes, then the virtual camera's on top.
    private static IMFAttributes SourceAttributes(IMFAttributes activateAttributes, IMFMediaSource device)
    {
        PInvoke.MFCreateAttributes(out var attributes, 4).ThrowOnFailure();
        ((IMFMediaSourceEx)device).GetSourceAttributes(out var deviceAttributes).ThrowOnFailure();
        deviceAttributes.CopyAllItems(attributes).ThrowOnFailure();
        activateAttributes.GetCount(out var count).ThrowOnFailure();
        for (uint i = 0; i < count; i++)
        {
            Guid key;
            PROPVARIANT value = default;
            activateAttributes.GetItemByIndex(i, &key, (PROPVARIANT_unmanaged*)&value).ThrowOnFailure();
            try
            {
                attributes.SetItem(&key, in value).ThrowOnFailure();
            }
            finally
            {
                PInvoke.PropVariantClear(ref value);
            }
        }

        return attributes;
    }
}
