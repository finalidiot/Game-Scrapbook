# Game Scrapbook — Windows playtest

This checklist verifies the real Windows interaction. A successful build and the
core data checks do not establish that a game keeps running during capture or
that Windows returns focus afterward. Those behaviours need this playtest.

## First setup

1. Close the previous Game Scrapbook window. Extract the complete portable package and launch `GameScrapbook.exe`. Confirm **v0.1.5** and a **Snip** button at the top.
2. Put its board window on the second monitor. Leave **Always on top** off for
   the first test. Keep **Return cursor** enabled.
3. Open an ordinary desktop application on the game monitor. Start here, then
   repeat the capture test in a game using windowed or borderless mode.

First try **Snip**, move the pointer to the desired screen during the three-second countdown, and confirm that screen darkens. Then compare with Ctrl+Alt+S. Click the visible **Cancel** button to stop a countdown; Escape is a global cancel only after capture has begun.

The shortcut state should remain visible after any board autosave. If capture fails, record the message above the board and the corresponding `capture-diagnostics.log` from **Settings → Open saved-board folder**.

## Capture, place, and resume

1. Select the source application and place the pointer over the information to
   capture. Press **Ctrl+Alt+S**. The pointer's monitor should be selected even if the foreground window is on another monitor.
2. Drag a rectangle around it and release. Check that the selected image is
   readable, correctly aligned, and has its original brightness.
3. Grab the selected cut-out and drag it across to an empty part of the board.
   Release at a recognisable spot.
4. Check that **exactly one** snip appears where you placed it. It must not be
   added automatically before the drop.
5. Check that the original source application becomes active again without
   clicking it and that the pointer returns to its starting position.
6. Add two more snips at different positions. The previous ones should remain.

Repeat in the game. Check whether the game continues to animate while the
selection is visible, whether dragging causes unintended game actions, and
whether controls resume after the drop. Record the game and its display mode
if any of these checks fail. Do not treat a successful desktop test as evidence
that the game test passed.

## Edit the board

1. Click an existing snip. Its selection outline should be clear and the board
   should retain focus.
2. Drag it to a new location. Use its bottom-right grip to resize it; the image
   should retain its proportions.
3. Press **Delete**. Only the selected snip should disappear, immediately.
4. Press **Ctrl+Z** to restore it. Check its image, size, and position. Press
   **Ctrl+Y** to redo the deletion, then **Ctrl+Z** to restore it again.
5. Test toolbar **−**, **+**, and **100%**, and **Ctrl+mouse wheel**. After
   zooming, try another capture and drop; the drop location should still match
   the pointer.
6. Close and reopen the app. Check every image, its placement and size, the
   saved window location, and settings. Delete one snip, close, and reopen:
   the deleted snip must stay deleted.

## Top-right delete button check

1. Keep at least two snips on the board. Select one, then click the **×** at the top-right of the other. Confirm the clicked snip is deleted, without a confirmation dialog or a drag starting.
2. Press **Ctrl+Z** to restore it at the same position and size. Confirm **Ctrl+Y** redoes the deletion.
3. Check the **×** remains visible on differently sized snips and at your usual board zoom. Confirm normal image dragging and the resize grip still work.
4. Delete a snip with **×**, close and reopen the app, and confirm the deletion persisted.

## Cancel and recover

- Start a capture and press **Escape**. The overlay should disappear and the
  original application should be active. Capture should work again afterward.
- Repeat using **right-click**, including after making a selection.
- Drop a captured snip outside the board. It should remain available for a
  valid drop or cancellation; it must not be silently placed or lost.
- Disable **Return cursor** and repeat a successful drop. The pointer should
  remain at the drop position while focus returns to the source application.
- If the chosen shortcut is already in use, settings should show the conflict
  and preserve a working previous shortcut.

## Monitor checks, when applicable

Repeat one capture with the board on a monitor positioned to the left of the
main monitor, and with different display scaling on the two monitors (for
example, 100% and 150%). Check selection edges, drag preview, and final drop
location. If a saved monitor is disconnected, the board should reopen on an
available screen.

## Report a failure

Record: app version, Windows version, game/application, windowed/borderless/
exclusive mode, monitor arrangement and scaling, the failed step, and what
happened. A screenshot of the board or visible error is useful. Exclusive
fullscreen and games with cursor locking or unusual input handling are
compatibility checks, not established support in this first version.

## Archive check

1. Add two snips at recognisable board positions and sizes. Click **Archive** on one image. Confirm only that image leaves the active board and the Archive count increases.
2. Open **Archive**, choose its thumbnail and check that the image can be read in the larger view. Close the window and confirm the active board still contains only the other snip.
3. Close and reopen the app. Confirm the archived image remains in Archive with its original contents.
4. Click **Restore to scrapbook** and close Archive. Confirm the image returns at its saved position and size and is removed from the archived list.
5. Press **Ctrl+Z** on the board to undo the restore, then **Ctrl+Y** to redo it. Repeat an archive and undo it. Confirm each snip exists once and the counts match.
6. Open an empty Archive and check its explanation and Close button. Archive several differently sized images and check thumbnail selection and the larger view.

## Appearance check

1. Open a board using the previous default palette and confirm its background changes to dark grey, with white text and blue buttons. A custom palette should retain its colours. Check the toolbar, lower instructions and capture hint for readable text.
2. Open Settings and confirm the Recipe notes sample preview is absent. Check that **Return mouse after snipping** and **Keep scrapbook on top** both have a visible checkbox, label and description. Toggle them and confirm the check mark is clear.
3. Under Appearance, change an accent using a swatch and a background/text pair using custom hex values. Confirm the swatches and contrast hint follow the choices. Cancel Settings and confirm the app kept its previous theme.
4. Repeat and save. Check the toolbar, selected card border, resize grip, capture outline and Settings labels. Close/reopen the app and confirm the theme and board both persist.
5. Try white background with black text, then the default dark grey background with white text. Ensure both remain readable. Try identical background/text colours and confirm saving is blocked with a readable explanation.
6. Click **Reset to blue, white and dark grey**, then Save. Check the default palette returns. Delete and undo a snip and confirm the chosen theme stays in place.
7. At your usual Windows display scaling, resize Settings smaller and confirm its content scrolls while Save/Cancel stay accessible. Use Tab and Shift+Tab through the shortcut box to check keyboard navigation.

8. After the upgrade, deliberately choose black as the background and save. Reopen the app and confirm it remains black. Reset restores the dark grey default.
