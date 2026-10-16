using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace RBX15.App.Controls;

/// <summary>
/// The canvas's UI Automation face (spec §4 accessibility; contract "Canvas accessibility"). 4.1's
/// &lt;canvas&gt; had no role, name or value (a11y-focus-rings). v5: an Image named "Template canvas"
/// (AutomationProperties.Name), bounds from the element, not keyboard focusable, with a read-only Value
/// pattern whose value is the selected layer's name, or "" with nothing selected.
/// </summary>
public sealed class TemplateCanvasHostAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
{
    public TemplateCanvasHostAutomationPeer(TemplateCanvasHost owner) : base(owner) { }

    TemplateCanvasHost Host => (TemplateCanvasHost)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string GetClassNameCore() => nameof(TemplateCanvasHost);

    protected override bool IsKeyboardFocusableCore() => false;

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

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
}
