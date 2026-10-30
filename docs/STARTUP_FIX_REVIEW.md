# Capture startup correction — review notes

## Report

The first Windows playtest produced no visible selection overlay when Ctrl + Alt + S was pressed. The user clarified that the capture should operate on the screen currently being used. The correction uses the monitor containing the mouse pointer at the instant capture starts.

## Confirmed findings in 0.1.0 source

- Starting a capture while Game Scrapbook was the foreground application was rejected before the overlay appeared.
- Capture monitor selection used the foreground window, so moving the pointer to another monitor did not change the selected display.
- There was no clickable Snip control; the shortcut was the only entry point.
- Shortcut registration failure was shown only in the ordinary status line. A later successful autosave could overwrite that message.
- The transparent overlay was initially shown as a 1 × 1 window offscreen, then positioned through native calls. If its selection content rendered at that transient size, its hint rectangle could have a negative width. The corrected renderer guards tiny layouts and prepares a nonempty overlay before revealing it. Whether this transient render occurred on the user's machine is unconfirmed.

These findings explain plausible no-overlay paths. They do not prove which path occurred on the user's PC; the original build did not record enough capture-start events to establish that.

## Focused checks for the corrected build

1. With the scrapbook visible, confirm the shortcut state is clearly displayed and a Snip button is available.
2. Use the Snip button, move the pointer to the game display during its countdown, and verify that display darkens.
3. Try the shortcut with the game active. Compare the resulting overlay with the button path.
4. Keep the scrapbook active but place the pointer on the other monitor; the pointer's monitor should still be selected.
5. Select a rectangle, grab the cut-out, and drop it at a chosen canvas position. Confirm the expected source window regains focus.
6. Cancel the countdown with its visible Cancel button, then cancel an active selection with Escape. A cancelled countdown must not create an overlay later. Escape also cancels the countdown while the scrapbook itself has keyboard focus; it is not a global countdown shortcut.
7. If a shortcut conflict is detected, confirm that its warning stays visible after a layout save and that the Snip button still works.
8. Repeat with different monitor scaling or a monitor to the left of the main display if those match the actual setup.

If the overlay still fails to appear, the capture diagnostic log should distinguish shortcut registration/delivery, rejected startup, selected monitor, screenshot acquisition, and overlay geometry. The log should contain operational metadata only, not screenshot pixels, game text, or window titles.

## Verification boundary

Compilation and core checks can establish source consistency and tested storage behaviour in the Linux build environment. They do not establish native hotkey delivery, overlay visibility, game input behaviour, or successful Windows focus restoration.

## Correction integration review

Source review found the revised pointer-based monitor selection and separate focus-return target coherent. The countdown stops when a capture starts, when startup fails, when Settings/Help opens, or when the app closes. Shortcut availability and capture errors have dedicated UI states that ordinary autosave status messages do not replace. Launching an updated executable while an older instance is running produces an explicit instruction to close the old window first.

No remaining source-level blocker was found in this focused review. The Windows checks above remain required for behavioural acceptance.
