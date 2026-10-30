namespace GameScrapbook.Core;

public static class BoardValidation
{
    public const int MaximumSnips = 10_000;
    public const int MaximumImageDimension = 32_768;
    public const long MaximumImagePixels = 100_000_000;

    public static void ValidateDocument(BoardDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Version != 1)
            throw new InvalidDataException($"Board version {document.Version} is not supported by this version of Game Scrapbook.");
        if (document.Snips is null || document.Settings is null)
            throw new InvalidDataException("The board is missing its snippets or settings.");
        if (document.Snips.Count > MaximumSnips)
            throw new InvalidDataException($"A board cannot contain more than {MaximumSnips:N0} snippets.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SnipRecord snip in document.Snips)
        {
            if (snip is null)
                throw new InvalidDataException("The board contains an empty snippet record.");
            if (string.IsNullOrWhiteSpace(snip.Id) || snip.Id.Length > 100 || !ids.Add(snip.Id))
                throw new InvalidDataException("Snippet identifiers must be nonempty, unique, and at most 100 characters.");
            ValidateImageFileName(snip.ImageFile);
            ValidateCoordinate(snip.X, "Snippet X");
            ValidateCoordinate(snip.Y, "Snippet Y");
            ValidateSize(snip.Width, 0.1, 100_000, "Snippet width");
            ValidateSize(snip.Height, 0.1, 100_000, "Snippet height");
            ValidateImageDimensions(snip.PixelWidth, snip.PixelHeight);
        }

        BoardSettings settings = document.Settings;
        if (settings.WindowLeft.HasValue) ValidateCoordinate(settings.WindowLeft.Value, "Window left");
        if (settings.WindowTop.HasValue) ValidateCoordinate(settings.WindowTop.Value, "Window top");
        ValidateSize(settings.WindowWidth, 320, 32_768, "Window width");
        ValidateSize(settings.WindowHeight, 200, 32_768, "Window height");
        ValidateSize(settings.BoardZoom, 0.1, 8, "Board zoom");
        if (settings.CaptureHotkeyModifiers < 1 || settings.CaptureHotkeyModifiers > 15 ||
            settings.CaptureHotkeyKey < 1 || settings.CaptureHotkeyKey > 254)
            throw new InvalidDataException("The capture shortcut has an invalid modifier or key.");

        // Theme preferences are optional presentation data. Repair them without
        // rejecting an otherwise healthy board or falling back to an older copy.
        settings.BackgroundColor = ThemeColors.NormalizeOrDefault(settings.BackgroundColor, ThemeColors.DefaultBackground);
        settings.TextColor = ThemeColors.NormalizeOrDefault(settings.TextColor, ThemeColors.DefaultText);
        settings.AccentColor = ThemeColors.NormalizeOrDefault(settings.AccentColor, ThemeColors.DefaultAccent);
        if (settings.ThemeDefaultsVersion < ThemeColors.CurrentDefaultsVersion)
        {
            // Upgrade only the complete old default palette. A changed text or
            // accent colour means this is a custom palette and should stay put.
            if (settings.BackgroundColor == "#000000" && settings.TextColor == "#FFFFFF" &&
                settings.AccentColor == "#1976D2")
                settings.BackgroundColor = ThemeColors.DefaultBackground;
            settings.ThemeDefaultsVersion = ThemeColors.CurrentDefaultsVersion;
        }
        if (ThemeColors.ContrastRatio(settings.BackgroundColor, settings.TextColor) < ThemeColors.MinimumTextContrast)
        {
            // Recover a reliably readable pair; keep a valid chosen accent.
            settings.BackgroundColor = ThemeColors.DefaultBackground;
            settings.TextColor = ThemeColors.DefaultText;
        }
    }

    /// <summary>Apply explicit Windows filename rules even when checking a build on Linux.</summary>
    public static void ValidateImageFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 100 ||
            !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("..", StringComparison.Ordinal) ||
            fileName.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')) ||
            !char.IsAsciiLetterOrDigit(fileName[0]))
            throw new InvalidDataException("Image filenames must be simple PNG basenames inside the scrapbook images folder.");

        string stem = fileName[..^4];
        string firstComponent = stem.Split('.')[0];
        if (firstComponent.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            firstComponent.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            firstComponent.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            firstComponent.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (firstComponent.Length == 4 && firstComponent[3] is >= '1' and <= '9' &&
             (firstComponent.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || firstComponent.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("The image filename is reserved by Windows.");
    }

    public static void ValidateImageDimensions(int width, int height)
    {
        if (width < 1 || height < 1 || width > MaximumImageDimension || height > MaximumImageDimension ||
            (long)width * height > MaximumImagePixels)
            throw new InvalidDataException("The image has invalid or excessively large dimensions.");
    }

    private static void ValidateCoordinate(double value, string name)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > 1_000_000)
            throw new InvalidDataException($"{name} must be a finite coordinate within the board's bounds.");
    }

    private static void ValidateSize(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new InvalidDataException($"{name} must be between {minimum} and {maximum}.");
    }
}
