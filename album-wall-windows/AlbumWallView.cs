using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AlbumWall;

internal sealed class AlbumWallView : Canvas
{
    private readonly IReadOnlyList<BitmapSource> _covers;
    private readonly Settings _settings;
    private readonly DispatcherTimer _timer;
    private readonly List<Tile> _tiles = [];
    private readonly FlipSurface _surface = new();
    private Tile? _flipping;
    private long _flipStart;
    private int _nextCover;
    private Tile? _lastTile;
    private int _columns;

    public AlbumWallView(IReadOnlyList<BitmapSource> covers, Settings settings)
    {
        _covers = covers;
        _settings = settings;
        Background = new SolidColorBrush(Color.FromRgb(17, 22, 18));
        ClipToBounds = true;
        UseLayoutRounding = true;
        Focusable = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        SetZIndex(_surface.Image, 1);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(settings.IntervalMs) };
        _timer.Tick += (_, _) => FlipOne();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            CancelFlip();
        };
        SizeChanged += (_, _) => ApplyLayout();
    }

    private void ApplyLayout()
    {
        var layout = WallpaperLayout.Fit(ActualWidth, ActualHeight, _settings.Rows);
        if (layout.Columns == 0 || _covers.Count == 0)
            return;

        if (_columns != layout.Columns)
        {
            CancelFlip();
            _columns = layout.Columns;
            _lastTile = null;
            Children.Clear();
            _tiles.Clear();

            int[] deck = Enumerable.Range(0, _covers.Count).ToArray();
            Random.Shared.Shuffle(deck.AsSpan());
            for (int i = 0; i < layout.Rows * layout.Columns; i++)
            {
                int index = deck[i % deck.Length];
                var tile = new Tile(_covers[index], index);
                _tiles.Add(tile);
                Children.Add(tile.Viewport);
            }
        }

        for (int i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            var frame = layout.GetTile(i);
            tile.Viewport.Width = frame.VisibleWidth;
            tile.Viewport.Height = layout.TileHeight;
            SetLeft(tile.Viewport, frame.X);
            SetTop(tile.Viewport, frame.Y);
            PositionContent(tile.Image, layout.TileWidth, layout.TileHeight, frame.ContentOffset);
            if (tile == _flipping)
                PositionContent(_surface.Image, layout.TileWidth, layout.TileHeight, frame.ContentOffset);
        }
    }

    private static void PositionContent(Image image, double width, double height, double offset)
    {
        image.Width = width;
        image.Height = height;
        SetLeft(image, offset);
    }

    private void FlipOne()
    {
        if (_flipping is not null || _tiles.Count == 0 || _covers.Count < 2)
            return;

        int tileIndex = Random.Shared.Next(_tiles.Count);
        if (_tiles.Count > 1 && _tiles[tileIndex] == _lastTile)
            tileIndex = (tileIndex + Random.Shared.Next(1, _tiles.Count)) % _tiles.Count;
        var tile = _tiles[tileIndex];
        var visible = _tiles.Select(item => item.CoverIndex).ToHashSet();
        var nextCovers = Enumerable.Range(0, _covers.Count).Where(index => !visible.Contains(index)).ToArray();
        if (nextCovers.Length == 0)
            nextCovers = Enumerable.Range(0, _covers.Count).Where(index => index != tile.CoverIndex).ToArray();
        int next = nextCovers[Random.Shared.Next(nextCovers.Length)];

        var image = tile.Image;
        var surface = _surface.Image;
        int pixelWidth = (int)Math.Ceiling(Math.Max(image.Width, image.Height) * VisualTreeHelper.GetDpi(this).DpiScaleX);
        _surface.Prepare(_covers[tile.CoverIndex], _covers[next], pixelWidth);
        PositionContent(surface, image.Width, image.Height, GetLeft(image));
        tile.Viewport.Children.Add(surface);

        _flipping = tile;
        _nextCover = next;
        _flipStart = Stopwatch.GetTimestamp();
        image.Visibility = Visibility.Hidden;
        CompositionTarget.Rendering += AdvanceFlip;
    }

    private void AdvanceFlip(object? sender, EventArgs args)
    {
        if (_flipping is null)
            return;

        double progress = Stopwatch.GetElapsedTime(_flipStart).TotalMilliseconds / FlipProjection.DurationMs;
        if (progress >= 1)
        {
            SetCover(_flipping, _nextCover);
            CancelFlip();
        }
        else
        {
            _surface.Draw(progress);
        }
    }

    private void SetCover(Tile tile, int next)
    {
        tile.CoverIndex = next;
        tile.Image.Source = _covers[next];
        _lastTile = tile;
    }

    private void CancelFlip()
    {
        if (_flipping is null)
            return;

        CompositionTarget.Rendering -= AdvanceFlip;
        _flipping.Viewport.Children.Remove(_surface.Image);
        _flipping.Image.Visibility = Visibility.Visible;
        _flipping = null;
    }

    private sealed class Tile
    {
        public Tile(BitmapSource cover, int coverIndex)
        {
            Image = new Image { Source = cover, Stretch = Stretch.UniformToFill };
            CoverIndex = coverIndex;
            Viewport.Children.Add(Image);
        }

        public Canvas Viewport { get; } = new() { ClipToBounds = true };
        public Image Image { get; }
        public int CoverIndex { get; set; }
    }
}
