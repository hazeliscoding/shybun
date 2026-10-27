using System.Reflection;
using System.Runtime.InteropServices;

namespace Shybun.Tests;

/// <summary>Structural rules from AGENTS.md: the core has no UI and no Win32, and Shybun's own code never uses the network; the update check belongs to Velopack.</summary>
public class GuardrailTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Assembly Core = Assembly.Load("Shybun.Core");
    private static readonly Assembly Desktop = Assembly.Load("Shybun");
    private static readonly Assembly Camera = Assembly.Load("Shybun.Camera");

    [Fact]
    public void Core_does_not_reference_Avalonia() => Assert.Empty(References(Core, "Avalonia"));

    // The rules must test without devices, so the Windows calls live in the tray app.
    [Fact]
    public void Core_has_no_Win32_interop() => Assert.Empty(Interop(Core));

    [Fact]
    public void No_Shybun_assembly_uses_the_network()
    {
        Assert.Empty(References(Core, "System.Net"));
        Assert.Empty(References(Desktop, "System.Net"));
        Assert.Empty(References(Camera, "System.Net"));
    }

    // Velopack may apply a downloaded update as it starts, which must never happen with admin rights.
    [Fact]
    public void The_elevated_camera_step_runs_before_Velopack_starts()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(folder.FullName, "Shybun.slnx")))
        {
            folder = folder.Parent!;
        }

        var main = File.ReadAllText(Path.Combine(folder.FullName, "src", "Shybun.Desktop", "Program.cs"));
        Assert.True(main.IndexOf("CameraSetup.Install()", StringComparison.Ordinal) is > 0 and var step
            && step < main.IndexOf("VelopackApp.Build()", StringComparison.Ordinal));
    }

    // Positive controls: the same checks find what they exist to catch.
    [Fact]
    public void The_check_finds_Avalonia_in_the_tray_app() => Assert.NotEmpty(References(Desktop, "Avalonia"));

    [Fact]
    public void The_check_finds_network_references() =>
        Assert.NotEmpty(References(Assembly.Load("System.Net.Http"), "System.Net"));

    [Fact]
    public void The_check_finds_Win32_interop()
    {
        var found = Interop(typeof(GuardrailTests).Assembly);
        Assert.Contains($"{typeof(InteropFixture).FullName}.{nameof(InteropFixture.GetSystemMetrics)}", found);
        Assert.Contains(typeof(IComFixture).FullName, found);
    }

    private static string[] References(Assembly assembly, string prefix) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))];

    // P/Invoke methods (CsWin32 and LibraryImport both compile to these) and COM interfaces.
    private static string[] Interop(Assembly assembly) =>
        [.. assembly.GetTypes().Where(type => type.IsImport).Select(type => type.FullName!),
         .. assembly.GetTypes()
            .SelectMany(type => type.GetMethods(Declared))
            .Where(method => method.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")];

    private static class InteropFixture
    {
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int index);
    }

    [ComImport, Guid("00000000-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IComFixture;
}
