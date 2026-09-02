using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using BluePeak.App.Services;
using BluePeak.Domain;

namespace BluePeak.App.Shell;

public sealed record PostureLine(string Label, string Value, HealthState Tone);

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _suppressNavEvent;
    private readonly List<ListBox> _navLists = new();

    public MainWindow()
    {
        InitializeComponent();

        NavGroups.ItemsSource = WorkspaceCatalog.Grouped.ToList();
        Loaded += OnLoaded;
        Closed += (_, _) => _clock.Stop();

        Navigator.Current.Navigated += OnNavigated;
        FocusService.Current.Changed += _ => UpdateFocusChip();

        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        _clock.Start();
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");

        PreviewKeyDown += OnShortcut;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        CollectNavLists(NavGroups);
        BuildPosture();
        Navigator.Current.Navigate("overview", recordHistory: false);

        int tier = RenderCapability.Tier >> 16;
        var dpi = VisualTreeHelper.GetDpi(this);
        StatusRender.Text = $"tier {tier} · {(tier >= 2 ? "hardware" : "software")} rendering · {dpi.PixelsPerInchX:0} dpi";
        StatusEvidence.Text = "Evidence authority: local operator unless countersigned";
        UpdateFocusChip();
    }

    private void CollectNavLists(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ListBox { Tag: "navgroup" } list && !_navLists.Contains(list)) _navLists.Add(list);
            CollectNavLists(child);
        }
    }

    private void BuildPosture()
    {
        var model = EstateService.Current.Model;
        var unhealthy = model.Unhealthy().ToList();
        int critical = unhealthy.Count(n => n.Health is HealthState.Critical or HealthState.Offline);
        int degraded = unhealthy.Count(n => n.Health == HealthState.Degraded);
        int maintenance = model.Nodes.Count(n => n.Health == HealthState.Maintenance);
        int healthy = model.Nodes.Count - critical - degraded - maintenance;

        PostureVerdict.Text = critical > 0 ? "Impaired" : degraded > 0 ? "Degraded" : "Nominal";
        PostureVerdict.Foreground = Design.Theme.ForHealth(
            critical > 0 ? HealthState.Critical : degraded > 0 ? HealthState.Degraded : HealthState.Healthy);
        PostureCount.Text = $"{model.Nodes.Count} elements";

        PostureBar.Critical = critical;
        PostureBar.Degraded = degraded;
        PostureBar.Maintenance = maintenance;
        PostureBar.Healthy = healthy;

        var openIncidents = model.Incidents.Count(i => i.State != IncidentState.Resolved);
        var breaching = model.Tickets.Count(t => t.SlaBreached(model.Now));
        var openCases = model.Cases.Count(c => c.Status is not (AlertStatus.Closed or AlertStatus.FalsePositive));

        PostureLines.ItemsSource = new List<PostureLine>
        {
            new("Open incidents", openIncidents.ToString(), openIncidents > 0 ? HealthState.Critical : HealthState.Healthy),
            new("Impaired services", critical.ToString(), critical > 0 ? HealthState.Critical : HealthState.Healthy),
            new("Degraded services", degraded.ToString(), degraded > 0 ? HealthState.Degraded : HealthState.Healthy),
            new("SLA breaching", breaching.ToString(), breaching > 0 ? HealthState.Degraded : HealthState.Healthy),
            new("Security cases", openCases.ToString(), openCases > 0 ? HealthState.Critical : HealthState.Healthy)
        };
    }

    private void OnNavigated(WorkspaceDefinition? workspace)
    {
        if (workspace is null) return;
        Host.Content = workspace.View;
        StatusWorkspace.Text = $"{workspace.Group} · {workspace.Title} — {workspace.Purpose}";

        _suppressNavEvent = true;
        foreach (var list in _navLists)
        {
            var match = list.Items.Cast<WorkspaceDefinition>().FirstOrDefault(w => w.Id == workspace.Id);
            list.SelectedItem = match;
        }
        _suppressNavEvent = false;
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressNavEvent) return;
        if (sender is not ListBox list || list.SelectedItem is not WorkspaceDefinition target) return;
        Navigator.Current.Navigate(target.Id);
    }

    private void UpdateFocusChip()
    {
        var subject = FocusService.Current.Subject;
        if (!subject.IsSet)
        {
            FocusChip.Visibility = Visibility.Collapsed;
            return;
        }
        FocusChip.Visibility = Visibility.Visible;
        FocusKindText.Text = subject.Kind.ToString().ToUpperInvariant();
        FocusIdText.Text = subject.Id;
        FocusLabelText.Text = subject.Label;
        FocusChip.ToolTip = new TextBlock
        {
            Text = $"Carried subject. Click to push it into the current workspace.\n{subject.Detail ?? subject.Label}",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320
        };
    }

    private void FocusChip_Click(object sender, MouseButtonEventArgs e)
    {
        Navigator.Current.PushFocus();
        e.Handled = true;
    }

    private void ClearFocus_Click(object sender, RoutedEventArgs e)
    {
        FocusService.Current.Clear();
        UpdateFocusChip();
    }

    private void OnShortcut(object? sender, KeyEventArgs e)
    {
        // F11 is the convention operators will already try, and it must work from any workspace
        // including while the simulator has keyboard focus.
        if (e.Key == Key.F11)
        {
            SetFullScreen(!IsFullScreen);
            e.Handled = true;
            return;
        }

        if (e.KeyboardDevice.Modifiers != ModifierKeys.Control) return;
        string? id = e.Key switch
        {
            Key.D1 => "overview",
            Key.D2 => "noc",
            Key.D3 => "soc",
            Key.D4 => "servicedesk",
            Key.D5 => "tickets",
            Key.D6 => "incidents",
            Key.D7 => "diagnostics",
            Key.D8 => "infrastructure",
            Key.D9 => "simulator",
            Key.D0 => "automation",
            _ => null
        };
        if (id is null) return;
        Navigator.Current.Navigate(id);
        e.Handled = true;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click is the other conventional way out of full screen.
            if (IsFullScreen) SetFullScreen(false);
            else WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        // Dragging a full-screen window has no meaning and DragMove throws on some transitions.
        if (!IsFullScreen && e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Minimise_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximise_Click(object sender, RoutedEventArgs e)
    {
        if (IsFullScreen) { SetFullScreen(false); return; }
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ full screen

    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source) source.AddHook(WindowProc);
    }

    /// <summary>
    /// While full screen there is no WindowChrome to constrain the maximised size, and a
    /// borderless maximised window otherwise overhangs the monitor by the resize border on
    /// every side — which would push the caption buttons off the right edge. Reporting the
    /// monitor's own bounds here makes the window exactly the size of the display.
    /// </summary>
    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo || !IsFullScreen) return IntPtr.Zero;

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return IntPtr.Zero;

        var info = new MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;

        var minMax = System.Runtime.InteropServices.Marshal.PtrToStructure<MinMaxInfo>(lParam);
        // Deliberately the full monitor rectangle, not the work area: covering the taskbar is
        // the point of the mode.
        minMax.MaxPosition = new NativePoint { X = 0, Y = 0 };
        minMax.MaxSize = new NativePoint
        {
            X = info.Monitor.Right - info.Monitor.Left,
            Y = info.Monitor.Bottom - info.Monitor.Top
        };
        minMax.MaxTrackSize = minMax.MaxSize;
        System.Runtime.InteropServices.Marshal.StructureToPtr(minMax, lParam, false);

        handled = true;
        return IntPtr.Zero;
    }

    public bool IsFullScreen { get; private set; }

    private WindowState _stateBeforeFullScreen = WindowState.Normal;
    private System.Windows.Shell.WindowChrome? _chromeBeforeFullScreen;

    private void FullScreen_Click(object sender, RoutedEventArgs e) => SetFullScreen(!IsFullScreen);

    /// <summary>
    /// True full screen: the window covers the whole monitor including the taskbar.
    ///
    /// The WindowChrome that gives the app its custom caption also handles WM_GETMINMAXINFO,
    /// which constrains a maximised window to the monitor's *work area* — so simply maximising
    /// leaves the taskbar strip visible. Detaching the chrome for the duration restores WPF's
    /// plain WindowStyle=None behaviour, which does cover the full monitor, and it is put back
    /// on exit so resize borders and snap behave normally again.
    /// </summary>
    public void SetFullScreen(bool enable)
    {
        if (enable == IsFullScreen) return;

        IsFullScreen = enable;

        if (enable)
        {
            _stateBeforeFullScreen = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
            _chromeBeforeFullScreen = System.Windows.Shell.WindowChrome.GetWindowChrome(this);

            System.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
            // Toggling through Normal forces the maximise to be recalculated without the chrome.
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.CanResize;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, _chromeBeforeFullScreen);
            WindowState = _stateBeforeFullScreen;
        }

        UpdateFullScreenAffordances();
    }

    private void UpdateFullScreenAffordances()
    {
        FullScreenGlyph.Data = (Geometry)FindResource(IsFullScreen ? "I.ExitFullScreen" : "I.FullScreen");
        FullScreenButton.ToolTip = IsFullScreen ? "Leave full screen (F11)" : "Full screen (F11)";

        // The window border reads as a seam against a bezel, so drop it while full screen.
        WindowFrame.BorderThickness = new Thickness(IsFullScreen ? 0 : 1);

        // Minimise and restore are meaningless in this mode and would leave the operator stranded.
        MinimiseButton.Visibility = IsFullScreen ? Visibility.Collapsed : Visibility.Visible;
        MaximiseButton.Visibility = IsFullScreen ? Visibility.Collapsed : Visibility.Visible;

        StatusFullScreen.Visibility = IsFullScreen ? Visibility.Visible : Visibility.Collapsed;
    }
}
