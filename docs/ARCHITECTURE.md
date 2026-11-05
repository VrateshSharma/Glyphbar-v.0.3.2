# Architecture

## Layers

```
Glyphbar.App        Presentation: WinUI 3 views, view models, windows, DI composition root
    │
    ├── Glyphbar.Core       Application: models, interfaces, pure logic. No Windows dependency.
    ├── Glyphbar.Data       Data: SQLite, migrations, JSON settings, file logging
    └── Glyphbar.Platform   System integration and native interop: Win32, COM, WinRT
```

`Core` targets plain `net10.0`, so its logic is tested without Windows. Views and view models depend on interfaces from `Core`. `AppHost` is the only place that names concrete platform types.

### Differences from the PRD structure

- **`Glyphbar.System` is named `Glyphbar.Platform`.** A namespace called `Glyphbar.System` shadows the BCL inside every `Glyphbar.*` namespace, so `System.IO` stops resolving.
- **`Interop/` was added to Platform** for the Win32 and COM declarations that several areas share.
- **`Commands/` was dropped.** CommunityToolkit.Mvvm generates commands from `[RelayCommand]`.
- **The calendar is one widget** rather than a separate `CalendarView`: its large size has the month grid and day list, and medium shows upcoming events.
- **The dot-matrix font is code,** not a font file. `DotMatrixFont` in `Core` holds the glyphs and `DotMatrixText` draws them as vector dots, so nothing needs licensing or installing.

## Runtime structure

`ShellCoordinator` owns every window and reacts to the shell:

| Window | Lifetime |
|---|---|
| `TaskbarWindow` | One per monitor, created and removed as monitors and settings change |
| `MainWindow` (dashboard) | Created on first use. Closing it hides it. |
| `FloatingWindow` (search, launcher, widget panel, power) | Created on first use, then hidden and reused so it reopens instantly. Closes on focus loss or Escape. |
| `SettingsWindow` | Created when opened, destroyed when closed |
| `ShellEventWindow` | One hidden window for hotkeys, tray callbacks and system broadcasts |

`ShellEventWindow` is a real top-level window that is never shown. A message-only window would be simpler, but it receives no broadcasts, and `TaskbarCreated` and `WM_DISPLAYCHANGE` are both broadcasts.

## Threading and idle cost

Nothing blocking runs on the UI thread, and nothing wakes without a reason:

| Source | Mechanism |
|---|---|
| Clock | `ClockService`: one timer, aligned to the minute, or the second while something visible shows seconds |
| Battery | `SystemMonitor`, woken by `PowerManager` events, with a slow poll as a safety net |
| Storage, system info | `SystemMonitor`, polled only while a visible widget holds a lease |
| Media | Event-driven through session events, coalesced over 120 ms |
| Running apps | `SetWinEventHook`, debounced by 120 ms |
| Shell COM calls | `ShellWorker`: one background STA thread |
| SQLite | Pool threads through `GlyphbarDatabase.RunAsync` |

`SystemMonitor` is the only polling loop, a single hosted service rather than a thread per widget. Widgets subscribe in `Activate` and unsubscribe in `Deactivate`, so hidden widgets cost nothing.

## Persistence

All state is per-user under `%LOCALAPPDATA%\Glyphbar`. MSIX redirects this to the package's private folder, which Windows removes on uninstall.

| File | Contents |
|---|---|
| `settings.json` | Preferences. Written to a temp file and atomically swapped in, with the previous version kept as `settings.json.bak`. A corrupt file falls back to the backup. |
| `glyphbar.db` | SQLite with a rollback journal: pinned apps, launch history, widget layout, calendar events |
| `logs\` | Daily log files; the last seven are kept |

`Migrator` applies ordered migrations tracked in `PRAGMA user_version`, each in a transaction, and backs up an existing database first. `SettingsSanitizer` clamps every loaded value, so a hand-edited file can't break the bar.

## Safety boundaries

- **The UI cannot run commands.** `ShellLauncher` accepts only AppsFolder items, existing paths and allow-listed `ms-settings:` pages.
- **Shutdown and restart always confirm.** `PowerMenuController` enforces this, and no setting can turn it off.
- **Search text cannot alter the query.** Terms pass through a character allow-list before reaching the index query.
- **Glyphbar runs `asInvoker`.** "Open as administrator" elevates the launched app through UAC, never Glyphbar.
- **The Windows taskbar is left alone.** Explorer is never touched, and no taskbar registry setting is changed.

## Known limits

- **Bottom and top edges only.** The bar's layout is horizontal; left and right edges would need a vertical layout.
- **Dark only.** The palette in the PRD is dark, so there is no light mode and no toggle that would do nothing.
- **Overlay "acrylic" is a slightly translucent panel.** System acrylic needs a rectangular window, which would lose the rounded corners. The dashboard uses real acrylic.
- **x64 only.** ARM64 has not been built or tested.
