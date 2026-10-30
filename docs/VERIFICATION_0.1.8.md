# Game Scrapbook v0.1.8 verification

31 October 2026, Australia/Perth.

## Changes

- **Snap edges** joins or aligns nearby image edges during movement and placement. The option persists; Shift bypasses snapping for an individual drag. Alignment guides appear while moving saved snips.
- **Magnetise** compacts all non-archived snips at the board origin, preserving their dimensions, IDs, images and individual editability. The arrangement has no overlaps or intentional gaps between neighboring images; differently sized images may leave unused space around the outside of the composition. One Undo restores the previous positions; Redo reapplies them.
- **Export image** writes the whole active board to JPG quality 95 or PNG. It crops empty outside canvas, preserves image sizes, gaps and visible overlap order, and excludes archives and interface controls. Output dimensions use one pixel per board unit and are independent of board zoom/scroll. Original stored PNGs remain unchanged.
- Image bounds now match snap and export geometry. **Archive selected** moved to the toolbar so the former action footer does not create a gap between images. The selection outline is an overlay without an image inset. Tiny cards use toolbar Delete and a size-aware resize grip.
- The board format remains version 1 with an optional `snapToSnips` preference. Existing data remains compatible.

## Automated and offscreen verification

| Suite | Result | Scope |
| --- | --- | --- |
| Core | 22 passed | Existing persistence/history/archive/recovery/theme checks plus legacy snap-preference migration, save and undo/redo behavior |
| Layout | 29 passed | Exact edge joins, alignment, thresholds, deterministic ties, self/archive exclusion, zoom tolerance, packing connection/non-overlap, size preservation, limits and distant-board compaction |
| Capture lifecycle | 22 passed | Existing focus acquisition/loss, cursor-return ordering, retry, cancellation and disposal behavior against the actual coordinator with simulated platform calls |
| Image export | 21 passed | Actual WPF composition, cropping, horizontal/vertical joins, gaps, layering, archives, missing images, aspect ratio, bounds/source limits, real JPG/PNG encoding and safe replacement failure |
| Compiled Windows interface | 14 cases passed | Actual MainWindow constructor, XAML controls and action handlers with isolated BoardStore and synthetic images |

The layout suite also arranged 10,000 normal-sized snips in approximately 30 ms in this environment. Oversized or impossible arrangements fail before changing the board.

The Windows interface checks verified one-entry Magnetise history, no-op behavior, persistence and rollback after I/O/validation failure, Archive selected with undo, selected-overlap export matching the board, unchanged source data, exact card image geometry, tiny-card control adaptation, snapped capture placement, snap preferences, read-only export and empty-board controls. The final case checks that managed images/metadata cannot be chosen as an export destination, external/sibling paths remain allowed, and original source bytes remain intact.

The real XAML content was measured and rendered at 1040×780 and 650×440. The toolbar controls fit at both sizes; the minimum-height board remains scrollable. No Window.Show, native HWND, physical input, SaveFileDialog interaction, real user data or live game was involved. [Compiled-interface results](verification/composition-0.1.8.json), [export results](verification/export-0.1.8.json), [interface preview](images/composition-0.1.8.png) and [actual JPG example](images/export-example-0.1.8.jpg) are included.

## Build and artifact identity

Built with Microsoft .NET SDK 10.0.403 as a self-contained Windows x64 single-file executable. All four project check suites passed before the application publish. The first publish attempt was blocked by package-feed access during a vulnerability-data lookup; only the publish step was retried with network access. It completed successfully, retaining a NU1900 feed-availability warning. A final incremental publish added the managed-data destination guard without another restore or component-suite run; the complete 14-case Windows integration suite then passed against that final binary. No application compiler warning or error was reported, and no GitHub Actions job was added or run.

Compiled source commit: `d18d80584fc476a79292994275099c5a1ec66cc7`. Subsequent release documentation and image examples do not change executable source.

Executable SHA256:

```text
47cf27fd771e7a252e41df2b3c9ae0303d504edcd853eb5e272a8efbf828bc59
```

Tested application DLL SHA256: `fe87cd6fd090bee94f4021bb15ac131a2f0f8005e9344c9da4c0244b5e6abc6d`, matching the DLL used for the release publish.

The release package records its final source commit and includes that exact committed source, the MIT license and bundled Microsoft runtime notices. Source review and credential-pattern scanning found no secrets; examples use synthetic content.

## Limits and live testing

The owner previously confirmed the capture/focus workflow in Nivalis Nights with v0.1.7. That result is retained in its report. The new v0.1.8 physical dragging, native save dialog and live game workflow were not exercised in this automated session; use the [Windows checklist](WINDOWS_PLAYTEST.md) for those checks.

Exports are limited to 32 million output pixels, 32,767 pixels per edge and 64 million unique source-image pixels. Missing active images stop export. Exports must be saved outside the managed saved-board folder to protect original images and metadata. Existing external files are replaced only after a complete image is encoded to a temporary file in the same directory; a failed write leaves an existing destination intact. Both JPG and PNG use an opaque background matching the board colour.

The application remains unsigned. Game-specific background input, pausing, fullscreen/HDR and protected-surface limitations still apply.
