# Board image export checks

Run on Windows with the .NET 10 SDK, from the repository root:

```powershell
dotnet run --project tests/GameScrapbook.Export.Checks -c Release -- --report artifacts/export-checks.json
```

The project links the actual `BoardImageExporter.cs` service and renders only
synthetic in-memory images. It verifies tight bounds, horizontal/vertical joins,
background gaps, layering, archive exclusion, missing-image failure, aspect
ratio, zoom independence, invalid geometry and memory limits. Real JPG and PNG
encoding is decoded and checked; overwrite and failed-replacement cases verify
that existing files remain intact and temporary files are removed.

All fixture files are confined beneath the ignored test `bin/` directory and
removed after the checks. The report directory receives `sample-export.jpg` and
`sample-export.png` using synthetic flat-colour snippets for visual inspection.
No app window, native input, live game or user board data is used.
