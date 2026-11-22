using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AlbumWall;

internal static class CoverCatalog
{
    private const int LocalCoverLimit = 160;
    private const int MaxDecodedSide = 480;
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };

    public static List<BitmapSource> LoadLocal(string folder, string? importedFolder = null)
    {
        var covers = new List<BitmapSource>();
        try
        {
            foreach (string file in CoverFiles(folder, importedFolder))
            {
                if (!ImageExtensions.Contains(Path.GetExtension(file)))
                    continue;
                try
                {
                    using var stream = File.OpenRead(file);
                    covers.Add(Decode(stream));
                    if (covers.Count == LocalCoverLimit)
                        break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    NotSupportedException or FormatException or ArgumentException or
                    System.Runtime.InteropServices.COMException)
                {
                    // One unreadable image doesn't prevent the other covers from loading.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unavailable folder is handled by the caller.
        }
        return covers;
    }

    private static IEnumerable<string> CoverFiles(string folder, string? importedFolder)
    {
        if (importedFolder is null)
            return Directory.Exists(folder) ? Directory.EnumerateFiles(folder) : [];

        // Sample both sources when their combined size exceeds the decoder limit.
        var paths = AvailableImages(folder).Concat(AvailableImages(importedFolder))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Random.Shared.Shuffle(paths.AsSpan());
        return paths;
    }

    private static string[] AvailableImages(string folder)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder).Where(file => ImageExtensions.Contains(Path.GetExtension(file))).ToArray()
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static List<BitmapSource> LoadDemo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("AlbumWall.Artwork.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                return Decode(stream);
            }).ToList();
    }

    private static BitmapSource Decode(Stream stream)
    {
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        int width = frame.PixelWidth;
        int height = frame.PixelHeight;
        stream.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        if (Math.Max(width, height) > MaxDecodedSide)
        {
            if (width >= height)
                bitmap.DecodePixelWidth = MaxDecodedSide;
            else
                bitmap.DecodePixelHeight = MaxDecodedSide;
        }
        bitmap.EndInit();

        int side = Math.Min(bitmap.PixelWidth, bitmap.PixelHeight);
        BitmapSource square = bitmap.PixelWidth == bitmap.PixelHeight
            ? bitmap
            : new CroppedBitmap(bitmap, new Int32Rect(
                (bitmap.PixelWidth - side) / 2, (bitmap.PixelHeight - side) / 2, side, side));
        square.Freeze();
        return square;
    }
}
