using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;

namespace RBX15.App.Tests;

/// <summary>
/// The in-process UI Automation client the UI tests drive (M5/M6 decision U1): it finds elements by AutomationId, Name or
/// control type in the peer tree and acts on them only through the provider patterns (Invoke, Value, Toggle,
/// SelectionItem, ExpandCollapse), as a screen reader or the UIA client library would. No mouse, no keyboard device, no
/// synthetic desktop input, no process. Shared by the flow suite and the accessibility pass; everything here runs on the StaThread.
/// A disabled element refuses Invoke, Toggle and Select with <see cref="ElementNotEnabledException"/>, as in a real client.
/// </summary>
internal static class UiaKit
{
    /// <summary>The peer UIA sees for an element (the same one every client call reaches).</summary>
    public static AutomationPeer PeerOf(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element) ?? throw new InvalidOperationException(element.GetType().Name + " has no automation peer.");

    /// <summary>
    /// Whether a client could reach the element: an element that is Hidden or Collapsed, or sits under one, is not shown (WPF still builds its
    /// peer, but UIA reports it off-screen and a user cannot reach it). Peers that are not elements (a canvas layer's item) follow their parent.
    /// </summary>
    public static bool IsShown(AutomationPeer peer) => peer is not UIElementAutomationPeer element || element.Owner.IsVisible;

    /// <summary>The root and every peer below it, depth first, in tree order. A subtree under an element that is not shown (an inactive ribbon pane) is skipped.</summary>
    public static IEnumerable<AutomationPeer> Descendants(AutomationPeer root)
    {
        var stack = new Stack<AutomationPeer>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var peer = stack.Pop();
            if (!IsShown(peer)) continue;
            yield return peer;
            var children = peer.GetChildren();
            if (children is null) continue;
            for (int i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
        }
    }

    /// <summary>
    /// The topmost peers inside <paramref name="container"/>: for content UIA reaches only through its own root, such as an open
    /// Popup (a Popup and the Border in it have no peer, so the window's tree stops at the popup). Search each root with
    /// <see cref="Descendants"/>.
    /// </summary>
    public static IReadOnlyList<AutomationPeer> RootsWithin(DependencyObject container)
    {
        var roots = new List<AutomationPeer>();
        var stack = new Stack<DependencyObject>();
        stack.Push(container);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is UIElement element && !ReferenceEquals(node, container) && UIElementAutomationPeer.CreatePeerForElement(element) is { } peer)
            {
                roots.Add(peer);
                continue;
            }
            for (int i = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
        }
        return roots;
    }

    /// <summary>Every peer under <paramref name="root"/> (root included) matching all the given criteria.</summary>
    public static IReadOnlyList<AutomationPeer> FindAll(AutomationPeer root, string? name = null, string? id = null, AutomationControlType? type = null) =>
        Descendants(root)
            .Where(p => (name is null || p.GetName() == name)
                     && (id is null || p.GetAutomationId() == id)
                     && (type is null || p.GetAutomationControlType() == type))
            .ToList();

    /// <summary>The first match, or null.</summary>
    public static AutomationPeer? TryFind(AutomationPeer root, string? name = null, string? id = null, AutomationControlType? type = null) =>
        Descendants(root).FirstOrDefault(p => (name is null || p.GetName() == name)
                                           && (id is null || p.GetAutomationId() == id)
                                           && (type is null || p.GetAutomationControlType() == type));

    /// <summary>The first match; a miss names what was asked for, so a renamed control reads as a rename.</summary>
    public static AutomationPeer Find(AutomationPeer root, string? name = null, string? id = null, AutomationControlType? type = null) =>
        TryFind(root, name, id, type)
        ?? throw new InvalidOperationException($"No element with name '{name}', id '{id}', type '{type}' under {root.GetClassName()} '{root.GetName()}'.");

    public static T Pattern<T>(AutomationPeer peer, PatternInterface which) where T : class =>
        peer.GetPattern(which) as T
        ?? throw new InvalidOperationException($"'{peer.GetName()}' ({peer.GetClassName()}) does not support {which}.");

    public static bool Supports(AutomationPeer peer, PatternInterface which) => peer.GetPattern(which) is not null;

    public static void Invoke(AutomationPeer peer) => Pattern<IInvokeProvider>(peer, PatternInterface.Invoke).Invoke();

    public static string GetValue(AutomationPeer peer) => Pattern<IValueProvider>(peer, PatternInterface.Value).Value;

    public static void SetValue(AutomationPeer peer, string value) => Pattern<IValueProvider>(peer, PatternInterface.Value).SetValue(value);

    public static ToggleState ToggleState(AutomationPeer peer) => Pattern<IToggleProvider>(peer, PatternInterface.Toggle).ToggleState;

    public static void Toggle(AutomationPeer peer) => Pattern<IToggleProvider>(peer, PatternInterface.Toggle).Toggle();

    public static bool IsSelected(AutomationPeer peer) => Pattern<ISelectionItemProvider>(peer, PatternInterface.SelectionItem).IsSelected;

    public static void Select(AutomationPeer peer) => Pattern<ISelectionItemProvider>(peer, PatternInterface.SelectionItem).Select();

    public static ExpandCollapseState ExpandCollapseState(AutomationPeer peer) =>
        Pattern<IExpandCollapseProvider>(peer, PatternInterface.ExpandCollapse).ExpandCollapseState;

    public static void Expand(AutomationPeer peer) => Pattern<IExpandCollapseProvider>(peer, PatternInterface.ExpandCollapse).Expand();

    public static void Collapse(AutomationPeer peer) => Pattern<IExpandCollapseProvider>(peer, PatternInterface.ExpandCollapse).Collapse();

    /// <summary>
    /// Runs the dispatcher (down to Background priority, so bindings attach and layout runs) until <paramref name="done"/> holds.
    /// For work that posts back to the UI thread: an Open's read, the Recent list's file check. A hang fails after
    /// <paramref name="timeout"/> (10 s by default) instead of stalling the suite. Not a timing assertion: it asserts nothing about how long.
    /// </summary>
    public static void PumpUntil(Func<bool> done, TimeSpan? timeout = null)
    {
        var clock = Stopwatch.StartNew();
        var limit = timeout ?? TimeSpan.FromSeconds(10);
        StaThread.Pump();
        while (!done())
        {
            if (clock.Elapsed > limit) throw new TimeoutException($"The dispatcher work did not finish in {limit.TotalSeconds:0} s.");
            StaThread.Pump();
            Thread.Yield();
        }
    }
}
