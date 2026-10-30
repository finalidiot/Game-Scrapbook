# Game Scrapbook 0.1.1 — build report

Capture startup correction following the first owner playtest, 30 October 2026 (Perth).

## Report and correction

The owner reported that Ctrl+Alt+S produced no visible overlay and there was no visible snip control. The owner clarified that capture should operate on the screen currently being used. Version 0.1.1 uses the physical mouse pointer to choose that monitor.

Confirmed problems in the first source included own-app foreground/minimized start rejection, a shortcut-only entry point, and shortcut errors that ordinary autosave status could overwrite. A tiny initial overlay could also create an invalid hint rectangle. These are corrected, but the exact path encountered on the owner's PC is not established without runtime evidence.

## Delivered changes

- Pointer-based capture monitor, with the focus-return window resolved separately.
- Visible Snip button with a three-second countdown and Cancel control.
- Capture allowed when the scrapbook is selected or minimized.
- Persistent shortcut readiness and capture-error indicators.
- Nonempty overlay layout before reveal, with native/rendered bounds and DPI checks.
- Local capture lifecycle diagnostics, excluding window titles and screen contents.
- Visible v0.1.1 label and a message when an old instance prevents the update from launching.
- Existing board data reused without migration.

## Checks actually performed

| Check | Result |
| --- | --- |
| Integrated Windows WPF release build | Passed: zero warnings, zero errors |
| Self-contained Windows x64 publish | Passed |
| Existing core durability/history checks | 10 passed |
| Focused independent source review | No remaining source-level blocker found |
| Executable architecture and version | Verified during packaging: x64 GUI, 0.1.1 |
| Archive integrity | Verified during packaging |
| Native Windows launch, shortcut delivery, overlay rendering | Not performed in the Linux build environment |
| Game input, focus return and physical monitor/DPI behaviour | Not performed; owner playtest required |

The ten core checks cover new-board behaviour, image/geometry persistence, Delete/undo/redo/restart, backup recovery, unrecoverable data protection, incomplete schemas, missing images, unsafe filenames, invalid geometry and independent bounded history. They do not test the Windows capture UI.

## Build tools

.NET SDK 10.0.403; net10.0-windows; win-x64; bundled .NET 10.0.14 Windows Desktop runtime. Single-file self-contained publish, native extraction enabled, compression enabled, trimming disabled. No third-party application NuGet packages and no GitHub Actions runs. The executable is unsigned.

## First Windows check

Close the previous version, open the new executable, and confirm the v0.1.1 label and Snip button. Click Snip and move the pointer to the desired screen during the countdown. Compare that with Ctrl+Alt+S from the same screen. If neither produces an overlay, report the persistent error and capture-diagnostics.log. See WINDOWS_PLAYTEST.md and STARTUP_FIX_REVIEW.md for the focused checks.
