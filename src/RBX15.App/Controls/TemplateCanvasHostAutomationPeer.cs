using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace RBX15.App.Controls;

/// <summary>
/// The canvas's UI Automation face (spec §4 accessibility; contract "Canvas accessibility"). 4.1's
/// &lt;canvas&gt; had no role, name or value (a11y-focus-rings). v5: an Image named "Template canvas"
/// (AutomationProperties.Name), bounds from the element, not keyboard focusable, with a read-only Value
/// pattern whose value is the selected layer's name, or "" with nothing selected.
/// <para>
/// M4a: its children are the visible layers, top first, as <see cref="LayerAutomationPeer"/>s cached by layer id
/// (a client keeps the same element across edits), and it is their Selection container: one layer at a time, none
/// required. A document change resets the children, which makes WPF raise StructureChanged to listening clients;
/// a selection change raises ElementSelected on the newly selected layer. ControlType stays Image (M3b sign-off).
/// </para>
/// </summary>
public sealed class TemplateCanvasHostAutomationPeer : FrameworkElementAutomationPeer, IValueProvider, ISelectionProvider
{
    readonly Dictionary<int, LayerAutomationPeer> _layers = [];
    int? _announced;

    public TemplateCanvasHostAutomationPeer(TemplateCanvasHost owner) : base(owner)
    {
        _announced = owner.LayerAutomation?.SelectedLayerId;
    }

    internal TemplateCanvasHost Host => (TemplateCanvasHost)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string GetClassNameCore() => nameof(TemplateCanvasHost);

    protected override bool IsKeyboardFocusableCore() => false;

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Value or PatternInterface.Selection ? this : base.GetPattern(patternInterface);

    public bool IsReadOnly => true;

    public string Value => Host.AutomationValue;

    /// <summary>Read-only: the layer is renamed in the properties panel, not through the canvas.</summary>
    public void SetValue(string value) => throw new ElementNotEnabledException();

    /// <summary>Tells listening clients (Narrator, Accessibility Insights) that the selected layer changed.</summary>
    internal void RaiseValueChanged(string oldValue, string newValue)
    {
        if (ListenerExists(AutomationEvents.PropertyChanged))
            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldValue, newValue);
    }

    /// <summary>The visible layers, top first (the order the user sees them stacked), one cached peer per layer id.</summary>
    protected override List<AutomationPeer>? GetChildrenCore()
    {
        if (Host.Document is not { } doc)
        {
            _layers.Clear();
            return null;
        }
        var ids = doc.Layers.Select(l => l.Id).ToHashSet();
        foreach (int gone in _layers.Keys.Where(id => !ids.Contains(id)).ToList()) _layers.Remove(gone);
        var children = new List<AutomationPeer>();
        for (int i = doc.Layers.Count - 1; i >= 0; i--)
        {
            var layer = doc.Layers[i];
            if (!layer.Visible) continue;
            if (!_layers.TryGetValue(layer.Id, out var peer)) _layers[layer.Id] = peer = new LayerAutomationPeer(this, layer.Id);
            children.Add(peer);
        }
        return children.Count == 0 ? null : children;
    }

    /// <summary>The child peer of a visible layer, or null. It enumerates the children first, so the peer knows its parent.</summary>
    internal LayerAutomationPeer? LayerPeer(int id)
    {
        GetChildren();
        return Host.Document?.Layers.Find(l => l.Id == id) is { Visible: true } && _layers.TryGetValue(id, out var peer) ? peer : null;
    }

    internal IRawElementProviderSimple Provider() => ProviderFromPeer(this);

    public bool CanSelectMultiple => false;

    public bool IsSelectionRequired => false;

    /// <summary>The selected layer when it is visible, else nothing.</summary>
    public IRawElementProviderSimple[] GetSelection() =>
        Host.LayerAutomation?.SelectedLayerId is int id && LayerPeer(id) is { } peer ? [ProviderFromPeer(peer)] : [];

    /// <summary>The document changed: re-read the children (WPF raises StructureChanged to listening clients).</summary>
    internal void OnLayersChanged()
    {
        ResetChildrenCache();
        OnSelectionChanged();
    }

    /// <summary>The selection may have changed: announce the newly selected layer once.</summary>
    internal void OnSelectionChanged()
    {
        int? now = Host.LayerAutomation?.SelectedLayerId;
        if (now == _announced) return;
        _announced = now;
        if (now is int id && ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected) && LayerPeer(id) is { } peer)
            peer.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }
}
