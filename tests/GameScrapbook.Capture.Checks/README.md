# Capture lifecycle checks

Run on Windows with the .NET 10 SDK, from the repository root:

```powershell
dotnet run --project tests/GameScrapbook.Capture.Checks -c Release
```

This project compiles the real `CaptureCoordinator.cs` as linked source. The
platform helpers, screenshot backend and overlay windows are simulated inside
the check assembly. It uses an unshown WPF owner and dispatcher, but creates no
native window, moves no real cursor, registers no real hotkey and reads no user
data. The application project does not reference these test helpers.

The checks exercise the actual coordinator's selection, drag, drop, cancellation
and disposal paths, including foreground ownership, original cursor clipping,
return ordering, retries after an invalid drop or failed save, target identity,
Escape/right-click and swapped mouse buttons. Results, including the tested
coordinator source hash, are written to `results.json` beside the check executable
under `bin/`, which is ignored by Git.

To choose another report path, pass `--report` after the .NET argument separator:

```powershell
dotnet run --project tests/GameScrapbook.Capture.Checks -c Release -- --report artifacts/capture-checks.json
```

These checks do not validate the actual native overlay, physical input routing,
live capture, game focus behavior or Nivalis Nights compatibility. Use the
[Windows playtest checklist](../../docs/WINDOWS_PLAYTEST.md) for those checks.
