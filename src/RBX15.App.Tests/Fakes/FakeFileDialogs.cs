using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Returns canned picks and records what each dialog was asked (suggested names and starting folders).</summary>
sealed class FakeFileDialogs : IFileDialogs
{
    public IReadOnlyList<string> OpenResult { get; set; } = [];
    public string? SaveResult { get; set; }
    public int OpenCalls { get; private set; }
    public List<string> SaveSuggestions { get; } = [];

    /// <summary>OpenProject's answer (null = cancel) and the folder each call started in.</summary>
    public string? OpenProjectResult { get; set; }
    public List<string?> OpenProjectDirs { get; } = [];

    /// <summary>SaveProject's answer (null = cancel) and each call's suggested name and starting folder.</summary>
    public string? SaveProjectResult { get; set; }
    public List<(string Suggested, string? Dir)> SaveProjectCalls { get; } = [];

    /// <summary>PickFolder's answer (null = cancel) and the folder each call started in.</summary>
    public string? PickFolderResult { get; set; }
    public List<string?> PickFolderDirs { get; } = [];

    public IReadOnlyList<string> OpenImages()
    {
        OpenCalls++;
        return OpenResult;
    }

    public string? SavePng(string suggestedFileName)
    {
        SaveSuggestions.Add(suggestedFileName);
        return SaveResult;
    }

    public string? OpenProject(string? initialDirectory)
    {
        OpenProjectDirs.Add(initialDirectory);
        return OpenProjectResult;
    }

    public string? SaveProject(string suggestedFileName, string? initialDirectory)
    {
        SaveProjectCalls.Add((suggestedFileName, initialDirectory));
        return SaveProjectResult;
    }

    public string? PickFolder(string? initialDirectory)
    {
        PickFolderDirs.Add(initialDirectory);
        return PickFolderResult;
    }
}
