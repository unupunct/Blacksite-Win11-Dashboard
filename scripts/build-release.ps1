# Builds, tests and packages BLACKSITE: portable zip + Setup.exe (Inno Setup 6).
param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root
$version = ([xml](Get-Content src/BlackSite.App/BlackSite.App.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$dist = Join-Path $root 'dist'
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $dist | Out-Null

if (-not $SkipTests) {
    Write-Host '== Unit tests' -ForegroundColor Cyan
    dotnet test tests/BlackSite.Tests -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}

Write-Host '== Publish (self-contained, win-x64, ReadyToRun)' -ForegroundColor Cyan
$pub = Join-Path $dist 'publish'
dotnet publish src/BlackSite.App -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=none --nologo -v q -o $pub
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

Write-Host '== Portable zip' -ForegroundColor Cyan
$portDir = Join-Path $dist 'BLACKSITE-portable'
Copy-Item $pub $portDir -Recurse
Set-Content (Join-Path $portDir 'portable.flag') 'Presence of this file keeps settings and logs in the .\data folder next to BlackSite.exe.'
Copy-Item (Join-Path $root 'README.md') $portDir
$portableZip = Join-Path $dist "BlackSite-$version-portable-win-x64.zip"
Compress-Archive -Path $portDir -DestinationPath $portableZip -CompressionLevel Optimal
Remove-Item $portDir -Recurse -Force

Write-Host '== Installer (Inno Setup)' -ForegroundColor Cyan
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isinfo.php' }
& $iscc /Q "/DMyAppVersion=$version" "/DSourceDir=$pub" (Join-Path $root 'installer\blacksite.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }

Write-Host '== Done' -ForegroundColor Green
Get-ChildItem $dist -File | ForEach-Object { '{0,-52} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
