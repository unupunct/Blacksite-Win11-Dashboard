param(
    [string]$Package = (Get-ChildItem "$PSScriptRoot\..\dist\*portable*.zip" | Select-Object -First 1).FullName,
    [string]$Work = 'C:\blacksite-demo',   # short path so no user name shows in the Settings page
    [string]$Out = "$PSScriptRoot\..\docs\images"
)
# Captures README screenshots from the portable build using PrintWindow (works even if other windows overlap).
# Only the BLACKSITE window is captured; the header deliberately shows no computer or user name.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class PW {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
}
'@
[void][PW]::SetProcessDPIAware()
$AE = [System.Windows.Automation.AutomationElement]; $TS = [System.Windows.Automation.TreeScope]
if (Test-Path $Work) { [IO.Directory]::Delete($Work, $true) }
New-Item -ItemType Directory -Force $Work, $Out | Out-Null
Expand-Archive $Package $Work
$exe = (Get-ChildItem $Work -Recurse -Filter BlackSite.exe | Select-Object -First 1).FullName
$out = (Resolve-Path $Out).Path

function Snap($hwnd, $name) {
    $r = New-Object PW+RECT; [void][PW]::GetWindowRect([IntPtr]$hwnd, [ref]$r)
    $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
    [void][PW]::PrintWindow([IntPtr]$hwnd, $dc, 2)   # PW_RENDERFULLCONTENT
    $g.ReleaseHdc($dc); $g.Dispose()
    $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Host "saved $name"
}
function Find($root, $n) { $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $n))) }
function Go($main, $n) { (Find $main $n).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Seconds 3 }

# 1) default cyan theme
$app = Start-Process $exe -PassThru; Start-Sleep -Seconds 14   # let the graphs fill a little
$app.Refresh(); $h = $app.MainWindowHandle; $main = $AE::FromHandle($h)
Snap $h 'dashboard.png'
Go $main 'SETTINGS';    Snap $h 'settings.png'
Go $main 'INFORMATION'; $null = $null
Go $main 'DASHBOARD'

# 2) confirmation dialog (modal, so invoke from another process, then cancel it)
$helper = "Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes; `$r=[System.Windows.Automation.AutomationElement]::FromHandle($h); `$b=`$r.FindFirst('Descendants',(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'SHUT DOWN'))); `$b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()"
$j = Start-Process pwsh -ArgumentList '-NoProfile', '-Command', $helper -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
$dlg = $main.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))
if ($dlg) {
    Snap $dlg.Current.NativeWindowHandle 'confirm-shutdown.png'
    (Find $dlg 'CANCEL').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()   # never confirm
}
Stop-Process -Id $j.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $app.Id -Force

# 3) alternative accent / background
$settings = Join-Path (Split-Path $exe) 'data\settings.json'
New-Item -ItemType Directory -Force (Split-Path $settings) | Out-Null
'{ "AccentColor": "#FF4DD2", "BackgroundStyle": "Particles" }' | Set-Content $settings   # other values default
$app = Start-Process $exe -PassThru; Start-Sleep -Seconds 12
$app.Refresh(); Snap $app.MainWindowHandle 'dashboard-magenta.png'
Stop-Process -Id $app.Id -Force
Start-Sleep -Seconds 2; try { [IO.Directory]::Delete($Work, $true) } catch { Write-Warning "Could not remove $Work : $_" }
