param(
    [string]$Exe = "$PSScriptRoot\..\src\BlackSite.App\bin\Debug\net8.0-windows\BlackSite.exe",
    [string]$ShotDir = "$env:TEMP\blacksite-shots"
)
# Window behaviour + persistence test: fullscreen, resize, always-on-top, settings survive a restart.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class W6 {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
}
'@
[void][W6]::SetProcessDPIAware()
New-Item -ItemType Directory -Force $ShotDir | Out-Null
$AE = [System.Windows.Automation.AutomationElement]; $TS = [System.Windows.Automation.TreeScope]
$results = [System.Collections.Generic.List[object]]::new()
function Rec($n, $ok, $d) { $results.Add([pscustomobject]@{ Test = $n; Pass = $ok; Detail = $d }) }
function Rect($h) { $r = New-Object W6+RECT; [void][W6]::GetWindowRect([IntPtr]$h, [ref]$r); $r }
function Shot($h, $f) { $r = Rect $h; $b = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T); $g = [System.Drawing.Graphics]::FromImage($b); $g.CopyFromScreen($r.L, $r.T, 0, 0, $b.Size); $b.Save((Join-Path $ShotDir $f)); $g.Dispose(); $b.Dispose() }
function Find($root, $name) { $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name))) }
function Start-App { $a = Start-Process (Resolve-Path $Exe).Path -PassThru; Start-Sleep 4; $a.Refresh(); $a }
function Stop-AppGracefully($a) {
    $el = $AE::FromHandle($a.MainWindowHandle)
    $el.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    [void]$a.WaitForExit(8000)
}

$data = Join-Path (Split-Path (Resolve-Path $Exe).Path) 'data'
Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
$screen = [System.Windows.Forms.Screen]::FromPoint([System.Windows.Forms.Cursor]::Position).Bounds

# ---- run 1: fullscreen + resize + change settings
$app = Start-App
$h = $app.MainWindowHandle
$r0 = Rect $h; Rec 'Default window size' (($r0.R - $r0.L) -gt 900) "$($r0.R - $r0.L)x$($r0.B - $r0.T)"
$AE::FromHandle($h).SetFocus(); [void][W6]::SetForegroundWindow($h); Start-Sleep -Milliseconds 600
[System.Windows.Forms.SendKeys]::SendWait('{F11}'); Start-Sleep -Seconds 2
$r1 = Rect $h
$cover = ($r1.R - $r1.L) -ge $screen.Width -and ($r1.B - $r1.T) -ge $screen.Height
Rec 'F11 enters fullscreen (covers whole monitor)' $cover "window $($r1.R - $r1.L)x$($r1.B - $r1.T) vs screen $($screen.Width)x$($screen.Height)"
Shot $h 'fullscreen.png'
[System.Windows.Forms.SendKeys]::SendWait('{F11}'); Start-Sleep -Seconds 2
$r2 = Rect $h; Rec 'F11 again leaves fullscreen' (($r2.R - $r2.L) -lt $screen.Width) "$($r2.R - $r2.L)x$($r2.B - $r2.T)"

# resize to a small window (1366x768 class) and capture
[void][W6]::SetWindowPos($h, [IntPtr]::Zero, 100, 60, 1000, 640, 0x14); Start-Sleep -Seconds 2
$r3 = Rect $h; Rec 'Window resizes' (($r3.R - $r3.L) -le 1210) "$($r3.R - $r3.L)x$($r3.B - $r3.T)"
Shot $h 'small-window.png'
[void][W6]::SetWindowPos($h, [IntPtr]::Zero, 140, 80, 1500, 900, 0x14); Start-Sleep -Seconds 1

# change settings through the real UI
$main = $AE::FromHandle($h)
$sn = Find $main 'SETTINGS'; Rec 'Settings nav found' ($null -ne $sn) ''
$sn.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Seconds 2
$top = Find $main 'Always on top'
$top.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 500
$topmost = ([W6]::GetWindowLong($h, -20) -band 0x8) -ne 0
Rec 'Always-on-top applies live' $topmost "WS_EX_TOPMOST=$topmost"
(Find $main 'Magenta') ; $mag = Find $main 'Accent Magenta'
if ($mag) { $mag.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() ; Start-Sleep -Milliseconds 500 }
(Find $main 'PARTICLES').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 800
Shot $h 'settings-changed.png'
Stop-AppGracefully $app
Rec 'App exits cleanly on close' $app.HasExited ''

$file = Join-Path $data 'settings.json'
$json = if (Test-Path $file) { Get-Content $file -Raw | ConvertFrom-Json } else { $null }
Rec 'settings.json written' ($null -ne $json) $file
if ($json) {
    Rec 'Accent persisted' ($json.AccentColor -eq '#FF4DD2') $json.AccentColor
    Rec 'Background style persisted' ($json.BackgroundStyle -eq 'Particles') $json.BackgroundStyle
    Rec 'Always-on-top persisted' ($json.AlwaysOnTop -eq $true) $json.AlwaysOnTop
    Rec 'Window size persisted' ($json.WindowWidth -gt 900) "$($json.WindowWidth)x$($json.WindowHeight) at $($json.WindowLeft),$($json.WindowTop)"
}

# ---- run 2: everything restored
$app2 = Start-App
$h2 = $app2.MainWindowHandle
$t2 = ([W6]::GetWindowLong($h2, -20) -band 0x8) -ne 0
Rec 'After restart: always-on-top restored' $t2 ''
$a = Rect $h2; $b = Rect $h
Rec 'After restart: window placed where it was left' ($a.L -gt 0 -and [math]::Abs(($a.R - $a.L) - 1500) -lt 6) "$($a.R - $a.L)x$($a.B - $a.T) at $($a.L),$($a.T)"
Shot $h2 'restarted.png'
Stop-AppGracefully $app2
$results | Format-Table -AutoSize -Wrap | Out-String -Width 200
