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
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Minimise_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximise_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
