using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace AlbumWall;

internal sealed record NeteaseTrackList(string Title, int Total, long[] Songs);
internal sealed record NeteaseAlbum(long Id, string Title, string Artist, Uri Artwork);
internal sealed record NeteaseAlbums(NeteaseAlbum[] Albums, int MissingSongs);

internal sealed class NeteasePlaylist
{
    private static readonly Uri MusicRoot = new("https://music.163.com/");
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        UseCookies = false
    })
    {
        DefaultRequestHeaders = { UserAgent = { new("AlbumWall", "0.3") } }
    };

    private readonly HttpClient _client;

    public NeteasePlaylist() : this(Client) { }

    internal NeteasePlaylist(HttpClient client) => _client = client;

    public async Task<NeteaseTrackList> ReadPlaylistAsync(string input, CancellationToken cancellationToken)
    {
        string url = Regex.Match(input, @"https?://[^\s<>""，。！？]+", RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1)).Value.TrimEnd(')', ']', '>', '）', '】', ',', '.');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var link) || !IsPageOrShortLink(link))
            throw new InvalidDataException("请粘贴网易云音乐的公开歌单分享链接。");

        if (IsShortLink(link))
            (_, link) = await ReadAsync(link, IsPageOrShortLink, 2_000_000, cancellationToken, resolveShare: true).ConfigureAwait(false);
        if (!TryGetPageId(link, "/playlist", out long playlistId) &&
            !TryGetPageId(link, "/m/playlist", out playlistId) &&
            !TryGetPageId(link, "/discover/toplist", out playlistId))
            throw new InvalidDataException("这个链接没有指向公开歌单。");

        var (bytes, _) = await ReadAsync(MusicPage("playlist", playlistId), IsPage, 2_000_000, cancellationToken)
            .ConfigureAwait(false);
        var page = Parse(bytes);
        var tracks = page.DocumentNode.SelectNodes("//ul[contains(concat(' ', normalize-space(@class), ' '), ' f-hide ')]/li/a");
        var songs = new List<long>();
        if (tracks is not null)
        {
            foreach (var track in tracks)
            {
                if (TryGetPageId(track, "/song", out long id))
                    songs.Add(id);
            }
        }

        if (songs.Count == 0)
            throw new InvalidDataException("页面未提供曲目。请确认歌单已公开，并在浏览器里能打开。");

        int total = int.TryParse(page.GetElementbyId("playlist-track-count")?.InnerText, out int count) ? count : songs.Count;
        string title = Meta(page, "og:title");
        return new(title, total, songs.Distinct().ToArray());
    }

    public async Task<NeteaseAlbums> FindAlbumsAsync(
        long[] songs, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var results = new NeteaseAlbum?[songs.Length];
        int completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, songs.Length), ParallelOptionsFor(cancellationToken), async (i, token) =>
        {
            try
            {
                results[i] = await ReadSongAsync(songs[i], token).ConfigureAwait(false);
            }
            catch (Exception e) when (IsItemFailure(e, token))
            {
                // Keep the covers found in other tracks if one page is unavailable.
            }
            progress?.Report(Interlocked.Increment(ref completed));
        }).ConfigureAwait(false);

        return new(results.OfType<NeteaseAlbum>().DistinctBy(album => album.Id).ToArray(),
            results.Count(result => result is null));
    }

    public async Task<NeteaseAlbum?> ReadSongAsync(long songId, CancellationToken cancellationToken)
    {
        var (bytes, _) = await ReadAsync(MusicPage("song", songId), IsPage, 1_500_000, cancellationToken)
            .ConfigureAwait(false);
        var document = Parse(bytes);
        string cover = Meta(document, "og:image");
        if (!Uri.TryCreate(cover, UriKind.Absolute, out var artwork) || !IsArtwork(artwork))
            return null;

        const string descriptions = "//p[contains(concat(' ', normalize-space(@class), ' '), ' des ')]";
        var albumLink = document.DocumentNode.SelectSingleNode(descriptions + "/a[starts-with(@href, '/album?')]");
        if (albumLink is null || !TryGetPageId(albumLink, "/album", out long albumId))
            return null;

        var artists = document.DocumentNode.SelectNodes(descriptions + "//a[starts-with(@href, '/artist?')]");
        return new(albumId, Clean(albumLink.InnerText), artists is null ? "" :
            string.Join(" / ", artists.Select(artist => Clean(artist.InnerText)).Distinct()), artwork);
    }

    public async Task<byte[]> DownloadArtworkAsync(NeteaseAlbum album, CancellationToken cancellationToken)
    {
        var url = new UriBuilder(album.Artwork) { Query = "param=600y600" }.Uri;
        var (bytes, _) = await ReadAsync(url, IsArtwork, 2_000_000, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    public static bool IsItemFailure(Exception error, CancellationToken token) =>
        error is HttpRequestException or IOException or TimeoutException ||
        error is OperationCanceledException && !token.IsCancellationRequested;

    public static ParallelOptions ParallelOptionsFor(CancellationToken cancellationToken) =>
        new() { MaxDegreeOfParallelism = 5, CancellationToken = cancellationToken };

    private async Task<(byte[] Bytes, Uri Address)> ReadAsync(
        Uri url, Predicate<Uri> allowed, int byteLimit, CancellationToken cancellationToken, bool resolveShare = false)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var token = timeout.Token;

        for (int redirects = 0; redirects < 6; redirects++)
        {
            if (!allowed(url))
                throw new InvalidDataException("网易云链接跳转到了不支持的地址。");
            url = new UriBuilder(url) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;

            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location ?? throw new InvalidDataException("分享链接无法解析。");
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                // The resolved long link can be read later as a canonical playlist page.
                if (resolveShare && IsPage(url))
                    return ([], url);
                continue;
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > byteLimit)
                throw new InvalidDataException("网易云返回的数据过大。");

            using var output = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            var buffer = new byte[16_384];
            int size;
            while ((size = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                if (output.Length + size > byteLimit)
                    throw new InvalidDataException("网易云返回的数据过大。");
                output.Write(buffer, 0, size);
            }
            return (output.ToArray(), url);
        }
        throw new InvalidDataException("分享链接的跳转次数过多。");
    }

    private static bool IsWeb(Uri url) =>
        url.IsAbsoluteUri && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp) &&
        url.IsDefaultPort && url.UserInfo.Length == 0;

    private static bool IsPage(Uri url) => IsWeb(url) && url.Host is "music.163.com" or "y.music.163.com";
    private static bool IsShortLink(Uri url) => IsWeb(url) && url.Host is "163cn.tv" or "www.163cn.tv";
    private static bool IsPageOrShortLink(Uri url) => IsPage(url) || IsShortLink(url);
    private static bool IsArtwork(Uri url) => IsWeb(url) &&
        url.Host.EndsWith(".music.126.net", StringComparison.OrdinalIgnoreCase);

    private static Uri MusicPage(string path, long id) =>
        new(MusicRoot, path + "?id=" + id.ToString(CultureInfo.InvariantCulture));

    private static bool TryGetPageId(HtmlNode link, string path, out long id)
    {
        id = 0;
        return Uri.TryCreate(MusicRoot, Clean(link.GetAttributeValue("href", "")), out var address) &&
            TryGetPageId(address, path, out id);
    }

    private static bool TryGetPageId(Uri link, string path, out long id)
    {
        id = 0;
        if (!IsPage(link))
            return false;

        string section = link.AbsolutePath + link.Query;
        if (link.Fragment.StartsWith("#/", StringComparison.Ordinal))
            section = link.Fragment[1..];
        int query = section.IndexOf('?');
        if (query < 0 || !section.AsSpan(0, query).TrimEnd('/').Equals(path.AsSpan(), StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (string field in section[(query + 1)..].Split('&'))
        {
            if (field.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
                return long.TryParse(field.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
        }
        return false;
    }

    private static HtmlDocument Parse(byte[] bytes)
    {
        var document = new HtmlDocument();
        document.LoadHtml(Encoding.UTF8.GetString(bytes));
        return document;
    }

    private static string Meta(HtmlDocument document, string property) => Clean(
        document.DocumentNode.SelectSingleNode("//meta[@property='" + property + "']")
            ?.GetAttributeValue("content", "") ?? "");

    private static string Clean(string html) => HtmlEntity.DeEntitize(html).Trim();
}
