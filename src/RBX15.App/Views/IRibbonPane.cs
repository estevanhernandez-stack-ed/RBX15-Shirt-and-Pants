namespace RBX15.App.Views;

/// <summary>
/// A ribbon tab's pane. <see cref="HasGroups"/> is the "hide what does not work yet" rule for the tab: the window shows a
/// tab only when its pane has at least one group (a child of the pane's <c>Groups</c> panel), so adding the first group to
/// LayoutRibbon, DetailsRibbon or TextRibbon is all it takes to show the tab.
/// </summary>
public interface IRibbonPane
{
    /// <summary>True when the pane has any group.</summary>
    bool HasGroups { get; }
}
