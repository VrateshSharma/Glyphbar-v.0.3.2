# Windows API integration

Each system feature sits behind an interface in `Glyphbar.Core.Interfaces`, with its implementation in `Glyphbar.Platform`.

## Summary

| Feature | API | Implementation |
|---|---|---|
| Taskbar position and work area | `SHAppBarMessage` | `AppBar/AppBarController.cs` |
| Window chrome | `SetWindowSubclass`, `WM_NCCALCSIZE`, `DwmExtendFrameIntoClientArea` | `Windowing/WindowStyler.cs` |
| Shell events | Hidden window, `RegisterWindowMessage("TaskbarCreated")` | `Windowing/ShellEventWindow.cs` |
| Monitors and DPI | `EnumDisplayMonitors`, `GetMonitorInfo`, `GetDpiForMonitor` | `Windowing/MonitorInfo.cs` |
| Installed apps | `SHGetKnownFolderItem(FOLDERID_AppsFolder)`, `IEnumShellItems` | `Applications/ShellAppCatalog.cs` |
| App icons | `IShellItemImageFactory` | `Applications/ShellIconProvider.cs` |
| Launching | `ShellExecuteEx`, `SHOpenFolderAndSelectItems` | `Applications/ShellLauncher.cs` |
| Running apps | `SetWinEventHook`, `EnumWindows`, `SHGetPropertyStoreForWindow` | `Applications/RunningWindowTracker.cs` |
| Battery | `GetSystemPowerStatus`, `Windows.System.Power.PowerManager` | `Battery/BatteryService.cs` |
| Storage | `System.IO.DriveInfo`, `WM_DEVICECHANGE` | `Storage/StorageService.cs` |
| Media | `GlobalSystemMediaTransportControlsSessionManager` | `Media/MediaService.cs` |
| File search | Windows Search through OLE DB (`Search.CollatorDSO`) | `Search/WindowsSearchProvider.cs` |
| Power | `ExitWindowsEx`, `SetSuspendState`, `LockWorkStation` | `Power/PowerExecutor.cs` |
| Hotkeys | `RegisterHotKey` | `Hotkeys/HotkeyService.cs` |
| Tray | `Shell_NotifyIcon`, `TrackPopupMenuEx` | `SystemTray/TrayIcon.cs` |
| Launch at sign-in | `Windows.ApplicationModel.StartupTask`, per-user `Run` key when unpackaged | `Startup/StartupManager.cs` |
| System info | `GetSystemTimes`, `GlobalMemoryStatusEx` | `SystemInfo/SystemInfoService.cs` |
| Single instance | `AppInstance.FindOrRegisterForKey`, `RedirectActivationToAsync` | `Glyphbar.App/Program.cs` |

## AppBar lifecycle

1. `ABM_NEW` registers the window with a callback message.
2. `ABM_QUERYPOS` proposes a rectangle, and the shell moves it clear of the taskbar and other appbars.
3. `TaskbarGeometry.FitStrip` trims the result back to the bar's thickness.
4. `ABM_SETPOS` reserves it, then the window is moved by hand, because the shell doesn't move it.
5. `ABM_ACTIVATE` and `ABM_WINDOWPOSCHANGED` are forwarded so the shell keeps the z-order right.
6. `ABM_REMOVE` releases the space on exit, on sign-out, and when a monitor goes away.

| Event | Response |
|---|---|
| `ABN_POSCHANGED` | Re-negotiates, deferred until the shell's own call returns |
| `ABN_FULLSCREENAPP` | Leaves the topmost band while a full-screen app runs |
| `TaskbarCreated` | Registers again from scratch, because Explorer restarted and forgot every appbar |
| `WM_DISPLAYCHANGE`, `SPI_SETWORKAREA` | Re-syncs bars with monitors, debounced by 350 ms |
| `WM_DPICHANGED` | Re-negotiates at the new scale |
| `PBT_APMRESUMEAUTOMATIC` | Re-syncs after sleep |
| `WM_ENDSESSION` | Releases the space synchronously |

**Auto-hide:** `ABM_SETAUTOHIDEBAREX` allows one bar per edge, so it is refused when the Windows taskbar already auto-hides there. The slide behaviour doesn't depend on it. The bar reserves nothing and parks with a 2px sliver on screen. A 200 ms cursor check, which runs only while auto-hide is on, reveals and hides it. Pointer events would not work here, because the Windows taskbar takes the pointer first if it also auto-hides on that edge.

## Transparent windows

Three things combine to give the bar anti-aliased rounded corners and an opacity that leaves icons solid:

1. A `SystemBackdrop` subclass sets a fully transparent system-compositor brush.
2. `DwmExtendFrameIntoClientArea` with `-1` margins extends the frame across the window.
3. A subclass answers `WM_NCCALCSIZE` so there is no non-client area.

The third is needed because WinUI's presenter re-applies `WS_DLGFRAME` after style changes, so clearing style bits does not last. Windows App SDK 2.x also renders XAML in a child `DesktopChildSiteBridge` window, not in the top-level window.

## Applications

`shell:AppsFolder` is the virtual folder Start uses. It already merges Start Menu shortcuts with registered packaged apps, so nothing scans the disk. Items are stored by parsing name (the AppUserModelID or a path), never by a hardcoded path, and launched as `shell:AppsFolder\<id>`.

Running windows match an app by AppUserModelID, then by executable path. The "switchable window" rule mirrors Alt-Tab: visible, not cloaked, no owner, and not a tool window unless it has `WS_EX_APPWINDOW`. Packaged apps need the cloaking check, because suspended ones keep hidden windows.

## Battery values

`BatteryInterpreter` maps `SYSTEM_POWER_STATUS` directly and invents nothing:

- `BatteryFlag` 128 means no battery. 255 means unknown, which also has that bit set, so it is checked first.
- `BatteryLifePercent` 255 becomes "no percentage".
- `BatteryLifeTime` `0xFFFFFFFF`, or any value while on AC, becomes "no estimate".

## File search

```sql
SELECT TOP 12 System.ItemPathDisplay, System.ItemNameDisplay, System.ItemType
FROM SystemIndex
WHERE CONTAINS(System.FileName, '"budget*"') AND SCOPE='file:'
ORDER BY System.Search.Rank DESC, System.DateModified DESC
```

The provider doesn't support parameters inside `CONTAINS`, so `WindowsSearchProvider.Clean` keeps only letters, digits, `.`, `-` and `_`. Nothing typed can end the literal. Queries are debounced by 180 ms and cancelled by the next keystroke. Applications and Settings are ranked from memory and appear on the same keystroke.

## Power

`SeShutdownPrivilege` is held by every interactive user but disabled by default, so `PowerExecutor` enables it on its own token and needs no elevation.

Set `GLYPHBAR_POWER_DRY_RUN=1` to replace the executor with one that only logs. This exercises the whole power flow without acting on the machine.

## Environment variables

| Variable | Effect |
|---|---|
| `GLYPHBAR_DATA_DIR` | Stores data somewhere else |
| `GLYPHBAR_POWER_DRY_RUN=1` | Logs power actions instead of running them |
| `GLYPHBAR_KEEP_OVERLAYS=1` | Keeps overlays open when they lose focus; for UI automation |

## Windows version requirements

- **Minimum:** Windows 11 (10.0.22000).
- **`DWMWA_BORDER_COLOR` and `DWMWA_WINDOW_CORNER_PREFERENCE`:** Windows 11.
- **`ABM_SETAUTOHIDEBAREX`:** Windows 8 or later.
- **`GlobalSystemMediaTransportControlsSessionManager`:** Windows 10 1809 or later.
