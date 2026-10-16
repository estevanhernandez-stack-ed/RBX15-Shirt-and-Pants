using RBX15.Engine;

namespace RBX15.App.Canvas;

/// <summary>
/// The host's frame input beyond Document and Zoom: the layer whose selection overlay the engine
/// draws (only while the tool is Select, editor.js:362) and the live drawing preview
/// (editor.js:324-330). TemplateCanvasHost passes both into ViewOptions.
/// </summary>
public sealed record ViewOverlay(int? SelectedLayerId, LivePreview? Preview)
{
    public static readonly ViewOverlay None = new(null, null);
}
