using System.Runtime.InteropServices.Marshalling;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace Shybun.Camera;

[GeneratedComClass]
internal sealed unsafe partial class ClassFactory : IClassFactory
{
    public HRESULT CreateInstance(object? pUnkOuter, Guid* riid, out void* ppvObject)
    {
        ppvObject = null;
        if (pUnkOuter is not null)
        {
            return HRESULT.CLASS_E_NOAGGREGATION;
        }

        try
        {
            return Com.Pointer(new Activator(), *riid, out ppvObject);
        }
        catch (Exception e)
        {
            return (HRESULT)e.HResult;
        }
    }

    public HRESULT LockServer(BOOL fLock) => HRESULT.S_OK;
}
