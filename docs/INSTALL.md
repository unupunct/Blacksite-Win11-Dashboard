# Installation

Requirements: Windows 11 (or Windows 10 1809+), 64-bit. About 150 MB of disk space. Nothing else — the .NET runtime is bundled.

## Option A — Installer (recommended)

1. Run `BlackSite-<version>-Setup-win-x64.exe`.
2. It installs **for the current user only** (no UAC prompt) to `%LocalAppData%\Programs\BLACKSITE`, adds a Start menu shortcut (desktop shortcut optional) and an entry in *Settings → Apps → Installed apps*, then starts BLACKSITE.
3. Your settings live in `%AppData%\BlackSite` and survive upgrades. Running a newer Setup upgrades in place.

The installer is not code-signed, so Windows SmartScreen may show "Windows protected your PC". Choose **More info → Run anyway** if you trust the file.

**Uninstall:** *Settings → Apps → Installed apps → BLACKSITE → Uninstall* (your settings in `%AppData%\BlackSite` are kept; delete that folder to remove them).
Silent install: `Setup.exe /VERYSILENT /SUPPRESSMSGBOXES`.

## Option B — Portable

1. Unzip `BlackSite-<version>-portable-win-x64.zip` to any folder (a USB stick works).
2. Run `BlackSite.exe`.
3. Because the zip contains a `portable.flag` file, settings and logs are written to the `data` folder next to the program and nothing is written to `%AppData%`. Delete `portable.flag` to use `%AppData%` instead.

To remove it, delete the folder (and turn off *Launch at sign-in* first if you enabled it, so the Startup shortcut is removed).
