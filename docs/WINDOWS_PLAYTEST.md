# Game Scrapbook — Windows playtest

This checklist verifies the real Windows interaction. A successful build and the
core data checks do not establish that a game stops reading mouse movement during
capture or that Windows returns focus afterward. Those behaviours need this playtest.
The overlay temporarily takes focus; a game may pause while unfocused according to its settings.

## First setup

1. Close the previous Game Scrapbook window. Extract the complete portable package and launch `GameScrapbook.exe`. Confirm **v0.1.8** and a **Snip** button at the top.
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

Repeat in the game. Check that selecting and dragging do not turn the camera,
move the aim or cause unintended game actions, and that controls resume after
the drop. The game may pause while it is unfocused. Record the game and its
display mode if any checks fail. A successful desktop test does not establish
that the game test passed.

## Camera and focus regression check (v0.1.7)

The project owner confirmed successful live use of v0.1.7 in Nivalis Nights after the camera/aim fix. Use these steps for detailed regression checks on additional setups; a complete per-step record was not supplied for that session.

1. With **Return mouse after snipping** enabled, aim at a recognisable fixed point. Start a snip with Ctrl+Alt+S. Confirm the screenshot shows the intended scene before the overlay takes focus.
2. Move the mouse widely while selecting a rectangle, then while carrying the cut-out to the second monitor. Confirm the camera/aim stays at the same point and no game click action occurs. If the game pauses when unfocused, record that behaviour rather than treating it as a capture failure.
3. Drop on the board. Confirm exactly one snip is saved, focus returns to the original game, and there is no camera jump when the mouse is returned. Repeat cancellation with Escape and right-click and check the same return behaviour.
4. Drop outside the board. Confirm no snip is saved, the selected cut-out remains available, and moving during the retry still does not move the game camera. Finish with a valid drop.
5. Disable **Return mouse after snipping**. Repeat a successful drop and cancellation; the pointer should remain where it was released while game focus returns.
6. While selecting, ready to drag, and dragging, intentionally Alt+Tab to another application. Capture should cancel before placement. The selected application must keep focus and the pointer must not jump back. Repeat by intentionally reactivating the game during capture; the unfinished snip must not be placed.
7. If Windows refuses to activate the overlay, confirm capture ends with a readable error rather than proceeding while the game stays active. Start another capture afterward to check cleanup.
8. Repeat using **Snip** and its countdown, with the board minimised, and at your usual monitor scaling. Check pointer freedom across monitors and that any game cursor confinement resumes only on return.

Games that intentionally receive background mouse/raw input can behave differently. Record that case separately; the utility does not use a global input blocker or game-process injection.

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

## Snap edges and magnetise

1. Keep at least three differently sized active snips on the board and one in Archive. Leave **Snap edges** checked. Move a saved snip close to another image's left, right, top and bottom edges. Confirm the alignment guides appear near a valid edge and the images meet without an Archive footer or hidden gutter between them.
2. Move far from the other images. Confirm there is no jump toward an unrelated distant snip. Repeat at 25%, 100% and 300% board zoom and your usual Windows scaling.
3. Hold **Shift** while moving a saved snip near an edge. Confirm it moves freely. Release Shift during the move and confirm snapping is available again. Turn **Snap edges** off, close/reopen the app and confirm that preference is remembered; turn it back on afterward.
4. Move one previously joined snip away. Confirm the others remain where they were: snapping does not create a permanent group. Resize a snip and confirm its image proportions remain correct.
5. Spread snips apart, including one beyond the current scrolled view. Click **Magnetise**. Confirm all active snips move into a compact arrangement at the top-left, preserve their sizes and remain independently editable. Confirm the archived image is unchanged.
6. Press **Ctrl+Z** once. Confirm every active snip returns to its previous position. Press **Ctrl+Y** once to reapply Magnetise. Restart and check the final arrangement persists.
7. Select an image and use toolbar **Archive selected**. Confirm it is archived and the remaining images have no footer gap. Restore it from Archive and verify its saved size/position.
8. Try an empty board and a board with one image. Confirm Magnetise is unavailable when it cannot rearrange multiple snips, and the other board controls remain usable.

## Export a combined image

1. Arrange images edge-to-edge horizontally and vertically, with a separate pair leaving an intentional gap. Include one overlapping pair and select the image you want on top. Keep another image in Archive.
2. Click **Export image**, save a JPG, and open it in an image viewer. Confirm all active images are present, the outside empty canvas is cropped away, and edge joins do not gain Archive/Delete controls or blank control gutters.
3. Confirm intentional gaps use the board background colour, overlaps match the board's visible stacking, and archived images are excluded. Selection outlines, the grid, guides, buttons and resize grips must not appear.
4. Put an active snip beyond the visible scrolled area. Export at different board zoom levels and scroll positions. Confirm the complete non-archived board is included each time, with unchanged image dimensions and placement.
5. Save a PNG and compare image dimensions and layout with the JPG. Check text at 100% in the viewer; PNG is lossless and JPG uses quality 95. Check that changing board colours changes the solid export background.
6. Cancel the save dialog. Confirm no image file is created. Export to an existing filename and use the normal overwrite prompt; confirm the resulting file opens completely. Cancel the overwrite and check the existing file stays unchanged.
7. After an export, move, archive and undo a snip. Confirm the images remain individual editable board items and the export did not change their records.
8. With an empty active board, confirm **Export image** is unavailable. In a disposable test board only, test a missing active PNG and confirm export reports the problem without saving an incomplete image.
9. Use a very spread-out or oversized disposable layout and confirm the output guard reports a clear error rather than hanging or replacing an existing file. Limits are 32 million output pixels and 32,767 pixels per edge; the separate 64-million-source-pixel guard may require archiving some images.

## Top-right delete button check

1. Keep at least two snips on the board. Select one, then click the **×** at the top-right of the other. Confirm the clicked snip is deleted, without a confirmation dialog or a drag starting.
2. Press **Ctrl+Z** to restore it at the same position and size. Confirm **Ctrl+Y** redoes the deletion.
3. Check the **×** remains centered on ordinary-sized snips and at your usual board zoom. For very small images where it is hidden, select the image and test toolbar **Delete** and the Delete key. Confirm normal image dragging and the resize grip still work.
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

1. Add two snips at recognisable board positions and sizes. Select one and click toolbar **Archive selected**. Confirm only that image leaves the active board and the Archive count increases.
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
