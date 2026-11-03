namespace RBX15.Parity;

public static class RepoRoot
{
    /// <summary>The nearest ancestor of <paramref name="start"/> holding global.json (the repo root).</summary>
    public static string Find(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "global.json"))) return dir.FullName;
        throw new DirectoryNotFoundException($"no global.json above {start}");
    }

    /// <summary>
    /// A frozen 4.1 source file (editor.js, editor.html, styles.css or main.js) in tests/goldens/source-4.1. The cutover
    /// deleted the Electron app; these four are kept byte for byte from v4.1.0 so the tests that derive their
    /// expectations from 4.1's text (dialog sites, theme tokens, the Word Art font list) keep reading the real source.
    /// </summary>
    public static string Source41(string start, string file) =>
        Path.Combine(Find(start), "tests", "goldens", "source-4.1", file);
}
