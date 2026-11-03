# Building from source

Prerequisites (Windows 10/11 x64): **.NET 8 SDK** or newer (the project targets `net8.0-windows`; SDK 10 also works) and PowerShell 7 or Windows PowerShell. The installer additionally needs **Inno Setup 6** (free, https://jrsoftware.org/isinfo.php); the build script looks for it in `%LocalAppData%\Programs` and `Program Files`.

```bash
dotnet build BlackSite.sln -c Release
dotnet test tests/BlackSite.Tests
dotnet run --project src/BlackSite.App
```

## Release packages

```powershell
.\scripts\build-release.ps1          # add -SkipTests to skip the unit tests
```

Produces in `dist\`:

* `BlackSite-<version>-portable-win-x64.zip` — self-contained, contains `portable.flag`.
* `BlackSite-<version>-Setup-win-x64.exe` — Inno Setup installer built from `installer\blacksite.iss` (per-user install).

The version comes from `<Version>` in `src/BlackSite.App/BlackSite.App.csproj`.

## Useful tools

| Script | Purpose |
|---|---|
| `tools\make-icon.ps1` | regenerates `Assets\blacksite.ico` |
| `tools\screenshot.ps1` | launches the app and saves a PNG of its window |
| `tools\ui-test.ps1` | UI Automation: launches every shortcut (closing the windows afterwards), checks both power dialogs and **cancels** them, visits every page |
| `tools\ui-test2.ps1` | fullscreen, resize, always-on-top, settings persistence across a restart |

The UI tests need an unlocked interactive desktop and open real windows. When run from the build output folder, create an empty `portable.flag` beside `BlackSite.exe` so test settings stay out of `%AppData%`.

## Code map

* `BlackSite.Core` — no UI references. `Shortcuts/` (definitions, defaults, resolver, validator, launcher, manager), `Power/`, `Monitoring/` (`SystemMonitor` uses `GetSystemTimes` / `GlobalMemoryStatusEx` / `DriveInfo`; `NetworkMonitor` uses interface byte counters), `Settings/`, `Logging/`, `Platform/` (Windows info, startup shortcut).
* `BlackSite.App` — `MainWindow` (chrome, clock, navigation, window modes), `Views/`, `Dialogs/`, `Controls/` (gauge, graph, background), `Services/` (theme, animation switch, audio, icon loading), `Themes/Theme.xaml`.
