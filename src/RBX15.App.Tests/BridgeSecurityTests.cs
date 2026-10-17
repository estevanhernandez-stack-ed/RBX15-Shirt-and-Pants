using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RBX15.App.Testing;

namespace RBX15.App.Tests;

/// <summary>
/// The bridge's attack surface (M4a contract "Security"): who can open the pipe, what the launch flag matches, and
/// how strictly a request line is read. The pipe tests serve a fake <see cref="IInputBridge"/>, so no window and
/// no input is involved.
/// </summary>
public class BridgeSecurityTests
{
    sealed class EchoBridge : IInputBridge
    {
        public List<BridgeCommand> Seen { get; } = [];

        public Task<BridgeResponse> ExecuteAsync(BridgeCommand command)
        {
            Seen.Add(command);
            return Task.FromResult(new BridgeResponse(command.Id, true, null, new JsonObject { ["cmd"] = command.Cmd }));
        }
    }

    static SecurityIdentifier CurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User!;
    }

    static List<PipeAccessRule> Rules(PipeSecurity security) =>
        security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();

    [Fact] // protected, owned by the current user, the only Allow is the current user, and NETWORK is denied
    public void The_pipe_acl_allows_only_the_current_user()
    {
        var security = TestInputBridge.CreatePipeSecurity();
        var user = CurrentUser();
        var network = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
        Assert.True(security.AreAccessRulesProtected);
        Assert.Equal(user, security.GetOwner(typeof(SecurityIdentifier)));
        var rules = Rules(security);
        var allow = Assert.Single(rules, r => r.AccessControlType == AccessControlType.Allow);
        Assert.Equal((user, PipeAccessRights.FullControl), ((SecurityIdentifier)allow.IdentityReference, allow.PipeAccessRights));
        var deny = Assert.Single(rules, r => r.AccessControlType == AccessControlType.Deny);
        Assert.Equal(network, deny.IdentityReference);
        Assert.All(rules, r => Assert.False(r.IsInherited));
    }

    [Fact] // the live pipe: only the current user is allowed; one request line, one response line; one client, then it closes for good
    public async Task The_pipe_serves_one_client_one_line_per_request_and_never_listens_again()
    {
        var fake = new EchoBridge();
        using var bridge = TestInputBridge.Listen(fake);
        Assert.Matches(new Regex("^rbx15-bridge-" + Environment.ProcessId + "-[0-9a-f]{32}$"), bridge.PipeName);
        var user = CurrentUser();
        Assert.All(Rules(bridge.LiveSecurity()).Where(r => r.AccessControlType == AccessControlType.Allow),
            r => Assert.Equal(user, r.IdentityReference));

        using (var client = new NamedPipeClientStream(".", bridge.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(5000);
            using var reader = new StreamReader(client, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\n", AutoFlush = true };
            await writer.WriteLineAsync("""{"id":4,"cmd":"ping"}""");
            await writer.WriteLineAsync("""{"id":5,"cmd":"inspect","target":"canvas"}""");
            Assert.Equal("""{"id":4,"ok":true,"result":{"cmd":"ping"}}""", await reader.ReadLineAsync());
            Assert.Equal("""{"id":5,"ok":true,"result":{"cmd":"inspect"}}""", await reader.ReadLineAsync());
        }
        await bridge.Served.WaitAsync(TimeSpan.FromSeconds(5));
        using var second = new NamedPipeClientStream(".", bridge.PipeName, PipeDirection.InOut);
        await Assert.ThrowsAsync<TimeoutException>(() => second.ConnectAsync(300));
        Assert.Equal(new[] { "ping", "inspect" }, fake.Seen.Select(c => c.Cmd));
    }

    [Theory] // an over-long line, a non-JSON line or invalid UTF-8 gets one error line with id 0, then the server disconnects
    [InlineData("long")]
    [InlineData("junk")]
    [InlineData("utf8")]
    [InlineData("dup")]
    public async Task A_bad_request_line_gets_one_error_and_a_disconnect(string kind)
    {
        var fake = new EchoBridge();
        using var bridge = TestInputBridge.Listen(fake);
        using var client = new NamedPipeClientStream(".", bridge.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        byte[] line = kind switch
        {
            "long" => Encoding.ASCII.GetBytes("{\"id\":1,\"cmd\":\"" + new string('a', TestInputBridge.MaxLineBytes) + "\"}\n"),
            "junk" => Encoding.ASCII.GetBytes("{not json\n"),
            "dup" => Encoding.ASCII.GetBytes("{\"id\":1,\"id\":2,\"cmd\":\"ping\"}\n"),   // critic M6: a repeated key
            _ => [0x7B, 0xC3, 0x28, 0x7D, 0x0A],
        };
        try
        {
            await client.WriteAsync(line);
        }
        catch (IOException)
        {
            // the server may close before the whole over-long line is written
        }
        using var reader = new StreamReader(client, new UTF8Encoding(false));
        var response = JsonNode.Parse((await reader.ReadLineAsync())!)!.AsObject();
        Assert.Equal((0, false), (response["id"]!.GetValue<int>(), response["ok"]!.GetValue<bool>()));
        Assert.Null(await reader.ReadLineAsync());
        Assert.Empty(fake.Seen);
    }

    [Fact] // the launch flag is matched exactly and case-sensitively
    public void Requested_needs_the_exact_flag()
    {
        Assert.True(TestInputBridge.Requested(["--test-input-bridge"]));
        Assert.True(TestInputBridge.Requested(["--other", "--test-input-bridge"]));
        Assert.False(TestInputBridge.Requested([]));
        Assert.False(TestInputBridge.Requested(["--TEST-INPUT-BRIDGE"]));
        Assert.False(TestInputBridge.Requested(["--test-input-bridge=1"]));
        Assert.False(TestInputBridge.Requested(["-test-input-bridge"]));
        Assert.False(TestInputBridge.Requested([" --test-input-bridge"]));
    }

    [Theory] // strict parsing: anything unexpected is refused with the reason, before it reaches the app
    [InlineData("""{"cmd":"ping"}""", "\"id\" is required")]
    [InlineData("""{"id":1}""", "\"cmd\" is required")]
    [InlineData("""{"id":1,"cmd":"ping","extra":1}""", "unknown field \"extra\"")]
    [InlineData("""{"id":"1","cmd":"ping"}""", "\"id\" must be a 32-bit integer")]
    [InlineData("""{"id":1,"cmd":"key","key":"S","modifiers":["Control"]}""", "unknown modifier \"Control\"")]
    [InlineData("""{"id":1,"cmd":"key","key":"S","modifiers":["Ctrl","Ctrl"]}""", "a modifier is listed twice")]
    [InlineData("""{"id":1,"cmd":"pointer","at":{"canvas":{"x":1}}}""", "\"at.canvas\" must be")]
    [InlineData("""{"id":1,"cmd":"pointer","at":{"canvas":{"x":1,"y":2},"window":{"x":1,"y":2}}}""", "\"at\" must be")]
    [InlineData("""{"id":1,"cmd":"pointer","at":{"element":"","x":1,"y":2}}""", "\"at.element\" must be a non-empty string")]
    [InlineData("""[1,2]""", "a request is one JSON object")]
    [InlineData("""{"id":1,"id":2,"cmd":"ping"}""", "a field is listed twice")]
    public void Parsing_refuses_anything_unexpected(string line, string reason)
    {
        var e = Assert.Throws<FormatException>(() => BridgeJson.Parse(line));
        Assert.StartsWith(reason, e.Message);
    }

    [Fact] // the three "at" forms, modifiers and a null answer
    public void Parsing_reads_every_form()
    {
        var c = BridgeJson.Parse("""{"id":3,"cmd":"pointer","action":"down","button":"right","at":{"canvas":{"x":-5.5,"y":600}}}""");
        Assert.Equal(new BridgePoint(null, -5.5, 600, Canvas: true, Window: false), c.At);
        Assert.Equal(("pointer", "down", "right"), (c.Cmd, c.Action, c.Button));
        Assert.Equal(new BridgePoint("SvSquare", 10, 20, false, false), BridgeJson.Parse("""{"id":1,"cmd":"pointer","at":{"element":"SvSquare","x":10,"y":20}}""").At);
        Assert.Equal(new BridgePoint(null, 900, -40, false, true), BridgeJson.Parse("""{"id":1,"cmd":"pointer","at":{"window":{"x":900,"y":-40}}}""").At);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, BridgeJson.Parse("""{"id":1,"cmd":"key","key":"S","modifiers":["Shift","Ctrl"]}""").Modifiers);
        var dialog = BridgeJson.Parse("""{"id":2,"cmd":"file-dialog","answer":null}""");
        Assert.Null(dialog.Answer);
        Assert.Equal("""{"id":2,"ok":false,"error":"no"}""", BridgeJson.Write(new BridgeResponse(2, false, "no", null)));
    }

    [Fact] // R10: no native dialog under the bridge; answers come from the queue, an empty queue cancels, every call is logged
    public void The_bridge_file_dialogs_answer_from_the_queue_and_log_every_call()
    {
        var log = new StringWriter();
        var dialogs = new BridgeFileDialogs(log);
        dialogs.Enqueue(@"C:\work\a.r15proj");
        dialogs.Enqueue(null);
        dialogs.Enqueue(@"C:\art\1.png|C:\art\2.png");
        Assert.Equal(@"C:\work\a.r15proj", dialogs.SaveProject("shirt.r15proj", null));
        Assert.Null(dialogs.OpenProject(@"C:\work"));
        Assert.Equal(new[] { @"C:\art\1.png", @"C:\art\2.png" }, dialogs.OpenImages());
        Assert.Null(dialogs.PickFolder(null));
        Assert.Equal(new[]
        {
            @"RBX15-BRIDGE-DIALOG SaveProject shirt.r15proj -> C:\work\a.r15proj",
            @"RBX15-BRIDGE-DIALOG OpenProject C:\work -> cancel",
            @"RBX15-BRIDGE-DIALOG OpenImages  -> C:\art\1.png|C:\art\2.png",
            @"RBX15-BRIDGE-DIALOG PickFolder  -> cancel (queue empty)",
        }, log.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory] // R8's neutralizer: what an unmarked (hardware) report keeps
    [InlineData("AbsoluteMove", "AbsoluteMove")]
    [InlineData("Button1Press", "AbsoluteMove")]
    [InlineData("Activate, AbsoluteMove, Button1Press", "Activate, AbsoluteMove")]
    [InlineData("VerticalWheelRotate", "AbsoluteMove")]
    [InlineData("Activate", "Activate")]
    [InlineData("Deactivate", "Deactivate")]
    [InlineData("QueryCursor", "QueryCursor")]
    public void The_neutralizer_keeps_only_bookkeeping_plus_a_move(string actions, string kept) =>
        Assert.Equal(kept, InputBridgeDispatcher.Neutralize(actions));
}
