using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Shybun.Core;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Shybun.Camera;

/// <summary>The functions Windows calls on an in-process COM server.</summary>
public static unsafe class Exports
{
    internal static readonly Guid Clsid = new(CameraContract.ClassId);
    private const string ClassKey = @"Software\Classes\CLSID\{" + CameraContract.ClassId + "}";

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

    // Machine-wide, because the Frame Server and every app that opens the camera load it. regsvr32 runs it elevated.
    [UnmanagedCallersOnly(EntryPoint = nameof(DllRegisterServer))]
    public static int DllRegisterServer() => Guard(() =>
    {
        using var key = Registry.LocalMachine.CreateSubKey(ClassKey + @"\InprocServer32");
        key.SetValue(null, ModulePath());
        key.SetValue("ThreadingModel", "Both");
    });

    [UnmanagedCallersOnly(EntryPoint = nameof(DllUnregisterServer))]
    public static int DllUnregisterServer() => Guard(() => Registry.LocalMachine.DeleteSubKeyTree(ClassKey, throwOnMissingSubKey: false));

    private static int Guard(Action action)
    {
        try
        {
            action();
            return HRESULT.S_OK;
        }
        catch (Exception e)
        {
            return e.HResult;
        }
    }

    // AppContext.BaseDirectory would name the folder of the process that loaded this DLL, so ask Windows which
    // module holds one of its functions.
    private static string ModulePath()
    {
        HMODULE module;
        delegate* unmanaged<int> inThisModule = &DllCanUnloadNow;
        if (!PInvoke.GetModuleHandleEx(
                PInvoke.GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | PInvoke.GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                (PCWSTR)(char*)inThisModule,
                &module))
        {
            throw new Win32Exception();
        }

        var path = stackalloc char[1024];
        var length = PInvoke.GetModuleFileName(module, path, 1024);
        return length is 0 or >= 1024 ? throw new Win32Exception() : new string(path, 0, (int)length);
    }
}
