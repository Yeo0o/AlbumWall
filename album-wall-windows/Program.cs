using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AlbumWall;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);
        if (options.Mode == SaverMode.Invalid)
            return 0;

        // The published app can also be run when the build host couldn't embed a DPI manifest.
        NativeWindow.SetProcessDpiAwarenessContext(-4);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += (_, _) => Start(app, options);
        return app.Run();
    }

    private static void Start(Application app, LaunchOptions options)
    {
        var settings = Settings.Load();
        if (options.Mode == SaverMode.Settings)
        {
            var window = new SettingsWindow(settings);
            if (options.Parent != 0 && NativeWindow.IsWindow(options.Parent))
                new WindowInteropHelper(window).Owner = options.Parent;
            window.Closed += (_, _) => app.Shutdown();
            window.Show();
            return;
        }

        if (options.Mode == SaverMode.Preview && !NativeWindow.IsWindow(options.Parent))
        {
            app.Shutdown();
            return;
        }

        var covers = CoverCatalog.LoadLocal(settings.CoverFolder,
            settings.UseNeteaseCovers ? PlaylistCoverStore.Folder : null);
        if (covers.Count == 0 || (covers.Count == 1 && !settings.UseNeteaseCovers))
            covers = CoverCatalog.LoadDemo();

        if (options.Mode == SaverMode.Preview)
            ShowPreview(app, options.Parent, covers, settings);
        else
            ShowFullScreen(app, covers, settings);
    }

    private static void ShowPreview(Application app, nint parent, IReadOnlyList<BitmapSource> covers, Settings settings)
    {
        if (!NativeWindow.GetClientRect(parent, out var bounds))
        {
            app.Shutdown();
            return;
        }

        var source = new HwndSource(new HwndSourceParameters("Album Wall Preview")
        {
            ParentWindow = parent,
            WindowStyle = NativeWindow.ChildStyle | NativeWindow.VisibleStyle,
            ExtendedWindowStyle = NativeWindow.NoActivateStyle,
            Width = Math.Max(1, bounds.Width),
            Height = Math.Max(1, bounds.Height)
        });
        source.RootVisual = new AlbumWallView(covers, settings);

        var monitorParent = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        source.Disposed += (_, _) =>
        {
            monitorParent.Stop();
            if (!app.Dispatcher.HasShutdownStarted && !app.Dispatcher.HasShutdownFinished)
                app.Dispatcher.BeginInvoke(new Action(() => app.Shutdown()));
        };
        monitorParent.Tick += (_, _) =>
        {
            if (!NativeWindow.GetClientRect(parent, out var current))
            {
                monitorParent.Stop();
                source.Dispose();
            }
            else if (current.Width != bounds.Width || current.Height != bounds.Height)
            {
                bounds = current;
                NativeWindow.SetWindowPos(source.Handle, 0, 0, 0, Math.Max(1, current.Width),
                    Math.Max(1, current.Height), NativeWindow.NoZOrder | NativeWindow.NoActivate);
            }
        };
        app.Exit += (_, _) =>
        {
            monitorParent.Stop();
            source.Dispose();
        };
        monitorParent.Start();
    }

    private static void ShowFullScreen(Application app, IReadOnlyList<BitmapSource> covers, Settings settings)
    {
        var cursorWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        bool exiting = false;
        void Exit()
        {
            if (exiting)
                return;
            exiting = true;
            cursorWatch.Stop();
            app.Shutdown();
        }

        var monitors = NativeWindow.GetMonitorBounds();
        if (monitors.Count == 0)
        {
            Exit();
            return;
        }

        foreach (var bounds in monitors)
        {
            var window = new Window
            {
                Background = Brushes.Black,
                Content = new AlbumWallView(covers, settings),
                Cursor = Cursors.None,
                ForceCursor = true,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Topmost = true,
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height
            };
            window.PreviewKeyDown += (_, _) => Exit();
            window.PreviewMouseDown += (_, _) => Exit();
            window.PreviewMouseWheel += (_, _) => Exit();
            window.PreviewTouchDown += (_, _) => Exit();
            window.Closed += (_, _) => Exit();
            window.Show();
            nint handle = new WindowInteropHelper(window).Handle;
            NativeWindow.SetWindowPos(handle, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                NativeWindow.NoZOrder | NativeWindow.NoActivate);
        }

        if (!NativeWindow.GetCursorPos(out var start))
            return;

        cursorWatch.Tick += (_, _) =>
        {
            if (NativeWindow.GetCursorPos(out var current) &&
                (Math.Abs(current.X - start.X) >= 6 || Math.Abs(current.Y - start.Y) >= 6))
                Exit();
        };
        cursorWatch.Start();
        app.Exit += (_, _) => cursorWatch.Stop();
    }
}
