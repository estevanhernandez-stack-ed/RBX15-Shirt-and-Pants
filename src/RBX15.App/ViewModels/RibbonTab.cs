namespace RBX15.App.ViewModels;

/// <summary>
/// The ribbon's tabs, in 4.1's order (editor.html:23-27; ledger ribbon-tab-switch). The active one is
/// <see cref="MainViewModel.ActiveTab"/>.
/// </summary>
public enum RibbonTab
{
    Home,
    Layout,
    Details,
    Text,
}
