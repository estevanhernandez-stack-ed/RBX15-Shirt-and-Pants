namespace RBX15.Engine;

/// <summary>
/// exportPNG's blank-template guard (editor.js:1500-1506; ledger <c>export-png-guard</c> and
/// <c>io-empty-export-confirm</c>, both kept). The caller asks <see cref="EmptyTemplateMessage"/>
/// when <see cref="HasArt"/> is false, and exports only on OK.
/// </summary>
public static class ExportGuard
{
    /// <summary>4.1's confirm text, verbatim with its em dash (editor.js:1506).</summary>
    public const string EmptyTemplateMessage = "The template is empty — download a blank PNG anyway?";

    /// <summary>
    /// editor.js:1504-1505: any visible layer, any region fill, or a background image. A background
    /// color alone is not art, and hidden layers do not count.
    /// </summary>
    public static bool HasArt(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return doc.Layers.Exists(l => l.Visible) || !doc.RegionFills.IsEmpty || doc.BgImage is not null;
    }
}
