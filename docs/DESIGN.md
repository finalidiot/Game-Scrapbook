# Agreed interaction and implementation boundaries

## Owner-approved behaviour

The owner wants a **single application window on a second monitor**, used as a scrapbook containing multiple separate captures. The capture flow is deliberately manual:

1. Start with an easy shortcut or the visible Snip button and its three-second countdown.
2. Capture the monitor containing the mouse pointer, then give the darkened selection overlay focus for the snipping gesture.
3. Select a rectangular area.
4. Grab the resulting cut-out and drag it into the scrapbook.
5. Drop it at the chosen position.
6. Return focus to the exact window from which the capture started.

For existing snips: click one, press Delete, and it disappears immediately. Ctrl+Z restores an accidental deletion. Normal board editing keeps the board focused. Cursor return is an option.

Do not replace the manual drag/drop operation with automatically appending captures, a set of unrelated floating windows, or a browser screenshot demo.

## Current implementation

- C# / .NET 10 / WPF; native desktop window and Windows APIs.
- Physical screen coordinates inside the capture coordinator; board-space conversion occurs at the visible canvas boundary.
- Capture once before displaying tint or taking focus. The selection image is frozen; the utility does not explicitly pause the game process. Games may pause themselves when the overlay takes focus.
- Capture screen selection is separate from focus return: use the existing external foreground window, or the external window under the pointer when the scrapbook is selected. A capture is still allowed when no external focus target is available.
- Shortcut registration status and capture errors have persistent UI indicators. The button provides an entry point independent of shortcut delivery.
- Temporarily foreground the selection overlay while selecting, dragging and retrying drops. Keep the ghost preview non-activating and click-through; no OLE drag requires activating the board.
- Refuse to continue capture without overlay focus. Stop when that focus is lost, including intentional game reactivation, before placing an unfinished snip.
- A 12 ms mouse-state timer runs only during an active capture.
- Return the optional mouse position before releasing overlay focus, then make at most one ordinary focus-restoration request, verified against the original HWND and process. An intentional external app switch cancels capture without focus stealing or a pointer jump.
- Bitmap PNG persisted first, then an atomic layout save. A failed save leaves the capture available to retry.
- Captured images remain immutable; deletion changes layout metadata and enables undo.
- The build contains no AI, network requests, accounts, background recording, telemetry, injection or automatic updates.

## Deferred until feedback establishes a need

OCR/search, text notes, multiple game boards, automatic game detection, cloud sync, tray/startup integration, alternate capture backends, and broad fullscreen/raw-input compatibility are outside this first pass.

## Verification boundary

Record current build, core durability and focused Windows checks in the versioned verification report. Native mouse input, focus behaviour, mixed-DPI cursor interaction and real-game compatibility still require the included Windows playtest. Passing compilation or an offscreen check is never described as passing that playtest.

The project owner has confirmed successful live use of v0.1.7 in Nivalis Nights after the camera/aim fix. That validation does not establish compatibility with every game or display mode, including games that intentionally read background input or continuously lock the cursor. The utility does not suppress all system input or inject into the game.
