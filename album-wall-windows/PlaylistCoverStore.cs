using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AlbumWall;

internal sealed record PlaylistCover(NeteaseAlbum Album, BitmapSource Preview, string Path);
internal sealed record PreparedCovers(PlaylistCover[] Covers, int Failed);
internal sealed record CoverImportResult(int Added, int Existing, int Failed);

internal static class PlaylistCoverStore
{
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AlbumWall", "NeteaseCovers");

    public static async Task<PreparedCovers> PrepareAsync(
        NeteasePlaylist playlist, NeteaseAlbum[] albums, string temporaryFolder, IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(temporaryFolder);
        var covers = new PlaylistCover?[albums.Length];
        int completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, albums.Length), NeteasePlaylist.ParallelOptionsFor(cancellationToken),
            async (index, token) =>
        {
            var album = albums[index];
            try
            {
                var bytes = await playlist.DownloadArtworkAsync(album, token).ConfigureAwait(false);
                string extension = ImageExtension(bytes);
                using var input = new MemoryStream(bytes);
                var preview = DecodePreview(input);
                string path = Path.Combine(temporaryFolder, FileStem(album.Id) + extension);
                await File.WriteAllBytesAsync(path, bytes, token).ConfigureAwait(false);
                covers[index] = new(album, preview, path);
            }
            catch (Exception e) when (NeteasePlaylist.IsItemFailure(e, token) ||
                e is NotSupportedException or FormatException or ArgumentException or COMException)
            {
                // Invalid or unavailable images are excluded from the import list.
            }
            progress.Report(Interlocked.Increment(ref completed));
        }).ConfigureAwait(false);

        return new(covers.OfType<PlaylistCover>().ToArray(), covers.Count(cover => cover is null));
    }

    public static CoverImportResult Import(IReadOnlyList<PlaylistCover> covers)
    {
        Directory.CreateDirectory(Folder);
        int added = 0;
        int existing = 0;
        int failed = 0;
        foreach (var cover in covers)
        {
            string stem = Path.Combine(Folder, FileStem(cover.Album.Id));
            if (File.Exists(stem + ".jpg") || File.Exists(stem + ".png"))
            {
                existing++;
                continue;
            }

            string destination = stem + Path.GetExtension(cover.Path);
            string stagingPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(cover.Path, stagingPath);
                File.Move(stagingPath, destination);
                added++;
            }
            catch (IOException) when (File.Exists(destination))
            {
                existing++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
            finally
            {
                TryDeleteFile(stagingPath);
            }
        }
        return new(added, existing, failed);
    }

    private static string FileStem(long id) => "netease-" + id.ToString(CultureInfo.InvariantCulture);

    private static BitmapSource DecodePreview(Stream input)
    {
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if (frame.PixelWidth is <= 0 or > 1600 || frame.PixelHeight is <= 0 or > 1600)
            throw new InvalidDataException("无法识别封面尺寸。");
        input.Position = 0;

        var preview = new BitmapImage();
        preview.BeginInit();
        preview.CacheOption = BitmapCacheOption.OnLoad;
        preview.DecodePixelWidth = 96;
        preview.StreamSource = input;
        preview.EndInit();
        preview.Freeze();
        return preview;
    }

    private static string ImageExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
            return ".jpg";
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return ".png";
        throw new InvalidDataException("封面不是 JPEG 或 PNG。");
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
