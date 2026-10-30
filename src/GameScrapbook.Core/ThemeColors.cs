using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameScrapbook.Core;

/// <summary>Opaque interface colours, independent of the Windows UI framework.</summary>
public static class ThemeColors
{
    public const string DefaultBackground = "#242424";
    public const string DefaultText = "#FFFFFF";
    public const string DefaultAccent = "#1976D2";
    public const int CurrentDefaultsVersion = 1;
    public const double MinimumTextContrast = 4.5;

    /// <summary>Accept six hexadecimal RGB digits, optionally prefixed with #.</summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = "";
        if (value is null) return false;
        ReadOnlySpan<char> digits = value.AsSpan().Trim();
        if (digits.Length > 0 && digits[0] == '#') digits = digits[1..];
        if (digits.Length != 6) return false;
        foreach (char digit in digits)
            if (!char.IsAsciiHexDigit(digit)) return false;
        normalized = "#" + digits.ToString().ToUpperInvariant();
        return true;
    }

    /// <summary>A malformed preference cannot prevent the board from opening.</summary>
    public static string NormalizeOrDefault(string? value, string fallback) =>
        TryNormalize(value, out string normalized) ? normalized :
        TryNormalize(fallback, out normalized) ? normalized : DefaultBackground;

    /// <summary>
    /// WCAG relative-luminance contrast for opaque sRGB colours, from 1 to 21.
    /// Call TryNormalize first when a value comes from an unfinished text edit.
    /// </summary>
    public static double ContrastRatio(string first, string second)
    {
        double firstLuminance = RelativeLuminance(first);
        double secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) /
               (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    /// <summary>Choose black or white text with the larger contrast ratio.</summary>
    public static string ReadableForeground(string background) =>
        ContrastRatio(background, "#FFFFFF") >= ContrastRatio(background, "#000000")
            ? "#FFFFFF" : "#000000";

    private static double RelativeLuminance(string value)
    {
        if (!TryNormalize(value, out string normalized))
            throw new ArgumentException("Use a six-digit hexadecimal RGB colour, such as #1976D2.", nameof(value));
        double red = Linear(byte.Parse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d);
        double green = Linear(byte.Parse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d);
        double blue = Linear(byte.Parse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d);
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    private static double Linear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
}

/// <summary>
/// Colour preferences are recoverable even if a hand-edited JSON value has the
/// wrong type. The settings validator supplies the correct per-property default.
/// This deliberately does not weaken validation of any snippet or board field.
/// </summary>
internal sealed class ThemeColorJsonConverter : JsonConverter<string>
{
    public ThemeColorJsonConverter() { }

    public override bool HandleNull => true;

    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString() ?? "";
        reader.Skip();
        return "";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
