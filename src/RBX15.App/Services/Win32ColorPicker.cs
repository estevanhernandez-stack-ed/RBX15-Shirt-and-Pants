using System.Globalization;
using System.Runtime.InteropServices;
using RBX15.Engine;

namespace RBX15.App.Services;

/// <summary>
/// <see cref="IColorPicker"/> over comdlg32 <c>ChooseColorW</c>, the dialog Chromium opens for 4.1's
/// <c>&lt;input type=color&gt;</c> on Windows (contract ruling: P/Invoke, no WinForms reference). The
/// dialog opens fully expanded (<c>CC_FULLOPEN</c>) on the current color (<c>CC_RGBINIT</c>). The 16
/// custom colors persist for the life of the process, shared by both pickers. UI thread only.
/// </summary>
public sealed class Win32ColorPicker(Func<IntPtr> ownerHandle) : IColorPicker
{
    const int CC_RGBINIT = 0x1;
    const int CC_FULLOPEN = 0x2;

    // 16 COLORREFs, white until the user stores one. Allocated once and never freed: the process owns it.
    static readonly IntPtr CustomColors = AllocCustomColors();

    public string? Pick(string initialHex)
    {
        var cc = new ChooseColorStruct
        {
            lStructSize = Marshal.SizeOf<ChooseColorStruct>(),
            hwndOwner = ownerHandle(),
            rgbResult = ToColorRef(initialHex),
            lpCustColors = CustomColors,
            Flags = CC_RGBINIT | CC_FULLOPEN,
        };
        return ChooseColorW(ref cc) ? FromColorRef(cc.rgbResult) : null;
    }

    /// <summary><c>#rrggbb</c> (or <c>#rgb</c>) to a Win32 COLORREF, <c>0x00BBGGRR</c>. Unparsable reads as black, as <see cref="CssColor.HexToRgb"/> does.</summary>
    internal static uint ToColorRef(string hex)
    {
        var (r, g, b) = CssColor.HexToRgb(hex);
        return (uint)(r | (g << 8) | (b << 16));
    }

    /// <summary>A COLORREF to lowercase <c>#rrggbb</c>; the high byte is ignored.</summary>
    internal static string FromColorRef(uint colorRef) =>
        string.Create(CultureInfo.InvariantCulture, $"#{colorRef & 0xFF:x2}{(colorRef >> 8) & 0xFF:x2}{(colorRef >> 16) & 0xFF:x2}");

    static IntPtr AllocCustomColors()
    {
        var block = Marshal.AllocHGlobal(16 * sizeof(uint));
        for (int i = 0; i < 16; i++) Marshal.WriteInt32(block, i * sizeof(uint), 0x00FFFFFF);
        return block;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ChooseColorStruct
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public uint rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ChooseColorW(ref ChooseColorStruct cc);
}
