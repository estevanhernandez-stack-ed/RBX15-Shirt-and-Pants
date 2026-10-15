namespace RBX15.App.Services;

/// <summary>
/// The main window's restore bounds in device-independent pixels, and whether it was maximized.
/// 4.1 remembered none of this (main.js:5-13; ledger shell-window-size and shell-no-persistence, fix).
/// </summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);
