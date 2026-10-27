using Shybun.Core;

namespace Shybun.Tests;

public class CameraContractTests
{
    // A camera installed by an older release keeps running after the tray app updates, and it reads these values.
    // Changing one breaks every camera nobody has updated yet.
    [Fact]
    public void The_values_the_tray_app_and_the_camera_share_never_change()
    {
        Assert.Equal("807a9636-e6fd-4ac3-b184-27c3027c1cac", CameraContract.ClassId);
        Assert.Equal(@"Software\Shybun\Camera", CameraContract.MuteKeyPath);
        Assert.Equal("Mute", CameraContract.MuteValueName);
        Assert.Equal(new Guid("5d0bfc1c-3f53-4b7e-9b1e-2c47a3b8e0d4"), CameraContract.UserAttribute);
    }
}
