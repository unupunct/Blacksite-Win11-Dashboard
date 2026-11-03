# Troubleshooting

**Where are the logs?** `%AppData%\BlackSite\logs\blacksite.log` (portable: `data\logs\`). *Information → Open log file* opens it. Errors include a stack trace.

| Problem | Fix |
|---|---|
| Windows SmartScreen blocks Setup | The installer is unsigned. *More info → Run anyway*, or use the portable zip |
| A card shows **UNAVAILABLE** | The app is not installed (or is a Store app that is not registered for your account). Click the card → *Choose program*, or install the app |
| Photos / Snipping Tool / Terminal open nothing | These are Microsoft Store apps; reinstall them from the Store. The log names the target that failed |
| "Custom shortcuts cannot pass command-line arguments" | By design (safety). Create a `.lnk` shortcut that has the arguments you want and point BLACKSITE at the `.lnk` |
| A shortcut vanished after editing `settings.json` | Entries that fail validation are dropped on load and logged. Use the editor instead |
| Network shows **DISCONNECTED** | No adapter with a default gateway is up. Connect to a network; VPN/virtual adapters without a gateway are ignored |
| Network speed looks low or zero | The figure is the current rate in megabits per second, sampled each second |
| No sound | Check *Settings → Sound effects* (switch, volume, per-sound toggles) and that the Windows volume is up |
| Window is off-screen / on a monitor you removed | It re-centres automatically on next start. Or *Settings → Restore default layout* |
| Window covers the taskbar | You are in fullscreen. Press **F11**. Normal maximize respects the taskbar |
| Animations make the PC slow | *Settings → Animation*: lower the intensity, switch animations off, or choose the *Plain* background |
| Settings seem corrupted | The bad file is renamed `settings.json.bad` and defaults are used. Delete both to start fresh |
| "Launch at sign-in" does nothing | Check for `BlackSite.lnk` in `shell:startup`. If the portable folder was moved, toggle the setting off and on |
| App will not start a second time | Only one instance runs; starting another brings the first to the front |

If something still fails, attach the last lines of the log when reporting it.
