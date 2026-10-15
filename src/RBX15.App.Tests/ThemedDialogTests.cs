using System.Windows;
using System.Windows.Controls;
using RBX15.App.Views;

namespace RBX15.App.Tests;

/// <summary>
/// The themed dialog's shape, checked without showing it: title, message, buttons and their keys
/// (dialogs-inventory, io-empty-export-confirm). Showing it is the manual smoke step.
/// </summary>
public class ThemedDialogTests
{
    static T Find<T>(Window w, string name) where T : class => Assert.IsType<T>(w.FindName(name), exactMatch: false);

    [Fact]
    public void A_confirm_has_the_product_title_and_ok_and_cancel() => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Sure?", confirm: true);
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", d.Title);
        Assert.Equal("Sure?", Find<TextBlock>(d, "MessageText").Text);
        var ok = Find<Button>(d, "OkButton");
        var cancel = Find<Button>(d, "CancelButton");
        Assert.Equal(("OK", true, false), (ok.Content, ok.IsDefault, ok.IsCancel));
        Assert.Equal(("Cancel", true, Visibility.Visible), (cancel.Content, cancel.IsCancel, cancel.Visibility));
        Assert.True(d.IsConfirm);
    });

    [Fact] // spec §4 accessibility: the peer must report the question, not a fixed label
    public void The_message_is_read_as_the_question_not_as_a_label() => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Sure?", confirm: true);
        var name = System.Windows.Automation.AutomationProperties.GetName(Find<TextBlock>(d, "MessageText"));
        Assert.True(name.Length == 0 || name == "Sure?", $"automation name was '{name}'");
    });

    [Fact]
    public void An_alert_has_only_ok() => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Done.", confirm: false);
        Assert.Equal(Visibility.Collapsed, Find<Button>(d, "CancelButton").Visibility);
        Assert.False(Find<Button>(d, "CancelButton").IsCancel);
        Assert.False(d.IsConfirm);
    });
}
