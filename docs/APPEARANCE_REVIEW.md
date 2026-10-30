# 0.1.2 appearance source review

A separate reviewer inspected the main-window integration, capture drawing changes, resource templates, theme manager, Settings and colour picker.

## Findings resolved

1. Derived Settings and colour picker windows must bind their own background/foreground; a base Window implicit style alone can leave light labels on a light system background. Both constructors now bind their theme resources explicitly and set the larger font.
2. Shortcut editing must preserve normal dialog navigation. Tab, Shift+Tab, Enter and Escape are now allowed through as appropriate.

## Confirmed in source

- Main window, dialogs, controls, card borders and capture hints use the palette.
- Text inside accent-filled elements chooses black or white for contrast.
- Checkbox labels and descriptions are explicit; tick and keyboard focus states are drawn by the app.
- Draft theme edits are confined to a cloned settings object and a local preview; cancellation does not apply them.
- Saving rejects an unreadable text/background pair.
- Settings and colour picker content scroll while action buttons stay visible.
- Rectangle selection, manual drag/drop and focus-return coordination are preserved; overlay edits concern drawing and hint typography.

No remaining source-level blocker was found. This review does not substitute for native Windows visual inspection; that remains an owner playtest.
