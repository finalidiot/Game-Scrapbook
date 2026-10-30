# Game Scrapbook changes

## 0.1.6 — centered delete icon and open-source release

- Draw a centered X inside each snip's delete button, avoiding font baseline and spacing differences.
- Preserve the existing immediate-delete handler, keyboard/toolbar deletion and undo/redo behaviour.
- Prepare the public source with an MIT license, setup and shortcut guide, build instructions, limitations and a broader `.gitignore`.

## 0.1.5 — delete button on snips

- Add a visible **×** button at the top-right of each board snip.
- Delete the clicked snip immediately without requiring it to be selected first.
- Use the existing deletion and undo path, with **Ctrl+Z** restoring the removed snip. Keyboard and toolbar Delete remain available.

## 0.1.4 — dark grey default and archive

- Soften the default background from black to dark grey (`#242424`), keeping white text and blue accents.
- Upgrade the complete previous default palette once while preserving custom colour combinations. A deliberate black choice remains available afterward.
- Add an Archive button to each board image and an Archive count/button to open stored snips.
- Browse archived image thumbnails and a larger selected-image view, then restore images to their saved board position and size.
- Persist archive state across app restarts and preserve archive/restore operations through snippet undo and redo.
- Keep the simplified Settings layout without the Recipe notes sample panel.

## 0.1.3 — simpler Settings

Remove the Recipe notes sample preview from Settings at the owner’s request. Keep the colour controls, swatches, contrast hint, reset button and Save/Cancel actions.

## 0.1.2 — readable interface and custom colours

The owner accepted the capture workflow and requested more visible interface text, editable colours, a blue/white/black default, and an explanation for two apparently unlabelled Settings checkboxes.

- Start with black backgrounds, white text and blue accents; existing boards receive these defaults without a board migration.
- Increase ordinary interface text to 15, supporting copy to at least 13, and capture instructions to 16.
- Replace faint supporting text with brighter text and use readable text/background combinations throughout themed controls.
- Add Settings → Appearance with independent Background, Text and Accent choices, swatches, custom hex values, a contained preview, and a reset button.
- Save colour preferences with the existing board; Cancel discards the draft theme, and snippet undo/redo preserves the current theme.
- Give both checkbox options visible labels and descriptions: Return mouse after snipping and Keep scrapbook on top.
- Provide explicit checkbox, button, text-field and tooltip rendering so labels use the chosen palette consistently.
- Keep Settings actions visible while the content scrolls on smaller windows; preserve keyboard navigation in the shortcut field.
- Apply accent colours to snip selection and drag outlines, with black/white edge strokes for visibility over game pixels.
- Recover malformed colour preferences without rejecting valid snippet data.

Native Windows appearance still needs the owner's check. Build and core-data checks are recorded separately in BUILD_REPORT.md.

## 0.1.1 — capture startup correction

The first playtest reported no visible overlay after pressing Ctrl+Alt+S and no visible way to start a snip. The user clarified that the capture should use the screen currently being used.

- Capture the monitor containing the pointer at trigger time.
- Allow capture while the scrapbook is selected; separate capture-screen choice from the focus-return target.
- Restore a minimized board without requesting activation, after taking the source screenshot.
- Add a visible Snip button with a three-second countdown and Cancel control.
- Keep shortcut registration status and capture errors visible independently of autosave messages.
- Prepare the selection overlay with a nonempty layout before revealing it, and guard tiny transitional hint layouts.
- Record capture lifecycle and geometry diagnostics without recording screen contents or window titles.
- Show v0.1.1 in the header and explain when an old instance prevents a new executable from launching.

Existing board data and image files are reused without a migration. Move, resize, manual drop placement, game focus return, Delete and undo remain part of the same workflow.

The exact failure path on the user's machine is not established without runtime diagnostics. Native Windows validation remains separate from the successful build and core checks.

## 0.1.0 — first preview

Initial persistent board, global capture shortcut, rectangle selection and manual drag placement, focus return, Delete/undo/redo, local saving and backup recovery. The first owner test found capture startup/discoverability problems addressed in 0.1.1.
