# Glyphbar

A monochrome taskbar, launcher and widget environment for Windows 11, inspired by Nothing OS's visual language. Native C# / WinUI 3. All system data comes from real Windows APIs, and everything works offline.

- **Taskbar:** a real shell appbar with a floating or docked style, pinned and running apps, system status and a clock
- **Widgets:** clock, date, calendar with local events, battery, storage, music control, system info, kept on the desktop on translucent cards
- **Search:** apps, Windows Settings pages, and files and folders from the Windows Search index
- **Launcher:** every installed app, favourites and recents
- **Power menu:** lock, sleep, sign out, restart and shut down, with confirmations

Glyphbar is an independent project. It uses no Nothing assets: its dot-matrix type is an original 5×7 alphabet drawn in code.

## Requirements

- Windows 11 (build 22000 or later), x64
- .NET 10 SDK to build. Visual Studio and the Windows SDK are optional; the build tools come from NuGet.

## Build and run

```bash
dotnet build Glyphbar.sln -p:Platform=x64
```

```bash
src/Glyphbar.App/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/Glyphbar.exe
```

Run the tests:

```bash
dotnet test Glyphbar.sln -p:Platform=x64
```

Build the installer:

```bash
powershell -ExecutionPolicy Bypass -File tools/build-msix.ps1
```

## Getting out of trouble

Glyphbar never changes the Windows taskbar, Explorer or the registry settings behind them. To leave at any time, right-click the tray icon and choose **Exit Glyphbar**, or run:

```bash
Glyphbar.exe --exit
```

If the bar is hidden, `Glyphbar.exe --show:taskbar` brings it back. See [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

## Default shortcuts

| Action | Shortcut | Used if the default is taken |
|---|---|---|
| Search | Alt+Space | Win+Alt+Space, then Ctrl+Alt+G |
| Dashboard | Win+Shift+G | Win+Alt+G |
| Widget panel | Win+Shift+W | Win+Alt+W, then Ctrl+Alt+W |
| Power menu | Win+Shift+P | Win+Alt+P |

Launchers such as Flow Launcher and PowerToys Run often own Alt+Space. Glyphbar then uses the fallback and tells you. Choose your own in Settings > Search.

## Documentation

- [Installation and uninstallation](docs/INSTALLATION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Windows API integration](docs/API_INTEGRATION.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Test results](docs/TEST_RESULTS.md)
