using System;

namespace AlbumWall;

internal static class FlipProjection
{
    public const int DurationMs = 350;
    private const double CameraDistance = 6;

    public static void Render(
        ReadOnlySpan<uint> front, ReadOnlySpan<uint> back, Span<uint> output, int size, double progress)
    {
        if (progress <= 0)
        {
            front.CopyTo(output);
            return;
        }
        if (progress >= 1)
        {
            back.CopyTo(output);
            return;
        }

        // Use the same "ease-in-out" timing curve as the calibrated web preview.
        double low = 0;
        double high = 1;
        for (int i = 0; i < 16; i++)
        {
            double t = (low + high) / 2;
            double x = 3 * t * (1 - t) * (.42 * (1 - t) + .58 * t) + t * t * t;
            if (x < progress)
                low = t;
            else
                high = t;
        }
        double curve = (low + high) / 2;
        double angle = Math.PI * curve * curve * (3 - 2 * curve);
        ReadOnlySpan<uint> source = front;
        if (angle >= Math.PI / 2)
        {
            source = back;
            angle -= Math.PI;
        }

        double sine = Math.Sin(angle);
        double cosine = Math.Cos(angle);
        double halfSize = size / 2d;
        output.Clear();
        if (cosine < .001)
            return;

        // Invert the projected quad column by column. The outer bounds clip to the tile.
        for (int x = 0; x < size; x++)
        {
            double screenX = (x + .5) / halfSize - 1;
            double divisor = CameraDistance * cosine - screenX * sine;
            if (divisor <= 0)
                continue;

            double faceX = CameraDistance * screenX / divisor;
            if (faceX < -1 || faceX >= 1)
                continue;

            double depth = 1 + faceX * sine / CameraDistance;
            double sourceX = (faceX + 1) * halfSize - .5;
            int x0 = Math.Clamp((int)Math.Floor(sourceX), 0, size - 1);
            int x1 = Math.Min(x0 + 1, size - 1);
            int weight = Math.Clamp((int)((sourceX - x0) * 256), 0, 256);

            int firstY = Math.Max(0, (int)Math.Ceiling(halfSize * (1 - 1 / depth) - .5));
            int lastY = Math.Min(size, (int)Math.Ceiling(halfSize * (1 + 1 / depth) - .5));
            int outputIndex = firstY * size + x;
            double sourceY = (firstY + .5 - halfSize) * depth + halfSize;
            for (int y = firstY; y < lastY; y++)
            {
                int row = Math.Clamp((int)sourceY, 0, size - 1) * size;
                output[outputIndex] = Blend(source[row + x0], source[row + x1], (uint)weight);
                outputIndex += size;
                sourceY += depth;
            }
        }
    }

    private static uint Blend(uint left, uint right, uint weight)
    {
        const uint mask = 0x00ff00ff;
        uint inverse = 256 - weight;
        uint rb = ((left & mask) * inverse + (right & mask) * weight) >> 8;
        uint ag = ((left >> 8 & mask) * inverse + (right >> 8 & mask) * weight) & ~mask;
        return (rb & mask) | ag;
    }
}
