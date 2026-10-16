using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls.Primitives;

namespace RBX15.App.Controls;

/// <summary>
/// A Tools group button that tells assistive tech which tool is active through the Toggle pattern (final fix wave 14:
/// Narrator does not reliably announce ItemStatus). Its IsChecked is bound OneWay to the view-model's IsActive and is
/// the view-model's alone: a click runs Command and never flips the state locally, so pressing the active Select, which
/// never toggles off, stays checked. The automation peer's Toggle runs the same click, and it keeps Invoke, which a
/// plain Button had, so UIA drivers and assistive tech that invoke still work.
/// </summary>
public sealed class ToolToggleButton : ToggleButton
{
    /// <summary>The state comes from the binding only; the Command decides the tool.</summary>
    protected override void OnToggle() { }

    protected override AutomationPeer OnCreateAutomationPeer() => new ToolToggleButtonAutomationPeer(this);

    /// <summary>A click, as the mouse or Space would make one: raises Click and runs Command.</summary>
    internal void PerformClick() => OnClick();

    sealed class ToolToggleButtonAutomationPeer(ToolToggleButton owner) : ToggleButtonAutomationPeer(owner), IToggleProvider, IInvokeProvider
    {
        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

        ToggleState IToggleProvider.ToggleState => owner.IsChecked == true ? ToggleState.On : ToggleState.Off;

        void IToggleProvider.Toggle() => Click();

        void IInvokeProvider.Invoke() => Click();

        void Click()
        {
            if (!IsEnabled()) throw new ElementNotEnabledException();
            owner.PerformClick();
        }
    }
}
