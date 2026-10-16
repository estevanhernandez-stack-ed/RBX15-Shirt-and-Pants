using RBX15.Engine;

namespace RBX15.App.Services;

/// <summary>
/// Turns picked or dropped files into layers: Add image, the drop zone and a canvas drop all land
/// here. Every successful file of one call is added in the given order as ONE undo step (spec §3;
/// ledger layers-multi-file-async-order, drop-filetypes, project-load-image-layer). The last file
/// ends on top and selected, as 4.1's last addLayer did. Failures go to the status strip, named
/// (diag-console-only-errors); a call that adds files and fails none clears the strip.
/// </summary>
public sealed class ImportService(EditorSession session, IStatus status)
{
    /// <summary>Raised after an import added at least one layer: addLayer sets the Select tool (editor.js:523; R8).</summary>
    public event EventHandler? LayersAdded;

    /// <summary>
    /// Imports <paramref name="paths"/>; every layer is centered on <paramref name="dropAt"/>
    /// (template pixels) when given, else fit in the front slot. Returns the number of layers added.
    /// </summary>
    public int Import(IReadOnlyList<string> paths, (double X, double Y)? dropAt = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0) return 0;
        var result = ImageImporter.Load(paths);
        if (result.Images.Count > 0)
            session.ApplyWithSelection(s =>
            {
                foreach (var image in result.Images) s = LayerOps.AddImage(s, image.Image, session.TakeId(), image.Name, dropAt);
                return s;
            });
        if (result.Failures.Count > 0) status.Show(FailureMessage(result.Failures));
        else if (result.Images.Count > 0) status.Clear();
        if (result.Images.Count > 0) LayersAdded?.Invoke(this, EventArgs.Empty);
        return result.Images.Count;
    }

    /// <summary>One line naming every file that failed and why.</summary>
    public static string FailureMessage(IReadOnlyList<ImportFailure> failures)
    {
        ArgumentOutOfRangeException.ThrowIfZero(failures.Count);
        if (failures.Count == 1) return $"Couldn't add {failures[0].FileName}: {failures[0].Reason}";
        return $"Couldn't add {failures.Count} files. " + string.Join(" ", failures.Select(f => $"{f.FileName}: {f.Reason}"));
    }
}
