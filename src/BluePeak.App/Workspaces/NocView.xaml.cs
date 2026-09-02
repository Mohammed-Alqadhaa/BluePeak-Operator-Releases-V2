using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public sealed record MetricRow(string Label, MetricSeries Series, string Reading, HealthState Tone);
public sealed record WorkRow(string Id, string Title, string Workspace, FocusKind Kind);

public partial class NocView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private readonly List<ToggleButton> _filters = new();
    private string _filter = "impaired";
    private string _search = "";

    public NocView()
    {
        InitializeComponent();
        BuildFilters();
        Canvas.Model = _model;
        Canvas.NodeActivated += id => Select(id, carryFocus: true);
        RefreshList();
        Select(_model.Unhealthy().FirstOrDefault()?.Id ?? _model.Nodes.First().Id, carryFocus: false);
    }

    public void ApplyFocus(FocusSubject subject)
    {
        if (subject.Kind != FocusKind.Service) return;
        if (_model.Node(subject.Id) is null) return;
        _filter = "all";
        foreach (var button in _filters) button.IsChecked = (string)button.Tag! == "all";
        RefreshList();
        Select(subject.Id, carryFocus: false);
    }

    private void BuildFilters()
    {
        foreach (var (key, label) in new[]
                 {
                     ("impaired", "Not healthy"), ("all", "All services"),
                     ("network", "Network"), ("identity", "Identity"), ("apps", "Applications")
                 })
        {
            var button = new ToggleButton
            {
                Content = label,
                Style = (Style)FindResource("Toggle.Segment"),
                Tag = key,
                IsChecked = key == _filter
            };
            button.Click += Filter_Click;
            _filters.Add(button);
            FilterGroup.Children.Add(button);
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked || clicked.Tag is not string key) return;
        _filter = key;
        foreach (var button in _filters) button.IsChecked = ReferenceEquals(button, clicked);
        RefreshList();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text.Trim();
        RefreshList();
    }

    private void RefreshList()
    {
        IEnumerable<ServiceNode> query = _filter switch
        {
            "impaired" => _model.Unhealthy(),
            "network" => _model.ByLayer(EstateLayer.Network),
            "identity" => _model.ByLayer(EstateLayer.Identity).Concat(_model.ByLayer(EstateLayer.CoreServices)),
            "apps" => _model.ByLayer(EstateLayer.Applications),
            _ => _model.Nodes.OrderByDescending(n => n.Health.Weight()).ThenBy(n => n.Layer)
        };

        if (_search.Length > 0)
            query = query.Where(n =>
                n.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                n.Id.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                n.Kind.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                n.Tags.Any(t => t.Contains(_search, StringComparison.OrdinalIgnoreCase)));

        var items = query.ToList();
        var selected = ServiceList.SelectedItem as ServiceNode;
        ServiceList.ItemsSource = items;
        if (selected is not null && items.Contains(selected)) ServiceList.SelectedItem = selected;

        int bad = items.Count(n => n.Health.IsBad());
        ListSummary.Text = _search.Length > 0
            ? $"{items.Count} matching '{_search}' · {bad} not healthy"
            : $"{items.Count} services · {bad} not healthy";
    }

    private void Service_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServiceList.SelectedItem is ServiceNode node) Select(node.Id, carryFocus: true);
    }

    private void Select(string id, bool carryFocus)
    {
        var node = _model.Node(id);
        if (node is null) return;

        Canvas.SubjectId = id;

        var items = ServiceList.ItemsSource as IEnumerable<ServiceNode>;
        if (items is not null && items.Contains(node) && !ReferenceEquals(ServiceList.SelectedItem, node))
            ServiceList.SelectedItem = node;

        DetailPip.State = node.Health;
        DetailState.Text = node.Health.Label();
        DetailState.Foreground = Theme.ForHealth(node.Health);
        DetailName.Text = node.Name;
        DetailReason.Text = node.StateReason;
        DetailSince.Text = node.DegradedSince is null
            ? $"{node.Kind} · {node.Location}"
            : $"since {AgoConverter.Format(_model.Now - node.DegradedSince.Value)} · {node.Location}";

        MetricList.ItemsSource = new List<MetricRow>
        {
            new("Latency", node.Metrics["latency"], $"{node.LatencyMs:0.#} ms",
                node.LatencyMs > 500 ? HealthState.Critical : node.LatencyMs > 120 ? HealthState.Degraded : HealthState.Healthy),
            new("Error rate", node.Metrics["errors"], $"{node.ErrorRate:0.##} %",
                node.ErrorRate > 5 ? HealthState.Critical : node.ErrorRate > 1 ? HealthState.Degraded : HealthState.Healthy),
            new("Throughput", node.Metrics["throughput"], $"{node.Metrics["throughput"].Latest:0} /s",
                node.Health.IsBad() ? HealthState.Degraded : HealthState.Healthy),
            new("Availability", node.Metrics["errors"], $"{node.Availability:0.00} %",
                node.Availability < 95 ? HealthState.Critical : node.Availability < 99.5 ? HealthState.Degraded : HealthState.Healthy)
        };

        var blast = _model.BlastRadius(node.Id);
        BlastList.ItemsSource = blast.Take(10).ToList();
        int impaired = blast.Count(n => n.Health.IsBad());
        BlastSummary.Text = blast.Count == 0
            ? "Nothing in the modelled estate depends on this service. A failure here is contained."
            : $"{blast.Count} services would be impaired if this failed. {impaired} of them are already showing impact.";

        BuildWorkList(node);
        BuildVerdict(node);

        VerificationText.Text = node.Health.IsBad()
            ? "Recovery must be asserted from a consumer of this service, not from its own status endpoint. "
            + "The endpoint stayed green throughout the current fault."
            : "Nominal. Any change to this service should be verified from at least one dependent service.";

        if (carryFocus)
            FocusService.Current.Set(FocusKind.Service, node.Id, node.Name, node.StateReason);
    }

    private void BuildWorkList(ServiceNode node)
    {
        var work = new List<WorkRow>();
        foreach (var incident in _model.Incidents.Where(i => i.AffectedServiceIds.Contains(node.Id) || i.RootCauseServiceId == node.Id))
            work.Add(new WorkRow(incident.Id, incident.Title, "incidents", FocusKind.Incident));
        foreach (var ticket in _model.Tickets.Where(t => t.LinkedServiceId == node.Id))
            work.Add(new WorkRow(ticket.Id, ticket.Subject, "tickets", FocusKind.Ticket));
        foreach (var change in _model.Changes.Where(c => c.TargetServiceId == node.Id || c.BlastRadiusServiceIds.Contains(node.Id)))
            work.Add(new WorkRow(change.Id, change.Title, "changes", FocusKind.Change));
        foreach (var runbook in _model.Runbooks.Where(r => r.TargetServiceIds.Contains(node.Id)))
            work.Add(new WorkRow(runbook.Id, runbook.Name, "automation", FocusKind.Runbook));

        WorkList.ItemsSource = work;
    }

    private void BuildVerdict(ServiceNode node)
    {
        var firstFailure = _model.FirstFailure(node.Id);
        if (!node.Health.IsBad())
        {
            VerdictBar.Fill = Theme.Healthy;
            VerdictHeadline.Text = "Healthy — no action indicated";
            VerdictHeadline.Foreground = Theme.Healthy;
            VerdictBody.Text = $"{node.Name} is meeting its objectives. "
                             + $"It requires {_model.DependenciesOf(node.Id).Count} services and "
                             + $"{_model.DependentsOf(node.Id).Count} services require it.";
            return;
        }

        if (firstFailure is not null && firstFailure.Id != node.Id)
        {
            VerdictBar.Fill = Theme.Critical;
            VerdictHeadline.Text = $"Inherited fault — first failure is {firstFailure.Name}";
            VerdictHeadline.Foreground = Theme.Critical;
            VerdictBody.Text = $"{node.Name} is degraded because a service it depends on has failed. "
                             + $"{firstFailure.Name} is the deepest unhealthy component on this path: {firstFailure.StateReason}. "
                             + "Acting on this service would mask the fault rather than fix it.";
        }
        else
        {
            VerdictBar.Fill = Theme.Critical;
            VerdictHeadline.Text = "First failure on this path";
            VerdictHeadline.Foreground = Theme.Critical;
            var blast = _model.BlastRadius(node.Id);
            VerdictBody.Text = $"Everything this service depends on is healthy, so the fault originates here. "
                             + $"{node.StateReason}. {blast.Count} downstream services are exposed.";
        }
    }

    private void Work_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkRow row }) return;
        Navigator.Current.NavigateWithSubject(row.Workspace, row.Kind, row.Id, row.Title, "Opened from NOC");
    }

    private void Diagnose_Click(object sender, RoutedEventArgs e)
    {
        var node = Canvas.SubjectId is null ? null : _model.Node(Canvas.SubjectId);
        var path = _model.DiagnosticPaths.FirstOrDefault(p =>
                       p.FirstFailureServiceId == node?.Id || p.Hops.Any(hop => hop.ServiceId == node?.Id))
                   ?? _model.DiagnosticPaths[0];
        Navigator.Current.NavigateWithSubject("diagnostics", FocusKind.Service, path.Id, path.Name,
            "Dependency walk opened from NOC");
    }

    private void Simulate_Click(object sender, RoutedEventArgs e)
    {
        var node = Canvas.SubjectId is null ? null : _model.Node(Canvas.SubjectId);
        string journeyId = node?.Layer switch
        {
            EstateLayer.Network => "journey.network",
            EstateLayer.Identity => "journey.auth",
            _ => "journey.dns"
        };
        Navigator.Current.NavigateWithSubject("simulator", FocusKind.Journey, journeyId,
            node?.Name ?? "Operations core", "Opened from NOC");
    }
}
