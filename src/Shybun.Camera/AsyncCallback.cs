using System.Runtime.InteropServices.Marshalling;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace Shybun.Camera;

/// <summary>Runs a handler on a Media Foundation work queue when an asynchronous call completes.</summary>
[GeneratedComClass]
internal sealed partial class AsyncCallback(uint queue, Action<IMFAsyncResult> handler) : IMFAsyncCallback
{
    public HRESULT GetParameters(out uint pdwFlags, out uint pdwQueue)
    {
        pdwFlags = 0;
        pdwQueue = queue;
        return HRESULT.S_OK;
    }

    public HRESULT Invoke(IMFAsyncResult pAsyncResult)
    {
        // An exception must not cross into the Frame Server: the event is lost, and the next one still arrives.
        try
        {
            handler(pAsyncResult);
        }
        catch (Exception)
        {
        }

        return HRESULT.S_OK;
    }
}
