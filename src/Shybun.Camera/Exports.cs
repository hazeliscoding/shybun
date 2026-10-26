using System.Runtime.InteropServices;
using Shybun.Core;
using Windows.Win32.Foundation;

namespace Shybun.Camera;

/// <summary>
/// The functions Windows calls on an in-process COM server. The tray app's install step registers it, so it has no
/// DllRegisterServer of its own.
/// </summary>
public static unsafe class Exports
{
    internal static readonly Guid Clsid = new(CameraContract.ClassId);

    [UnmanagedCallersOnly(EntryPoint = nameof(DllGetClassObject))]
    public static int DllGetClassObject(Guid* clsid, Guid* iid, void** ppv)
    {
        *ppv = null;
        if (*clsid != Clsid)
        {
            return HRESULT.CLASS_E_CLASSNOTAVAILABLE;
        }

        try
        {
            return Com.Pointer(new ClassFactory(), *iid, out *ppv);
        }
        catch (Exception e)
        {
            return e.HResult;
        }
    }

    // A NativeAOT runtime can't be unloaded from a process.
    [UnmanagedCallersOnly(EntryPoint = nameof(DllCanUnloadNow))]
    public static int DllCanUnloadNow() => HRESULT.S_FALSE;
}
