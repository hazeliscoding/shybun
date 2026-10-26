using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;
using Windows.Win32.System.Com.StructuredStorage;

namespace Shybun.Camera;

/// <summary>
/// What the Frame Server creates from the class ID. Its attributes reach it from the virtual camera, and
/// <see cref="ActivateObject"/> makes the media source.
/// </summary>
[GeneratedComClass]
internal sealed unsafe partial class Activator : IMFActivate
{
    private readonly IMFAttributes _attributes;
    private MediaSource? _source;

    public Activator()
    {
        PInvoke.MFCreateAttributes(out _attributes, 2).ThrowOnFailure();
        // Asks the Frame Server for the real camera that AddDeviceSourceInfo named.
        var key = PInvoke.MF_VIRTUALCAMERA_PROVIDE_ASSOCIATED_CAMERA_SOURCES;
        _attributes.SetUINT32(&key, 1).ThrowOnFailure();
        key = PInvoke.MFT_TRANSFORM_CLSID_Attribute;
        var clsid = Exports.Clsid;
        _attributes.SetGUID(&key, &clsid).ThrowOnFailure();
    }

    public HRESULT ActivateObject(Guid* riid, out void* ppv)
    {
        ppv = null;
        try
        {
            _source ??= new MediaSource(_attributes, RealCamera());
            return Com.Pointer(_source, *riid, out ppv);
        }
        catch (Exception e)
        {
            return (HRESULT)e.HResult;
        }
    }

    public HRESULT ShutdownObject()
    {
        _source?.Shutdown();
        return HRESULT.S_OK;
    }

    public HRESULT DetachObject()
    {
        _source = null;
        return HRESULT.S_OK;
    }

    // The Frame Server passes the real camera as a one-element collection of activators.
    private IMFMediaSource RealCamera()
    {
        var key = PInvoke.MF_VIRTUALCAMERA_ASSOCIATED_CAMERA_SOURCES;
        var iid = typeof(IMFCollection).GUID;
        _attributes.GetUnknown(&key, &iid, out var collection).ThrowOnFailure();
        var cameras = Com.Take<IMFCollection>(collection);
        cameras.GetElementCount(out var count);
        if (count != 1)
        {
            throw new InvalidOperationException($"The Shybun camera wraps one real camera, and the Frame Server passed {count}.");
        }

        cameras.GetElement(0, out var element);
        iid = typeof(IMFMediaSource).GUID;
        ((IMFActivate)element).ActivateObject(&iid, out var source).ThrowOnFailure();
        return Com.Take<IMFMediaSource>(source);
    }

    public HRESULT GetItem(Guid* guidKey, PROPVARIANT_unmanaged* pValue) => _attributes.GetItem(guidKey, pValue);

    public HRESULT GetItemType(Guid* guidKey, MF_ATTRIBUTE_TYPE* pType) => _attributes.GetItemType(guidKey, pType);

    public HRESULT CompareItem(Guid* guidKey, in PROPVARIANT Value, BOOL* pbResult) => _attributes.CompareItem(guidKey, in Value, pbResult);

    public HRESULT Compare(IMFAttributes? pTheirs, MF_ATTRIBUTES_MATCH_TYPE MatchType, BOOL* pbResult) => _attributes.Compare(pTheirs!, MatchType, pbResult);

    public HRESULT GetUINT32(Guid* guidKey, out uint punValue) => _attributes.GetUINT32(guidKey, out punValue);

    public HRESULT GetUINT64(Guid* guidKey, out ulong punValue) => _attributes.GetUINT64(guidKey, out punValue);

    public HRESULT GetDouble(Guid* guidKey, out double pfValue) => _attributes.GetDouble(guidKey, out pfValue);

    public HRESULT GetGUID(Guid* guidKey, Guid* pguidValue) => _attributes.GetGUID(guidKey, pguidValue);

    public HRESULT GetStringLength(Guid* guidKey, out uint pcchLength) => _attributes.GetStringLength(guidKey, out pcchLength);

    public HRESULT GetString(Guid* guidKey, PWSTR pwszValue, uint cchBufSize, uint* pcchLength) => _attributes.GetString(guidKey, pwszValue, cchBufSize, pcchLength);

    public HRESULT GetAllocatedString(Guid* guidKey, PWSTR* ppwszValue, out uint pcchLength) => _attributes.GetAllocatedString(guidKey, ppwszValue, out pcchLength);

    public HRESULT GetBlobSize(Guid* guidKey, out uint pcbBlobSize) => _attributes.GetBlobSize(guidKey, out pcbBlobSize);

    public HRESULT GetBlob(Guid* guidKey, byte[] pBuf, uint cbBufSize, uint* pcbBlobSize) => _attributes.GetBlob(guidKey, pBuf, cbBufSize, pcbBlobSize);

    public HRESULT GetAllocatedBlob(Guid* guidKey, byte** ppBuf, out uint pcbSize) => _attributes.GetAllocatedBlob(guidKey, ppBuf, out pcbSize);

    public HRESULT GetUnknown(Guid* guidKey, Guid* riid, out void* ppv) => _attributes.GetUnknown(guidKey, riid, out ppv);

    public HRESULT SetItem(Guid* guidKey, in PROPVARIANT Value) => _attributes.SetItem(guidKey, in Value);

    public HRESULT DeleteItem(Guid* guidKey) => _attributes.DeleteItem(guidKey);

    public HRESULT DeleteAllItems() => _attributes.DeleteAllItems();

    public HRESULT SetUINT32(Guid* guidKey, uint unValue) => _attributes.SetUINT32(guidKey, unValue);

    public HRESULT SetUINT64(Guid* guidKey, ulong unValue) => _attributes.SetUINT64(guidKey, unValue);

    public HRESULT SetDouble(Guid* guidKey, double fValue) => _attributes.SetDouble(guidKey, fValue);

    public HRESULT SetGUID(Guid* guidKey, Guid* guidValue) => _attributes.SetGUID(guidKey, guidValue);

    public HRESULT SetString(Guid* guidKey, PCWSTR wszValue) => _attributes.SetString(guidKey, wszValue);

    public HRESULT SetBlob(Guid* guidKey, byte[] pBuf, uint cbBufSize) => _attributes.SetBlob(guidKey, pBuf, cbBufSize);

    public HRESULT SetUnknown(Guid* guidKey, object pUnknown) => _attributes.SetUnknown(guidKey, pUnknown);

    public HRESULT LockStore() => _attributes.LockStore();

    public HRESULT UnlockStore() => _attributes.UnlockStore();

    public HRESULT GetCount(out uint pcItems) => _attributes.GetCount(out pcItems);

    public HRESULT GetItemByIndex(uint unIndex, Guid* pguidKey, PROPVARIANT_unmanaged* pValue) => _attributes.GetItemByIndex(unIndex, pguidKey, pValue);

    public HRESULT CopyAllItems(IMFAttributes pDest) => _attributes.CopyAllItems(pDest);
}
