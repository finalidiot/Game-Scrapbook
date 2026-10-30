# Game Scrapbook 0.1.4 — focused archive review

A separate source review found no remaining blocker in the archive integration.

- Archive and restore save the new state before changing visible board cards.
- Failed saves restore the archive flag and timestamp; undo history records only a successful operation.
- Board rendering, counts, canvas extents and cached images exclude archived snippets.
- Restore retains image identity, position and size; history keeps current preferences.
- The Archive button sits below the image and handles its own mouse input. Resize grips are tracked independently of child order.
- Thin images remain aligned to the left so a wider action footer does not move their drop anchor.
- The archive viewer uses cloned metadata, bounded thumbnail decoding/cache and one selected full image. It retains failed restores and releases image references when closed.
- Capture is blocked while the modal Archive window is open, consistent with other modal scrapbook actions.
- The one-time default-palette upgrade preserves custom palettes; explicit black remains available afterward.

The integrated build passed with no warnings or errors, and all 21 core checks passed. Native Windows rendering, input, layout and game interaction were not executed in the Linux build environment.
