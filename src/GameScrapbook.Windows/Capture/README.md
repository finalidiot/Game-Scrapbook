# Capture implementation notes

The capture backend uses a single GDI desktop snapshot before any capture UI is displayed or focus is transferred. Its intended test target is an ordinary windowed or borderless SDR game. Exclusive fullscreen, protected surfaces and HDR may produce an unavailable or inaccurate screenshot and are not advertised as verified.

The source display comes from the physical mouse location at capture time using `MonitorFromPoint`. Focus return is resolved separately: prefer an existing external foreground window, or an external root window under the pointer when the scrapbook is selected. Captures without an external focus target remain valid. Selection, crop and drag coordinates remain physical pixels, including negative monitor origins. WPF overlays are positioned with `SetWindowPos`; the board alone converts the physical drop point into its own coordinates. Application manifest DPI awareness must be PerMonitorV2.

The optional `CanBeginCapture` host gate prevents a new operation during modal settings, readonly recovery or an existing card drag. An already-active capture remains cancellable with the capture shortcut. A minimized board is restored without an activation request after the screenshot is taken. The host's visible Snip button calls `BeginCapture()` after its countdown; `IsHotkeyRegistered` and `CaptureError` drive persistent availability/error UI.

## Input ownership

From v0.1.7, the selection overlay temporarily becomes the foreground window after the frozen snapshot has been acquired. It retains that focus while selecting, carrying a snip or retrying an invalid drop. This replaces the former entirely non-activating selection overlay, which left the game free to read foreground mouse movement and could turn its camera during a snip.

The overlay must successfully take focus before capture proceeds. If focus cannot be acquired or is lost during the operation, capture stops rather than continuing to place a snip while the game or another application is active. An intentional Alt+Tab to another external app leaves that app active and does not return the pointer. The coordinator does not repeatedly fight focus changes.

The drag preview remains non-activating and click-through, with its HWND disabled so `WindowFromPoint` ignores it for board hit-testing. There is no activating OLE drag to the board. A 12 ms dispatcher timer reads current physical button state and `GetPhysicalCursorPos` only during an active capture, so releases on another screen do not depend on the source overlay receiving mouse-up messages. Button mapping respects the left-handed mouse setting. Escape is registered as a temporary session hotkey, with session polling as a fallback when registration fails.

Foreground ownership prevents games that read only foreground input from consuming the snipping gesture. It does not block all mouse input system-wide. Windows allows applications registered with `RIDEV_INPUTSINK` to [receive raw input in the background](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input), and some games may still react or re-confine their cursor. Games may also pause according to their unfocused-window settings. No game-process injection, global input blocker or input-blocking driver is used. The project owner has confirmed that v0.1.7 works in a live Nivalis Nights session, including the camera/aim fix; this is separate from the automated and offscreen checks.

## Presentation and placement

Overlay presentation prepares a non-empty layout at zero opacity, applies the appropriate selection or passive-preview styles, sets physical bounds, updates layout, checks native/rendered size and DPI, then reveals the image. Tiny intermediate layouts do not create negative-size hint rectangles. Capture diagnostics distinguish shortcut delivery, snapshot acquisition, overlay geometry, focus and completion; no window titles or screen contents are logged.

The drop cursor marks the new card's upper-left. The host returns `CaptureDropTarget.BoardPosition` in board coordinates and may supply `DisplayScale` (physical pixels per board unit) for a preview matching board zoom and monitor DPI. Both host and preview use initial image dimensions capped at 520 by 420 board units, preserving aspect ratio and never enlarging a small capture.

A successful drop calls the host once. If the host throws while saving, the selected crop stays available for another attempt. An invalid target never auto-places the image.

## Cleanup and return

On ordinary completion or cancellation, optional cursor return happens while the overlay still owns focus, before returning focus to the original game. This avoids delivering the pointer-return movement to an already-focused game. Focus restoration makes at most one ordinary `SetForegroundWindow` request and verifies the actual foreground HWND and original process. Windows can [refuse a foreground request](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow); there are no simulated clicks, Alt presses or repeated attempts. Intentional external application switches do not trigger focus or cursor return.

Existing cursor confinement is remembered for the session. Original bounds are restored only when the original source still owns foreground focus and no other application has installed new clip bounds. Games that immediately re-confine the pointer require game-specific testing; the utility does not repeatedly fight their input management.

Every completion, cancellation, exception and disposal path removes its windows and stops the capture timer as appropriate. A host-save failure preserves a retryable crop. Long-lived hotkeys are unregistered on disposal; changing a hotkey registers its replacement first so a conflict leaves the previous binding usable.
