# Layout checks

Run from the repository root with the .NET 10 SDK:

```powershell
dotnet run --project tests/GameScrapbook.Layout.Checks -c Release
```

These platform-independent checks exercise real snapping and packing geometry.
They cover adjacent-edge joins, alignment, proximity, thresholds, deterministic
ties, self/archive exclusion, malformed bounds, clamping, fractional geometry,
and zoom-adjusted tolerances. Packing checks verify identity/size preservation,
unchanged archived records, connected edge-touching placement, non-overlap,
determinism and a compact rectangular arrangement.

`BoardSnapping` accepts image bounds supplied by its caller and never mutates
them. The UI chooses the screen-space tolerance and handles temporary bypass.
`BoardPacking` returns positions in original input order, anchored at the origin
for active snips; the UI records that operation for undo and persistence.

These checks do not render or interact with the Windows interface and do not
verify image encoding or export dialogs.
