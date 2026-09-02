using System.Windows;
using System.Windows.Shell;
using BluePeak.App.Shell;
using Xunit;

namespace BluePeak.UiTests;

/// <summary>
/// Full screen has to survive being toggled repeatedly and must always leave the window in a
/// state the operator can get out of.
/// </summary>
[Collection("sta")]
public class FullScreenTests
{
    private readonly StaHost _host;
    public FullScreenTests(StaHost host) => _host = host;

    private T With<T>(Func<MainWindow, T> action) => _host.Run(() =>
    {
        var window = new MainWindow();
        try { return action(window); }
        finally { window.Close(); }
    });

    [Fact]
    public void A_new_window_is_not_full_screen()
    {
        Assert.False(With(w => w.IsFullScreen));
    }

    [Fact]
    public void The_native_caption_is_suppressed_in_every_window_mode()
    {
        // The custom caption is drawn by the app. If WindowStyle is ever anything but None, the
        // operating system draws its own title bar as well — which is exactly what happened when
        // the chrome was detached for full screen and the app had been relying on WindowChrome
        // alone to hide it.
        var styles = With(w =>
        {
            var windowed = w.WindowStyle;
            w.SetFullScreen(true);
            var full = w.WindowStyle;
            w.SetFullScreen(false);
            var restored = w.WindowStyle;
            return (windowed, full, restored);
        });

        Assert.Equal(WindowStyle.None, styles.windowed);
        Assert.Equal(WindowStyle.None, styles.full);
        Assert.Equal(WindowStyle.None, styles.restored);
    }

    [Fact]
    public void Entering_full_screen_maximises_without_a_resizable_chrome()
    {
        var (full, state, resize, chrome) = With(w =>
        {
            w.SetFullScreen(true);
            return (w.IsFullScreen, w.WindowState, w.ResizeMode, WindowChrome.GetWindowChrome(w));
        });

        Assert.True(full);
        Assert.Equal(WindowState.Maximized, state);
        Assert.Equal(ResizeMode.NoResize, resize);
        // The chrome constrains a maximised window to the work area, which would leave the
        // taskbar visible, so it must be detached for the duration.
        Assert.Null(chrome);
    }

    [Fact]
    public void Leaving_full_screen_restores_the_chrome_and_the_previous_state()
    {
        var (full, state, resize, chrome) = With(w =>
        {
            w.WindowState = WindowState.Normal;
            w.SetFullScreen(true);
            w.SetFullScreen(false);
            return (w.IsFullScreen, w.WindowState, w.ResizeMode, WindowChrome.GetWindowChrome(w));
        });

        Assert.False(full);
        Assert.Equal(WindowState.Normal, state);
        Assert.Equal(ResizeMode.CanResize, resize);
        Assert.NotNull(chrome);
    }

    [Fact]
    public void A_maximised_window_returns_to_maximised_rather_than_restored()
    {
        var state = With(w =>
        {
            w.WindowState = WindowState.Maximized;
            w.SetFullScreen(true);
            w.SetFullScreen(false);
            return w.WindowState;
        });

        Assert.Equal(WindowState.Maximized, state);
    }

    [Fact]
    public void Toggling_repeatedly_ends_in_a_usable_windowed_state()
    {
        var (full, resize, chrome) = With(w =>
        {
            for (int i = 0; i < 8; i++)
            {
                w.SetFullScreen(true);
                w.SetFullScreen(false);
            }
            return (w.IsFullScreen, w.ResizeMode, WindowChrome.GetWindowChrome(w));
        });

        Assert.False(full);
        Assert.Equal(ResizeMode.CanResize, resize);
        Assert.NotNull(chrome);
    }

    [Fact]
    public void Setting_the_same_value_twice_is_a_no_op()
    {
        var (full, chrome) = With(w =>
        {
            w.SetFullScreen(true);
            w.SetFullScreen(true);   // must not stash the detached chrome as the value to restore
            w.SetFullScreen(false);
            return (w.IsFullScreen, WindowChrome.GetWindowChrome(w));
        });

        Assert.False(full);
        Assert.NotNull(chrome);
    }
}
