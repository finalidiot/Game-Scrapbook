using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameScrapbook.Core;

namespace GameScrapbook.Windows;

public enum BoardImageFormat { Jpeg, Png }

/// <summary>Composes saved images, independently of board zoom, selection and UI controls.</summary>
public static class BoardImageExporter
{
    public const long MaximumOutputPixels = 32_000_000;
    public const int MaximumOutputDimension = 32_767;
    public const long MaximumSourcePixels = 64_000_000;

    /// <summary>
    /// Export dimensions use board units at 96 DPI. Active snips keep their positions,
    /// sizes and document order, with the outside empty canvas cropped away.
    /// </summary>
    public static BitmapSource Create(BoardDocument document,
        Func<SnipRecord, BitmapSource?> imageLoader, Color background)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(imageLoader);
        if (document.Snips is null || document.Snips.Count > BoardValidation.MaximumSnips)
            throw new InvalidDataException("The board has invalid or too many snips to export.");

        var active = new List<SnipRecord>();
        var declaredImages = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        long declaredSourcePixels = 0;
        Rect bounds = Rect.Empty;
        foreach (SnipRecord snip in document.Snips)
        {
            if (snip is null) throw new InvalidDataException("The board contains an invalid snip.");
            if (snip.IsArchived) continue;
            if (!double.IsFinite(snip.X) || !double.IsFinite(snip.Y) ||
                !double.IsFinite(snip.Width) || !double.IsFinite(snip.Height) ||
                snip.Width <= 0 || snip.Height <= 0 ||
                !double.IsFinite(snip.X + snip.Width) || !double.IsFinite(snip.Y + snip.Height))
                throw new InvalidDataException("A snip has invalid placement or dimensions and cannot be exported.");
            BoardValidation.ValidateImageFileName(snip.ImageFile);
            BoardValidation.ValidateImageDimensions(snip.PixelWidth, snip.PixelHeight);
            long declaredPixels = (long)snip.PixelWidth * snip.PixelHeight;
            declaredImages.TryGetValue(snip.ImageFile, out long previousPixels);
            if (declaredPixels > previousPixels)
            {
                declaredSourcePixels += declaredPixels - previousPixels;
                declaredImages[snip.ImageFile] = declaredPixels;
            }
            if (declaredSourcePixels > MaximumSourcePixels)
                throw new InvalidOperationException("The board contains too much image data to export at once. Archive some snips and try again.");
            bounds.Union(new Rect(snip.X, snip.Y, snip.Width, snip.Height));
            active.Add(snip);
        }
        if (active.Count == 0)
            throw new InvalidOperationException("Add or restore a snip to the board before exporting.");

        double roundedWidth = Math.Ceiling(bounds.Width);
        double roundedHeight = Math.Ceiling(bounds.Height);
        if (!double.IsFinite(roundedWidth) || !double.IsFinite(roundedHeight) ||
            roundedWidth < 1 || roundedHeight < 1 ||
            roundedWidth > MaximumOutputDimension || roundedHeight > MaximumOutputDimension ||
            roundedWidth * roundedHeight > MaximumOutputPixels)
            throw new InvalidOperationException("The board is too large to export. Bring the snips closer together or resize them, then try again (maximum 32 million pixels and 32,767 pixels per edge).");

        int width = (int)roundedWidth, height = (int)roundedHeight;
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        var opaqueBackground = new SolidColorBrush(Color.FromRgb(background.R, background.G, background.B));
        opaqueBackground.Freeze();
        long sourcePixels = 0;
        var loadedImages = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(opaqueBackground, null, new Rect(0, 0, width, height));
            foreach (SnipRecord snip in active)
            {
                if (!loadedImages.TryGetValue(snip.ImageFile, out BitmapSource? image))
                {
                    image = imageLoader(snip)
                        ?? throw new InvalidDataException("A saved snip image is unavailable. Restore the missing image before exporting; no incomplete image was saved.");
                    if (image.PixelWidth < 1 || image.PixelHeight < 1)
                        throw new InvalidDataException("A saved snip image has invalid dimensions.");
                    sourcePixels += (long)image.PixelWidth * image.PixelHeight;
                    if (sourcePixels > MaximumSourcePixels)
                        throw new InvalidOperationException("The board contains too much image data to export at once. Archive some snips and try again.");
                    // Images are immutable PNGs. Reuse a decoded source when a
                    // legacy board refers to the same file more than once.
                    loadedImages.Add(snip.ImageFile, image);
                }

                // Uniform fitting protects older or manually edited layouts from
                // stretching their screenshots. New snips already have matching ratios.
                double scale = Math.Min(snip.Width / image.PixelWidth, snip.Height / image.PixelHeight);
                var destination = new Rect(snip.X - bounds.X, snip.Y - bounds.Y,
                    image.PixelWidth * scale, image.PixelHeight * scale);
                drawing.DrawImage(image, destination);
            }
        }
        var output = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        output.Render(visual);
        output.Freeze();
        return output;
    }

    /// <summary>
    /// Encodes completely to a temporary file next to the destination, then commits
    /// the finished file. A failed encoding or replacement leaves an existing file intact.
    /// </summary>
    public static void Save(BitmapSource image, string path, BoardImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        BitmapEncoder encoder = format switch
        {
            BoardImageFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = 95 },
            BoardImageFormat.Png => new PngBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        string fullPath = Path.GetFullPath(path);
        string extension = Path.GetExtension(fullPath);
        bool extensionMatches = format == BoardImageFormat.Jpeg
            ? extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            : extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
        if (!extensionMatches)
            throw new ArgumentException("Use .jpg or .jpeg for a JPG export, or .png for a PNG export.", nameof(path));
        string directory = Path.GetDirectoryName(fullPath)!;
        string temporaryPath = Path.Combine(directory, ".GameScrapbook-export-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                encoder.Save(stream);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, null);
            else File.Move(temporaryPath, fullPath);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
