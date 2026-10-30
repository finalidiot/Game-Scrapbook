# Game Scrapbook 0.1.5 — build report

Top-right snip delete button, 30 October 2026 (Perth).

## Change

Add a visible **×** button at the top-right of each board snip. Clicking it deletes that specific snip without first selecting it. The button uses the existing deletion and undo behaviour: deletion is immediate and **Ctrl+Z** restores the removed image during the current session.

Keyboard and toolbar Delete remain available. This update adds a UI entry point and shares the deletion method; it does not change the saved-board format or archive state.

## Verification

| Check | Result |
| --- | --- |
| Integrated Windows build and self-contained x64 publish | Passed: self-contained Windows x64 publish, no warnings or errors reported |
| Core durability/history/theme/archive checks | Prior 0.1.4 result retained: 21 passed; not rerun for this UI entry point |
| Executable version and package integrity | Recorded by the release packaging step |
| Native Windows launch and visual interaction | Not performed in this Linux environment |

No new tests were added for the delete button. The prior archive build report is preserved in BUILD_REPORT_0.1.4.md.

## Short Windows check

Close the previous app and launch v0.1.5. With two snips present, select one and click the other's top-right **×**. Confirm only the clicked snip is removed and **Ctrl+Z** restores it. Check dragging, resizing and the Archive button remain usable.
