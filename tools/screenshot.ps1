param(
    [string]$Exe = "$PSScriptRoot\..\src\BlackSite.App\bin\Debug\net8.0-windows\BlackSite.exe",
    [string]$Out = "$env:TEMP\blacksite.png",
    [int]$WaitSeconds = 4,
    [switch]$KeepRunning
)
# Launches BLACKSITE, captures its main window to a PNG, optionally leaves it running.
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class W {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@
[void][W]::SetProcessDPIAware()
$exeFull = (Resolve-Path $Exe).Path
$p = Start-Process $exeFull -PassThru
Start-Sleep -Seconds $WaitSeconds
$p.Refresh()
[void][W]::SetForegroundWindow($p.MainWindowHandle)
Start-Sleep -Milliseconds 400
$r = New-Object W+RECT
[void][W]::GetWindowRect($p.MainWindowHandle, [ref]$r)
$w = $r.R - $r.L; $h = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Captured ${w}x${h} -> $Out"
if (-not $KeepRunning) { Stop-Process -Id $p.Id -Force }
