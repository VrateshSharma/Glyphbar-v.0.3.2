# Test results

Final run on 6 November 2026 against package version 1.0.5.0, on the machine Glyphbar was built on: Windows 11 build 26300, x64, one 1920×1080 monitor at 100% scale, with a battery. The Windows taskbar was set to auto-hide, and Flow Launcher was running.

## Summary

| Suite | Result |
|---|---|
| Core unit tests | 100 of 100 passed |
| Data tests (real SQLite and files) | 26 of 26 passed |
| Platform integration tests (real Windows APIs) | 30 of 30 passed |
| End-to-end checks on the real desktop, including desktop widgets | 111 of 111 passed |
| Installed MSIX checks | 17 of 17 passed |
| Uninstall and reinstall checks | 19 of 19 passed, run on 1.0.3.0 and 1.0.4.0 (below) |
| Crashes during the final run | 0 |

The uninstall run covered removing 1.0.3.0 and installing 1.0.4.0. It was not repeated for 1.0.5.0, which adds desktop widgets.

Reproduce these with:

```bash
dotnet test Glyphbar.sln -p:Platform=x64
```

```bash
powershell -ExecutionPolicy Bypass -File tests/e2e/run-all.ps1
```

```bash
powershell -ExecutionPolicy Bypass -File tests/e2e/7-installed-package.ps1
```

The end-to-end suites use UI Automation, plus a few seconds of real pointer input. They expect a 1920×1080 primary monitor at 100% scale. Suite 7 needs the MSIX installed, runs with real power actions, and never opens the power menu.

## Performance

| Measure | Result |
|---|---|
| CPU over 30 seconds, bar only | Under 0.06% of the machine across four runs |
| CPU over 30 seconds, final build, desktop widgets on | 0.061% |
| Private memory | About 120 MB with the bar and desktop widgets |

## Acceptance criteria

**Verified** means the behaviour was exercised on the real desktop. **Partly** and **Not verified** are explained in the notes.

| Criterion | Status | Evidence |
|---|---|---|
| Taskbar appears in the correct position | Verified | Window rectangles checked for bottom, top, floating, docked, centred and part-width |
| Registers and unregisters with Windows | Verified | Work area went from 1080 to 1012 and back on every exit, including from the package |
| Pinned applications launch | Verified | Calculator launched through search; pin, unpin and reorder persist |
| Running applications identified | Verified | Chrome and Claude appeared with indicators; UWP and Win32 matching is unit tested |
| Clock shows system time | Verified | Weekday and time compared with the system |
| Date shows system date | Verified | |
| Calendar navigation | Verified | Month, year and Today |
| Events created, edited, deleted | Verified | Confirmed in SQLite through the real dialog, including rejecting a blank title |
| Battery percentage from Windows | Verified | Live `GetSystemPowerStatus` values |
| Charging state accurate | Verified | Showed both "On battery" and "Charging" as the machine changed state |
| Storage from real disks | Verified | Matched `DriveInfo` |
| Multiple drives | Partly | The code handles many drives; this machine exposed only what the tests covered |
| Music metadata from real sessions | Verified | Read titles from Chrome and Media Player |
| Play/pause and track navigation | Partly | Play/pause verified on a silent session owned by the test. Next and previous were not pressed |
| Application search | Verified | |
| File search | Verified | Real results from the Windows Search index |
| Power actions with confirmations | Partly | Full flow verified with the dry-run executor, including Cancel. Real shutdown, restart, sleep, lock and sign-out were **not** run |
| Global shortcuts | Partly | A real press opened search with keyboard focus, on the fallback shortcut. Alt+Space belongs to Flow Launcher here |
| Settings persist after restart | Verified | |
| Widgets enabled, disabled, rearranged | Verified | Through Settings, confirmed in SQLite. Dragging a widget card was not tested |
| Launches at sign-in | Partly | The packaged startup task turns on and off correctly (state 0, 2, 0), and the unpackaged `Run` value works. An actual sign-in was not tested |
| Survives Explorer restarts | Partly | The `TaskbarCreated` message was sent to the app, and it recovered. Explorer itself was not restarted |
| Multiple monitors | **Not verified** | Only one monitor was available. Geometry on negative coordinates is unit tested |
| Can be safely exited | Verified | Exit code 0, with the work area restored; `Glyphbar.exe --exit` works from the installed package |
| Windows taskbar can be restored | Verified | Glyphbar never changes it; exit releases all space |
| Uninstalls cleanly | Verified | Package, alias, Start entry and all app data removed (0 files left). The work area and Windows taskbar were unaffected |

## Not yet tested

- **Desktop widgets beyond one monitor at 100% scale,** and dragging them to a custom position, which isn't supported yet.
- **Real power actions.**
- **A real sign-in with the startup task on.**
- **A real Explorer restart, sleep and resume, display or scaling changes, and DPI above 100%.**
- **Multiple monitors.**
- **`tools/uninstall-msix.ps1` itself.** Its package removal step was tested by hand. Its certificate removal step was not run, so that the app could stay installed.
- **The tray's right-click menu.** The same actions were verified through Settings and command-line arguments.
- **"Open as administrator", "Add application" and "Change icon".** These need UAC or a file picker.
- **Next and previous track.**
- **Hover animations and the Windows reduced-motion setting,** which were not checked visually.

## Open question: the first activation crash

The installed 1.0.0.0 build crashed the first time it received arguments through its Start menu entry. The same action on the fixed build works and logs no exception.

The fix moved reading the activation arguments onto the thread that receives them, so that is the likely cause, but the original exception was never captured. If a similar failure happens now, it is logged rather than fatal.

## Bugs found by this testing

All of these are fixed, and each fix was retested.

| Bug | Effect |
|---|---|
| Closing a widget window left services pointing at it | Turning desktop widgets off, then changing anything, crashed the app natively |
| Widgets could rise above apps | `SetWindowPos` silently did nothing with some reference windows, so each move is now verified |
| Show Desktop left desktop widgets behind | Win+D hid them |
| Exceptions in dispatcher callbacks and in `async void` startup bypassed all error handling | The app died with `0xc000027b` and no log entry |
| SQLite WAL's shared-memory file could stay mapped by Windows Error Reporting's snapshot of a crashed process | One crash stopped every later launch until a reboot. The database now uses a rollback journal |
| Startup stopped at the first failure | An unavailable database cost the user the whole taskbar. Each step now stands alone |
| Settings sliders fed their snapped value back into their setter without limit | Stack overflow. Dragging Opacity or using Reset crashed the app |
| WinUI ended the process when the last window closed | Restart interface, and hiding the bar with no other window open, quit Glyphbar |
| The package had no execution alias | `Glyphbar.exe --exit` and the other recovery commands could not reach an installed app |
| Auto-hide relied on pointer events over a 2px strip | The bar could not be revealed when the Windows taskbar also auto-hid on that edge |
| A menu's open state relied on its Closed event | After unpinning, auto-hide stopped working |
| `dotnet publish` left out the compiled XAML | A published build could not load its pages |
| The power menu treated opening as "action finished" | The menu closed itself straight away |
| Icon-only buttons and list rows had no accessible name | Screen readers announced nothing useful |
| WinUI kept re-adding a window frame | White border, and square corners on the floating bar |

## Test weaknesses found and corrected

- Some checks passed without testing anything: search with no text to read, deleting an event that was never created, and idle CPU reading the wrong process. Each was fixed.
- "Exits through `--exit`" passed on an app that had already crashed. The suite now confirms the app is alive first.
