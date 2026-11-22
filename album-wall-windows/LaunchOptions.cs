using System;
using System.Globalization;

namespace AlbumWall;

internal enum SaverMode { Settings, FullScreen, Preview, Invalid }

internal readonly record struct LaunchOptions(SaverMode Mode, nint Parent)
{
    public static LaunchOptions Parse(string[] args)
    {
        if (args.Length == 0)
            return new(SaverMode.Settings, 0);

        var argument = args[0].Split(':', 2);
        var mode = argument[0].ToLowerInvariant() switch
        {
            "/c" or "-c" => SaverMode.Settings,
            "/s" or "-s" => SaverMode.FullScreen,
            "/p" or "-p" => SaverMode.Preview,
            _ => SaverMode.Invalid
        };
        var handleText = argument.Length == 2 ? argument[1] : args.Length > 1 ? args[1] : "";
        var parent = nint.TryParse(handleText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var handle)
            ? handle : 0;
        return mode == SaverMode.Preview && parent == 0
            ? new(SaverMode.Invalid, 0)
            : new(mode, parent);
    }
}
