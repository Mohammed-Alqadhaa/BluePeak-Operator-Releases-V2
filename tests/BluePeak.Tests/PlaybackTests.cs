using BluePeak.Simulation;
using BluePeak.Simulation.Journeys;
using Xunit;

namespace BluePeak.Tests;

public class PlaybackTests
{
    private static PlaybackController Controller(string id = "journey.dns")
        => new(new JourneyTimeline(JourneyCatalog.ById(id)!));

    private static void Run(PlaybackController c, double seconds, double frame = 1.0 / 60.0)
    {
        for (double t = 0; t < seconds; t += frame) c.Advance(frame);
    }

    [Fact]
    public void Starts_idle_at_zero()
    {
        var c = Controller();
        Assert.Equal(PlaybackState.Idle, c.State);
        Assert.Equal(0, c.Position);
        Assert.False(c.IsPlaying);
    }

    [Fact]
    public void Play_advances_and_pause_holds()
    {
        var c = Controller();
        c.Play();
        Run(c, 3.0);
        double at = c.Position;
        Assert.InRange(at, 2.8, 3.2);

        c.Pause();
        Run(c, 2.0);
        Assert.Equal(at, c.Position, 6);
        Assert.Equal(PlaybackState.Paused, c.State);
    }

    [Fact]
    public void Resume_continues_from_where_it_paused()
    {
        var c = Controller();
        c.Play();
        Run(c, 4.0);
        c.Pause();
        double at = c.Position;
        c.Play();
        Run(c, 2.0);
        Assert.InRange(c.Position - at, 1.8, 2.2);
    }

    [Fact]
    public void Playback_completes_and_clamps_at_the_end()
    {
        var c = Controller();
        bool completed = false;
        c.Completed += () => completed = true;
        c.Play();
        c.Speed = 8;
        Run(c, c.Duration + 5);
        Assert.True(completed);
        Assert.Equal(PlaybackState.Completed, c.State);
        Assert.Equal(c.Duration, c.Position, 6);
    }

    [Fact]
    public void Play_after_completion_restarts_from_zero()
    {
        var c = Controller();
        c.Play();
        c.Speed = 20;
        Run(c, c.Duration + 2);
        Assert.True(c.IsComplete);
        c.Play();
        Assert.Equal(0, c.Position);
        Assert.True(c.IsPlaying);
    }

    [Fact]
    public void Scrub_backward_reconstructs_the_earlier_state_exactly()
    {
        var c = Controller();
        var early = c.Timeline.Evaluate(6.0);

        c.Play();
        c.Speed = 4;
        Run(c, 12);

        c.BeginScrub();
        c.ScrubTo(6.0);
        var scrubbed = c.Snapshot();
        c.EndScrub();

        Assert.Equal(early.StageIndex, scrubbed.StageIndex);
        foreach (var (key, pose) in early.Poses)
        {
            Assert.Equal(pose.Extract, scrubbed.Poses[key].Extract, 6);
            Assert.Equal(pose.Spin, scrubbed.Poses[key].Spin, 6);
            Assert.Equal(pose.Emphasis, scrubbed.Poses[key].Emphasis, 6);
        }
        Assert.Equal(early.Camera.Azimuth, scrubbed.Camera.Azimuth, 5);
    }

    [Fact]
    public void Scrub_forward_reconstructs_the_later_state_exactly()
    {
        var c = Controller();
        double target = c.Duration * 0.8;
        var expected = c.Timeline.Evaluate(target);

        c.BeginScrub();
        c.ScrubTo(target);
        var actual = c.Snapshot();
        c.EndScrub();

        Assert.Equal(expected.StageIndex, actual.StageIndex);
        foreach (var (key, pose) in expected.Poses)
            Assert.Equal(pose.Extract, actual.Poses[key].Extract, 6);
    }

    [Fact]
    public void Resume_after_scrub_continues_from_the_release_point()
    {
        var c = Controller();
        c.Play();
        Run(c, 2);
        c.BeginScrub();
        c.ScrubTo(20.0);
        c.EndScrub();

        Assert.True(c.IsPlaying);          // it was playing before the drag, so it keeps playing
        Assert.Equal(20.0, c.Position, 6);
        Run(c, 1.0);
        Assert.InRange(c.Position, 20.9, 21.1);
    }

    [Fact]
    public void Scrub_from_paused_stays_paused_on_release()
    {
        var c = Controller();
        c.Play();
        Run(c, 2);
        c.Pause();

        c.BeginScrub();
        c.ScrubTo(15.0);
        c.EndScrub();

        Assert.Equal(PlaybackState.Paused, c.State);
        Run(c, 1.0);
        Assert.Equal(15.0, c.Position, 6);
    }

    [Fact]
    public void Scrub_to_the_very_end_while_playing_reports_completion()
    {
        var c = Controller();
        c.Play();
        c.BeginScrub();
        c.ScrubTo(c.Duration);
        c.EndScrub();
        Assert.Equal(PlaybackState.Completed, c.State);
    }

    [Fact]
    public void Scrubbing_never_leaves_the_timeline_bounds()
    {
        var c = Controller();
        c.BeginScrub();
        c.ScrubTo(-500);
        Assert.Equal(0, c.Position);
        c.ScrubTo(99999);
        Assert.Equal(c.Duration, c.Position, 6);
        c.EndScrub();
    }

    [Fact]
    public void Replay_returns_to_the_start_and_plays()
    {
        var c = Controller();
        c.Play();
        c.Speed = 6;
        Run(c, c.Duration + 1);
        c.Replay();
        Assert.Equal(0, c.Position);
        Assert.True(c.IsPlaying);
        Run(c, 1);
        Assert.True(c.Position > 0);
    }

    [Fact]
    public void Step_controls_land_exactly_on_stage_boundaries()
    {
        var c = Controller();
        for (int i = 0; i < c.Timeline.Journey.Stages.Count; i++)
        {
            c.SeekStage(i);
            Assert.Equal(i, c.StageIndex);
            Assert.Equal(c.Timeline.SeekToStage(i), c.Position, 6);
        }

        c.SeekStage(3);
        c.StepForward();
        Assert.Equal(4, c.StageIndex);
        c.StepBack();
        Assert.Equal(3, c.StageIndex);
    }

    [Fact]
    public void Step_back_from_mid_stage_returns_to_the_start_of_that_stage()
    {
        var c = Controller();
        c.SeekStage(4);
        c.Play();
        Run(c, 1.2);
        Assert.Equal(4, c.StageIndex);
        c.StepBack();
        Assert.Equal(4, c.StageIndex);
        Assert.Equal(c.Timeline.SeekToStage(4), c.Position, 6);
        c.StepBack();
        Assert.Equal(3, c.StageIndex);
    }

    [Fact]
    public void Stage_changed_fires_once_per_stage_during_a_full_run()
    {
        var c = Controller();
        var seen = new List<int>();
        c.StageChanged += seen.Add;
        c.Play();
        c.Speed = 3;
        Run(c, c.Duration + 2);
        Assert.Equal(Enumerable.Range(0, c.Timeline.Journey.Stages.Count), seen);
    }

    [Fact]
    public void Speed_scales_the_playhead_proportionally()
    {
        var a = Controller();
        var b = Controller();
        a.Play();
        b.Play();
        b.Speed = 2.0;
        Run(a, 4);
        Run(b, 4);
        Assert.InRange(b.Position / a.Position, 1.9, 2.1);
    }

    [Fact]
    public void A_stalled_render_loop_cannot_skip_the_timeline()
    {
        var c = Controller();
        c.Play();
        c.Advance(30.0);   // simulates a long stall, e.g. the window being restored
        Assert.True(c.Position <= 0.26, $"a single stalled frame advanced {c.Position:F3}s");
    }

    [Fact]
    public void Changing_scenario_resets_the_transport_cleanly()
    {
        var c = Controller();
        c.Play();
        c.Speed = 4;
        Run(c, 10);

        var next = new JourneyTimeline(JourneyCatalog.ById("journey.soc")!);
        c.Load(next, autoPlay: false);

        Assert.Equal(0, c.Position);
        Assert.Equal(PlaybackState.Idle, c.State);
        Assert.Equal(next.Duration, c.Duration, 6);
        Assert.Equal(0, c.StageIndex);
    }

    [Fact]
    public void Every_journey_survives_a_full_transport_exercise()
    {
        foreach (var journey in JourneyCatalog.All)
        {
            var c = new PlaybackController(new JourneyTimeline(journey));
            c.Play();
            c.Speed = 5;
            Run(c, journey.Duration / 5 * 0.4);
            c.Pause();
            c.BeginScrub();
            c.ScrubTo(journey.Duration * 0.9);
            c.ScrubTo(journey.Duration * 0.15);
            c.EndScrub();
            c.Play();
            Run(c, 2);
            c.StepForward();
            c.StepBack();
            c.Replay();
            Run(c, journey.Duration + 1);
            Assert.Equal(PlaybackState.Completed, c.State);
            Assert.Equal(journey.Duration, c.Position, 5);

            var final = c.Snapshot();
            foreach (var module in OperationsCore.Modules)
                Assert.Equal(0f, final.Poses[module.Id].Extract, 4);
        }
    }
}
