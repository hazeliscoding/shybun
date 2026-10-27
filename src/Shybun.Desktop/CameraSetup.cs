using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using Shybun.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.WinTrust;
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

    /// <summary>
    /// Whether an update brought a media source that differs from the installed one. Compared by content, since every
    /// build from source carries the same version.
    /// </summary>
    public static bool Outdated => Installed && Differ(Path.Combine(Folder, FileName), Path.Combine(AppContext.BaseDirectory, FileName));

    /// <summary>Whether both files exist and their contents differ.</summary>
    public static bool Differ(string installed, string shipped)
    {
        try
        {
            return File.Exists(installed) && File.Exists(shipped)
                && !File.ReadAllBytes(installed).AsSpan().SequenceEqual(File.ReadAllBytes(shipped));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
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

    /// <summary>The elevated install: copies the media source shipped beside Shybun.exe, and registers it.</summary>
    public static int Install() =>
        Install(Path.Combine(AppContext.BaseDirectory, FileName), Folder, Registry.LocalMachine, Environment.ProcessPath!);

    /// <summary>The elevated uninstall. The tray app takes the Shybun camera out of the camera lists first.</summary>
    public static int Uninstall() => Uninstall(Folder, Registry.LocalMachine);

    /// <summary>
    /// Registers under <paramref name="machine"/>: HKEY_LOCAL_MACHINE, or a scratch key in tests. When
    /// <paramref name="publisher"/>, Shybun.exe, is signed, the media source must be signed by the same publisher:
    /// anyone who can write Shybun's folder could otherwise swap in a DLL that the camera's services then load.
    /// </summary>
    public static int Install(string source, string folder, RegistryKey machine, string publisher) => Exit(() =>
    {
        var dll = Path.Combine(folder, FileName);
        Directory.CreateDirectory(folder);
        Retire(dll);
        File.Copy(source, dll);
        // The copy is checked, in a folder only an admin can change, so the file checked is the file registered. An
        // unsigned Shybun.exe is a build from source, and its UAC prompt already says the publisher is unknown.
        // shortcut: the exe is read by its path, which anyone who can write Shybun's folder could rename and replace
        // while the prompt is up; build the expected publisher into a signed release if that race ever matters.
        if (Signer(publisher) is { } expected && Signer(dll) != expected)
        {
            File.Delete(dll);
            throw new SecurityException("The Shybun camera's media source isn't signed by Shybun's publisher.");
        }

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

    // The subject of the certificate that signed this file, when the file's own signature is valid; otherwise null.
    // Windows checks it from its local cache, with no revocation lookup, so the check never touches the network.
    private static unsafe string? Signer(string path)
    {
        fixed (char* file = path)
        {
            var info = new WINTRUST_FILE_INFO { cbStruct = (uint)sizeof(WINTRUST_FILE_INFO), pcwszFilePath = file };
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)sizeof(WINTRUST_DATA),
                dwUIChoice = WINTRUST_DATA_UICHOICE.WTD_UI_NONE,
                fdwRevocationChecks = WINTRUST_DATA_REVOCATION_CHECKS.WTD_REVOKE_NONE,
                dwUnionChoice = WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_FILE,
                dwProvFlags = WINTRUST_DATA_PROVIDER_FLAGS.WTD_CACHE_ONLY_URL_RETRIEVAL | WINTRUST_DATA_PROVIDER_FLAGS.WTD_REVOCATION_CHECK_NONE,
            };
            data.Anonymous.pFile = &info;
            data.dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_VERIFY;
            var action = PInvoke.WINTRUST_ACTION_GENERIC_VERIFY_V2;
            try
            {
                if (PInvoke.WinVerifyTrust(HWND.Null, &action, &data) != 0)
                {
                    return null;
                }

                // The certificate from the signature just checked.
                var provider = PInvoke.WTHelperProvDataFromStateData(data.hWVTStateData);
                var signer = PInvoke.WTHelperGetProvSignerFromChain(provider, 0, false, 0);
                var certificate = PInvoke.WTHelperGetProvCertFromChain(signer, 0);
                using var x509 = new X509Certificate2((nint)certificate->pCert);
                return x509.Subject;
            }
            finally
            {
                data.dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_CLOSE;
                PInvoke.WinVerifyTrust(HWND.Null, &action, &data);
            }
        }
    }

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
