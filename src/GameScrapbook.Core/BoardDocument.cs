using System.Text.Json.Serialization;

namespace GameScrapbook.Core;

/// <summary>Image geometry is stored in board units, independent of monitor DPI.</summary>
public sealed class BoardDocument
{
    [JsonRequired] public int Version { get; set; } = 1;
    [JsonRequired] public List<SnipRecord> Snips { get; set; } = [];
    [JsonRequired] public BoardSettings Settings { get; set; } = new();

    public BoardDocument DeepClone() => new()
    {
        Version = Version,
        Snips = Snips.Select(snip => snip.Clone()).ToList(),
        Settings = Settings.Clone()
    };
}

public sealed class SnipRecord
{
    [JsonRequired] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonRequired] public string ImageFile { get; set; } = "";
    [JsonRequired] public double X { get; set; }
    [JsonRequired] public double Y { get; set; }
    [JsonRequired] public double Width { get; set; } = 320;
    [JsonRequired] public double Height { get; set; } = 200;
    [JsonRequired] public int PixelWidth { get; set; }
    [JsonRequired] public int PixelHeight { get; set; }
    [JsonRequired] public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    // Optional fields keep existing snippets active when an older board is read.
    // Archiving retains the same immutable image and its original board geometry.
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedUtc { get; set; }

    public SnipRecord Clone() => (SnipRecord)MemberwiseClone();
}

public sealed class BoardSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 760;
    public bool IsMaximized { get; set; }
    public bool ReturnCursor { get; set; } = true;
    public bool Topmost { get; set; }
    // Win32 MOD_ALT | MOD_CONTROL and the S virtual-key code.
    public int CaptureHotkeyModifiers { get; set; } = 3;
    public int CaptureHotkeyKey { get; set; } = 0x53;
    public double BoardZoom { get; set; } = 1;
    public bool SnapToSnips { get; set; } = true;
    // Optional preferences keep existing version-1 boards compatible. Invalid
    // colours are repaired during validation, never treated as lost board data.
    [JsonConverter(typeof(ThemeColorJsonConverter))]
    public string BackgroundColor { get; set; } = ThemeColors.DefaultBackground;
    [JsonConverter(typeof(ThemeColorJsonConverter))]
    public string TextColor { get; set; } = ThemeColors.DefaultText;
    [JsonConverter(typeof(ThemeColorJsonConverter))]
    public string AccentColor { get; set; } = ThemeColors.DefaultAccent;
    // Missing in older manifests. A completed default upgrade is recorded so a
    // later deliberate choice of pure black is not changed on the next startup.
    public int ThemeDefaultsVersion { get; set; }

    public BoardSettings Clone() => (BoardSettings)MemberwiseClone();
}

public sealed record BoardLoadResult(
    BoardDocument Document,
    IReadOnlyList<string> Warnings,
    bool RecoveredFromBackup,
    bool CanSave);

public sealed record StoredImage(string FileName, int PixelWidth, int PixelHeight);
