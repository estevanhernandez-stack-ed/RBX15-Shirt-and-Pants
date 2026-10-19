using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Placement buttons and the Presets as session commands (ledger btn-fit-front, btn-fit-fb, btn-stretch, btn-allover,
/// btn-scatter, preset-meme, preset-allover, preset-sleeve-pop, preset-mirror, preset-scattered, preset-fullbleed,
/// placement-selection-guard). The geometry is <see cref="Placement"/>; this class only applies it to the selected layer.
/// <para>
/// Each action is exactly one <see cref="EditorSession.ApplyWithSelection"/>, so it is one undo step; a Preset that 4.1 ran
/// as two (editor.js:1734-1749 plus the delegated button's own saveUndo) is one here. With nothing selected an action
/// returns false before it touches the session: no step, no Changing event, no id taken. The selection stays on the
/// original layer, since <see cref="Placement"/> leaves it there. Clone ids come from <see cref="EditorSession.TakeId"/>.
/// </para>
/// </summary>
internal static class PlacementActions
{
    /// <summary>Fit Front, and the Meme Tee preset.</summary>
    public static bool FitFront(EditorSession session) =>
        Run(session, (s, id) => new EditorSnapshot(Placement.FitFront(s.Document, id), s.SelectedLayerId));

    /// <summary>F+B, and the Mirror F+B preset.</summary>
    public static bool FrontAndBack(EditorSession session) =>
        Run(session, (s, id) => Placement.FrontAndBack(s, id, session.TakeId()));

    public static bool Stretch(EditorSession session) =>
        Run(session, (s, id) => Placement.Stretch(s, id, session.TakeId()));

    /// <summary>Allover, and the Allover preset.</summary>
    public static bool Allover(EditorSession session) =>
        Run(session, (s, id) => Placement.Allover(s, id, session.TakeId));

    /// <summary>Scatter, and the Scatter preset. <paramref name="rng"/> is read once per click.</summary>
    public static bool Scatter(EditorSession session, Func<Random> rng) =>
        Run(session, (s, id) => Placement.Scatter(s, id, session.TakeId, rng()));

    public static bool SleevePop(EditorSession session) =>
        Run(session, (s, id) => Placement.SleevePop(s, id, session.TakeId));

    public static bool Fullbleed(EditorSession session) =>
        Run(session, (s, id) => new EditorSnapshot(Placement.Fullbleed(s.Document, id), s.SelectedLayerId));

    /// <summary>"Center on front torso" (the Selected layer panel's button).</summary>
    public static bool CenterOnFront(EditorSession session) =>
        Run(session, (s, id) => new EditorSnapshot(Placement.CenterOnFront(s.Document, id), s.SelectedLayerId));

    static bool Run(EditorSession session, Func<EditorSnapshot, int, EditorSnapshot> op)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.SelectedLayer is null) return false;
        return session.ApplyWithSelection(s => s.SelectedLayerId is int id ? op(s, id) : s);
    }
}
