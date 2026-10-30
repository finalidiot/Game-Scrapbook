using GameScrapbook.Core;
using System.Text.Json.Nodes;

byte[] pixelPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
string root = Path.Combine(Path.GetTempPath(), "GameScrapbook-CoreChecks-" + Guid.NewGuid().ToString("N"));
int passed = 0;
try
{
    Run("new board is saveable without creating files", () =>
    {
        var store = NewStore();
        var load = store.Load();
        Check(load.CanSave && !load.RecoveredFromBackup && load.Document.Snips.Count == 0, "new board state");
        Check(load.Document.Settings.BackgroundColor == "#242424" &&
              load.Document.Settings.ThemeDefaultsVersion == ThemeColors.CurrentDefaultsVersion, "new board defaults not initialized");
        Check(!Directory.Exists(store.RootDirectory), "read-only load unexpectedly wrote files");
    });

    Run("capture image and non-integer board geometry survive restart", () =>
    {
        var store = NewStore();
        StoredImage image = store.SaveImage(pixelPng);
        var board = NewBoard(image);
        board.Settings.WindowLeft = -1920;
        board.Settings.WindowTop = -200;
        board.Settings.ReturnCursor = false;
        board.Settings.BoardZoom = 1.25;
        store.Save(board);
        var load = new BoardStore(store.RootDirectory).Load();
        Check(load.CanSave && load.Warnings.Count == 0, "reload failed");
        Check(load.Document.Snips[0].X == -15.75 && load.Document.Snips[0].Width == 233.5, "geometry changed");
        Check(load.Document.Settings.WindowLeft == -1920 && !load.Document.Settings.ReturnCursor, "settings changed");
        Check(File.ReadAllBytes(store.GetImagePath(image.FileName)).SequenceEqual(pixelPng), "PNG changed");
    });

    Run("delete, undo, redo and restart retain correct board and immutable image", () =>
    {
        var store = NewStore();
        StoredImage image = store.SaveImage(pixelPng);
        var board = NewBoard(image);
        store.Save(board);
        var history = new BoardHistory();
        history.Record(board);
        board.Snips.Clear();
        store.Save(board);
        Check(new BoardStore(store.RootDirectory).Load().Document.Snips.Count == 0, "delete did not persist");
        board.Settings.ReturnCursor = false;
        board = history.Undo(board)!;
        Check(board.Snips.Count == 1 && !board.Settings.ReturnCursor, "undo lost snippet or changed preferences");
        store.Save(board);
        Check(new BoardStore(store.RootDirectory).Load().Document.Snips.Count == 1, "undo did not persist");
        board = history.Redo(board)!;
        Check(board.Snips.Count == 0, "redo failed");
        Check(File.Exists(store.GetImagePath(image.FileName)), "undo image was deleted");
    });

    Run("corrupt primary recovers backup and preserves damaged file on next save", () =>
    {
        var store = NewStore();
        var board = NewBoard(store.SaveImage(pixelPng));
        store.Save(board);
        board.Snips[0].X = 900;
        store.Save(board);
        File.WriteAllText(store.ManifestPath, "{this is corrupted");
        var recoveredStore = new BoardStore(store.RootDirectory);
        BoardLoadResult load = recoveredStore.Load();
        Check(load.CanSave && load.RecoveredFromBackup && load.Document.Snips[0].X == -15.75, "backup not recovered");
        recoveredStore.Save(load.Document);
        Check(Directory.GetFiles(store.RootDirectory, "board.unreadable.*.json").Length == 1, "bad primary not preserved");
        Check(new BoardStore(store.RootDirectory).Load().Warnings.Count == 0, "recovered save not readable");
    });

    Run("unrecoverable metadata disables writes without replacing history", () =>
    {
        var store = NewStore();
        Directory.CreateDirectory(store.RootDirectory);
        File.WriteAllText(store.ManifestPath, "bad primary");
        File.WriteAllText(store.BackupPath, "bad backup");
        var load = store.Load();
        Check(!load.CanSave && load.Warnings.Count >= 3, "corruption did not block writes");
        Expect<InvalidOperationException>(() => store.Save(new BoardDocument()));
        Expect<InvalidOperationException>(() => store.SaveImage(pixelPng));
        Check(File.ReadAllText(store.ManifestPath) == "bad primary", "original was overwritten");
    });

    Run("incomplete schema cannot silently become an empty board", () =>
    {
        var store = NewStore();
        var board = NewBoard(store.SaveImage(pixelPng));
        store.Save(board);
        store.Save(board);
        File.WriteAllText(store.ManifestPath, "{}");
        var load = new BoardStore(store.RootDirectory).Load();
        Check(load.RecoveredFromBackup && load.Document.Snips.Count == 1, "incomplete schema silently erased snippets");
    });

    Run("missing or damaged images remain in metadata with warnings", () =>
    {
        var store = NewStore();
        StoredImage image = store.SaveImage(pixelPng);
        var board = NewBoard(image);
        store.Save(board);
        File.Delete(store.GetImagePath(image.FileName));
        var load = new BoardStore(store.RootDirectory).Load();
        Check(load.Document.Snips.Count == 1 && load.Warnings.Count == 1, "missing image was discarded");
        File.WriteAllBytes(store.GetImagePath(image.FileName), [1, 2, 3]);
        load = new BoardStore(store.RootDirectory).Load();
        Check(load.Document.Snips.Count == 1 && load.Warnings.Count == 1, "damaged image was discarded");
    });

    Run("path traversal and Windows special filenames are rejected", () =>
    {
        var store = NewStore();
        foreach (string path in new[] { "../lost.png", "..\\lost.png", "/tmp/a.png", "C:\\a.png", "a.png:stream", "CON.png", "lpt1.png", "a..b.png", ".png" })
            Expect<InvalidDataException>(() => store.GetImagePath(path));
    });

    Run("invalid dimensions, NaN geometry, duplicate identifiers and unknown versions are rejected", () =>
    {
        var store = NewStore();
        var board = NewBoard(store.SaveImage(pixelPng));
        board.Snips[0].Width = 0;
        Expect<InvalidDataException>(() => store.Save(board));
        board.Snips[0].Width = 100;
        board.Snips[0].X = double.NaN;
        Expect<InvalidDataException>(() => store.Save(board));
        board.Snips[0].X = 0;
        board.Snips.Add(board.Snips[0].Clone());
        Expect<InvalidDataException>(() => store.Save(board));
        board.Snips.RemoveAt(1);
        board.Version = 999;
        Expect<InvalidDataException>(() => store.Save(board));
        Expect<InvalidDataException>(() => store.SaveImage(new byte[50]));
        byte[] damagedHeader = (byte[])pixelPng.Clone();
        damagedHeader[19] = 2;
        Expect<InvalidDataException>(() => store.SaveImage(damagedHeader));
        Expect<InvalidDataException>(() => store.SaveImage(pixelPng[..33]));
        Expect<InvalidDataException>(() => store.SaveImage(pixelPng[..^4]));
    });

    Run("history snapshots do not alias live records and respect capacity", () =>
    {
        var board = NewBoard(new StoredImage("image.png", 1, 1));
        var history = new BoardHistory(2);
        history.Record(board);
        board.Snips[0].X = 10;
        history.Record(board);
        board.Snips[0].X = 20;
        history.Record(board);
        board.Snips[0].X = 30;
        board = history.Undo(board)!;
        Check(board.Snips[0].X == 20, "first undo was aliased");
        board = history.Undo(board)!;
        Check(board.Snips[0].X == 10 && !history.CanUndo, "history capacity not enforced");
        history.Record(board);
        Check(!history.CanRedo, "new mutation did not clear redo");
    });

    Run("legacy boards without colour preferences retain snips and receive readable defaults", () =>
    {
        var store = NewStore();
        var board = NewBoard(store.SaveImage(pixelPng));
        store.Save(board);
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(store.ManifestPath))!;
        JsonObject settings = manifest["settings"]!.AsObject();
        settings.Remove("backgroundColor");
        settings.Remove("textColor");
        settings.Remove("accentColor");
        settings.Remove("themeDefaultsVersion");
        File.WriteAllText(store.ManifestPath, manifest.ToJsonString());
        var load = new BoardStore(store.RootDirectory).Load();
        Check(load.CanSave && !load.RecoveredFromBackup && load.Warnings.Count == 0, "legacy board required recovery");
        Check(load.Document.Snips.Count == 1 && load.Document.Snips[0].X == board.Snips[0].X, "legacy snippet changed");
        Check(load.Document.Settings.BackgroundColor == ThemeColors.DefaultBackground &&
              load.Document.Settings.TextColor == ThemeColors.DefaultText &&
              load.Document.Settings.AccentColor == ThemeColors.DefaultAccent, "legacy defaults missing");
    });

    Run("custom colours persist through restart and survive snippet undo and redo", () =>
    {
        var store = NewStore();
        var board = NewBoard(store.SaveImage(pixelPng));
        var history = new BoardHistory();
        history.Record(board);
        board.Snips.Clear();
        board.Settings.BackgroundColor = " ffffff ";
        board.Settings.TextColor = "#12213a";
        board.Settings.AccentColor = "#7d44c9";
        store.Save(board);
        board = new BoardStore(store.RootDirectory).Load().Document;
        Check(board.Settings.BackgroundColor == "#FFFFFF" && board.Settings.TextColor == "#12213A" &&
              board.Settings.AccentColor == "#7D44C9", "custom colours did not persist or normalize");
        board = history.Undo(board)!;
        Check(board.Snips.Count == 1 && board.Settings.AccentColor == "#7D44C9" &&
              board.Settings.BackgroundColor == "#FFFFFF" && board.Settings.TextColor == "#12213A", "undo reverted theme");
        board = history.Redo(board)!;
        Check(board.Snips.Count == 0 && board.Settings.AccentColor == "#7D44C9", "redo reverted theme");
    });

    Run("malformed colour values fall back without losing board data or triggering backup recovery", () =>
    {
        var store = NewStore();
        var board = NewBoard(store.SaveImage(pixelPng));
        store.Save(board);
        string original = File.ReadAllText(store.ManifestPath);
        foreach (string malformed in new[] { "null", "123", "true", "[]", "{\"wrong\":1}", "\"transparent\"", "\"#FFFFFF00\"" })
        {
            JsonNode manifest = JsonNode.Parse(original)!;
            foreach (string name in new[] { "backgroundColor", "textColor", "accentColor" })
                manifest["settings"]![name] = JsonNode.Parse(malformed);
            File.WriteAllText(store.ManifestPath, manifest.ToJsonString());
            var load = new BoardStore(store.RootDirectory).Load();
            Check(load.CanSave && !load.RecoveredFromBackup && load.Warnings.Count == 0, "bad theme triggered recovery: " + malformed);
            Check(load.Document.Snips.Count == 1 && load.Document.Snips[0].X == board.Snips[0].X, "bad theme damaged snips");
            Check(load.Document.Settings.BackgroundColor == ThemeColors.DefaultBackground &&
                  load.Document.Settings.TextColor == ThemeColors.DefaultText &&
                  load.Document.Settings.AccentColor == ThemeColors.DefaultAccent, "bad theme defaults missing");
        }
        Check(!File.Exists(store.BackupPath), "theme recovery created an unexpected backup");
        Check(File.ReadAllBytes(store.GetImagePath(board.Snips[0].ImageFile)).SequenceEqual(pixelPng), "theme recovery changed image");
    });

    Run("unreadable saved text pairs recover to dark grey and white while retaining the chosen accent", () =>
    {
        var store = NewStore();
        store.Save(NewBoard(store.SaveImage(pixelPng)));
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(store.ManifestPath))!;
        manifest["settings"]!["backgroundColor"] = "#FFFFFF";
        manifest["settings"]!["textColor"] = "#777777";
        manifest["settings"]!["accentColor"] = "#b74be8";
        File.WriteAllText(store.ManifestPath, manifest.ToJsonString());
        var load = new BoardStore(store.RootDirectory).Load();
        Check(load.CanSave && !load.RecoveredFromBackup && load.Document.Snips.Count == 1, "low contrast lost board");
        Check(load.Document.Settings.BackgroundColor == ThemeColors.DefaultBackground &&
              load.Document.Settings.TextColor == ThemeColors.DefaultText &&
              load.Document.Settings.AccentColor == "#B74BE8", "low contrast repair changed valid accent");
    });

    Run("RGB validation and contrast calculations cover both sides of the readable text threshold", () =>
    {
        Check(ThemeColors.TryNormalize(" aBcDeF ", out string normalized) && normalized == "#ABCDEF", "normalization failed");
        foreach (string? invalid in new string?[] { null, "", "white", "#FFF", "#FFFFFFFF", "#GG0000", "##FFFFFF" })
            Check(!ThemeColors.TryNormalize(invalid, out _), "invalid colour accepted");
        Check(Math.Abs(ThemeColors.ContrastRatio("#000000", "#FFFFFF") - 21) < 1e-10, "black-white contrast wrong");
        Check(ThemeColors.ContrastRatio("#1976D2", "#1976D2") == 1, "same-colour contrast wrong");
        Check(ThemeColors.ContrastRatio("#FFFFFF", "#777777") < 4.5 &&
              ThemeColors.ContrastRatio("#FFFFFF", "#767676") > 4.5, "contrast threshold is inaccurate");
        Check(ThemeColors.ReadableForeground("#000000") == "#FFFFFF" &&
              ThemeColors.ReadableForeground("#FFFFFF") == "#000000", "foreground selection wrong");
        Check(ThemeColors.ContrastRatio(ThemeColors.DefaultAccent, ThemeColors.ReadableForeground(ThemeColors.DefaultAccent)) >= 4.5,
              "default accent does not have readable text");
    });

    Run("legacy snippets without archive fields stay active and retain their image and placement", () =>
    {
        var store = NewStore();
        var board = NewBoard(store.SaveImage(pixelPng));
        store.Save(board);
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(store.ManifestPath))!;
        JsonObject snip = manifest["snips"]![0]!.AsObject();
        snip.Remove("isArchived");
        snip.Remove("archivedUtc");
        File.WriteAllText(store.ManifestPath, manifest.ToJsonString());
        var load = new BoardStore(store.RootDirectory).Load();
        SnipRecord reopened = load.Document.Snips.Single();
        Check(load.CanSave && !load.RecoveredFromBackup && load.Warnings.Count == 0, "legacy archive fields caused recovery");
        Check(!reopened.IsArchived && reopened.ArchivedUtc is null, "legacy snippet was archived");
        Check(reopened.Id == board.Snips[0].Id && reopened.ImageFile == board.Snips[0].ImageFile &&
              reopened.X == -15.75 && reopened.Y == 44.125 && reopened.Width == 233.5, "legacy snippet changed");
    });

    Run("archive and restore survive restart without copying images or changing saved placement", () =>
    {
        var store = NewStore();
        StoredImage firstImage = store.SaveImage(pixelPng);
        var board = NewBoard(firstImage);
        SnipRecord second = NewBoard(store.SaveImage(pixelPng)).Snips[0];
        second.X = 600;
        board.Snips.Add(second);
        string archivedId = board.Snips[0].Id;
        DateTimeOffset archivedAt = DateTimeOffset.Parse("2026-10-30T08:15:00+00:00");
        board.Snips[0].IsArchived = true;
        board.Snips[0].ArchivedUtc = archivedAt;
        store.Save(board);
        var load = new BoardStore(store.RootDirectory).Load();
        Check(load.CanSave && load.Warnings.Count == 0 && load.Document.Snips.Count == 2, "archive lost board data");
        Check(load.Document.Snips.Count(s => s.IsArchived) == 1 && load.Document.Snips.Count(s => !s.IsArchived) == 1,
              "archive changed an unrelated snippet");
        SnipRecord archived = load.Document.Snips.Single(s => s.Id == archivedId);
        Check(archived.ArchivedUtc == archivedAt && archived.ImageFile == firstImage.FileName &&
              archived.X == -15.75 && archived.Y == 44.125 && archived.Width == 233.5 && archived.Height == 233.5,
              "archive changed image identity, time or placement");
        archived.IsArchived = false;
        archived.ArchivedUtc = null;
        store.Save(load.Document);
        var restored = new BoardStore(store.RootDirectory).Load().Document;
        Check(restored.Snips.Count == 2 && restored.Snips.All(s => !s.IsArchived && s.ArchivedUtc is null),
              "restore did not persist or duplicated a snippet");
        Check(restored.Snips.Single(s => s.Id == archivedId).X == -15.75, "restore lost original position");
        Check(Directory.GetFiles(store.ImagesDirectory, "*.png").Length == 2, "archive or restore copied an image");
        Check(File.ReadAllBytes(store.GetImagePath(firstImage.FileName)).SequenceEqual(pixelPng), "archive changed PNG bytes");
    });

    Run("archive and restore undo and redo keep one independent record and current preferences", () =>
    {
        var board = NewBoard(new StoredImage("image.png", 1, 1));
        var history = new BoardHistory();
        string id = board.Snips[0].Id;
        DateTimeOffset archivedAt = DateTimeOffset.Parse("2026-10-30T08:15:00+00:00");
        history.Record(board);
        board.Snips[0].IsArchived = true;
        board.Snips[0].ArchivedUtc = archivedAt;
        board.Settings.AccentColor = "#7D44C9";
        board.Settings.ReturnCursor = false;
        board.Settings.ThemeDefaultsVersion = ThemeColors.CurrentDefaultsVersion;
        board = history.Undo(board)!;
        Check(board.Snips.Count == 1 && board.Snips[0].Id == id && !board.Snips[0].IsArchived &&
              board.Snips[0].ArchivedUtc is null, "archive undo failed");
        board = history.Redo(board)!;
        Check(board.Snips.Count == 1 && board.Snips[0].IsArchived && board.Snips[0].ArchivedUtc == archivedAt,
              "archive redo failed");
        history.Record(board);
        board.Snips[0].IsArchived = false;
        board.Snips[0].ArchivedUtc = null;
        board = history.Undo(board)!;
        Check(board.Snips[0].IsArchived && board.Snips[0].ArchivedUtc == archivedAt, "restore undo was aliased");
        board = history.Redo(board)!;
        Check(board.Snips.Count == 1 && board.Snips[0].Id == id && !board.Snips[0].IsArchived &&
              board.Snips[0].ArchivedUtc is null && board.Snips[0].X == -15.75 && board.Snips[0].Width == 233.5,
              "restore redo changed identity or geometry");
        Check(!board.Settings.ReturnCursor && board.Settings.AccentColor == "#7D44C9" &&
              board.Settings.ThemeDefaultsVersion == ThemeColors.CurrentDefaultsVersion, "archive history reverted preferences");
    });

    Run("the former exact default palette upgrades once and a later black choice persists", () =>
    {
        var store = NewStore();
        var board = NewBoard(store.SaveImage(pixelPng));
        store.Save(board);
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(store.ManifestPath))!;
        manifest["settings"]!["backgroundColor"] = "#000000";
        manifest["settings"]!.AsObject().Remove("themeDefaultsVersion");
        string oldManifest = manifest.ToJsonString();
        File.WriteAllText(store.ManifestPath, oldManifest);
        var load = new BoardStore(store.RootDirectory).Load();
        Check(load.CanSave && !load.RecoveredFromBackup && load.Warnings.Count == 0, "palette upgrade triggered recovery");
        Check(load.Document.Settings.BackgroundColor == "#242424" &&
              load.Document.Settings.TextColor == "#FFFFFF" && load.Document.Settings.AccentColor == "#1976D2" &&
              load.Document.Settings.ThemeDefaultsVersion == ThemeColors.CurrentDefaultsVersion, "old default was not upgraded");
        Check(File.ReadAllText(store.ManifestPath) == oldManifest, "load unexpectedly wrote the upgraded palette");
        Check(load.Document.Snips.Single().Id == board.Snips[0].Id && load.Document.Snips[0].X == -15.75,
              "palette upgrade changed snippets");
        store.Save(load.Document);
        board = new BoardStore(store.RootDirectory).Load().Document;
        Check(board.Settings.BackgroundColor == "#242424" && board.Settings.ThemeDefaultsVersion == 1, "upgrade did not persist");
        board.Settings.BackgroundColor = "#000000";
        store.Save(board);
        board = new BoardStore(store.RootDirectory).Load().Document;
        Check(board.Settings.BackgroundColor == "#000000" && board.Settings.ThemeDefaultsVersion == 1,
              "a deliberate black choice was upgraded again");
    });

    Run("the default palette upgrade preserves any valid custom background text or accent", () =>
    {
        foreach (var colours in new[]
        {
            (Background: "#181818", Text: "#FFFFFF", Accent: "#1976D2"),
            (Background: "#000000", Text: "#DDDDDD", Accent: "#1976D2"),
            (Background: "#000000", Text: "#FFFFFF", Accent: "#9F5AE5")
        })
        {
            var store = NewStore();
            store.Save(NewBoard(store.SaveImage(pixelPng)));
            JsonNode manifest = JsonNode.Parse(File.ReadAllText(store.ManifestPath))!;
            JsonObject settings = manifest["settings"]!.AsObject();
            settings["backgroundColor"] = colours.Background;
            settings["textColor"] = colours.Text;
            settings["accentColor"] = colours.Accent;
            settings.Remove("themeDefaultsVersion");
            File.WriteAllText(store.ManifestPath, manifest.ToJsonString());
            var load = new BoardStore(store.RootDirectory).Load();
            Check(load.CanSave && !load.RecoveredFromBackup && load.Warnings.Count == 0, "custom palette needed recovery");
            Check(load.Document.Settings.BackgroundColor == colours.Background && load.Document.Settings.TextColor == colours.Text &&
                  load.Document.Settings.AccentColor == colours.Accent && load.Document.Settings.ThemeDefaultsVersion == 1,
                  "default upgrade replaced a custom colour");
            store.Save(load.Document);
            Check(new BoardStore(store.RootDirectory).Load().Document.Settings.BackgroundColor == colours.Background,
                  "custom background changed after upgraded settings were saved");
        }
    });

    Run("a new board can choose black before its first save without triggering a legacy upgrade", () =>
    {
        var store = NewStore();
        BoardDocument board = store.Load().Document;
        board.Settings.BackgroundColor = "#000000";
        store.Save(board);
        var load = new BoardStore(store.RootDirectory).Load();
        Check(load.CanSave && load.Document.Settings.BackgroundColor == "#000000" &&
              load.Document.Settings.ThemeDefaultsVersion == 1, "first custom black choice was replaced");
    });

    Console.WriteLine($"PASS: {passed} core checks.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("FAIL: " + ex);
    return 1;
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

BoardStore NewStore() => new(Path.Combine(root, Guid.NewGuid().ToString("N")));
BoardDocument NewBoard(StoredImage image) => new()
{
    Snips = [new SnipRecord
    {
        ImageFile = image.FileName, X = -15.75, Y = 44.125,
        Width = 233.5, Height = 233.5, PixelWidth = image.PixelWidth, PixelHeight = image.PixelHeight
    }]
};
void Run(string name, Action check)
{
    check();
    passed++;
    Console.WriteLine("PASS " + name);
}
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Expect<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
