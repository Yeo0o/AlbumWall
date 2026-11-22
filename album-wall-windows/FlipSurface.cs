using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AlbumWall;

internal sealed class FlipSurface
{
    private const int MaximumResolution = 600;

    private WriteableBitmap? _bitmap;
    private uint[] _front = [];
    private uint[] _back = [];
    private uint[] _output = [];
    private int _size;

    public Image Image { get; } = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };

    public void Prepare(BitmapSource front, BitmapSource back, int pixelWidth)
    {
        int size = Math.Clamp(pixelWidth, 1, MaximumResolution);
        if (_size != size)
        {
            _size = size;
            _bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Pbgra32, null);
            _front = new uint[size * size];
            _back = new uint[size * size];
            _output = new uint[size * size];
            Image.Source = _bitmap;
        }

        CopyPixels(front, _front);
        CopyPixels(back, _back);
        Draw(0);
    }

    public void Draw(double progress)
    {
        FlipProjection.Render(_front, _back, _output, _size, progress);
        _bitmap!.WritePixels(new Int32Rect(0, 0, _size, _size), _output, _size * sizeof(uint), 0);
    }

    private void CopyPixels(BitmapSource source, uint[] destination)
    {
        if (source.PixelWidth != _size)
        {
            double scale = (double)_size / source.PixelWidth;
            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }
        if (source.Format != PixelFormats.Pbgra32)
            source = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);

        source.CopyPixels(new Int32Rect(0, 0, _size, _size), destination, _size * sizeof(uint), 0);
    }
}
