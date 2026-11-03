# User manual

## The dashboard

* **Header** — Windows edition and build, live clock and date, the *SYSTEM ONLINE* light and activity bars. With the borderless window (default) the header is also the title bar: drag it to move the window, double-click to maximize, and use the buttons on the right to fullscreen, minimize, maximize and close.
* **System shortcuts (left)**
  * *My Computer* opens This PC. *Control Panel* opens the classic Control Panel. *God Mode* opens the all-tasks settings folder (nothing is created or changed).
  * *Shut Down* and *Restart* open a confirmation. **Cancel is the default button** and Esc cancels; only the red button executes. There is no way to trigger them with a stray click or the Enter key.
* **Applications (centre)** — click a card to launch the real program. A green dot means it was found; a red **UNAVAILABLE** badge means it was not. Clicking an unavailable card offers to let you pick its program manually. A thin bar animates on a card for a moment after it launches. Hover for a tooltip.
* **Live monitoring (bottom)**
  * *CPU* and *Memory*: gauge + scrolling 90-second graph. Memory shows used / available / total.
  * *Disk (C:)*: used, free and capacity. *Network*: download and upload in Mbps with a graph, summed over adapters that have a gateway (i.e. the ones really connected). If no adapter is connected it says **DISCONNECTED**.
  * Gauges turn orange at 75 % and red at 90 %.
* **Navigation bar** — Dashboard, Performance, Settings and Information are full pages. Computer, Network, Security and Files are labelled *future modules*.

Keyboard: **F11** fullscreen · **Ctrl+1…8** switch pages · **Tab / Enter / Space** operate buttons · **Esc** closes dialogs.

## Performance page

Large graphs for CPU, memory and network, processor name, core count, uptime, process count and the eight biggest processes by memory (refreshed every 3 s only while the page is open).

## Shortcut editor

Open it from the **EDIT SHORTCUTS** button on the dashboard or from Settings.

* **Add** — name, description, target and optional icon. The target can be a program (`Browse…`), or a link such as `ms-settings:network`, `https://example.com` or `shell:AppsFolder\<AppUserModelID>`.
* **Edit / Replace** (also double-click) — change any field, including pointing a built-in card at a different program.
* **Remove**, **Restore defaults**, and **drag items to reorder**.
* Icons can be `.png .jpg .bmp .ico .gif` images or a program (its own icon is used). Without an icon, custom programs show their own icon automatically.
* Up to 20 shortcuts. Targets are validated before saving: the file must exist and be an `.exe`, `.lnk`, `.msc` or `.cpl`; links must use an allowed scheme; **arguments are not supported** for custom shortcuts (a safety rule).

## Settings

| Section | Options |
|---|---|
| Appearance | accent colour (8 presets), background style (Circuit, Grid, Particles, Plain), panel transparency |
| Animation | master switch and intensity (scan line, particles, gauge sweeps, status lights, page transitions). Hover / press feedback always stays on |
| Sound effects | master switch, volume (default 25 %), and individual switches for click, launch, navigation, system online and warning sounds. Sounds are short synthesised blips |
| Window | always on top, fullscreen, borderless (custom title bar) or normal Windows frame |
| Startup | launch when you sign in to Windows (a shortcut in your Startup folder) and an option to start minimized when launched that way |
| Shortcuts | open the editor / restore the 10 default shortcuts |
| Maintenance | **Restore default layout** (window size/position, fullscreen, shortcuts) and **Reset all settings** (everything, including the startup shortcut) |

Window size and position are remembered between runs; if a monitor is disconnected the window re-centres on the current screen.

## Where things are stored

| | Installed | Portable |
|---|---|---|
| Settings | `%AppData%\BlackSite\settings.json` | `data\settings.json` beside the exe |
| Log | `%AppData%\BlackSite\logs\blacksite.log` | `data\logs\blacksite.log` |

The log rotates at 512 KB. *Information → Open log file* opens it.
