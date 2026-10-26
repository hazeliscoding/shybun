using System.Security.Principal;
using Microsoft.Win32;
using Shybun.Camera;
using Shybun.Core;

namespace Shybun.Tests;

public sealed class MuteSwitchTests : IDisposable
{
    // A key of the test's own, so a Shybun running on this machine keeps its Mute, and nothing is left behind.
    private readonly string _path = $@"Software\Shybun.Tests.{Guid.NewGuid():N}";
    private readonly string _sid = WindowsIdentity.GetCurrent().User!.Value;

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);

    [Fact]
    public void A_camera_that_doesnt_know_its_user_sends_the_camera_off_image() =>
        Assert.True(new MuteSwitch(null, _path).IsOn);

    [Fact]
    public void A_camera_whose_user_has_no_key_sends_the_camera_off_image() =>
        Assert.True(new MuteSwitch(_sid, _path).IsOn);

    [Fact]
    public void It_follows_the_value_the_tray_app_sets()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_path);
        var mute = new MuteSwitch(_sid, _path);

        key.SetValue(CameraContract.MuteValueName, 0);
        Assert.False(mute.IsOn);
        key.SetValue(CameraContract.MuteValueName, 1);
        Assert.True(mute.IsOn);
    }

    [Fact]
    public void It_finds_a_key_made_after_it_first_looked()
    {
        var mute = new MuteSwitch(_sid, _path);
        Assert.True(mute.IsOn);

        using var key = Registry.CurrentUser.CreateSubKey(_path);
        key.SetValue(CameraContract.MuteValueName, 0);
        Assert.False(mute.IsOn);
    }

    [Fact]
    public void A_value_of_the_wrong_kind_counts_as_on()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_path);
        key.SetValue(CameraContract.MuteValueName, "0");
        Assert.True(new MuteSwitch(_sid, _path).IsOn);
    }
}
