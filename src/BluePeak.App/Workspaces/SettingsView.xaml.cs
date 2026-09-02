using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.Domain;
using BluePeak.Simulation;
using BluePeak.Simulation.Journeys;

namespace BluePeak.App.Workspaces;

public sealed record CapabilityRow(string Capability, string Detail, bool Supported)
{
    public Geometry? Glyph => Application.Current.TryFindResource(Supported ? "I.Soc" : "I.Close") as Geometry;
    public Brush Brush => Supported ? Theme.Healthy : Theme.Critical;
}

public partial class SettingsView : UserControl
{
    private readonly AppSettings _settings = AppSettings.Current;

    public SettingsView()
    {
        InitializeComponent();
        BuildContract();
        BuildCapabilities();
        BuildToggles();
        BuildRendering();
        BuildSession();
    }

    private void BuildContract()
    {
        ContractList.ItemsSource = new List<AnswerRow>
        {
            new("Can this application change production?",
                "No. There is no credential store, no configured endpoint and no network client in the build. "
                + "Every action described as an execution runs against the in-memory estate model in this process.",
                HealthState.Healthy),

            new("Why does automation still refuse things, then?",
                "Because the gates are the product. A runbook that will not run without policy, pre-check, "
                + "simulation and an authorisation record behaves the same way whether the target is real or modelled, "
                + "and that behaviour is what is being demonstrated.",
                HealthState.Degraded),

            new("What authority does anything produced here carry?",
                "Local operator authority. A record captured on this workstation is an operator observation. It becomes "
                + "platform-attested only when a control plane countersigns it, and project-authoritative only when it "
                + "is accepted into the immutable record. The Evidence workspace shows which of the three applies to "
                + "every record and never lets the distinction be edited away.",
                HealthState.Degraded),

            new("Does the estate reflect a real environment?",
                "No. The estate is a deterministic fixture built at launch from a fixed seed, so the same situation "
                + "appears on every run and captures are reproducible. Names, people and addresses are fictional and "
                + "the addresses are from documentation ranges reserved for examples.",
                HealthState.Healthy),

            new("What leaves this process?",
                "Nothing, unless you ask for a capture. Screenshots are written to the path given on the command line. "
                + "No telemetry, no crash reporting and no update check.",
                HealthState.Healthy)
        };
    }

    private void BuildCapabilities()
    {
        // Counts are read from the live model rather than written into the copy, so this page
        // cannot drift away from what the build actually contains.
        var model = EstateService.Current.Model;
        CapabilityList.ItemsSource = new List<CapabilityRow>
        {
            new("Observe a modelled estate",
                $"{model.Nodes.Count} elements, {model.Edges.Count} modelled dependencies, health rolled up by layer", true),
            new("Walk a dependency path", "Ordered hops with expected against actual and a first-failure verdict", true),
            new("Compute blast radius", "Transitive closure over consumers, worst-first", true),
            new("Correlate detections onto entities", "Alerts resolved to shared subjects, not grouped by rule", true),
            new("Run gated automation", "Policy, pre-check, simulation, authorisation, verification, evidence", true),
            new("Simulate the estate in 3D", "Procedural geometry, deterministic choreography, scrubbable timeline", true),
            new("Record decisions in session", "Triage attachment, approvals and runbook outcomes, in memory", true),
            new("Connect to a live estate", "No collectors, no credentials, no network client", false),
            new("Mutate production configuration", "No writer of any kind exists in the build", false),
            new("Persist state between sessions", "Deliberate: every launch starts from the same fixture", false),
            new("Send telemetry or reports outward", "No outbound path is compiled in", false)
        };
    }

    private void BuildToggles()
    {
        AddToggle("Reduce motion",
            "Stops ambient drift and flow markers. Journey choreography still runs, because that motion carries meaning.",
            _settings.ReduceMotion, v =>
            {
                _settings.ReduceMotion = v;
                if (v) { _settings.IdleDrift = false; _settings.LinkFlow = false; }
                RefreshToggles();
            });

        AddToggle("Dependency flow markers",
            "Animated markers travelling along links in the simulator, showing direction of dependency.",
            _settings.LinkFlow, v => _settings.LinkFlow = v);

        AddToggle("Idle camera drift",
            "Slow orbit while the simulator sits on the journey list.",
            _settings.IdleDrift, v => _settings.IdleDrift = v);

        AddToggle("Frame rate readout",
            "Shows the simulator's measured frame interval, so dropped frames are visible rather than hidden.",
            _settings.ShowFrameRate, v => _settings.ShowFrameRate = v);

        AddToggle("Play journeys on open",
            "Start playback immediately when a journey is chosen, rather than holding on the first beat.",
            _settings.AutoPlayJourneys, v => _settings.AutoPlayJourneys = v);
    }

    private readonly List<(ToggleButton Button, Func<bool> Read)> _toggles = new();

    private void AddToggle(string label, string detail, bool initial, Action<bool> apply)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        text.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Cell.Primary") });
        text.Children.Add(new TextBlock
        {
            Text = detail,
            Style = (Style)FindResource("Text.Prose"),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("Toggle.Switch"),
            IsChecked = initial,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            Width = 34,
            Height = 18
        };
        toggle.Click += (_, _) => apply(toggle.IsChecked == true);
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);

        TogglePanel.Children.Add(grid);
        _toggles.Add((toggle, () => false));
    }

    private void RefreshToggles()
    {
        // Reduce motion turns the dependent options off; reflect that in the switches.
        if (TogglePanel.Children.Count < 3) return;
        SetSwitch(1, _settings.LinkFlow);
        SetSwitch(2, _settings.IdleDrift);
    }

    private void SetSwitch(int index, bool value)
    {
        if (TogglePanel.Children[index] is Grid grid && grid.Children.Count > 1 &&
            grid.Children[1] is ToggleButton toggle)
            toggle.IsChecked = value;
    }

    private void BuildRendering()
    {
        int tier = RenderCapability.Tier >> 16;
        var dpi = VisualTreeHelper.GetDpi(this);
        int triangles = OperationsCore.Modules.Count;

        RenderList.ItemsSource = new List<FieldRow>
        {
            new("Pipeline", "WPF Viewport3D, procedural MeshGeometry3D"),
            new("Rendering tier", $"{tier} — {(tier >= 2 ? "hardware accelerated" : "software")}"),
            new("Display scaling", $"{dpi.PixelsPerInchX:0} dpi, per-monitor aware"),
            new("Scene modules", $"{triangles} functional modules on a shared spine"),
            new("Journeys", $"{JourneyCatalog.All.Count} loaded, "
                            + $"{JourneyCatalog.All.Sum(j => j.Stages.Count)} stages total"),
            new("Timeline model", "Scene state is a pure function of playhead position"),
            new("Geometry", "Built once and frozen; only transforms and brush colours change per frame")
        };
    }

    private void BuildSession()
    {
        var model = EstateService.Current.Model;
        SessionList.ItemsSource = new List<FieldRow>
        {
            new("Estate fixture", $"seeded at {model.Now:yyyy-MM-dd HH:mm}"),
            new("Elements", model.Nodes.Count.ToString()),
            new("Dependencies", model.Edges.Count.ToString()),
            new("Open incidents", model.Incidents.Count(i => i.State != IncidentState.Resolved).ToString()),
            new("Evidence records", model.Evidence.Count.ToString()),
            new("Default authority", "Local operator"),
            new("Persistence", "None — memory only")
        };
    }
}
