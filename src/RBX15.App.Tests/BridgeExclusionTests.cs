using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// The test input bridge must be impossible in a shipped build (M4a contract "Security"; interfaces R9, §2.4). A build
/// without <c>EnableTestInputBridge=true</c> holds no bridge type and no bridge string; a flagged build holds both
/// (the positive control, so a broken check cannot pass by finding nothing); publishing with the flag is refused; and
/// no source file calls a global input API.
/// </summary>
public class BridgeExclusionTests
{
    static readonly string[] BridgeTypes = ["TestInputBridge", "InputBridgeDispatcher", "BridgeKeyboardDevice", "RawMouse", "BridgeFileDialogs", "BridgeJson"];
    static readonly string[] BridgeStrings = ["--test-input-bridge", "RBX15-BRIDGE-PIPE"];

    /// <summary>Whether the assembly defines any bridge type (by namespace or by name) and any bridge user string.</summary>
    static (bool Types, bool Strings) Scan(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        bool types = md.TypeDefinitions.Select(md.GetTypeDefinition).Any(t =>
            md.GetString(t.Namespace) == "RBX15.App.Testing" || BridgeTypes.Contains(md.GetString(t.Name)));
        bool strings = false;
        for (var h = MetadataTokens.UserStringHandle(1); !h.IsNil; h = md.GetNextHandle(h))
            if (BridgeStrings.Any(md.GetUserString(h).Contains)) strings = true;
        return (types, strings);
    }

    [Fact] // the RBX15.App.dll these tests run against was built without the flag
    public void The_app_under_test_has_no_bridge() =>
        Assert.Equal((false, false), Scan(typeof(App).Assembly.Location));

    /// <summary>
    /// Set by scripts/build-msix.ps1 to the RBX15.App.dll it unpacked from the finished MSIX. Unset in a plain test run.
    /// </summary>
    public const string PackagedDllVariable = "RBX15_PACKAGED_APP_DLL";

    [Fact] // the DLL inside the shipped MSIX (ledger msix-make-script): the script unpacks the package and points this at it
    public void The_packaged_app_has_no_bridge()
    {
        string? path = Environment.GetEnvironmentVariable(PackagedDllVariable);
        if (string.IsNullOrEmpty(path)) return; // not a packaging run; the script asserts this test ran and passed
        Assert.True(File.Exists(path), $"{PackagedDllVariable} names a file that does not exist: {path}");
        Assert.Equal("RBX15.App.dll", Path.GetFileName(path));
        Assert.Equal((false, false), Scan(path));
    }

    [Fact] // a plain Release build of the app has no bridge; the same build with the flag has it (positive control)
    public void A_release_build_has_no_bridge_and_a_flagged_build_does()
    {
        string root = RepoRoot.Find(AppContext.BaseDirectory);
        string temp = Path.Combine(Path.GetTempPath(), "rbx15-exclusion-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            string plain = Build(root, Path.Combine(temp, "a"));
            string flagged = Build(root, Path.Combine(temp, "b"), "-p:EnableTestInputBridge=true");
            Assert.Equal((false, false), Scan(plain));
            Assert.Equal((true, true), Scan(flagged));
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact] // the flag never reaches a published app: publish and MSIX packaging refuse it before compiling (critic M5)
    public void Publishing_with_the_flag_is_refused()
    {
        string root = RepoRoot.Find(AppContext.BaseDirectory);
        string temp = Path.Combine(Path.GetTempPath(), "rbx15-exclusion-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (code, output) = Dotnet(root, "publish", Path.Combine(root, "src", "RBX15.App", "RBX15.App.csproj"), "-c", "Release",
                "--artifacts-path", temp, "-p:EnableTestInputBridge=true", "-nologo", "-v", "q", "--disable-build-servers");
            Assert.NotEqual(0, code);
            Assert.Contains("a published RBX15 never carries the test input bridge", output);
            // M6's Store path packages through MSBuild, which does not set _IsPublishing; the MSIX property refuses as well.
            var (msix, msixOutput) = Dotnet(root, "build", Path.Combine(root, "src", "RBX15.App", "RBX15.App.csproj"), "-c", "Release",
                "--artifacts-path", Path.Combine(temp, "msix"), "-p:EnableTestInputBridge=true", "-p:WindowsPackageType=MSIX", "-nologo", "-v", "q", "--disable-build-servers");
            Assert.NotEqual(0, msix);
            Assert.Contains("a published RBX15 never carries the test input bridge", msixOutput);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact] // no global input anywhere: the app's sources name none of the Windows APIs that send input or hook it
    public void No_source_calls_a_global_input_api()
    {
        string root = RepoRoot.Find(AppContext.BaseDirectory);
        var forbidden = new Regex(@"\b(SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|ClipCursor|SetWindowsHookEx|BlockInput)\b");
        var hits = Directory.EnumerateFiles(Path.Combine(root, "src", "RBX15.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => forbidden.IsMatch(x.line))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}")
            .ToList();
        Assert.Empty(hits);
    }

    static string Build(string root, string artifacts, params string[] extra)
    {
        var (code, output) = Dotnet(root, ["build", Path.Combine(root, "src", "RBX15.App", "RBX15.App.csproj"), "-c", "Release",
            "--artifacts-path", artifacts, "-nologo", "-v", "q", "--disable-build-servers", .. extra]);
        Assert.True(code == 0, output);
        return Directory.EnumerateFiles(Path.Combine(artifacts, "bin", "RBX15.App"), "RBX15.App.dll", SearchOption.AllDirectories).Single();
    }

    static (int Code, string Output) Dotnet(string root, params string[] args)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("dotnet " + string.Join(' ', args) + " ran past 5 minutes");
        }
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
