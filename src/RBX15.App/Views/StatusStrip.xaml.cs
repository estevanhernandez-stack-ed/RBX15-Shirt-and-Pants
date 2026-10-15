using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;

namespace RBX15.App.Views;

/// <summary>The status strip. A new message is announced to screen readers as a polite live region.</summary>
public partial class StatusStrip : UserControl
{
    public StatusStrip() => InitializeComponent();

    void OnMessageUpdated(object? sender, DataTransferEventArgs e) =>
        UIElementAutomationPeer.CreatePeerForElement(MessageText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
}
