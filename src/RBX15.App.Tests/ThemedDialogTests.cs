using System.Windows;
using System.Windows.Controls;
using RBX15.App.Services;
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
        Assert.Equal(Visibility.Collapsed, Find<Button>(d, "DontSaveButton").Visibility);
        Assert.False(d.IsConfirm);
    });

    [Fact] // M4a R13: Save (default, Enter), Don't save, Cancel (Esc), in the Windows order, with the product title
    public void The_save_changes_prompt_has_save_dont_save_and_cancel() => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Do you want to save changes to Untitled?", ThemedDialogKind.SaveChanges);
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", d.Title);
        Assert.Equal("Do you want to save changes to Untitled?", Find<TextBlock>(d, "MessageText").Text);
        var ok = Find<Button>(d, "OkButton");
        var dontSave = Find<Button>(d, "DontSaveButton");
        var cancel = Find<Button>(d, "CancelButton");
        Assert.Equal(("Save", true, false), (ok.Content, ok.IsDefault, ok.IsCancel));
        Assert.Equal(("Don't save", false, false, Visibility.Visible), (dontSave.Content, dontSave.IsDefault, dontSave.IsCancel, dontSave.Visibility));
        Assert.Equal(("Cancel", true, Visibility.Visible), (cancel.Content, cancel.IsCancel, cancel.Visibility));
        var row = (StackPanel)ok.Parent;
        Assert.Equal(new object[] { ok, dontSave, cancel }, row.Children.Cast<object>());
        Assert.False(d.IsConfirm);
        Assert.Equal(SaveChoice.Cancel, d.Choice);          // nothing pressed yet, as the caption's close button leaves it
    });

    [Theory] // R13: each button's choice, set before DialogResult closes the dialog; the test raises Click on a dialog it never shows
    [InlineData("OkButton", SaveChoice.Save)]
    [InlineData("DontSaveButton", SaveChoice.DontSave)]
    public void Each_button_reports_its_choice(string button, SaveChoice expected) => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Do you want to save changes to a.r15proj?", ThemedDialogKind.SaveChanges);
        var b = Find<Button>(d, button);
        // A window that was never shown refuses DialogResult; Click reaches the handler, which sets the choice first.
        Assert.Throws<InvalidOperationException>(() => b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, b)));
        Assert.Equal(expected, d.Choice);
    });

    [Fact] // the existing (message, bool) constructor is the Alert and Confirm kinds
    public void The_bool_constructor_maps_to_alert_and_confirm() => StaThread.Run(() =>
    {
        Assert.True(new ThemedDialog("a", confirm: true).IsConfirm);
        Assert.True(new ThemedDialog("a", ThemedDialogKind.Confirm).IsConfirm);
        Assert.Equal(Visibility.Collapsed, Find<Button>(new ThemedDialog("a", ThemedDialogKind.Confirm), "DontSaveButton").Visibility);
        Assert.Equal("OK", Find<Button>(new ThemedDialog("a", ThemedDialogKind.Confirm), "OkButton").Content);
    });
}
