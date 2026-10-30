# Agreed interaction and implementation boundaries

## Owner-approved behaviour

The owner wants a **single application window on a second monitor**, used as a scrapbook containing multiple separate captures. The capture flow is deliberately manual:

1. Start with an easy shortcut or the visible Snip button and its three-second countdown.
2. Display a darkened view of the monitor containing the mouse pointer when capture starts.
3. Select a rectangular area.
4. Grab the resulting cut-out and drag it into the scrapbook.
5. Drop it at the chosen position.
6. Return focus to the exact window from which the capture started.

For existing snips: click one, press Delete, and it disappears immediately. Ctrl+Z restores an accidental deletion. Normal board editing keeps the board focused. Cursor return is an option.

Do not replace the manual drag/drop operation with automatically appending captures, a set of unrelated floating windows, or a browser screenshot demo.

## First build

- C# / .NET 10 / WPF; native desktop window and Windows APIs.
- Physical screen coordinates inside the capture coordinator; board-space conversion occurs at the visible canvas boundary.
- Capture once before displaying tint. The selection image is frozen; the game process is not explicitly paused.
- Capture screen selection is separate from focus return: use the existing external foreground window, or the external window under the pointer when the scrapbook is selected. A capture is still allowed when no external focus target is available.
- Shortcut registration status and capture errors have persistent UI indicators. The button provides an entry point independent of shortcut delivery.
- Non-activating overlay; no OLE drag that would require activating the board.
- A 12 ms mouse-state timer runs only during an active capture.
- At most one ordinary focus-restoration request, verified against the original HWND and process. An intentional external app switch cancels capture without focus stealing.
- Bitmap PNG persisted first, then an atomic layout save. A failed save leaves the capture available to retry.
- Captured images remain immutable; deletion changes layout metadata and enables undo.
- The build contains no AI, network requests, accounts, background recording, telemetry, injection or automatic updates.

## Deferred until feedback establishes a need

OCR/search, text notes, multiple game boards, automatic game detection, cloud sync, tray/startup integration, alternate capture backends, and broad fullscreen/raw-input compatibility are outside this first pass.

## Verification boundary

Compile and core durability checks are possible in the current Linux environment. Native Windows rendering, focus behaviour, mixed-DPI cursor interaction, and real-game compatibility require the included Windows playtest. Passing compilation is never described as passing that playtest.
