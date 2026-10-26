using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32.Foundation;

namespace Shybun.Camera;

/// <summary>Moves objects across the COM boundary with the source-generated wrappers, which NativeAOT supports.</summary>
internal static unsafe class Com
{
    private static readonly StrategyBasedComWrappers Wrappers = new();

    /// <summary>The <paramref name="iid"/> interface of a managed object, with a reference the caller owns.</summary>
    public static HRESULT Pointer(object instance, in Guid iid, out void* ppv)
    {
        var unknown = Wrappers.GetOrCreateComInterfaceForObject(instance, CreateComInterfaceFlags.None);
        var hr = (HRESULT)Marshal.QueryInterface(unknown, in iid, out var result);
        Marshal.Release(unknown);
        ppv = (void*)result;
        return hr;
    }

    /// <summary>Wraps a native object, which keeps the reference its caller holds.</summary>
    public static T Get<T>(void* pointer)
        where T : class => (T)Wrappers.GetOrCreateObjectForComInstance((nint)pointer, CreateObjectFlags.None);

    /// <summary>Wraps a native object and takes over the reference that came with it.</summary>
    public static T Take<T>(void* pointer)
        where T : class
    {
        var wrapper = (T)Wrappers.GetOrCreateObjectForComInstance((nint)pointer, CreateObjectFlags.None);
        Marshal.Release((nint)pointer);
        return wrapper;
    }
}
