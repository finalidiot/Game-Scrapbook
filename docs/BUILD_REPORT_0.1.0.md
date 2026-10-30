# Game Scrapbook 0.1.0 — build report

Prepared for the first owner playtest, 30 October 2026 (Perth).

## Delivered

A portable, self-contained Windows x64 executable, complete source, build script, usage guide and Windows playtest checklist. The bundled runtime means the user does not need a separate .NET installation. This preview is unsigned.

## Checks actually performed

| Check | Result |
| --- | --- |
| Integrated Windows WPF release build and self-contained publish | Passed, zero warnings and zero errors in the final publish |
| Windows executable format | Verified PE, AMD64/x64, Windows GUI subsystem |
| Core regression checks | 10 passed |
| Independent source review | Findings corrected; no remaining source-level blocker identified |
| Archive integrity | Verified when the release archive was assembled |
| Native Windows launch/rendering | Not performed in the Linux build environment |
| Actual game capture/input/focus behaviour | Not performed; owner playtest required |
| Physical mixed-DPI/multiple-monitor behaviour | Not performed; owner playtest required |

The ten core checks cover:

1. A new board is saveable without creating files merely by loading.
2. Captured image metadata and non-integer board geometry survive a restart.
3. Delete, undo, redo and restart preserve the expected board and immutable image.
4. A corrupt primary manifest recovers from backup while retaining the damaged file.
5. Unrecoverable metadata disables writes rather than replacing history.
6. An incomplete schema cannot silently become an empty board.
7. Missing or damaged images keep their metadata and produce warnings.
8. Path traversal and Windows special filenames are rejected.
9. Invalid dimensions, non-finite geometry, duplicate IDs and unsupported versions are rejected.
10. History snapshots are independent of mutable state and obey the configured capacity.

## Review corrections included

- Preserve existing card-model references when a new capture cannot be saved.
- Decode saved crops independently so each small snip does not retain an entire screen image.
- Release image cache entries after deletion; undo reloads the PNG.
- Keep resize bounds valid for extremely thin captures.
- Catch invalid saved-data errors explicitly.
- Prevent a capture shortcut from opening an overlay over Settings/Help or during an existing-card drag.
- Keep highlight geometry fixed during target hit-testing.
- Capture Escape temporarily during selection and make the drag preview click-through.
- Verify focus restoration rather than assuming it succeeded.

## Build tools

- .NET SDK: 10.0.403, official Microsoft Linux x64 distribution; upstream SHA-512 verified before extraction.
- Runtime included in the publish: .NET 10.0.14 with Windows Desktop support.
- Target: `net10.0-windows`, `win-x64`.
- WPF with Windows P/Invoke, System.Text.Json storage, no third-party application NuGet dependencies.
- Single-file publish, native libraries included for extraction, compression enabled, trimming disabled.
- No GitHub Actions runs were used.

## Acceptance still required

This report establishes that the source compiles, the distributable is produced, and the tested storage/history behaviours work. It does **not** establish that a specific game stays unpaused, ignores selection gestures, releases its pointer lock, or returns focus correctly. Use `WINDOWS_PLAYTEST.md` for those checks, starting with a windowed/borderless SDR game.
