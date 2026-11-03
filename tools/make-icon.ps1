# Generates src/BlackSite.App/Assets/blacksite.ico (hexagon logo, PNG-compressed frames).
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\BlackSite.App\Assets'
New-Item -ItemType Directory -Force $out | Out-Null
$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::FromArgb(255, 4, 8, 16))
    $c = $s / 2.0; $r = $s * 0.44
    $pts = foreach ($i in 0..5) {
        $a = [Math]::PI / 180 * (60 * $i - 90)
        New-Object System.Drawing.PointF ($c + $r * [Math]::Cos($a)), ($c + $r * [Math]::Sin($a))
    }
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 0, 229, 255)), ([Math]::Max(1.2, $s / 16))
    $g.DrawPolygon($pen, [System.Drawing.PointF[]]$pts)
    $r2 = $r * 0.5
    $pts2 = foreach ($i in 0..5) {
        $a = [Math]::PI / 180 * (60 * $i - 90)
        New-Object System.Drawing.PointF ($c + $r2 * [Math]::Cos($a)), ($c + $r2 * [Math]::Sin($a))
    }
    $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 229, 255))
    $g.FillPolygon($br, [System.Drawing.PointF[]]$pts2)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Data = $ms.ToArray() }
}
$fs = [System.IO.File]::Create((Join-Path $out 'blacksite.ico'))
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$f.Data.Length); $w.Write([uint32]$offset)
    $offset += $f.Data.Length
}
foreach ($f in $frames) { $w.Write($f.Data) }
$w.Close(); $fs.Close()
Write-Host "Wrote icon with $($frames.Count) sizes"
