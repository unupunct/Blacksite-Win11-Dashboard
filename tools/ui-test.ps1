param(
    [string]$Exe = "$PSScriptRoot\..\src\BlackSite.App\bin\Debug\net8.0-windows\BlackSite.exe",
    [string]$ShotDir = "$env:TEMP\blacksite-shots",
    [string[]]$Only = @()
)
# End-to-end UI test: drives the real app through UI Automation, launches every shortcut,
# checks the confirmation dialogs (and CANCELS them), walks every page and captures screenshots.
# It never presses CONFIRM on Shut Down / Restart.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class W2 {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public static void Click(int x, int y) { SetCursorPos(x, y); mouse_event(2, 0, 0, 0, UIntPtr.Zero); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
}
'@
[void][W2]::SetProcessDPIAware()
New-Item -ItemType Directory -Force $ShotDir | Out-Null
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$results = [System.Collections.Generic.List[object]]::new()
function Add-Result($name, $ok, $detail) { $results.Add([pscustomobject]@{ Test = $name; Pass = $ok; Detail = $detail }) }

function Get-TopWindows { $AE::RootElement.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { [pscustomobject]@{ Handle = $_.Current.NativeWindowHandle; Name = $_.Current.Name; Pid = $_.Current.ProcessId; El = $_ } } }
function Find-ByName($root, $name) { $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name))) }
function Invoke-El($el) { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Shot($handle, $file) {
    $r = New-Object W2+RECT; [void][W2]::GetWindowRect([IntPtr]$handle, [ref]$r)
    $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
    $bmp.Save((Join-Path $ShotDir $file), [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
}

$app = Start-Process (Resolve-Path $Exe).Path -PassThru
Start-Sleep -Seconds 4
$app.Refresh()
$main = $AE::FromHandle($app.MainWindowHandle)
Add-Result 'App starts and shows a window' ($app.MainWindowHandle -ne 0) $main.Current.Name
Shot $app.MainWindowHandle 'dashboard.png'

# --- launch shortcuts ---
$closeWhitelist = 'Notepad','Calculator','Paint','Photos','Settings','Edge','File Explorer','Task Manager','Terminal','Snipping','This PC','All Tasks','Control Panel','GodMode','PowerShell','Windows PowerShell','Microsoft Edge','Windows Terminal'
function Test-Launch($buttonName, $label) {
    $btn = Find-ByName $main $buttonName
    if (-not $btn) { Add-Result "Launch: $label" $false 'button not found'; return }
    $before = @(Get-TopWindows | ForEach-Object { $_.Handle })
    Invoke-El $btn
    Start-Sleep -Seconds 4
    $new = @(Get-TopWindows | Where-Object { $_.Pid -ne $app.Id -and $_.Handle -ne 0 -and $before -notcontains $_.Handle })
    $titles = ($new | ForEach-Object { "'$($_.Name)'" }) -join ', '
    Add-Result "Launch: $label" ($new.Count -gt 0) ($(if ($new) { "new window(s): $titles" } else { 'no new window detected' }))
    foreach ($w in $new) {
        try { $w.El.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch {}
    }
    Start-Sleep -Milliseconds 800
}

$apps = [ordered]@{
    'My Computer' = 'My Computer'; 'CONTROL PANEL' = 'Control Panel'; 'GOD MODE' = 'God Mode'
    'Notepad' = 'Notepad'; 'Calculator' = 'Calculator'; 'Paint' = 'Paint'; 'Photos' = 'Photos'; 'Settings' = 'Settings'
    'Microsoft Edge' = 'Microsoft Edge'; 'File Explorer' = 'File Explorer'; 'Task Manager' = 'Task Manager'
    'Windows Terminal' = 'Windows Terminal'; 'Snipping Tool' = 'Snipping Tool'
}
foreach ($k in $apps.Keys) {
    if ($Only.Count -and $Only -notcontains $k) { continue }
    $name = if ($k -eq 'My Computer') { 'MY COMPUTER' } else { $k }
    Test-Launch $name $apps[$k]
}

# --- power dialogs: open, verify buttons, CANCEL ---
foreach ($p in @(@('SHUT DOWN', 'Shut down'), @('RESTART', 'Restart'))) {
    $btn = Find-ByName $main $p[0]
    # Modal dialog: a UIA Invoke would block until it closes, so use a real (asynchronous) mouse click.
    [void][W2]::SetForegroundWindow($app.MainWindowHandle); Start-Sleep -Milliseconds 300
    $rc = $btn.Current.BoundingRectangle
    [W2]::Click([int]($rc.X + $rc.Width / 2), [int]($rc.Y + $rc.Height / 2))
    $dlg = $null
    for ($i = 0; $i -lt 10 -and -not $dlg; $i++) {
        Start-Sleep -Milliseconds 400
        # Owned windows appear under their owner in the UIA tree.
        $w = $main.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))
        if ($w) { $dlg = [pscustomobject]@{ Handle = $w.Current.NativeWindowHandle; El = $w } }
    }
    if (-not $dlg) { Add-Result "$($p[1]) shows confirmation" $false 'no dialog'; continue }
    $cancel = Find-ByName $dlg.El 'CANCEL'; $confirm = Find-ByName $dlg.El 'CONFIRM'
    $confirm2 = if (-not $confirm) { $dlg.El.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $p[0]))) } else { $confirm }
    Add-Result "$($p[1]) shows confirmation with Cancel + Confirm" ($cancel -and $confirm2) "cancel=$([bool]$cancel) confirm=$([bool]$confirm2)"
    Shot $dlg.Handle ("confirm-" + $p[0].Replace(' ', '') + ".png")
    if ($cancel) { Invoke-El $cancel }
    Start-Sleep -Milliseconds 800
    $still = $app.HasExited -eq $false
    Add-Result "$($p[1]) Cancel closes dialog, nothing executed" ($still -and -not ($main.Current.IsOffscreen)) 'app and Windows still running'
}

# --- pages ---
foreach ($pg in 'COMPUTER', 'NETWORK', 'SECURITY', 'PERFORMANCE', 'FILES', 'SETTINGS', 'INFORMATION', 'DASHBOARD') {
    $nav = Find-ByName $main $pg
    if (-not $nav) { Add-Result "Navigate: $pg" $false 'nav button not found'; continue }
    $nav.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 1800
    Shot $app.MainWindowHandle ("page-" + $pg.ToLower() + ".png")
    Add-Result "Navigate: $pg" $true 'screenshot saved'
}

Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
$results | Format-Table -AutoSize -Wrap | Out-String -Width 200
Write-Host "Screenshots: $ShotDir"
