using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameScrapbook.Core;
using GameScrapbook.Windows;

internal static class Program
{
    private static readonly List<object> Results = [];
    private static readonly Color Background = Color.FromRgb(36, 36, 36);
    private static int _passed;
    private static int _failed;

    [STAThread]
    private static int Main(string[] args)
    {
        string reportPath = args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "results.json")
            : args.Length == 2 && args[0] == "--report" ? Path.GetFullPath(args[1])
            : throw new ArgumentException("Usage: GameScrapbook.Export.Checks [--report path.json]");
        string fixture = Path.Combine(AppContext.BaseDirectory, "export-fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            Run("export crops outside empty canvas and contains image pixels only", () =>
            {
                var board = Board(Snip("red", 125, 60, 20, 10));
                BitmapSource image = Export(board);
                Check(image.PixelWidth == 20 && image.PixelHeight == 10, "tight image bounds");
                Pixel(image, 0, 0, Colors.Red); Pixel(image, 19, 9, Colors.Red);
                Check(image.IsFrozen, "output should be immutable");
            });
            Run("horizontal edge joins export without a control gutter", () =>
            {
                BitmapSource image = Export(Board(Snip("red", 100, 30, 10, 10), Snip("blue", 110, 30, 10, 10)));
                Check(image.PixelWidth == 20 && image.PixelHeight == 10, "joined dimensions");
                Pixel(image, 9, 5, Colors.Red); Pixel(image, 10, 5, Colors.Blue);
            });
            Run("vertical edge joins export without archive buttons or selection chrome", () =>
            {
                BitmapSource image = Export(Board(Snip("red", 8, 20, 10, 10), Snip("blue", 8, 30, 10, 10)));
                Check(image.PixelWidth == 10 && image.PixelHeight == 20, "joined dimensions exclude controls");
                Pixel(image, 5, 9, Colors.Red); Pixel(image, 5, 10, Colors.Blue);
            });
            Run("intentional gaps retain the opaque board background", () =>
            {
                BitmapSource image = Export(Board(Snip("red", 0, 0, 10, 10), Snip("blue", 20, 0, 10, 10)));
                Check(image.PixelWidth == 30 && image.PixelHeight == 10, "gap retained");
                Pixel(image, 15, 5, Background);
                var transparentBackground = BoardImageExporter.Create(Board(Snip("red", 0, 0, 5, 5), Snip("blue", 10, 0, 5, 5)), Load, Color.FromArgb(0, 10, 20, 30));
                Pixel(transparentBackground, 7, 2, Color.FromRgb(10, 20, 30));
            });
            Run("overlap follows document order rather than selection", () =>
            {
                BitmapSource image = Export(Board(Snip("red", 0, 0, 20, 20), Snip("blue", 5, 5, 10, 10)));
                Pixel(image, 2, 2, Colors.Red); Pixel(image, 7, 7, Colors.Blue);
            });
            Run("archives do not affect bounds and their images are never loaded", () =>
            {
                SnipRecord archived = Snip("missing", 90_000, 90_000, 100, 100); archived.IsArchived = true;
                int loaded = 0;
                BitmapSource image = BoardImageExporter.Create(Board(Snip("red", 10, 20, 10, 10), archived), snip => { loaded++; return Load(snip); }, Background);
                Check(loaded == 1 && image.PixelWidth == 10 && image.PixelHeight == 10, "archive exclusion");
            });
            Run("empty and archive-only boards fail before loading images", () =>
            {
                int loaded = 0;
                Expect<InvalidOperationException>(() => BoardImageExporter.Create(Board(), _ => { loaded++; return null; }, Background));
                var archived = Snip("red", 0, 0, 10, 10); archived.IsArchived = true;
                Expect<InvalidOperationException>(() => BoardImageExporter.Create(Board(archived), _ => { loaded++; return null; }, Background));
                Check(loaded == 0, "empty export must not load anything");
            });
            Run("a missing active image fails rather than exporting an incomplete board", () =>
            {
                Expect<InvalidDataException>(() => Export(Board(Snip("red", 0, 0, 10, 10), Snip("missing", 10, 0, 10, 10))));
            });
            Run("images retain aspect ratio in a mismatched legacy layout", () =>
            {
                SnipRecord snip = Snip("red", 0, 0, 10, 10); snip.PixelWidth = 20; snip.PixelHeight = 10;
                BitmapSource image = Export(Board(snip));
                Pixel(image, 5, 2, Colors.Red); Pixel(image, 5, 7, Background);
            });
            Run("board zoom does not change exported geometry or mutate the document", () =>
            {
                var board = Board(Snip("red", -10.5, 20.25, 10, 4), Snip("blue", -0.5, 20.25, 10, 4));
                board.Settings.BoardZoom = .25;
                string before = JsonSerializer.Serialize(board);
                BitmapSource smallZoom = Export(board);
                Check(JsonSerializer.Serialize(board) == before, "export mutated board");
                board.Settings.BoardZoom = 3;
                BitmapSource largeZoom = Export(board);
                Check(smallZoom.PixelWidth == 20 && smallZoom.PixelHeight == 4, "fractional/negative origins cropped correctly");
                Check(Bytes(smallZoom).SequenceEqual(Bytes(largeZoom)), "zoom affected pixels");
            });
            Run("invalid active geometry is rejected without image decoding", () =>
            {
                foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    var snip = Snip("red", invalid, 0, 10, 10);
                    Expect<InvalidDataException>(() => BoardImageExporter.Create(Board(snip), _ => throw new Exception("must not decode"), Background));
                }
                var empty = Snip("red", 0, 0, 0, 10);
                Expect<InvalidDataException>(() => BoardImageExporter.Create(Board(empty), _ => throw new Exception("must not decode"), Background));
            });
            Run("large dimensions and sparse-board pixel budgets fail before decoding", () =>
            {
                Expect<InvalidOperationException>(() => BoardImageExporter.Create(Board(Snip("red", 0, 0, 10, 10), Snip("blue", 40_000, 0, 10, 10)), _ => throw new Exception("must not decode"), Background));
                Expect<InvalidOperationException>(() => BoardImageExporter.Create(Board(Snip("red", 0, 0, 6000, 6000)), _ => throw new Exception("must not decode"), Background));
            });
            Run("source pixel budget rejects a lazy oversized source before rendering", () =>
            {
                BitmapSource tiny = Load(Snip("red", 0, 0, 1, 1))!;
                var oversized = new TransformedBitmap(tiny, new ScaleTransform(8001, 8001));
                Check((long)oversized.PixelWidth * oversized.PixelHeight > BoardImageExporter.MaximumSourcePixels, "oversized fixture");
                Expect<InvalidOperationException>(() => BoardImageExporter.Create(Board(Snip("red", 0, 0, 10, 10)), _ => oversized, Background));
            });
            Run("declared source budget is checked before image loading", () =>
            {
                SnipRecord large = Snip("red", 0, 0, 10, 10); large.PixelWidth = large.PixelHeight = 8001;
                Expect<InvalidOperationException>(() => BoardImageExporter.Create(Board(large), _ => throw new Exception("must not decode"), Background));
            });
            Run("duplicate immutable image files are decoded once and reused", () =>
            {
                int loaded = 0;
                BitmapSource image = BoardImageExporter.Create(Board(Snip("red", 0, 0, 10, 10), Snip("red", 10, 0, 10, 10)), snip => { loaded++; return Load(snip); }, Background);
                Check(loaded == 1 && image.PixelWidth == 20, "duplicate source should be reused");
                Pixel(image, 15, 5, Colors.Red);
            });
            Run("PNG encoding round-trips dimensions and exact flat colours", () =>
            {
                BitmapSource image = Export(Board(Snip("red", 10, 20, 40, 30), Snip("blue", 50, 20, 40, 30)));
                string path = Path.Combine(fixture, "board.png"); BoardImageExporter.Save(image, path, BoardImageFormat.Png);
                byte[] file = File.ReadAllBytes(path);
                Check(file.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "PNG signature");
                BitmapSource reopened = Open(path);
                Check(reopened.PixelWidth == 80 && reopened.PixelHeight == 30, "PNG dimensions");
                Pixel(reopened, 20, 15, Colors.Red); Pixel(reopened, 60, 15, Colors.Blue);
            });
            Run("JPG encoding round-trips dimensions and readable flat colours", () =>
            {
                BitmapSource image = Export(Board(Snip("red", 0, 0, 40, 30), Snip("blue", 40, 0, 40, 30)));
                string path = Path.Combine(fixture, "board.jpg"); BoardImageExporter.Save(image, path, BoardImageFormat.Jpeg);
                byte[] file = File.ReadAllBytes(path);
                Check(file[0] == 255 && file[1] == 216 && file[2] == 255, "JPG signature");
                BitmapSource reopened = Open(path);
                Check(reopened.PixelWidth == 80 && reopened.PixelHeight == 30, "JPG dimensions");
                Pixel(reopened, 20, 15, Colors.Red, 8); Pixel(reopened, 60, 15, Colors.Blue, 8);
            });
            Run("successful overwrite replaces the complete image and leaves no temporary file", () =>
            {
                string path = Path.Combine(fixture, "replace.png"); File.WriteAllText(path, "old file");
                BoardImageExporter.Save(Export(Board(Snip("blue", 0, 0, 7, 9))), path, BoardImageFormat.Png);
                BitmapSource reopened = Open(path);
                Check(reopened.PixelWidth == 7 && reopened.PixelHeight == 9, "replacement encoded");
                Pixel(reopened, 3, 4, Colors.Blue); NoTemporaryFiles(fixture);
            });
            Run("failed replacement preserves a locked destination and removes its temporary file", () =>
            {
                string path = Path.Combine(fixture, "locked.png"); byte[] previous = [1, 2, 3, 4, 5]; File.WriteAllBytes(path, previous);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    ExpectIo(() => BoardImageExporter.Save(Export(Board(Snip("red", 0, 0, 10, 10))), path, BoardImageFormat.Png));
                Check(File.ReadAllBytes(path).SequenceEqual(previous), "existing destination was changed");
                NoTemporaryFiles(fixture);
            });
            Run("format and extension errors preserve an existing destination", () =>
            {
                string path = Path.Combine(fixture, "wrong.png"); byte[] previous = [9, 8, 7]; File.WriteAllBytes(path, previous);
                BitmapSource image = Export(Board(Snip("red", 0, 0, 10, 10)));
                Expect<ArgumentException>(() => BoardImageExporter.Save(image, path, BoardImageFormat.Jpeg));
                Expect<ArgumentOutOfRangeException>(() => BoardImageExporter.Save(image, path, (BoardImageFormat)99));
                Check(File.ReadAllBytes(path).SequenceEqual(previous), "extension error changed destination"); NoTemporaryFiles(fixture);
            });
            Run("a directory collision fails without leaving partial image output", () =>
            {
                string path = Path.Combine(fixture, "directory.jpg"); Directory.CreateDirectory(path);
                ExpectIo(() => BoardImageExporter.Save(Export(Board(Snip("red", 0, 0, 10, 10))), path, BoardImageFormat.Jpeg));
                Check(Directory.Exists(path), "existing directory was disturbed"); NoTemporaryFiles(fixture);
            });

            string directory = Path.GetDirectoryName(reportPath)!;
            Directory.CreateDirectory(directory);
            BitmapSource sample = BoardImageExporter.Create(Board(Snip("red", 120, 80, 180, 100), Snip("blue", 300, 80, 180, 100), Snip("green", 120, 180, 360, 80)), Load, Background);
            BoardImageExporter.Save(sample, Path.Combine(directory, "sample-export.jpg"), BoardImageFormat.Jpeg);
            BoardImageExporter.Save(sample, Path.Combine(directory, "sample-export.png"), BoardImageFormat.Png);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new
            {
                status = _failed == 0 ? "passed" : "failed", passed = _passed, failed = _failed,
                boundary = "Actual linked BoardImageExporter with WPF offscreen rendering and real JPG/PNG encoding; synthetic in-memory images and isolated fixture files only. No user data or live UI.",
                cases = Results
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed.");
            return _failed == 0 ? 0 : 1;
        }
        finally
        {
            string allowed = Path.GetFullPath(AppContext.BaseDirectory);
            if (!Path.GetFullPath(fixture).StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup escaped its fixture directory.");
            if (Directory.Exists(fixture)) Directory.Delete(fixture, recursive: true);
        }
    }

    private static BoardDocument Board(params SnipRecord[] snips) => new() { Snips = snips.ToList() };
    private static SnipRecord Snip(string name, double x, double y, double width, double height) => new()
    {
        ImageFile = name + ".png", X = x, Y = y, Width = width, Height = height,
        PixelWidth = Math.Max(1, (int)Math.Ceiling(width)), PixelHeight = Math.Max(1, (int)Math.Ceiling(height))
    };
    private static BitmapSource Export(BoardDocument board) => BoardImageExporter.Create(board, Load, Background);
    private static BitmapSource? Load(SnipRecord snip)
    {
        Color colour = snip.ImageFile switch { "red.png" => Colors.Red, "blue.png" => Colors.Blue, "green.png" => Colors.Green, _ => default };
        if (snip.ImageFile == "missing.png") return null;
        int width = snip.PixelWidth, height = snip.PixelHeight;
        byte[] pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = colour.B; pixels[i + 1] = colour.G; pixels[i + 2] = colour.R; pixels[i + 3] = 255; }
        BitmapSource image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        image.Freeze(); return image;
    }
    private static BitmapSource Open(string path)
    {
        using var stream = File.OpenRead(path);
        BitmapSource image = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        image.Freeze(); return image;
    }
    private static byte[] Bytes(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        byte[] bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(bytes, converted.PixelWidth * 4, 0); return bytes;
    }
    private static void Pixel(BitmapSource image, int x, int y, Color expected, int tolerance = 0)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        byte[] pixel = new byte[4]; converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        Check(Math.Abs(pixel[0] - expected.B) <= tolerance && Math.Abs(pixel[1] - expected.G) <= tolerance &&
            Math.Abs(pixel[2] - expected.R) <= tolerance && pixel[3] == 255,
            $"Unexpected pixel at {x},{y}: BGRA {string.Join(',', pixel)}; expected {expected}");
    }
    private static void NoTemporaryFiles(string root) => Check(Directory.GetFiles(root, ".GameScrapbook-export-*.tmp").Length == 0, "temporary export left behind");
    private static void Run(string name, Action test)
    {
        try { test(); _passed++; Results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failed++; Results.Add(new { name, passed = false, error = ex.GetBaseException().Message }); Console.WriteLine("FAIL " + name + ": " + ex.GetBaseException().Message); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static void ExpectIo(Action action)
    {
        try { action(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Expected file write/replacement failure");
    }
}
