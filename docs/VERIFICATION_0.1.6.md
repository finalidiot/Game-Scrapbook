# Game Scrapbook v0.1.6 verification

30 October 2026, Australia/Perth.

## Source and focused change

Continued the full source included with the original v0.1.5 Windows package. The delete button now contains a symmetric 12×12 DIP vector X instead of a font glyph. Its 24×24 DIP button, accessible name, tooltip, event routing and existing delete handler are preserved. Other executable source changes only update version strings to 0.1.6. Storage, history, capture, archive, drag and resize implementations are unchanged.

The X geometry runs from (1,1) to (11,11), with equal round-capped strokes. Its visible bounds are 0.125 through 11.875 on both axes. The existing button template centers its content; the icon's ink center is therefore the center of the button without depending on font metrics.

## Build and existing regression checks

- Used stable Microsoft .NET SDK **10.0.403** on Windows x64. The SDK archive's SHA512 was verified against Microsoft's official release metadata.
- Ran `scripts/build.ps1` once: **21 core checks passed**, followed by one successful self-contained Windows x64 single-file publish. No compiler warnings or errors were reported. No GitHub Actions workflow was added or run.
- The existing checks cover new and saved boards, non-integer geometry, delete/undo/redo persistence, archive/restore persistence, immutable image retention, history independence/capacity, backup recovery, corrupt-schema write protection, missing images, path traversal and special filenames, invalid geometry/IDs/versions, colour validation/contrast and legacy-theme migration.
- Published executable product version: **0.1.6**.

Executable SHA256:

```text
8151a64edb5efe72748b0b7ccded6f0edb9075eb3086a408acdad82889fcc5b3
```

## Offscreen Windows WPF checks

Loaded the actual compiled application assembly and its compiled App resources in a separate, windowless WPF process. Invoked the actual card creation method with synthetic in-memory images. No application startup, real saved board, hotkey registration, capture or live UI automation was involved.

| Check | Result |
| --- | --- |
| 96, 120, 144 and 192 DPI, each at 25%, 100% and 300% board zoom | All 12 combinations passed |
| Button and icon dimensions | 24×24 and 12×12 DIPs respectively |
| Vector ink center relative to button | Exactly (12,12) |
| Raster alpha-weighted ink centroid | Maximum offset 0.06257 pixel from center |
| Theme/resource updates, accessible name, tooltip and non-interactive glyph | Passed |
| Archive action and resize grip remain present | Passed |
| Actual delete Button.Click handler with another snip selected | Only the clicked snip was removed |
| Delete, actual Undo and actual Redo, with an isolated on-disk BoardStore | Correct states persisted; unrelated snip retained |
| Original PNG bytes across delete/undo/redo | Unchanged |

WPF antialiasing produces small raster-coverage differences; pixel-perfect mirrored alpha values are not claimed. Geometric centering and the measured raster centroid both passed.

The tested application DLL's SHA256 is `88f9a90505e2b280d09abc87c3908f0c869214bf702fb613d2047df8e8bd1734`, identical to the DLL from the single release publish. [Structured render results](verification/wpf-0.1.6.json) and [an actual card render](images/delete-button-card.png) are included. The README illustration uses synthetic content and actual compiled cards.

## Publication review

Reviewed the complete source tree and the exact staged files. Automated patterns checked for private keys, common access-token formats and hard-coded credential values. No secrets were found. No private board manifests, captured images, diagnostics, local SDK/cache, build output or credentials are included in source. The only published PNGs contain synthetic demonstration content.

The source has an MIT license and a `.gitignore` for build output, local configuration, credentials, runtime boards, images and logs. The application has no third-party application packages or network/upload feature. The Windows package includes the Microsoft .NET runtime's license and third-party notices alongside the application license.

## Verification boundary

These checks verify the focused rendering change, programmatic deletion and history integration, and the existing core regression suite. Physical mouse input, live game capture, monitor transitions, cursor locks and focus restoration were not exercised in this session. Use [WINDOWS_PLAYTEST.md](WINDOWS_PLAYTEST.md) for that manual check. GDI capture and unsigned-preview limitations remain documented in the README.
