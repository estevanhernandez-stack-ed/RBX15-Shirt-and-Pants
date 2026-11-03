param([string]$Exe, [string]$Project, [string]$Out, [string[]]$Steps, [int]$ZoomClicks = 1)
# Store screenshots and trailer frames at 1920x1080. Launches RBX15 on a project, sizes the visible frame to
# 1920x1080 and runs steps. Everything is driven through UI Automation patterns and PrintWindow: no synthetic
# mouse or keyboard input. settings.json is backed up first and restored after.
#   select:<layer name pattern>   tab:<Home|Background|Details|Text>   r15   classic   wait:<ms>
#   shot:<name>                   clip:<name>:<seconds>:<fps>   (frames to <Out>\<name>\f0000.png ...)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class W {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
}
'@
[W]::SetProcessDpiAwarenessContext([IntPtr]-4) | Out-Null
New-Item -ItemType Directory -Force $Out | Out-Null

$settings = Join-Path $env:LOCALAPPDATA 'RBX15\settings.json'
$backup = "$settings.capture-backup"
if (Test-Path $settings) { Copy-Item $settings $backup -Force }

$proc = Start-Process -FilePath $Exe -ArgumentList "`"$Project`"" -PassThru
try {
    for ($i = 0; $i -lt 60 -and $proc.MainWindowHandle -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 500; $proc.Refresh() }
    $h = $proc.MainWindowHandle
    if ($h -eq [IntPtr]::Zero) { throw 'no main window' }
    Start-Sleep -Seconds 3
    [W]::ShowWindow($h, 9) | Out-Null

    function Frame { $f = New-Object W+RECT; [W]::DwmGetWindowAttribute($h, 9, [ref]$f, 16) | Out-Null; $f }
    function Outer { $r = New-Object W+RECT; [W]::GetWindowRect($h, [ref]$r) | Out-Null; $r }
    [W]::SetWindowPos($h, [IntPtr]::Zero, 100, 100, 1920, 1080, 0x0014) | Out-Null
    Start-Sleep -Milliseconds 800
    $o = Outer; $f = Frame
    $dw = ($o.R - $o.L) - ($f.R - $f.L); $dh = ($o.B - $o.T) - ($f.B - $f.T)
    [W]::SetWindowPos($h, [IntPtr]::Zero, 100, 100, 1920 + $dw, 1080 + $dh, 0x0014) | Out-Null
    Start-Sleep -Seconds 2
    $f = Frame; "frame $($f.R - $f.L)x$($f.B - $f.T)"

    function Grab([string]$path) {
        $o = Outer; $f = Frame
        $bmp = New-Object System.Drawing.Bitmap ($o.R - $o.L), ($o.B - $o.T)
        $g = [System.Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
        [W]::PrintWindow($h, $dc, 2) | Out-Null
        $g.ReleaseHdc($dc); $g.Dispose()
        $crop = New-Object System.Drawing.Rectangle ($f.L - $o.L), ($f.T - $o.T), ($f.R - $f.L), ($f.B - $f.T)
        $c = $bmp.Clone($crop, $bmp.PixelFormat)
        $c.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $size = "$($c.Width)x$($c.Height)"; $c.Dispose(); $bmp.Dispose(); $size
    }

    $script:Jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
    function GrabScreen([string]$path) {
        $f = Frame
        $bmp = New-Object System.Drawing.Bitmap ($f.R - $f.L), ($f.B - $f.T)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($f.L, $f.T, 0, 0, $bmp.Size)
        $g.Dispose()
        $ep = New-Object System.Drawing.Imaging.EncoderParameters 1
        $ep.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new([System.Drawing.Imaging.Encoder]::Quality, [long]95)
        $bmp.Save($path, $script:Jpeg, $ep); $bmp.Dispose()
    }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    $Scope = [System.Windows.Automation.TreeScope]::Descendants
    function Find([string]$type, [string]$name) {
        $ct = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty), ([System.Windows.Automation.ControlType]::$type)
        foreach ($e in $root.FindAll($Scope, $ct)) { if ($e.Current.Name -like $name) { return $e } }
        $null
    }
    function Press($e) {
        foreach ($pat in [System.Windows.Automation.SelectionItemPattern]::Pattern, [System.Windows.Automation.InvokePattern]::Pattern, [System.Windows.Automation.TogglePattern]::Pattern) {
            $p = $null
            if ($e.TryGetCurrentPattern($pat, [ref]$p)) {
                if ($p -is [System.Windows.Automation.SelectionItemPattern]) { $p.Select() } elseif ($p -is [System.Windows.Automation.InvokePattern]) { $p.Invoke() } else { $p.Toggle() }
                return
            }
        }
        throw "no select or invoke pattern on '$($e.Current.Name)'"
    }

    $zin = Find 'Button' 'Zoom in'
    for ($z = 0; $z -lt $ZoomClicks; $z++) { $zin.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 200 }

    foreach ($s in $Steps) {
        $parts = $s.Split(':')
        switch ($parts[0]) {
            'select'  { $e = Find 'ListItem' $parts[1]; if (-not $e) { throw "no layer row like '$($parts[1])'" }; Press $e; "selected '$($e.Current.Name)'" }
            'tab'     { $e = Find 'RadioButton' "$($parts[1]) tab"; if (-not $e) { throw "no tab $($parts[1])" }; Press $e }
            'r15'     { $e = Find 'Button' 'R15*'; if (-not $e) { throw 'no R15 switch' }; Press $e }
            'classic' { $e = Find 'Button' 'Classic*'; if (-not $e) { throw 'no Classic switch' }; Press $e }
            'wait'    { Start-Sleep -Milliseconds ([int]$parts[1]) }
            'shot'    { Start-Sleep -Milliseconds 1200; "$($parts[1]) $(Grab (Join-Path $Out "$($parts[1]).png"))" }
            'screen'  {
                [W]::SetWindowPos($h, [IntPtr]-1, 0, 0, 0, 0, 0x0003) | Out-Null
                [W]::SetForegroundWindow($h) | Out-Null
                Start-Sleep -Milliseconds 1200
                GrabScreen (Join-Path $Out "$($parts[1]).jpg")
                [W]::SetWindowPos($h, [IntPtr]-2, 0, 0, 0, 0, 0x0003) | Out-Null
                "$($parts[1]).jpg"
            }
            'clip'    {
                $dir = Join-Path $Out $parts[1]; New-Item -ItemType Directory -Force $dir | Out-Null
                $secs = [double]$parts[2]; $fps = [double]$parts[3]
                # PrintWindow serves a stale 3D viewport, so clips copy the window from the screen, on top of everything.
                [W]::SetWindowPos($h, [IntPtr]-1, 0, 0, 0, 0, 0x0003) | Out-Null
                [W]::SetForegroundWindow($h) | Out-Null
                Start-Sleep -Milliseconds 400
                $sw = [Diagnostics.Stopwatch]::StartNew(); $n = 0
                while ($sw.Elapsed.TotalSeconds -lt $secs) {
                    $due = $n / $fps
                    $wait = ($due - $sw.Elapsed.TotalSeconds) * 1000
                    if ($wait -gt 0) { Start-Sleep -Milliseconds ([int]$wait) }
                    GrabScreen (Join-Path $dir ('f{0:D4}.jpg' -f $n))
                    $n++
                }
                [W]::SetWindowPos($h, [IntPtr]-2, 0, 0, 0, 0, 0x0003) | Out-Null
                "$($parts[1]) $n frames in $([math]::Round($sw.Elapsed.TotalSeconds,2)) s ($([math]::Round($n / $sw.Elapsed.TotalSeconds,1)) fps)"
            }
            default   { throw "unknown step $s" }
        }
    }
}
finally {
    if (-not $proc.HasExited) { $proc.CloseMainWindow() | Out-Null; if (-not $proc.WaitForExit(8000)) { Stop-Process -Id $proc.Id -Force } }
    Start-Sleep -Milliseconds 500
    if (Test-Path $backup) { Move-Item $backup $settings -Force; 'settings.json restored' }
}
