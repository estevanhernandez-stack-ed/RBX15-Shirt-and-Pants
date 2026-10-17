using System.Windows;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App.Tests;

/// <summary>
/// The window title (interfaces R3): 4.1's product name while untitled and clean (main.js:8, editor.html:6; ledger
/// shell-window-title, keep), otherwise the Windows convention "[*]name - product" (ledger shell-window-frame, title part).
/// </summary>
[Collection("MainWindow")] // builds a MainWindow; see ShellLookTests
public class WindowTitleTests
{
    const string Product = "RBX15 Classic Shirt and Pants Maker";

    [Theory]
    [InlineData(null, false, Product)]
    [InlineData(null, true, "*Untitled - " + Product)]
    [InlineData("C:\\work\\shirt.r15proj", false, "shirt.r15proj - " + Product)]
    [InlineData("C:\\work\\shirt.r15proj", true, "*shirt.r15proj - " + Product)]
    public void The_title_formula(string? file, bool dirty, string expected) =>
        Assert.Equal(expected, MainViewModel.WindowTitle(file, dirty));

    [Fact] // the title follows an edit, an undo back to clean, a save and an open; selection alone never raises it
    public void The_title_follows_the_document_and_the_file()
    {
        var session = new EditorSession();
        var main = TestShell.Main(session);
        var raised = new List<string>();
        main.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.Title)) raised.Add(main.Title); };

        Assert.Equal(Product, main.Title);
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        Assert.True(session.Apply(d => d with { BgColor = "#000001" }));   // still dirty: no second raise
        Assert.True(session.Undo());
        Assert.True(session.Undo());
        session.MarkSaved("C:\\work\\a.r15proj", session.Document);
        session.Select(null);
        session.Open(new ProjectLoad(new Document(), 1, []), "C:\\work\\b.r15proj");

        Assert.Equal(new[]
        {
            "*Untitled - " + Product,
            Product,
            "a.r15proj - " + Product,
            "b.r15proj - " + Product,
        }, raised);
    }

    [Fact] // the window's caption is bound to it
    public void The_window_shows_the_title() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var window = new MainWindow(TestShell.Main(session), new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        Assert.Equal(Product, window.Title);
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        Assert.Equal("*Untitled - " + Product, window.Title);
    });
}
