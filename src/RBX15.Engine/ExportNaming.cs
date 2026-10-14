using System.Text;

namespace RBX15.Engine;

/// <summary>4.1 file naming (ledger io-export-filename, kept): title || default, each UTF-16 unit
/// outside [a-zA-Z0-9_-] becomes '_', not trimmed.</summary>
public static class ExportNaming
{
    public static string PngFileName(string? title) => Sanitize(string.IsNullOrEmpty(title) ? "roblox_shirt" : title) + "_roblox.png";

    public static string ProjectFileName(string? title) => Sanitize(string.IsNullOrEmpty(title) ? "untitled" : title) + ".r15proj";

    static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s) sb.Append(char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_');
        return sb.ToString();
    }
}
