using RBX15.App.ViewModels;

namespace RBX15.App.Tests;

/// <summary>The status strip's view-model (spec §4; diag-console-only-errors).</summary>
public class StatusViewModelTests
{
    [Fact]
    public void It_starts_hidden()
    {
        var s = new StatusViewModel();
        Assert.Null(s.Message);
        Assert.False(s.IsVisible);
    }

    [Fact]
    public void Show_replaces_the_message_and_raises_both_properties()
    {
        var s = new StatusViewModel();
        var raised = new List<string?>();
        s.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        s.Show("first");
        s.Show("second");
        Assert.Equal("second", s.Message);
        Assert.True(s.IsVisible);
        Assert.Equal(new[] { "Message", "IsVisible", "Message", "IsVisible" }, raised);
    }

    [Fact]
    public void Dismiss_hides_the_strip()
    {
        var s = new StatusViewModel();
        s.Show("x");
        s.DismissCommand.Execute(null);
        Assert.Null(s.Message);
        Assert.False(s.IsVisible);
    }

    [Fact]
    public void An_empty_message_is_refused()
    {
        var s = new StatusViewModel();
        Assert.Throws<ArgumentException>(() => s.Show(""));
        Assert.Throws<ArgumentNullException>(() => s.Show(null!));
    }

    [Fact] // the live region only announces on a change, so the same text shown twice must be seen as a change
    public void Showing_the_same_message_again_raises_a_change()
    {
        var s = new StatusViewModel();
        s.Show("Saved.");
        var raised = new List<string?>();
        s.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        s.Show("Saved.");
        Assert.Equal("Saved.", s.Message);
        Assert.True(s.IsVisible);
        Assert.Contains("Message", raised);
        Assert.Equal("Saved.", s.Message);
    }
}
