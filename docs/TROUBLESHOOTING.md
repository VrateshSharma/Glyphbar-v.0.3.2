# Troubleshooting

Logs are in `%LOCALAPPDATA%\Glyphbar\logs`, or in **Settings > About > Open logs**. For an installed package, that folder is under `%LOCALAPPDATA%\Packages\Glyphbar_*\LocalCache\Local\Glyphbar`.

## Recovery commands

| Command | Effect |
|---|---|
| `Glyphbar.exe --exit` | Exits cleanly and releases screen space |
| `Glyphbar.exe --show:taskbar` | Turns the bar back on |
| `Glyphbar.exe --show:settings` | Opens Settings |
| `Glyphbar.exe --show:dashboard` | Opens the dashboard |

These reach the running instance, and also work when no window can be clicked.

## The bar has disappeared

1. Check the tray overflow for the Glyphbar icon, then right-click it and choose **Toggle taskbar visibility**.
2. Or run `Glyphbar.exe --show:taskbar`.
3. If auto-hide is on, move the pointer to the very edge of the screen.
4. Choose **Restart interface** from the tray menu.

## Windows leave a gap where the bar was

Glyphbar gives the space back on exit. If it was force-killed, the space stays reserved until Explorer notices the bar has gone. Start Glyphbar and exit it normally, or sign out and back in.

## A shortcut doesn't work

Open **Settings > Search**. Each shortcut shows its state. "Already in use" means another app registered it first, so Glyphbar is on a fallback. Close that app or choose another shortcut. Windows reserves some shortcuts, such as Win+L, and Glyphbar refuses those.

To find what owns Alt+Space, check launchers first: Flow Launcher, PowerToys Run, Raycast and similar.

## The Windows taskbar appears over Glyphbar

With auto-hide on, the Windows taskbar slides up when an app flashes for attention or the pointer reaches the edge, and it covers Glyphbar until it hides again. This is Windows behaviour.

## Desktop widgets are hard to read

Widgets have no background by default, so a bright wallpaper can wash them out. Raise **Settings > Widgets > Desktop widget background**. The same page moves them to the left side or turns them off.

## File search shows no results

The status line says **Windows Search is unavailable** if the provider can't be reached.

- Check that the **Windows Search** service is running.
- Only indexed locations can be found. Add folders under **Settings > Privacy & security > Searching Windows**.
- Check that **Files** is on in **Settings > Search** and that any folder limit there is correct.

Application and Settings results don't depend on the index.

## The music widget shows "Nothing playing"

The widget controls sessions that apps publish to Windows. Some apps and browsers don't publish one, or only do while media is playing. Play/pause and track buttons are disabled when the app doesn't support them.

## Sleep does nothing or shows an error

Glyphbar calls `SetSuspendState`. Some Modern Standby devices don't support it, and policy can block it. Run `powercfg /a` to see which sleep states the device offers. The power menu shows the error Windows returns.

## The battery estimate is missing

Windows only reports an estimate on battery power, and not always then. Glyphbar shows nothing rather than invent one.

## Installation fails

| Error | Fix |
|---|---|
| Certificate not trusted (`0x800B0109`) | Run `tools/install-msix.ps1`, which trusts the local certificate |
| `0x80073CF3` or a version conflict | Run `tools/uninstall-msix.ps1`, then install again |

## Reset everything

**Settings > About > Reset all settings** restores the defaults and keeps calendar events and pinned apps. For a full reset, exit Glyphbar and delete `%LOCALAPPDATA%\Glyphbar`.
