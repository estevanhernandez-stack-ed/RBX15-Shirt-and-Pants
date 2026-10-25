using System.IO;

namespace RBX15.App.Services;

/// <summary>
/// The file a launch names (M5 item 4; spec §3 "Upgrades", file association). Explorer, the jump list and "Open with" all start the
/// app with the project's full path as one argument; the packaged FileTypeAssociation does the same.
/// </summary>
public static class LaunchArguments
{
    /// <summary>
    /// The first argument that is a fully qualified path to an existing file, or null. Any extension qualifies: <c>ProjectIO</c>
    /// refuses what is not a project. Flags (<c>--test-input-bridge</c> and any other <c>--</c> argument) are skipped. The command
    /// line parser has already removed the quotes around a path with spaces; stray surrounding quotes are trimmed.
    /// </summary>
    public static string? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith("--", StringComparison.Ordinal)) continue;
            var path = arg.Trim().Trim('"');
            try
            {
                if (Path.IsPathFullyQualified(path) && File.Exists(path)) return path;
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
            {
                // A malformed argument is not a file; the next one may be.
            }
        }
        return null;
    }
}
