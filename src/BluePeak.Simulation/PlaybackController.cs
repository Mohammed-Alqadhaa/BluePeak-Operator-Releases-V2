namespace BluePeak.Simulation;

public enum PlaybackState { Idle, Playing, Paused, Completed, Scrubbing }

/// <summary>
/// Transport for a journey timeline. Holds nothing but a position, a rate and a mode, which
/// is why detaching and re-attaching the renderer cannot corrupt playback: the controller is
/// the only owner of time, and the scene is derived from it.
/// </summary>
public sealed class PlaybackController
{
    private JourneyTimeline _timeline;
    private double _position;
    private PlaybackState _state = PlaybackState.Idle;
    private PlaybackState _stateBeforeScrub = PlaybackState.Idle;

    public PlaybackController(JourneyTimeline timeline) => _timeline = timeline;

    public JourneyTimeline Timeline => _timeline;
    public double Duration => _timeline.Duration;
    public double Speed { get; set; } = 1.0;

    public double Position
    {
        get => _position;
        private set => _position = Math.Clamp(value, 0, Duration);
    }

    public PlaybackState State => _state;
    public bool IsPlaying => _state == PlaybackState.Playing;
    public bool IsScrubbing => _state == PlaybackState.Scrubbing;
    public bool IsComplete => _state == PlaybackState.Completed;
    public double Progress => Duration <= 0 ? 0 : Position / Duration;
    public int StageIndex => _timeline.StageAt(Position);
    public JourneyStage Stage => _timeline.Journey.Stages[Math.Clamp(StageIndex, 0, _timeline.Journey.Stages.Count - 1)];

    public event Action<int>? StageChanged;
    public event Action? Completed;
    public event Action? StateChanged;

    private int _lastStage = -1;

    public void Load(JourneyTimeline timeline, bool autoPlay)
    {
        _timeline = timeline;
        _position = 0;
        _lastStage = -1;
        _state = autoPlay ? PlaybackState.Playing : PlaybackState.Idle;
        RaiseStage();
        StateChanged?.Invoke();
    }

    public void Play()
    {
        if (Duration <= 0) return;
        if (_state == PlaybackState.Completed || Position >= Duration - 1e-6) Position = 0;
        _state = PlaybackState.Playing;
        StateChanged?.Invoke();
    }

    public void Pause()
    {
        if (_state is PlaybackState.Playing or PlaybackState.Scrubbing)
        {
            _state = PlaybackState.Paused;
            StateChanged?.Invoke();
        }
    }

    public void Toggle()
    {
        if (IsPlaying) Pause(); else Play();
    }

    public void Replay()
    {
        Position = 0;
        _lastStage = -1;
        _state = PlaybackState.Playing;
        RaiseStage();
        StateChanged?.Invoke();
    }

    public void Restart(bool play)
    {
        Position = 0;
        _lastStage = -1;
        _state = play ? PlaybackState.Playing : PlaybackState.Idle;
        RaiseStage();
        StateChanged?.Invoke();
    }

    /// <summary>Begin a drag. Playback is suspended but the pre-drag mode is remembered.</summary>
    public void BeginScrub()
    {
        if (_state == PlaybackState.Scrubbing) return;
        _stateBeforeScrub = _state;
        _state = PlaybackState.Scrubbing;
        StateChanged?.Invoke();
    }

    public void ScrubTo(double time)
    {
        Position = time;
        RaiseStage();
    }

    /// <summary>
    /// End a drag. Playback continues from wherever the playhead was released, and only
    /// resumes automatically if it was running when the drag started.
    /// </summary>
    public void EndScrub()
    {
        if (_state != PlaybackState.Scrubbing) return;
        _state = _stateBeforeScrub switch
        {
            PlaybackState.Playing => Position >= Duration - 1e-6 ? PlaybackState.Completed : PlaybackState.Playing,
            PlaybackState.Completed => Position >= Duration - 1e-6 ? PlaybackState.Completed : PlaybackState.Paused,
            PlaybackState.Idle => PlaybackState.Paused,
            _ => PlaybackState.Paused
        };
        StateChanged?.Invoke();
    }

    /// <summary>Jump straight to a stage boundary. Used by step controls and the stage list.</summary>
    public void SeekStage(int index)
    {
        int clamped = Math.Clamp(index, 0, _timeline.Journey.Stages.Count - 1);
        Position = _timeline.SeekToStage(clamped);
        if (_state == PlaybackState.Completed) _state = PlaybackState.Paused;
        RaiseStage();
        StateChanged?.Invoke();
    }

    public void StepBack()
    {
        int current = StageIndex;
        // If we are more than a moment into the stage, go to its start rather than the previous one.
        double intoStage = Position - _timeline.StageStart(current);
        SeekStage(intoStage > 0.35 ? current : current - 1);
    }

    public void StepForward() => SeekStage(StageIndex + 1);

    /// <summary>Advance the playhead. Called once per rendered frame with the real elapsed time.</summary>
    public void Advance(double deltaSeconds)
    {
        if (_state != PlaybackState.Playing || Duration <= 0) return;
        // Guard against a stalled render loop dumping a huge delta into the timeline.
        double dt = Math.Clamp(deltaSeconds, 0, 0.25) * Speed;
        Position += dt;
        RaiseStage();
        if (Position >= Duration - 1e-6)
        {
            Position = Duration;
            _state = PlaybackState.Completed;
            Completed?.Invoke();
            StateChanged?.Invoke();
        }
    }

    public SceneSnapshot Snapshot() => _timeline.Evaluate(Position);

    private void RaiseStage()
    {
        int s = StageIndex;
        if (s == _lastStage) return;
        _lastStage = s;
        StageChanged?.Invoke(s);
    }
}
