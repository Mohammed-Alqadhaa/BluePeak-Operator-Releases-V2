using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.App.Simulator3D;
using BluePeak.Domain;
using BluePeak.Simulation;
using BluePeak.Simulation.Journeys;

namespace BluePeak.App.Workspaces;

public partial class SimulatorView : UserControl, ILifecycleAware, IFocusAware
{
    private readonly SceneRenderer _renderer = new();
    private readonly PlaybackController _controller;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly double[] _speeds = { 0.5, 1.0, 1.5, 2.0 };
    private readonly List<ToggleButton> _speedButtons = new();

    private double _lastFrameSeconds;
    private bool _renderLoopAttached;
    private bool _running;
    private int _lastStageRendered = -1;
    private double _idleClock;

    // Frame statistics, reported in the top bar because a 3D surface that silently drops
    // frames is a defect the operator should be able to see.
    private int _frameCount;
    private double _frameAccumulator;
    private double _displayedFps;

    public SimulatorView()
    {
        InitializeComponent();

        _controller = new PlaybackController(new JourneyTimeline(JourneyCatalog.Default));
        _controller.StateChanged += OnPlaybackStateChanged;

        ViewportHost.Children.Add(_renderer.Viewport);
        _renderer.SetModuleStates(SimulatorContext.ModuleHealth());

        var cards = JourneyCatalog.All.Select(j => new JourneyCard { Journey = j }).ToList();
        JourneyList.ItemsSource = cards;
        ScenarioPicker.ItemsSource = cards;
        JourneyList.SelectedIndex = 1;

        BuildSpeedControls();

        ScrubBar.ScrubStarted += () => _controller.BeginScrub();
        ScrubBar.Scrubbed += time =>
        {
            _controller.ScrubTo(time);
            RenderCurrent(force: true);
        };
        ScrubBar.ScrubEnded += () =>
        {
            _controller.EndScrub();
            OnPlaybackStateChanged();
        };

        Loaded += (_, _) => OnActivated();
        Unloaded += (_, _) => OnDeactivated();
    }

    // ------------------------------------------------------------------ lifecycle

    public void OnActivated()
    {
        if (_renderLoopAttached) return;
        _renderLoopAttached = true;
        _lastFrameSeconds = _clock.Elapsed.TotalSeconds;
        CompositionTarget.Rendering += OnRendering;
        // Re-derive the frame from the controller so a return to the workspace resumes exactly
        // where it left off rather than snapping to a default pose.
        RenderCurrent(force: true);
    }

    public void OnDeactivated()
    {
        if (!_renderLoopAttached) return;
        _renderLoopAttached = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    public void ApplyFocus(FocusSubject subject)
    {
        // A subject may be a journey id directly, or the id of a record a journey covers.
        var journey = JourneyCatalog.ById(subject.Id) ?? JourneyCatalog.ForSubject(subject.Id);
        if (journey is null) return;
        SelectJourney(journey, autoPlay: false);
    }

    /// <summary>Used by the capture harness to place the playhead deterministically.</summary>
    public void CaptureSeek(double fraction)
    {
        if (!_running) Launch(JourneyCatalog.Default, autoPlay: false);
        _controller.Pause();
        _controller.ScrubTo(_controller.Duration * Math.Clamp(fraction, 0, 1));
        RenderCurrent(force: true);
    }

    // ------------------------------------------------------------------ render loop

    private void OnRendering(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;
        double delta = now - _lastFrameSeconds;
        _lastFrameSeconds = now;
        if (delta <= 0) return;

        _frameCount++;
        _frameAccumulator += delta;
        if (_frameAccumulator >= 0.5)
        {
            _displayedFps = _frameCount / _frameAccumulator;
            _frameCount = 0;
            _frameAccumulator = 0;
            FrameStat.Text = _running && Services.AppSettings.Current.ShowFrameRate
                ? $"{_displayedFps:00} fps"
                : "";
        }

        if (!_running)
        {
            _idleClock += delta;
            _renderer.Tick(delta, moving: true);
            _renderer.ApplyIdle(_idleClock);
            return;
        }

        _controller.Advance(delta);
        _renderer.Tick(delta, _controller.IsPlaying);
        RenderCurrent(force: false);
    }

    private void RenderCurrent(bool force)
    {
        var snapshot = _controller.Snapshot();
        _renderer.Apply(snapshot);

        ScrubBar.Position = _controller.Position;
        ScrubBar.IsPlaying = _controller.IsPlaying;
        ScrubBar.Timeline = _controller.Timeline;

        if (force || snapshot.StageIndex != _lastStageRendered)
        {
            _lastStageRendered = snapshot.StageIndex;
            UpdateInspector(snapshot.StageIndex);
        }
    }

    // ------------------------------------------------------------------ inspector

    private void UpdateInspector(int stageIndex)
    {
        var journey = _controller.Timeline.Journey;
        if (stageIndex < 0 || stageIndex >= journey.Stages.Count) return;
        var stage = journey.Stages[stageIndex];

        StageTitle.Text = stage.Title;
        StageCaption.Text = stage.Caption;
        StageIndexText.Text = $"STAGE {stageIndex + 1:00} OF {journey.Stages.Count:00}";

        var kindColour = SimulatorContext.KindColour(stage.Kind);
        StageKindText.Text = SimulatorContext.KindLabel(stage.Kind);
        StageKindText.Foreground = Theme.Frozen(kindColour);
        StageKindChip.Background = Theme.Frozen(Theme.WithAlpha(kindColour, 0.14));
        StageKindChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(kindColour, 0.42));

        if (stage.Verdict == HealthState.Unknown)
        {
            VerdictChip.Visibility = Visibility.Collapsed;
        }
        else
        {
            VerdictChip.Visibility = Visibility.Visible;
            var verdict = Theme.ForHealth(stage.Verdict);
            VerdictText.Text = stage.Verdict.Label().ToUpperInvariant();
            VerdictText.Foreground = verdict;
            VerdictChip.Background = Theme.Frozen(Theme.WithAlpha(verdict.Color, 0.14));
            VerdictChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(verdict.Color, 0.42));
        }

        // Module panel.
        var module = OperationsCore.Module(stage.FocusModuleId);
        if (module is null)
        {
            ModulePanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ModulePanel.Visibility = Visibility.Visible;
            ModuleCode.Text = module.Code;
            ModuleName.Text = module.Name;
            ModuleRole.Text = module.Role;
            var node = EstateService.Current.Model.Node(module.ServiceId);
            ModulePip.State = node?.Health ?? HealthState.Unknown;
            ModuleState.Text = node is null ? "—" : $"{node.Name} · {node.Health.Label()}";
            ModuleState.Foreground = Theme.ForHealth(node?.Health ?? HealthState.Unknown);
        }

        DetailList.ItemsSource = stage.Detail
            .Select(d => new DetailLine { Label = d.Label, Value = d.Value, Tone = d.Tone })
            .ToList();

        var links = stage.Links.Select(l => new LinkLine
        {
            Route = Code(l.FromModuleId) + " → " + (l.ToModuleId is null ? "SPN" : Code(l.ToModuleId)),
            Label = l.Label,
            Tone = l.State
        }).ToList();
        LinkList.ItemsSource = links;
        RelationshipHeader.Visibility = links.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        BuildHandoffs(journey, stage);
    }

    private static string Code(string moduleId) => OperationsCore.Module(moduleId)?.Code ?? "—";

    private void BuildHandoffs(Journey journey, JourneyStage stage)
    {
        HandoffPanel.Children.Clear();
        foreach (var handoff in SimulatorContext.HandoffsFor(journey, stage))
        {
            var button = new Button
            {
                Content = handoff.Caption,
                Style = (Style)FindResource("Sim.Button"),
                Margin = new Thickness(0, 0, 8, 8),
                Height = 25,
                FontSize = 11,
                Tag = handoff
            };
            button.Click += Handoff_Click;
            HandoffPanel.Children.Add(button);
        }
    }

    private void Handoff_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Handoff handoff }) return;
        _controller.Pause();
        Navigator.Current.NavigateWithSubject(handoff.Workspace, handoff.Kind, handoff.Id, handoff.Label,
            "Carried from the simulator");
    }

    // ------------------------------------------------------------------ transport

    private void BuildSpeedControls()
    {
        foreach (double speed in _speeds)
        {
            var button = new ToggleButton
            {
                Content = speed.ToString("0.#") + "×",
                Style = (Style)FindResource("Toggle.Segment"),
                Height = 26,
                MinWidth = 40,
                IsChecked = Math.Abs(speed - 1.0) < 0.001,
                Tag = speed
            };
            button.Click += Speed_Click;
            _speedButtons.Add(button);
            SpeedGroup.Children.Add(button);
        }
    }

    private void Speed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked || clicked.Tag is not double speed) return;
        _controller.Speed = speed;
        foreach (var button in _speedButtons) button.IsChecked = ReferenceEquals(button, clicked);
    }

    private void OnPlaybackStateChanged()
    {
        PlayGlyph.Data = (Geometry)FindResource(_controller.IsPlaying ? "I.Pause" : "I.Play");
        PlaybackStateText.Text = _controller.State switch
        {
            PlaybackState.Playing => "RUNNING",
            PlaybackState.Paused => "HELD",
            PlaybackState.Completed => "COMPLETE — REPLAY TO RUN AGAIN",
            PlaybackState.Scrubbing => "SCRUBBING",
            _ => "READY"
        };
        ScrubBar.IsPlaying = _controller.IsPlaying;
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e) => _controller.Toggle();
    private void Replay_Click(object sender, RoutedEventArgs e) { _controller.Replay(); RenderCurrent(true); }
    private void StepBack_Click(object sender, RoutedEventArgs e) { _controller.StepBack(); RenderCurrent(true); }
    private void StepForward_Click(object sender, RoutedEventArgs e) { _controller.StepForward(); RenderCurrent(true); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_running) { base.OnKeyDown(e); return; }
        switch (e.Key)
        {
            case Key.Space: _controller.Toggle(); e.Handled = true; break;
            case Key.R: _controller.Replay(); RenderCurrent(true); e.Handled = true; break;
            case Key.OemComma: _controller.StepBack(); RenderCurrent(true); e.Handled = true; break;
            case Key.OemPeriod: _controller.StepForward(); RenderCurrent(true); e.Handled = true; break;
            case Key.Escape: ShowChooser(); e.Handled = true; break;
        }
        base.OnKeyDown(e);
    }

    // ------------------------------------------------------------------ journey selection

    private void Journey_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (JourneyList.SelectedItem is not JourneyCard card) return;
        ChooserSummary.Text = card.Journey.Summary;
        LaunchButton.IsEnabled = true;
    }

    private void Journey_Key(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Journey_Launch(sender, e);
            e.Handled = true;
        }
    }

    private void Journey_Launch(object sender, RoutedEventArgs e)
    {
        if (JourneyList.SelectedItem is not JourneyCard card) return;
        Launch(card.Journey, Services.AppSettings.Current.AutoPlayJourneys);
    }

    private void SelectJourney(Journey journey, bool autoPlay)
    {
        var card = (JourneyList.ItemsSource as IEnumerable<JourneyCard>)?
            .FirstOrDefault(c => c.Journey.Id == journey.Id);
        if (card is not null) JourneyList.SelectedItem = card;
        Launch(journey, autoPlay);
    }

    private void Launch(Journey journey, bool autoPlay)
    {
        _controller.Load(new JourneyTimeline(journey), autoPlay);
        _lastStageRendered = -1;

        JourneyName.Text = journey.Name;
        DisciplineChip.Text = journey.Discipline.ToUpperInvariant();
        JourneyQuestion.Text = journey.Question;

        var cards = (IEnumerable<JourneyCard>)ScenarioPicker.ItemsSource;
        var match = cards.FirstOrDefault(c => c.Journey.Id == journey.Id);
        ScenarioPicker.SelectionChanged -= Scenario_Changed;
        ScenarioPicker.SelectedItem = match;
        ScenarioPicker.SelectionChanged += Scenario_Changed;

        ShowRunning();
        RenderCurrent(force: true);
        OnPlaybackStateChanged();
        Keyboard.Focus(this);
    }

    private void Scenario_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ScenarioPicker.SelectedItem is not JourneyCard card) return;
        if (card.Journey.Id == _controller.Timeline.Journey.Id) return;
        SelectJourney(card.Journey, Services.AppSettings.Current.AutoPlayJourneys);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowChooser();

    // ------------------------------------------------------------------ mode switching

    private void ShowRunning()
    {
        _running = true;
        // The stage is the area the panels leave free.
        ViewportHost.Margin = new Thickness(0, 46, 392, 116);
        Chooser.Visibility = Visibility.Collapsed;
        TopBar.Visibility = Visibility.Visible;
        Inspector.Visibility = Visibility.Visible;
        Transport.Visibility = Visibility.Visible;
        Fade(TopBar); Fade(Inspector); Fade(Transport);
    }

    private void ShowChooser()
    {
        // Playback is held, not destroyed: returning to the journey resumes from the same beat.
        _controller.Pause();
        _running = false;
        _idleClock = 0;
        ViewportHost.Margin = new Thickness(452, 0, 0, 0);
        Chooser.Visibility = Visibility.Visible;
        TopBar.Visibility = Visibility.Collapsed;
        Inspector.Visibility = Visibility.Collapsed;
        Transport.Visibility = Visibility.Collapsed;
        FrameStat.Text = "";
        Fade(Chooser);
    }

    private static void Fade(UIElement element)
    {
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
