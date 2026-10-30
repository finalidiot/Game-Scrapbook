using System.Buffers.Binary;
using System.Text.Json;

namespace GameScrapbook.Core;

public sealed class BoardStore
{
    public const long MaximumManifestBytes = 8 * 1024 * 1024;
    public const long MaximumImageBytes = 128 * 1024 * 1024;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        MaxDepth = 32
    };

    private readonly object _gate = new();
    private bool _loaded;
    private bool _canSave = true;

    public string RootDirectory { get; }
    public string ImagesDirectory { get; }
    public string ManifestPath => Path.Combine(RootDirectory, "board.json");
    public string BackupPath => Path.Combine(RootDirectory, "board.backup.json");

    public BoardStore(string? rootDirectory = null)
    {
        RootDirectory = Path.GetFullPath(rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameScrapbook"));
        ImagesDirectory = Path.Combine(RootDirectory, "images");
    }

    public BoardLoadResult Load()
    {
        lock (_gate)
        {
            var warnings = new List<string>();
            BoardDocument? document = null;
            bool recovered = false;
            bool primaryExists = File.Exists(ManifestPath);
            bool backupExists = File.Exists(BackupPath);

            if (primaryExists)
            {
                try { document = ReadManifest(ManifestPath); }
                catch (Exception ex) when (IsReadableFailure(ex))
                { warnings.Add($"The current board could not be opened: {ex.Message}"); }
            }

            if (document is null && backupExists)
            {
                try
                {
                    document = ReadManifest(BackupPath);
                    recovered = true;
                    warnings.Add("The previous saved board was recovered. The unreadable current file will be preserved separately when you next save.");
                }
                catch (Exception ex) when (IsReadableFailure(ex))
                { warnings.Add($"The board backup could not be opened: {ex.Message}"); }
            }

            _loaded = true;
            _canSave = document is not null || (!primaryExists && !backupExists);
            // A new board already uses today's defaults. Mark that here so a
            // deliberate black choice before its first save is not mistaken for
            // an older installation's unchanged black default.
            document ??= new BoardDocument
            {
                Settings = new BoardSettings { ThemeDefaultsVersion = ThemeColors.CurrentDefaultsVersion }
            };
            if (!_canSave)
                warnings.Add("Saving is disabled to protect the existing files. Keep the data folder intact and recover the board before making further changes.");

            foreach (SnipRecord snip in document.Snips)
            {
                string path = GetImagePath(snip.ImageFile);
                try
                {
                    (int width, int height) = ReadPngHeader(path);
                    if (width != snip.PixelWidth || height != snip.PixelHeight)
                        warnings.Add($"Image {snip.ImageFile} does not match its saved dimensions. Its board record has been kept.");
                }
                catch (Exception ex) when (IsReadableFailure(ex))
                { warnings.Add($"Image {snip.ImageFile} could not be read. Its board record has been kept. {ex.Message}"); }
            }

            return new BoardLoadResult(document, warnings, recovered, _canSave);
        }
    }

    public void Save(BoardDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_gate)
        {
            EnsureSaveAllowed();
            BoardValidation.ValidateDocument(document);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            if (bytes.LongLength > MaximumManifestBytes)
                throw new InvalidDataException("This board is too large to save. The existing saved board has been kept.");

            Directory.CreateDirectory(RootDirectory);
            string temporary = Path.Combine(RootDirectory, $"board.{Guid.NewGuid():N}.tmp");
            try
            {
                WriteDurably(temporary, bytes);

                if (File.Exists(ManifestPath))
                {
                    bool oldManifestValid;
                    try { _ = ReadManifest(ManifestPath); oldManifestValid = true; }
                    catch (Exception ex) when (IsReadableFailure(ex)) { oldManifestValid = false; }

                    if (oldManifestValid)
                    {
                        // Both files are on the same volume. Keep the previous complete
                        // manifest as backup and atomically install the complete new one.
                        File.Replace(temporary, ManifestPath, BackupPath, ignoreMetadataErrors: true);
                    }
                    else
                    {
                        // Do not let a corrupt primary replace a useful backup.
                        string quarantine = Path.Combine(RootDirectory,
                            $"board.unreadable.{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.{Guid.NewGuid():N}.json");
                        File.Move(ManifestPath, quarantine);
                        File.Move(temporary, ManifestPath);
                    }
                }
                else
                {
                    File.Move(temporary, ManifestPath);
                }
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    /// <summary>Persist a new immutable image before adding its record to the manifest.</summary>
    public StoredImage SaveImage(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        lock (_gate)
        {
            EnsureSaveAllowed();
            if (pngBytes.LongLength > MaximumImageBytes)
                throw new InvalidDataException("The captured image is too large to save.");
            if (pngBytes.Length < 57)
                throw new InvalidDataException("The captured PNG is truncated.");
            ValidatePngTerminator(pngBytes.AsSpan(pngBytes.Length - 12));
            (int width, int height) = ParsePngHeader(pngBytes);
            Directory.CreateDirectory(ImagesDirectory);
            string fileName = Guid.NewGuid().ToString("N") + ".png";
            string finalPath = GetImagePath(fileName);
            string temporary = finalPath + ".tmp";
            try
            {
                WriteDurably(temporary, pngBytes);
                File.Move(temporary, finalPath);
                return new StoredImage(fileName, width, height);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    public string GetImagePath(string fileName)
    {
        BoardValidation.ValidateImageFileName(fileName);
        return Path.Combine(ImagesDirectory, fileName);
    }

    private void EnsureSaveAllowed()
    {
        if (!_loaded) _ = Load();
        if (!_canSave)
            throw new InvalidOperationException("Saving is disabled because the existing board and backup could not be read. The original files have been kept.");
    }

    private static BoardDocument ReadManifest(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumManifestBytes)
            throw new InvalidDataException("The board file exceeds the supported size.");
        BoardDocument document = JsonSerializer.Deserialize<BoardDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("The board file is empty.");
        BoardValidation.ValidateDocument(document);
        return document;
    }

    private static (int Width, int Height) ReadPngHeader(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumImageBytes)
            throw new InvalidDataException("The image exceeds the supported file size.");
        if (stream.Length < 57)
            throw new InvalidDataException("The image is truncated.");
        Span<byte> header = stackalloc byte[33];
        stream.ReadExactly(header);
        Span<byte> terminator = stackalloc byte[12];
        stream.Seek(-12, SeekOrigin.End);
        stream.ReadExactly(terminator);
        ValidatePngTerminator(terminator);
        return ParsePngHeader(header);
    }

    private static (int Width, int Height) ParsePngHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 33 || !bytes[..8].SequenceEqual(PngSignature) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(8, 4)) != 13 ||
            !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("The image is not a supported PNG.");
        uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(29, 4));
        if (PngCrc(bytes.Slice(12, 17)) != expectedCrc)
            throw new InvalidDataException("The PNG header is damaged.");

        uint unsignedWidth = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
        uint unsignedHeight = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
        if (unsignedWidth > int.MaxValue || unsignedHeight > int.MaxValue)
            throw new InvalidDataException("The image dimensions are invalid.");
        int width = (int)unsignedWidth;
        int height = (int)unsignedHeight;
        BoardValidation.ValidateImageDimensions(width, height);
        return (width, height);
    }

    private static uint PngCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    private static void ValidatePngTerminator(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> expected = [0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130];
        if (!bytes.SequenceEqual(expected))
            throw new InvalidDataException("The PNG is incomplete or has an invalid ending.");
    }

    private static void WriteDurably(string path, ReadOnlySpan<byte> bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static bool IsReadableFailure(Exception ex) =>
        ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException;
}
