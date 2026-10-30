using System.Windows;
using System.Windows.Media;
using GameScrapbook.Core;

namespace GameScrapbook.Windows;

/// <summary>One palette for the scrapbook and settings, without recolouring captured images.</summary>
internal static class ThemeManager
{
    public static void Apply(BoardSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string background = ThemeColors.NormalizeOrDefault(settings.BackgroundColor, ThemeColors.DefaultBackground);
        string text = ThemeColors.NormalizeOrDefault(settings.TextColor, ThemeColors.DefaultText);
        string accent = ThemeColors.NormalizeOrDefault(settings.AccentColor, ThemeColors.DefaultAccent);
        if (ThemeColors.ContrastRatio(background, text) < 4.5)
        {
            background = ThemeColors.DefaultBackground;
            text = ThemeColors.DefaultText;
        }

        string panel = Surface(background, text, 0.06);
        string control = Surface(background, text, 0.09);
        string hover = Surface(background, text, 0.15);
        string pressed = Surface(background, text, 0.20);
        string card = Surface(background, text, 0.07);
        string notice = Mix(background, accent, 0.18);
        if (ThemeColors.ContrastRatio(notice, text) < 4.5) notice = panel;
        string[] textSurfaces = [background, panel, control, hover, pressed, card, notice];

        // Supporting copy remains readable rather than becoming faint grey.
        string muted = Mix(background, text, 0.85);
        if (textSurfaces.Any(surface => ThemeColors.ContrastRatio(surface, muted) < 4.5)) muted = text;

        var palette = new Dictionary<string, string>
        {
            ["BackgroundBrush"] = background,
            ["PanelBrush"] = panel,
            ["TextBrush"] = text,
            ["MutedBrush"] = muted,
            ["AccentBrush"] = accent,
            ["AccentTextBrush"] = ThemeColors.ReadableForeground(accent),
            ["BorderBrush"] = Mix(background, text, 0.42),
            ["ControlBrush"] = control,
            ["HoverBrush"] = hover,
            ["PressedBrush"] = pressed,
            ["BoardBrush"] = background,
            ["GridDotBrush"] = Mix(background, text, 0.20),
            ["CardBrush"] = card,
            ["NoticeBrush"] = notice,
            ["ErrorBrush"] = ReadableStatus(textSurfaces, text, "#FFB2A5", "#9A1A17"),
            ["SuccessBrush"] = ReadableStatus(textSurfaces, text, "#ABDFBC", "#17642E")
        };

        foreach (var entry in palette)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(entry.Value));
            brush.Freeze();
            Application.Current.Resources[entry.Key] = brush;
        }
    }

    private static string Surface(string background, string text, double amount)
    {
        // Very close-to-threshold user colours may not allow a lighter/darker
        // panel. Keep the user's base colour in that case rather than losing text.
        for (int attempt = 0; attempt < 6; attempt++, amount /= 2)
        {
            string candidate = Mix(background, text, amount);
            if (ThemeColors.ContrastRatio(candidate, text) >= 4.5) return candidate;
        }
        return background;
    }

    private static string ReadableStatus(string[] backgrounds, string fallback, params string[] candidates)
    {
        foreach (string candidate in candidates)
            if (backgrounds.All(background => ThemeColors.ContrastRatio(background, candidate) >= 4.5)) return candidate;
        return fallback;
    }

    private static string Mix(string from, string to, double amount)
    {
        Color first = (Color)ColorConverter.ConvertFromString(from);
        Color second = (Color)ColorConverter.ConvertFromString(to);
        static byte Channel(byte a, byte b, double ratio) => (byte)Math.Round(a + (b - a) * ratio);
        return $"#{Channel(first.R, second.R, amount):X2}{Channel(first.G, second.G, amount):X2}{Channel(first.B, second.B, amount):X2}";
    }
}
