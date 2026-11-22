using System;

namespace AlbumWall;

internal readonly record struct WallpaperLayout(
    int Rows, int Columns, double TileWidth, double TileHeight, double ColumnPitch, double RowGap)
{
    public static WallpaperLayout Fit(double width, double height, int rows)
    {
        if (width <= 0 || height <= 0 || rows < 1)
            return default;

        // Match the calibrated video's 2-pixel seam at 720p at any scaling factor.
        double rowGap = height / 360;
        double tileHeight = (height - (rows - 1) * rowGap) / rows;
        if (tileHeight <= 0)
            return default;

        // One combined full column is split evenly between the two edges.
        int columnSteps = Math.Max(1, (int)Math.Round(width / (tileHeight + rowGap), MidpointRounding.AwayFromZero));
        double columnPitch = width / columnSteps;
        double columnGap = Math.Min(rowGap, columnPitch / 4);
        return new(rows, columnSteps + 1, columnPitch - columnGap, tileHeight, columnPitch, rowGap);
    }

    public TileFrame GetTile(int index)
    {
        int column = index % Columns;
        double halfWidth = TileWidth / 2;
        double y = index / Columns * (TileHeight + RowGap);
        if (column == 0)
            return new(0, y, halfWidth, -halfWidth);

        double x = column * ColumnPitch - halfWidth;
        double width = column == Columns - 1 ? halfWidth : TileWidth;
        return new(x, y, width, 0);
    }
}

internal readonly record struct TileFrame(double X, double Y, double VisibleWidth, double ContentOffset);
