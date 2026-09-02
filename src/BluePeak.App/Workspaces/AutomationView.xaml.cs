using System.Windows;
using System.Windows.Controls;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;
using BluePeak.Simulation;

namespace BluePeak.App.Workspaces;

public partial class AutomationView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private readonly Dictionary<string, RunbookEngine> _engines = new(StringComparer.OrdinalIgnoreCase);
    private RunbookEngine? _engine;

    public AutomationView()
    {
        InitializeComponent();
        RunbookList.ItemsSource = _model.Runbooks;
        RunbookList.SelectedIndex = 0;
    }

    public void ApplyFocus(FocusSubject subject)
    {
        if (subject.Kind != FocusKind.Runbook) return;
        var runbook = _model.Runbook(subject.Id);
        if (runbook is not null) RunbookList.SelectedItem = runbook;
    }

    private void Runbook_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RunbookList.SelectedItem is not Runbook runbook) return;

        // Engines are kept per runbook so switching away and back does not lose a run in progress.
        if (!_engines.TryGetValue(runbook.Id, out var engine))
        {
            engine = new RunbookEngine(runbook);
            _engines[runbook.Id] = engine;
        }

        if (_engine is not null) _engine.Changed -= OnEngineChanged;
        _engine = engine;
        _engine.Changed += OnEngineChanged;

        Show(runbook);
        OnEngineChanged();
        FocusService.Current.Set(FocusKind.Runbook, runbook.Id, runbook.Name, runbook.Purpose);
    }

    private void Show(Runbook runbook)
    {
        DetailId.Text = runbook.Id;
        DetailName.Text = runbook.Name;
        DetailPurpose.Text = runbook.Purpose;
        DetailOwner.Text = runbook.Owner;
        DetailRuns.Text = runbook.LastRunAt is null
            ? "never run"
            : $"{runbook.RunCount} runs · last {AgoConverter.Format(_model.Now - runbook.LastRunAt.Value)} ago";

        var tone = runbook.Risk switch
        {
            ChangeRisk.Critical => HealthState.Critical,
            ChangeRisk.High => HealthState.Critical,
            ChangeRisk.Moderate => HealthState.Degraded,
            _ => HealthState.Healthy
        };
        var brush = Theme.ForHealth(tone);
        RiskText.Text = runbook.Risk.ToString().ToUpperInvariant() + " RISK";
        RiskText.Foreground = brush;
        RiskChip.Background = Theme.Frozen(Theme.WithAlpha(brush.Color, 0.14));
        RiskChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(brush.Color, 0.4));

        TargetList.ItemsSource = runbook.TargetServiceIds
            .Select(id => _model.Node(id))
            .Where(n => n is not null)
            .ToList();

        int mutating = runbook.Steps.Count(s => s.Mutating);
        ContractList.ItemsSource = new List<AnswerRow>
        {
            new("Can this change production?",
                mutating == 0
                    ? "No. Every step in this runbook is read-only."
                    : $"{mutating} steps write state, and none of them run without an authorisation gate first.",
                mutating == 0 ? HealthState.Healthy : HealthState.Degraded),
            new("What stops a bad run?",
                $"{runbook.Steps.Count(s => s.Gate == "Pre-check")} pre-check assertions run against the live estate. "
                + "A failed assertion halts the run rather than warning and continuing.",
                HealthState.Healthy),
            new("What authorises it?",
                runbook.RequiresChange
                    ? "An approved change record. The engine checks the record, not the caller's assertion that one exists."
                    : "Operator role only — this runbook makes no changes.",
                runbook.RequiresChange ? HealthState.Degraded : HealthState.Healthy),
            new("How is success proved?",
                $"{runbook.Steps.Count(s => s.Gate == "Verify")} verification steps assert the outcome from a consumer's "
                + "position, then the result is sealed to the evidence ledger.",
                HealthState.Healthy),
            new("What does this build actually do?",
                "Nothing outside this process. Execution is simulated against the local estate model; there are no "
                + "credentials and no network calls.",
                HealthState.Degraded)
        };

        BuildActions(runbook);
    }

    private void OnEngineChanged()
    {
        if (_engine is null) return;
        Dispatcher.Invoke(() =>
        {
            GateList.ItemsSource = null;
            GateList.ItemsSource = _engine.Groups();
            LogList.ItemsSource = null;
            LogList.ItemsSource = _engine.Log.AsEnumerable().Reverse().ToList();

            bool running = _engine.Outcome == RunOutcome.Running;
            bool waiting = _engine.Outcome == RunOutcome.WaitingApproval;

            DryRunButton.IsEnabled = !running && !waiting;
            GatedRunButton.IsEnabled = !running && !waiting;
            AbortButton.IsEnabled = running || waiting;
            AuthoriseButton.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed;

            var (text, tone) = _engine.Outcome switch
            {
                RunOutcome.Running => ("RUNNING", HealthState.Unknown),
                RunOutcome.WaitingApproval => ("HELD FOR AUTHORISATION", HealthState.Degraded),
                RunOutcome.Blocked => ("BLOCKED — PRE-CHECK FAILED", HealthState.Critical),
                RunOutcome.Completed => (_engine.Mode == RunMode.DryRun ? "DRY RUN COMPLETE" : "COMPLETE", HealthState.Healthy),
                RunOutcome.Aborted => ("ABORTED", HealthState.Degraded),
                _ => ("READY", HealthState.Unknown)
            };
            OutcomeText.Text = text;
            OutcomeText.Foreground = Theme.ForHealth(tone);
            OutcomePip.State = tone;
        });
    }

    private void BuildActions(Runbook runbook)
    {
        ActionPanel.Children.Clear();

        void Add(string caption, Action handler)
        {
            var button = new Button
            {
                Content = caption,
                Style = (Style)FindResource("Button.Standard"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 8)
            };
            button.Click += (_, _) => handler();
            ActionPanel.Children.Add(button);
        }

        if (runbook.SuggestedForIncidentId is not null && _model.Incident(runbook.SuggestedForIncidentId) is { } incident)
            Add($"Open {incident.Id}", () => Navigator.Current.NavigateWithSubject(
                "incidents", FocusKind.Incident, incident.Id, incident.Title, "Opened from Automation"));

        var change = _model.Changes.FirstOrDefault(c =>
            runbook.TargetServiceIds.Contains(c.TargetServiceId) && c.State == ChangeState.AwaitingApproval);
        if (change is not null)
            Add($"Authorising change {change.Id}", () => Navigator.Current.NavigateWithSubject(
                "changes", FocusKind.Change, change.Id, change.Title, "Opened from Automation"));

        Add("Show gate sequence in simulator", () => Navigator.Current.NavigateWithSubject(
            "simulator", FocusKind.Journey, "journey.automation", runbook.Name, "Opened from Automation"));
    }

    private async void DryRun_Click(object sender, RoutedEventArgs e)
    {
        if (_engine is null) return;
        _engine.Reset();
        await _engine.RunAsync(RunMode.DryRun);
    }

    private async void GatedRun_Click(object sender, RoutedEventArgs e)
    {
        if (_engine is null) return;
        _engine.Reset();
        await _engine.RunAsync(RunMode.Gated);
    }

    private void Authorise_Click(object sender, RoutedEventArgs e) => _engine?.Authorise();
    private void Abort_Click(object sender, RoutedEventArgs e) => _engine?.Abort();
    private void Reset_Click(object sender, RoutedEventArgs e) => _engine?.Reset();
}
