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
}
