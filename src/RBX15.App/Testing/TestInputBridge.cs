using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows.Threading;

namespace RBX15.App.Testing;

/// <summary>
/// The test input bridge's pipe (M4a contract "test input bridge"; interfaces §3.8). It exists only in a build made
/// with <c>-p:EnableTestInputBridge=true</c>, and only starts when that build is launched with <see cref="LaunchFlag"/>.
/// <list type="bullet">
/// <item>One named pipe per launch, <c>rbx15-bridge-&lt;pid&gt;-&lt;32 random hex&gt;</c>, printed once to stdout as
///   <see cref="PipeLinePrefix"/> + name. Its ACL (<see cref="CreatePipeSecurity"/>) is protected, owned by the
///   current user, allows only the current user and denies network logons.</item>
/// <item>One client for the process lifetime: when it disconnects, the server closes, never listens again, and the
///   dispatcher is disposed, so hardware input works again.</item>
/// <item>UTF-8 NDJSON: one request line, one response line. A line over <see cref="MaxLineBytes"/>, invalid UTF-8 or
///   an unparsable request gets one error line (id 0) and a disconnect.</item>
/// </list>
/// </summary>
public sealed class TestInputBridge : IDisposable
{
    public const string LaunchFlag = "--test-input-bridge";
    public const string PipeLinePrefix = "RBX15-BRIDGE-PIPE ";

    /// <summary>The longest request line, in bytes, before the newline.</summary>
    internal const int MaxLineBytes = 64 * 1024;

    /// <summary>The pipe's in and out buffer sizes: a pipelining client never deadlocks against a response the server is writing.</summary>
    const int PipeBufferBytes = 128 * 1024;

    static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    readonly NamedPipeServerStream _server;
    readonly IInputBridge _bridge;
    readonly IDisposable? _owned;
    readonly Dispatcher? _ownerDispatcher;
    readonly CancellationTokenSource _stop = new();

    TestInputBridge(string pipeName, IInputBridge bridge, IDisposable? owned, Dispatcher? ownerDispatcher)
    {
        PipeName = pipeName;
        _bridge = bridge;
        _owned = owned;
        _ownerDispatcher = ownerDispatcher;
        _server = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, PipeBufferBytes, PipeBufferBytes, CreatePipeSecurity());
        Served = Task.Run(ServeAsync);
    }

    public string PipeName { get; }

    /// <summary>Completes when the server has closed for good. Read by tests.</summary>
    internal Task Served { get; }

    /// <summary>True when <paramref name="args"/> holds <see cref="LaunchFlag"/> exactly (case-sensitive).</summary>
    public static bool Requested(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(a => string.Equals(a, LaunchFlag, StringComparison.Ordinal));
    }

    /// <summary>Starts the bridge on <paramref name="main"/>'s UI thread and prints the pipe line to <paramref name="stdout"/>.</summary>
    public static TestInputBridge Start(MainWindow main, TextWriter stdout) => Start(main, stdout, null);

    internal static TestInputBridge Start(MainWindow main, TextWriter stdout, BridgeFileDialogs? fileDialogs)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(stdout);
        var dispatcher = new InputBridgeDispatcher(main, fileDialogs);
        var bridge = new TestInputBridge(NewPipeName(), dispatcher, dispatcher, main.Dispatcher);
        stdout.WriteLine(PipeLinePrefix + bridge.PipeName);
        stdout.Flush();
        return bridge;
    }

    /// <summary>A server over any <see cref="IInputBridge"/>; tests use it with a fake.</summary>
    internal static TestInputBridge Listen(IInputBridge bridge) => new(NewPipeName(), bridge, null, null);

    internal static string NewPipeName() =>
        $"rbx15-bridge-{Environment.ProcessId}-{RandomNumberGenerator.GetHexString(32, lowercase: true)}";

    /// <summary>Protected (no inherited entries); owner and only Allow: the current user, FullControl; Deny: NETWORK.</summary>
    internal static PipeSecurity CreatePipeSecurity()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("the current user has no SID");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }

    /// <summary>The live pipe's security, as Windows reports it. Read by tests.</summary>
    internal PipeSecurity LiveSecurity() => _server.GetAccessControl();

    public void Dispose()
    {
        _stop.Cancel();
        _server.Dispose();
        if (_ownerDispatcher is null || _ownerDispatcher.CheckAccess()) _owned?.Dispose();
        else _ownerDispatcher.InvokeAsync(() => _owned?.Dispose());
    }

    async Task ServeAsync()
    {
        try
        {
            await _server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
            var buffer = new List<byte>();
            var chunk = new byte[4096];
            int scanned = 0;
            while (true)
            {
                int newline = buffer.IndexOf((byte)'\n', scanned);
                if (newline < 0)
                {
                    scanned = buffer.Count;
                    if (buffer.Count > MaxLineBytes)
                    {
                        await WriteAsync(new BridgeResponse(0, false, $"a request line is longer than {MaxLineBytes} bytes", null)).ConfigureAwait(false);
                        return;
                    }
                    int read = await _server.ReadAsync(chunk, _stop.Token).ConfigureAwait(false);
                    if (read == 0) return;
                    buffer.AddRange(chunk.AsSpan(0, read));
                    continue;
                }
                if (newline > MaxLineBytes)
                {
                    await WriteAsync(new BridgeResponse(0, false, $"a request line is longer than {MaxLineBytes} bytes", null)).ConfigureAwait(false);
                    return;
                }
                var bytes = buffer.GetRange(0, newline).ToArray();
                buffer.RemoveRange(0, newline + 1);
                scanned = 0;
                BridgeCommand command;
                try
                {
                    command = BridgeJson.Parse(Utf8.GetString(bytes).TrimEnd('\r'));
                }
                catch (Exception e) when (e is FormatException or DecoderFallbackException or ArgumentException)
                {
                    await WriteAsync(new BridgeResponse(0, false, e.Message, null)).ConfigureAwait(false);
                    return;
                }
                await WriteAsync(await _bridge.ExecuteAsync(command).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException)
        {
        }
        finally
        {
            _server.Dispose();
            _ownerDispatcher?.InvokeAsync(() => _owned?.Dispose());
        }
    }

    async Task WriteAsync(BridgeResponse response)
    {
        var line = Utf8.GetBytes(BridgeJson.Write(response) + "\n");
        await _server.WriteAsync(line, _stop.Token).ConfigureAwait(false);
        await _server.FlushAsync(_stop.Token).ConfigureAwait(false);
    }
}
