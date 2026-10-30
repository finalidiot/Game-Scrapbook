# Game Scrapbook 0.1.4 — build report

Dark grey default and persistent image archive, 30 October 2026 (Perth).

## Changes

- Replace the default black background with neutral dark grey (`#242424`), retaining white text and blue accents.
- Upgrade the exact previous black/white/blue default palette once. Preserve custom palettes and allow a later deliberate black choice.
- Add an Archive action to board images and an Archive button/count to browse stored snippets.
- Show archived thumbnails and a readable selected image, with Restore to scrapbook returning the image to its original board position and size.
- Save archive state in the existing board data and include archive/restore changes in session undo/redo.
- Keep the previously simplified Settings layout, colour controls and readability checks.

## Verification

| Check | Result |
| --- | --- |
| Integrated Windows build and self-contained x64 publish | Passed: zero warnings, zero errors; self-contained x64 publish succeeded |
| Core durability/history/theme/archive checks | 21 passed: existing durability/history/theme checks plus six archive/default-upgrade checks |
| Focused independent source review | Passed; no remaining source-level blocker found |
| Executable version and package integrity | Recorded by the release packaging step |
| Native Windows launch and visual interaction | Not performed in this Linux environment |

The existing capture selection, manual drag/drop and game focus-return workflow remains the basis of this version. Data checks cover persistence and history; they do not establish native Windows visual behaviour. The previous report is preserved in BUILD_REPORT_0.1.3.md.

## Short Windows check

Close the previous app and launch v0.1.4. Confirm the softer default background, archive a snip, reopen Archive and restore it. Restart the app once with a snip archived to check persistence on the user's setup. See WINDOWS_PLAYTEST.md for the focused archive and appearance checks.
