# Installation

## Install

From the repository root:

```bash
powershell -ExecutionPolicy Bypass -File tools/build-msix.ps1
```

```bash
powershell -ExecutionPolicy Bypass -File tools/install-msix.ps1
```

The install script shows one administrator prompt. Windows installs packages only from trusted signers, and this package uses a local self-signed certificate, so the script adds that one certificate to the machine's Trusted People store. `tools/uninstall-msix.ps1` removes it again.

The package is self-contained, so it needs no .NET or Windows App SDK runtime.

## Make Glyphbar your only visible taskbar

Glyphbar runs alongside the Windows taskbar and never changes it. To hide the Windows one yourself:

1. Open Glyphbar's **Settings > Taskbar** and select **Open Windows taskbar settings**.
2. Expand **Taskbar behaviours**.
3. Turn on **Automatically hide the taskbar**.

The Windows taskbar then stays out of the way, and comes back if you move the pointer to the screen edge or an app asks for attention. Glyphbar reserves its own space, so maximised windows stop above it.

Only one auto-hide bar can own a screen edge. If you turn on auto-hide for both Glyphbar and the Windows taskbar on the same edge, Windows refuses Glyphbar's registration. Glyphbar still hides and reveals itself, and logs that the edge was taken.

## Launch at sign-in

Turn on **Settings > System > Launch at Windows sign-in**. It is off by default. The installed app uses a Windows startup task, which also appears under **Settings > Apps > Startup**.

## Return to the normal Windows taskbar

1. Right-click the Glyphbar tray icon and choose **Exit Glyphbar**.
2. If you hid the Windows taskbar, turn off **Automatically hide the taskbar** in Windows Settings.

Exiting releases Glyphbar's screen space straight away. If the tray icon isn't available, run `Glyphbar.exe --exit`.

## Uninstall

```bash
powershell -ExecutionPolicy Bypass -File tools/uninstall-msix.ps1
```

This exits Glyphbar, removes the package and its data, and removes the local certificate. To keep your settings and calendar events, add `-KeepData`. You can also uninstall from **Settings > Apps > Installed apps**, which leaves the certificate in place.

Uninstalling leaves the Windows taskbar exactly as you set it. If you turned on auto-hide, it stays on until you change it.

## Running without installing

The Debug build runs directly from `bin`, as shown in the [README](../README.md). It keeps its data in `%LOCALAPPDATA%\Glyphbar`, and launch at sign-in uses the per-user `Run` key instead of a startup task.
