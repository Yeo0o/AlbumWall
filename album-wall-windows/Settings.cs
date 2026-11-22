using System;
using System.IO;
using System.Text.Json;

namespace AlbumWall;

internal sealed record Settings
{
    public int Rows { get; init; } = 5;
    public int IntervalMs { get; init; } = 2600;
    public string CoverFolder { get; init; } = "";
    public bool UseNeteaseCovers { get; init; }

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AlbumWall", "settings.json");

    public static Settings Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            return settings with
            {
                Rows = Math.Clamp(settings.Rows, 3, 8),
                IntervalMs = Math.Clamp(settings.IntervalMs, 1000, 30000),
                CoverFolder = settings.CoverFolder ?? ""
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
