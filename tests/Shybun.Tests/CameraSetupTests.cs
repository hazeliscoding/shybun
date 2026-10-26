using Microsoft.Win32;
using Shybun.Core;
using Shybun.Desktop;
using Windows.Win32;
using Windows.Win32.System.LibraryLoader;

namespace Shybun.Tests;

/// <summary>
/// The camera's install step, run against a scratch folder and a scratch key under HKEY_CURRENT_USER instead of
/// Program Files and HKEY_LOCAL_MACHINE, so it needs no admin. A real DLL from Windows stands in for the media source.
/// </summary>
public sealed class CameraSetupTests : IDisposable
{
    private const string ServerKeyPath = @"Software\Classes\CLSID\{" + CameraContract.ClassId + @"}\InprocServer32";

    private static readonly string Source = Path.Combine(Environment.SystemDirectory, "msimg32.dll");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"Shybun.Tests.{Guid.NewGuid():N}");
    private readonly string _keyPath = $@"Software\Shybun.Tests.{Guid.NewGuid():N}";
    private readonly RegistryKey _machine;

    public CameraSetupTests() => _machine = Registry.CurrentUser.CreateSubKey(_keyPath);

    private string Dll => Path.Combine(_folder, "Shybun.Camera.dll");

    public void Dispose()
    {
        _machine.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Install_copies_the_media_source_and_registers_the_copy()
    {
        Assert.Equal(0, CameraSetup.Install(Source, _folder, _machine));

        Assert.Equal(File.ReadAllBytes(Source), File.ReadAllBytes(Dll));
        using var key = _machine.OpenSubKey(ServerKeyPath);
        Assert.NotNull(key);
        Assert.Equal(Dll, key.GetValue(null));
        Assert.Equal("Both", key.GetValue("ThreadingModel"));
    }

    [Fact]
    public void Uninstall_removes_the_registration_and_the_folder()
    {
        CameraSetup.Install(Source, _folder, _machine);

        Assert.Equal(0, CameraSetup.Uninstall(_folder, _machine));

        Assert.Null(_machine.OpenSubKey(@"Software\Classes\CLSID\{" + CameraContract.ClassId + "}"));
        Assert.False(Directory.Exists(_folder));
    }

    // Shybun itself and the Frame Server keep the media source loaded for good.
    [Fact]
    public void A_loaded_media_source_is_moved_aside_so_uninstall_and_a_new_install_work()
    {
        CameraSetup.Install(Source, _folder, _machine);
        using var loaded = PInvoke.LoadLibraryEx(Dll, LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_IMAGE_RESOURCE);
        Assert.False(loaded.IsInvalid);
        // The positive control: a plain delete fails while it's loaded.
        Assert.ThrowsAny<UnauthorizedAccessException>(() => File.Delete(Dll));

        Assert.Equal(0, CameraSetup.Uninstall(_folder, _machine));
        Assert.False(File.Exists(Dll));
        Assert.Null(_machine.OpenSubKey(ServerKeyPath));

        Assert.Equal(0, CameraSetup.Install(Source, _folder, _machine));
        Assert.Equal(File.ReadAllBytes(Source), File.ReadAllBytes(Dll));
    }

    [Fact]
    public void Install_fails_with_an_exit_code_when_the_media_source_is_missing() =>
        Assert.Equal(1, CameraSetup.Install(Path.Combine(_folder, "missing.dll"), _folder, _machine));
}
