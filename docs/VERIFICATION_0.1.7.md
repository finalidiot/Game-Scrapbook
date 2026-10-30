# Game Scrapbook v0.1.7 verification

30 October 2026, Australia/Perth.

## Reported problem and change

The user reported that Nivalis Nights' camera/aim moved with the mouse while carrying a snip. The previous capture window deliberately stayed non-activating, leaving the game foreground. A hit-testable visual overlay does not stop a foreground game from reading mouse movement.

The application now takes the frozen screenshot first, then explicitly gives the selection overlay input focus. Capture proceeds only after the overlay's native HWND is actually foreground. The preview remains disabled, passive and click-through. If the game or any other window becomes active during selection or dragging, the coordinator cancels before processing another gesture or placing a pending drop.

On normal completion or cancellation, the optional pointer return happens while the selection overlay still owns focus. The coordinator then makes at most one ordinary request to restore the original game, before closing the overlay. This avoids returning the pointer after the game is already active. An intentional application switch does not trigger cursor, focus or clip restoration. An own-app capture without an external target returns focus to the scrapbook.

The original cursor clip is remembered before taking focus, because games may unclip on deactivation. It is restored only after the original HWND/process has regained focus and only if another app has not installed a different clip. Invalid drops and save failures remain retryable while the overlay retains focus.

## Build and checks

- Windows x64 self-contained single-file publish succeeded with .NET SDK **10.0.403**; no compiler warnings or errors were reported.
- All **21 existing core checks** passed, covering persistence, delete/undo/redo, archives, backup recovery, validation and themes.
- All **22 capture-lifecycle checks** passed against the actual linked `CaptureCoordinator.cs` source. The platform calls, snapshot and overlay windows were simulated; the WPF owner remained unshown. These checks did not move the real pointer, register a real hotkey, read user data or interact with a game.
- No GitHub Actions workflow was added or run. The build script runs capture checks on Windows and explicitly skips those Windows-only checks on other platforms.
- The centered X, board interaction, archive and storage implementations remain unchanged from v0.1.6. Its rendering evidence remains in the historical report; it is not presented as a new game test.

The lifecycle cases include acquisition denial, snapshot/clip ordering, successful placement, pointer-before-focus return, source-game reactivation, Alt+Tab/board/no-foreground cancellation, invalid-drop retry, failed-save retry, Return mouse off, Escape/right-click, closed/reused source HWNDs, focus-return denial, preservation of another app's clip, swapped mouse buttons, disposal and the host gate. See [the reproducible test project](../tests/GameScrapbook.Capture.Checks/README.md) and [structured results](verification/capture-0.1.7.json).

## Actual native overlay styles

A separate harness loaded the built v0.1.7 application DLL and created hidden HWNDs using the actual overlay constructor and native style configuration. All 20 assertions passed: the selection HWND is enabled/focusable without no-activate or click-through styles; the preview HWND remains a disabled, unfocusable, no-activate, click-through tool window. The preview's actual `TakeInputFocus()` returned false.

Neither window was shown or activated. Foreground queries returned zero in that execution context, so this establishes native styles rather than live desktop focus acquisition. No pointer movement, game interaction or user-data access occurred. See [native style results](verification/overlay-0.1.7.json).

## Artifact identity

Published executable SHA256:

```text
1fbd74e2b2297fb1944b3b76aa4c4db66a1ba750392a492608a6a8e94f0e15d4
```

Compiled application DLL SHA256: `c710b5616ba127a070e2343aabfc12dc1d7d37f513d90f0a2bbcc656944088af`.

The package records the final source commit and contains that exact committed source, plus the application license and bundled runtime notices. The executable's .NET informational metadata also contains the v0.1.6 base commit, because this build was made from the reviewed working tree before the release commit. The hashes above identify the tested v0.1.7 binary; the interface and assembly version are 0.1.7.

## Remaining live check

**Nivalis Nights has not been live-tested with this build in this session.** The tests establish coordinator ordering and cancellation behavior, not actual Windows foreground permission or a game's input handling. Follow the [Nivalis Nights camera/focus checklist](WINDOWS_PLAYTEST.md) with the new executable.

Temporarily taking focus may pause a game according to that game's settings. Games registered to read background raw input may still react. This change does not inject into the game, synthesize game clicks, install an input hook/driver or block input system-wide. Windowed/borderless SDR remains the intended target.

The source was reviewed and scanned before publication. No credentials, private board data, diagnostic logs or live game captures are included. Release archive contents and uploaded checksums are verified separately during packaging/publication.
