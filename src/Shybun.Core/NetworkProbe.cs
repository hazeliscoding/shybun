namespace Shybun.Core;

// Throwaway: CI must fail on this, because Core may not use the network.
public static class NetworkProbe
{
    public static readonly HttpClient Client = new();
}
