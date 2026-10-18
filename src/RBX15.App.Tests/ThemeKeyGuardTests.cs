using System.IO;
using System.Text.RegularExpressions;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// A DynamicResource key that no dictionary defines fails silently at run time: no style, no brush, no
/// focus ring. Every key a view names must be defined in Task 1's theme (Themes/*.xaml) or in that
/// view's own file.
/// </summary>
public class ThemeKeyGuardTests
{
    static readonly string App = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "src", "RBX15.App");

    [Fact]
    public void Every_dynamic_resource_a_view_names_is_defined()
    {
        var theme = string.Concat(Directory.EnumerateFiles(Path.Combine(App, "Themes"), "*.xaml").Select(File.ReadAllText));
        var views = Directory.EnumerateFiles(Path.Combine(App, "Views"), "*.xaml").Append(Path.Combine(App, "MainWindow.xaml")).ToList();
        Assert.Equal(22, views.Count); // the 10 M3a view files, ColorPickerPopup.xaml, MainWindow.xaml, ToolsGroup.xaml and BrushGroup.xaml (M3b Task 2), LayerPropertiesPanel.xaml (M3b Task 3), AssetLibraryPanel.xaml (M4a Task 2), AvatarPreviewPanel.xaml (v5.1 item 2), LayoutRibbon.xaml, DetailsRibbon.xaml and TextRibbon.xaml (M4b item 2), PlacementGroup.xaml and PresetsGroup.xaml (M4b item 5)
        var missing = new List<string>();
        foreach (var file in views)
        {
            var text = File.ReadAllText(file);
            foreach (var key in Regex.Matches(text, @"\{DynamicResource (\w+)\}").Select(m => m.Groups[1].Value).Distinct())
                if (!theme.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal) && !text.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal))
                    missing.Add($"{Path.GetFileName(file)}: {key}");
        }
        Assert.Empty(missing);
    }
}
