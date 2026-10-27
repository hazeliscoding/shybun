using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using Shybun.Core;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace Shybun.Desktop;

/// <summary>
/// The Shybun camera's one-time install, the only part of Shybun that runs elevated. The tray app starts Shybun again
/// through UAC with one of these arguments, and that process copies or removes the media source and its machine-wide
/// registration, and does nothing else. It never touches the user's own settings: with an admin's password typed in,
/// it runs as someone else.
/// </summary>
public static class CameraSetup
{
    public const string InstallArgument = "--install-camera";
    public const string UninstallArgument = "--uninstall-camera";

    private const string FileName = "Shybun.Camera.dll";
    private const string ClassKeyPath = @"Software\Classes\CLSID\{" + CameraContract.ClassId + "}";
    private const string ServerKeyPath = ClassKeyPath + @"\InprocServer32";

    /// <summary>
    /// Where the media source goes. The Frame Server's services load it, so it must sit where they can read it, never
    /// under a user's profile.
    /// </summary>
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Shybun");

    /// <summary>Whether the media source is registered for the whole machine. Reading that needs no admin.</summary>
    public static bool Installed
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServerKeyPath);
            return key is not null;
        }
    }

    /// <summary>Runs this Shybun again, elevated, with one UAC prompt. False if the prompt was declined or the step failed.</summary>
    public static Task<bool> RunElevated(string argument) => RunElevated(Environment.ProcessPath!, argument);

    public static async Task<bool> RunElevated(string exe, string argument)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(exe, argument) { UseShellExecute = true, Verb = "runas" })!;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // The UAC prompt was declined.
            return false;
        }
    }

    /// <summary>
    /// Uninstalling Shybun undoes the install step: the Shybun camera leaves the camera lists, then the elevated step
    /// removes its media source. Velopack ends this hook after 30 seconds, so the camera goes first, needing no
    /// prompt, and the hook waits 25 seconds for the UAC prompt.
    /// </summary>
    public static void RemoveOnUninstall()
    {
        ShybunCamera.RemoveListed();
        if (Installed)
        {
            // Run, not called: starting an elevated process blocks until the prompt is answered.
            Task.Run(() => RunElevated(UninstallArgument)).Wait(TimeSpan.FromSeconds(25));
        }
    }

    // shortcut: whoever can write Shybun's folder picks the DLL installed here. Today they could swap Shybun.exe too,
    // so a check would add nothing; once releases are signed (M3), check the copy's signature against the exe's own.
    /// <summary>The elevated install: copies the media source shipped beside Shybun.exe, and registers it.</summary>
    public static int Install() => Install(Path.Combine(AppContext.BaseDirectory, FileName), Folder, Registry.LocalMachine);

    /// <summary>The elevated uninstall. The tray app takes the Shybun camera out of the camera lists first.</summary>
    public static int Uninstall() => Uninstall(Folder, Registry.LocalMachine);

    /// <summary>Registers under <paramref name="machine"/>: HKEY_LOCAL_MACHINE, or a scratch key in tests.</summary>
    public static int Install(string source, string folder, RegistryKey machine) => Exit(() =>
    {
        var dll = Path.Combine(folder, FileName);
        Directory.CreateDirectory(folder);
        Retire(dll);
        File.Copy(source, dll);
        using var key = machine.CreateSubKey(ServerKeyPath);
        key.SetValue(null, dll);
        key.SetValue("ThreadingModel", "Both");
    });

    /// <summary>Unregisters under <paramref name="machine"/>: HKEY_LOCAL_MACHINE, or a scratch key in tests.</summary>
    public static int Uninstall(string folder, RegistryKey machine) => Exit(() =>
    {
        // Unregistered first, so nothing loads it again while it goes.
        machine.DeleteSubKeyTree(ClassKeyPath, throwOnMissingSubKey: false);
        Retire(Path.Combine(folder, FileName));
        if (!Directory.Exists(folder))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(folder).Any())
        {
            // Holds a retired copy until the restart; this runs after it's deleted.
            PInvoke.MoveFileEx(folder, null, MOVE_FILE_FLAGS.MOVEFILE_DELAY_UNTIL_REBOOT);
        }
        else
        {
            Directory.Delete(folder);
        }
    });

    // A loaded DLL can't be deleted or overwritten, but it can be renamed. Shybun and the Frame Server keep the media
    // source loaded until they exit, since a NativeAOT runtime can't unload, so it's renamed aside and deleted at the
    // next restart. The unique name keeps that delete off a newer copy installed in the meantime.
    private static void Retire(string dll)
    {
        if (!File.Exists(dll))
        {
            return;
        }

        try
        {
            File.Delete(dll);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var aside = $"{dll}.{Guid.NewGuid():N}.old";
            File.Move(dll, aside);
            PInvoke.MoveFileEx(aside, null, MOVE_FILE_FLAGS.MOVEFILE_DELAY_UNTIL_REBOOT);
        }
    }

    // The exit code is all the tray app gets back, and a crash would show Windows' error dialog.
    private static int Exit(Action step)
    {
        try
        {
            step();
            return 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return 1;
        }
    }
}
