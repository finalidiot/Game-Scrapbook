# Game Scrapbook 0.1.2 — build report

Appearance and Settings correction, 30 October 2026 (Perth).

## Owner feedback and result

The owner accepted the 0.1.1 capture workflow, then requested clearer interface text, editable interface colours, blue/white/black defaults, and an explanation for two apparently unlabelled checkboxes in Settings.

Version 0.1.2 adds those colour controls and makes both checkbox options explicit. Source review identified that derived Settings windows were relying on an implicit base-Window style, while labels used explicit light foregrounds. Both dialogs now bind their background and foreground directly, and checkboxes have an explicit template. The precise old appearance on the owner's PC was not independently reproduced here.

## Delivered changes

- Black background, white text, blue accents by default, including for existing boards without theme preferences.
- Larger ordinary interface text, brighter supporting copy, and larger capture instructions.
- Background, Text and Accent controls, with a 24-colour palette, custom hex input, a local preview and a reset button.
- Clearly labelled Return mouse after snipping and Keep scrapbook on top checkboxes, each with an explanation.
- Explicit themed controls and contrasting text inside coloured buttons.
- Scrollable Settings and colour picker content with visible Save/Cancel actions.
- Theme preferences persist across restart and are preserved by snippet undo/redo. Cancelling Settings discards edits.
- Malformed theme preferences recover without making valid snippet data unloadable.
- Appearance changes to selection/drag outlines; capture input, manual drop placement and focus-return coordination were not redesigned.

## Checks actually performed

| Check | Result |
| --- | --- |
| Integrated Windows WPF release build after review fixes | Passed: zero warnings, zero errors |
| Core durability/history/theme checks | 15 passed: ten existing checks and five theme checks |
| Focused independent source review | No remaining source-level blocker found |
| Self-contained Windows x64 publish | Passed |
| Executable architecture, version and archive integrity | Recorded in PACKAGE_VERIFICATION.json in the release package |
| Native Windows launch and visual rendering of the updated interface | Not performed in this Linux environment |
| Live game capture/focus and physical monitor interaction in 0.1.2 | Not performed here; previous workflow was accepted by the owner in 0.1.1 |

The five new core checks cover legacy preferences, custom colour persistence and history, malformed colour types, low-contrast recovery, and colour normalization/contrast anchors. They test data behaviour rather than Windows rendering. Review corrected missing explicit dialog colours and keyboard navigation in the shortcut field. Compilation warnings from event closures were removed by initializing the referenced controls before registering their handlers.

## Build tools

.NET SDK 10.0.403; net10.0-windows; win-x64; bundled .NET 10.0.14 Windows Desktop runtime. Single-file self-contained publish, native extraction and compression enabled, trimming disabled. No third-party application packages and no GitHub Actions runs. The executable is unsigned.

## Short Windows check

Close the previous version, open the new executable, and confirm v0.1.2. Open Settings and check the checkbox labels, then change and save an accent colour. Reopen the app to confirm it persists. Reset restores blue, white and black. See WINDOWS_PLAYTEST.md for the focused appearance sequence if needed.
