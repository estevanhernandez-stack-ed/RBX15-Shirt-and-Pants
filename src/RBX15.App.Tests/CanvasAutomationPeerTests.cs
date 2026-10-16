using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The canvas automation peer (contract "Canvas accessibility"; spec §4: every control has an automation name).</summary>
public class CanvasAutomationPeerTests
{
    [Fact] // an Image named "Template canvas", not focusable, with a read-only Value
    public void The_peer_is_a_named_image_with_a_read_only_value() => StaThread.Run(() =>
    {
        var host = new TemplateCanvasHost { AutomationValue = "Taft" };
        var peer = Assert.IsType<TemplateCanvasHostAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host));
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        Assert.Equal(("TemplateCanvasHost", "Template canvas"), (peer.GetClassName(), peer.GetName()));
        Assert.False(peer.IsKeyboardFocusable());
        var value = Assert.IsAssignableFrom<IValueProvider>(peer.GetPattern(PatternInterface.Value));
        Assert.Equal(("Taft", true), (value.Value, value.IsReadOnly));
        Assert.Throws<ElementNotEnabledException>(() => value.SetValue("x"));
        host.AutomationValue = null!;
        Assert.Equal("", value.Value);
    });

    [Fact] // the Value is the selected layer's name through CanvasView's binding, "" with nothing selected
    public void The_value_follows_the_selection() => StaThread.Run(() =>
    {
        var session = SessionWith(LayerOf(1) with { Name = "drip_blood_front_v2" });
        var vm = new CanvasViewModel(session, new ToolsViewModel(), new BrushViewModel(new FakeColorPicker()), new FakeStatus());
        var view = new CanvasView { DataContext = vm };
        StaThread.Pump();
        var host = (TemplateCanvasHost)view.FindName("Host");
        var value = (IValueProvider)UIElementAutomationPeer.CreatePeerForElement(host).GetPattern(PatternInterface.Value);
        Assert.Equal("", value.Value);
        session.Select(1);
        Assert.Equal("drip_blood_front_v2", value.Value);
        session.Select(null);
        Assert.Equal("", value.Value);
    });
}
