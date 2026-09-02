using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BluePeak.App.Shell;

namespace BluePeak.App.Tools;

/// <summary>
/// Drives the running application through every workspace and writes a PNG of each.
/// The design review loop is done against these captures, not against XAML.
/// </summary>
public static class CaptureRunner
{
    public static void Schedule(Application app, string outputDirectory, bool exitWhenDone)
    {
        app.Activated += OnceActivated;

        async void OnceActivated(object? sender, EventArgs e)
        {
            app.Activated -= OnceActivated;
            await Task.Yield();
            await RunAsync(app, outputDirectory, exitWhenDone);
        }
    }

    private static async Task RunAsync(Application app, string outputDirectory, bool exitWhenDone)
    {
        var window = app.MainWindow;
        if (window is null) return;
        Directory.CreateDirectory(outputDirectory);

        // --size WIDTHxHEIGHT captures at a chosen window size, so the minimum supported size
        // can be reviewed as pixels rather than only asserted in a layout test.
        var args = Environment.GetCommandLineArgs();
        int sizeIndex = Array.IndexOf(args, "--size");
        if (sizeIndex >= 0 && sizeIndex + 1 < args.Length)
        {
            var parts = args[sizeIndex + 1].Split('x', 'X');
            if (parts.Length == 2 && double.TryParse(parts[0], out var w) && double.TryParse(parts[1], out var h))
            {
                window.WindowState = WindowState.Normal;
                window.Width = w;
                window.Height = h;
            }
        }

        // --fullscreen captures the real full-screen window so the mode can be reviewed as
        // pixels rather than only asserted.
        if (args.Contains("--fullscreen") && window is MainWindow shell)
        {
            shell.SetFullScreen(true);
            await Settle(window, 500);
        }

        await Settle(window, 900);

        foreach (var workspace in WorkspaceCatalog.All)
        {
            Navigator.Current.Navigate(workspace.Id, recordHistory: false);
            // The simulator needs a few frames of its render loop before it is worth looking at.
            await Settle(window, workspace.Id == "simulator" ? 2600 : 700);
            Save(window, Path.Combine(outputDirectory, $"{Order(workspace.Id)}-{workspace.Id}.png"));
        }

        // Extra simulator captures across the timeline so disassembly and inspection are both reviewable.
        Navigator.Current.Navigate("simulator", recordHistory: false);
        await Settle(window, 800);
        if (Navigator.Current.View is Workspaces.SimulatorView sim)
        {
            foreach (var (label, fraction) in new[]
                     {
                         ("open", 0.16), ("inspect", 0.42), ("diagnose", 0.55), ("verify", 0.86)
                     })
            {
                sim.CaptureSeek(fraction);
                await Settle(window, 700);
                Save(window, Path.Combine(outputDirectory, $"90-simulator-{label}.png"));
            }
        }

        if (exitWhenDone) app.Shutdown();
    }

    private static string Order(string id)
    {
        int index = 0;
        for (int i = 0; i < WorkspaceCatalog.All.Count; i++)
            if (WorkspaceCatalog.All[i].Id == id) index = i + 1;
        return index.ToString("00");
    }

    private static async Task Settle(Window window, int milliseconds)
    {
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await Task.Delay(milliseconds);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
    }

    public static void Save(Window window, string path)
    {
        try
        {
            var source = PresentationSource.FromVisual(window);
            double dpiX = 96, dpiY = 96;
            if (source?.CompositionTarget is not null)
            {
                dpiX = 96 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 96 * source.CompositionTarget.TransformToDevice.M22;
            }

            int w = (int)Math.Ceiling(window.ActualWidth * dpiX / 96);
            int h = (int)Math.Ceiling(window.ActualHeight * dpiY / 96);
            if (w <= 0 || h <= 0) return;

            var bitmap = new RenderTargetBitmap(w, h, dpiX, dpiY, PixelFormats.Pbgra32);
            bitmap.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
            Console.WriteLine($"captured {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"capture failed for {path}: {ex.Message}");
        }
    }
}
