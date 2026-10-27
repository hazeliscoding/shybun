using Shybun.Core;

namespace Shybun.Tests;

public class DeviceUseTests
{
    [Fact]
    public void An_app_started_and_not_stopped_is_using_its_device() =>
        Assert.True(DeviceUse.InUse([(133_000_000_000_000_000, 133_000_000_100_000_000), (133_000_000_200_000_000, 0)]));

    [Fact]
    public void Apps_that_stopped_use_nothing() =>
        Assert.False(DeviceUse.InUse([(133_000_000_000_000_000, 133_000_000_100_000_000)]));

    [Fact]
    public void An_app_that_never_used_a_device_uses_nothing() =>
        Assert.False(DeviceUse.InUse([(0, 0)]));
}
