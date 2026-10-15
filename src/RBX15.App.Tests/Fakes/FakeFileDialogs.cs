using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Returns canned picks and records what the Save dialog was asked to suggest.</summary>
sealed class FakeFileDialogs : IFileDialogs
{
    public IReadOnlyList<string> OpenResult { get; set; } = [];
    public string? SaveResult { get; set; }
    public int OpenCalls { get; private set; }
    public List<string> SaveSuggestions { get; } = [];

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
}
