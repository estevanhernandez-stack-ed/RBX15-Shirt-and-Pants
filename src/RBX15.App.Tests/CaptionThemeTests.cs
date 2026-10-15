using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Views;

namespace RBX15.App.Tests;

/// <summary>The themed native caption (ledger shell-window-frame, fix; Este 2026-10-14).</summary>
public class CaptionThemeTests
{
    [Theory] // COLORREF is 0x00BBGGRR; alpha is dropped
    [InlineData(0xFF, 0x0F, 0x1F, 0x31, 0x00311F0Fu)]
    [InlineData(0xFF, 0xE6, 0xEB, 0xF3, 0x00F3EBE6u)]
    [InlineData(0x14, 0xFF, 0xFF, 0xFF, 0x00FFFFFFu)]
    [InlineData(0xFF, 0x00, 0x00, 0x00, 0x00000000u)]
    public void Colors_become_COLORREFs(byte a, byte r, byte g, byte b, uint colorRef) =>
        Assert.Equal(colorRef, CaptionTheme.ToColorRef(Color.FromArgb(a, r, g, b)));

    [Fact] // caption = the header's middle stop, text = ink-100, border = brand-navy-soft, dark mode first
    public void The_caption_takes_the_header_band_and_light_ink() => StaThread.Run(() =>
    {
        var app = Application.Current;
        Color Token(string key) => (Color)app.FindResource(key);
        Assert.Equal(Color.FromRgb(0x0F, 0x1F, 0x31), Token(CaptionTheme.CaptionColorKey));
        Assert.Equal(Color.FromRgb(0xE6, 0xEB, 0xF3), Token(CaptionTheme.TextColorKey));
        Assert.Equal(Color.FromRgb(0x22, 0x3A, 0x54), Token(CaptionTheme.BorderColorKey));
        var attributes = CaptionTheme.Attributes(Token(CaptionTheme.CaptionColorKey), Token(CaptionTheme.TextColorKey), Token(CaptionTheme.BorderColorKey));
        Assert.Equal(new (int, uint)[] { (20, 1), (35, 0x00311F0Fu), (36, 0x00F3EBE6u), (34, 0x00543A22u) }, attributes);
        Assert.Equal(new (int, uint)[] { (20, 1) }, CaptionTheme.Attributes(null, null, null));
    });

    [Fact] // older Windows refuses attributes with an HRESULT; nothing throws either way
    public void Applying_to_a_window_never_throws_before_or_after_it_has_a_handle() => StaThread.Run(() =>
    {
        var dialog = new ThemedDialog("Sure?", confirm: true);       // its constructor applies before the HWND exists
        var hwnd = new WindowInteropHelper(dialog).EnsureHandle();   // SourceInitialized applies it; never shown
        Assert.Equal(4, CaptionTheme.Set(dialog, hwnd).Count);
        CaptionTheme.Apply(dialog);                                  // with a handle: applied at once
        Assert.Empty(CaptionTheme.Set(dialog, IntPtr.Zero));
        dialog.Close();
    });
}
