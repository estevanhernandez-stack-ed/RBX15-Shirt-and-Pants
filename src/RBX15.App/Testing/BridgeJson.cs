using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;

namespace RBX15.App.Testing;

/// <summary>
/// Where a pointer command lands (M4a interfaces §3.8). Exactly one form is set:
/// <list type="bullet">
/// <item><see cref="Canvas"/>: (X, Y) in template pixels on the main window's canvas, outside 0..585 x 0..559 allowed;</item>
/// <item><see cref="Element"/>: (X, Y) in DIPs from the top-left of the element whose AutomationId (else Name) it is;</item>
/// <item><see cref="Window"/>: (X, Y) in main-window client DIPs; negative or past the size is outside the window.</item>
/// </list>
/// </summary>
public sealed record BridgePoint(string? Element, double X, double Y, bool Canvas, bool Window);

/// <summary>One request line. Fields a command does not use are left at their defaults.</summary>
public sealed record BridgeCommand(int Id, string Cmd, string? Action, string? Button, BridgePoint? At, int Delta,
    string? Key, ModifierKeys Modifiers, int TimeoutMs, string? Answer, string? Target);

/// <summary>One response line: Ok with an optional Result, or not Ok with an Error.</summary>
public sealed record BridgeResponse(int Id, bool Ok, string? Error, JsonObject? Result);

/// <summary>Executes one command on the app's UI thread.</summary>
public interface IInputBridge
{
    Task<BridgeResponse> ExecuteAsync(BridgeCommand command);
}

/// <summary>
/// The bridge's wire format: one JSON object per line (UTF-8 NDJSON). Parsing is strict, because the pipe is an input
/// surface: an unknown field, a wrong type, a missing id or cmd, or an unknown modifier is a <see cref="FormatException"/>
/// naming the problem, and the server answers it with an error line and disconnects.
/// </summary>
public static class BridgeJson
{
    static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "id", "cmd", "action", "button", "at", "delta", "key", "modifiers", "timeoutMs", "answer", "target",
    };

    /// <summary>Parses one request line. Throws <see cref="FormatException"/> with the reason.</summary>
    public static BridgeCommand Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(line, documentOptions: new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException e)
        {
            throw new FormatException("not JSON: " + e.Message);
        }
        if (root is not JsonObject o) throw new FormatException("a request is one JSON object");
        try
        {
            foreach (var (name, _) in o)
                if (!Fields.Contains(name)) throw new FormatException($"unknown field \"{name}\"");
        }
        catch (ArgumentException e)
        {
            // JsonObject builds its dictionary on first use and throws ArgumentException on a repeated key (measured,
            // .NET 10): a request that names a field twice is refused like any other malformed line (critic M6).
            throw new FormatException("a field is listed twice: " + e.Message);
        }
        int id = Int(o, "id") ?? throw new FormatException("\"id\" is required");
        string cmd = Str(o, "cmd") ?? throw new FormatException("\"cmd\" is required");
        return new BridgeCommand(id, cmd, Str(o, "action"), Str(o, "button"), Point(o["at"]), Int(o, "delta") ?? 0,
            Str(o, "key"), Modifiers(o["modifiers"]), Int(o, "timeoutMs") ?? 0, Str(o, "answer"), Str(o, "target"));
    }

    /// <summary>One response line, no indentation and no line break.</summary>
    public static string Write(BridgeResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var o = new JsonObject { ["id"] = response.Id, ["ok"] = response.Ok };
        if (response.Error is not null) o["error"] = response.Error;
        if (response.Result is not null) o["result"] = response.Result.DeepClone();
        return o.ToJsonString();
    }

    static string? Str(JsonObject o, string name) => o[name] switch
    {
        null => null,
        JsonValue v when v.TryGetValue(out string? s) => s,
        _ => throw new FormatException($"\"{name}\" must be a string"),
    };

    static int? Int(JsonObject o, string name) => o[name] switch
    {
        null => null,
        JsonValue v when v.TryGetValue(out int i) => i,
        _ => throw new FormatException($"\"{name}\" must be a 32-bit integer"),
    };

    static double Num(JsonObject o, string name) => o[name] switch
    {
        JsonValue v when v.TryGetValue(out double d) && double.IsFinite(d) => d,
        _ => throw new FormatException($"\"{name}\" must be a finite number"),
    };

    /// <summary>{"canvas":{"x","y"}}, {"window":{"x","y"}} or {"element":"id","x","y"}; nothing else.</summary>
    static BridgePoint? Point(JsonNode? node)
    {
        if (node is null) return null;
        if (node is not JsonObject at) throw new FormatException("\"at\" must be an object");
        string[] keys = at.Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
        if (keys is ["canvas"] or ["window"])
        {
            if (at[keys[0]] is not JsonObject xy || xy.Count != 2 || !xy.ContainsKey("x") || !xy.ContainsKey("y"))
                throw new FormatException($"\"at.{keys[0]}\" must be {{\"x\":..,\"y\":..}}");
            return new BridgePoint(null, Num(xy, "x"), Num(xy, "y"), Canvas: keys[0] == "canvas", Window: keys[0] == "window");
        }
        if (keys is ["element", "x", "y"])
        {
            string element = Str(at, "element") is { Length: > 0 } e ? e : throw new FormatException("\"at.element\" must be a non-empty string");
            return new BridgePoint(element, Num(at, "x"), Num(at, "y"), Canvas: false, Window: false);
        }
        throw new FormatException("\"at\" must be {\"canvas\":{x,y}}, {\"window\":{x,y}} or {\"element\":id,\"x\":..,\"y\":..}");
    }

    /// <summary>An array of "Ctrl", "Shift" and "Alt", each at most once.</summary>
    static ModifierKeys Modifiers(JsonNode? node)
    {
        if (node is null) return ModifierKeys.None;
        if (node is not JsonArray list) throw new FormatException("\"modifiers\" must be an array");
        var result = ModifierKeys.None;
        foreach (var item in list)
        {
            var m = item is JsonValue v && v.TryGetValue(out string? s) ? s switch
            {
                "Ctrl" => ModifierKeys.Control,
                "Shift" => ModifierKeys.Shift,
                "Alt" => ModifierKeys.Alt,
                _ => throw new FormatException($"unknown modifier \"{s}\" (Ctrl, Shift, Alt)"),
            } : throw new FormatException("\"modifiers\" holds strings");
            if ((result & m) != 0) throw new FormatException("a modifier is listed twice");
            result |= m;
        }
        return result;
    }

    internal static string Invariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
