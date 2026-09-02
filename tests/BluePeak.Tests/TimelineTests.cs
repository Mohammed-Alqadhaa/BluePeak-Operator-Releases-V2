using BluePeak.Simulation;
using BluePeak.Simulation.Journeys;
using Xunit;

namespace BluePeak.Tests;

public class TimelineTests
{
    private static JourneyTimeline Timeline(string id) =>
        new(JourneyCatalog.ById(id) ?? throw new InvalidOperationException(id));

    public static IEnumerable<object[]> AllJourneys() =>
        JourneyCatalog.All.Select(j => new object[] { j.Id });

    [Fact]
    public void Catalog_contains_the_six_required_journeys()
    {
        var ids = JourneyCatalog.All.Select(j => j.Id).ToArray();
        Assert.Contains("journey.ticket", ids);
        Assert.Contains("journey.dns", ids);
        Assert.Contains("journey.auth", ids);
        Assert.Contains("journey.network", ids);
        Assert.Contains("journey.soc", ids);
        Assert.Contains("journey.automation", ids);
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Every_journey_has_a_complete_narrative_arc(string id)
    {
        var journey = JourneyCatalog.ById(id)!;
        Assert.True(journey.Stages.Count >= 8, $"{id} has only {journey.Stages.Count} stages");
        Assert.Equal(StageKind.Establish, journey.Stages[0].Kind);
        Assert.Equal(StageKind.Reassemble, journey.Stages[^1].Kind);
        Assert.Contains(journey.Stages, s => s.Kind == StageKind.Disassemble);
        Assert.Contains(journey.Stages, s => s.Kind == StageKind.Inspect);
        Assert.Contains(journey.Stages, s => s.Kind == StageKind.Verify);
        Assert.All(journey.Stages, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
            Assert.False(string.IsNullOrWhiteSpace(s.Caption));
            Assert.True(s.Duration > 0);
        });
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Every_journey_exposes_inspectable_detail_on_most_stages(string id)
    {
        var journey = JourneyCatalog.ById(id)!;
        int withDetail = journey.Stages.Count(s => s.Detail.Count >= 3);
        Assert.True(withDetail >= journey.Stages.Count - 1,
            $"{id}: only {withDetail} of {journey.Stages.Count} stages carry inspection detail");
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Every_journey_references_only_real_modules(string id)
    {
        var journey = JourneyCatalog.ById(id)!;
        foreach (var stage in journey.Stages)
        {
            foreach (var key in stage.Poses.Keys)
                Assert.True(OperationsCore.Module(key) is not null, $"{id}/{stage.Id}: unknown module {key}");
            foreach (var link in stage.Links)
            {
                Assert.True(OperationsCore.Module(link.FromModuleId) is not null, $"{id}/{stage.Id}: unknown link source {link.FromModuleId}");
                if (link.ToModuleId is not null)
                    Assert.True(OperationsCore.Module(link.ToModuleId) is not null, $"{id}/{stage.Id}: unknown link target {link.ToModuleId}");
            }
            if (stage.FocusModuleId is not null)
                Assert.True(OperationsCore.Module(stage.FocusModuleId) is not null, $"{id}/{stage.Id}: unknown focus {stage.FocusModuleId}");
        }
        foreach (var moduleId in journey.ModulePath)
            Assert.True(OperationsCore.Module(moduleId) is not null, $"{id}: unknown module in path {moduleId}");
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Evaluation_covers_every_module_at_every_instant(string id)
    {
        var timeline = Timeline(id);
        for (double t = 0; t <= timeline.Duration; t += 0.37)
        {
            var snap = timeline.Evaluate(t);
            Assert.Equal(OperationsCore.Modules.Count, snap.Poses.Count);
            foreach (var module in OperationsCore.Modules)
                Assert.True(snap.Poses.ContainsKey(module.Id), $"missing pose for {module.Id} at t={t:F2}");
        }
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Evaluation_is_deterministic_and_order_independent(string id)
    {
        var timeline = Timeline(id);
        var samples = new List<double>();
        for (double t = 0; t <= timeline.Duration; t += 0.41) samples.Add(t);

        // Forward pass.
        var forward = samples.Select(t => timeline.Evaluate(t)).ToList();
        // Reverse pass — scrubbing backwards must reconstruct identical state.
        var reverse = Enumerable.Reverse(samples).Select(t => timeline.Evaluate(t)).Reverse().ToList();
        // Random-access pass — dragging the playhead jumps arbitrarily.
        var rng = new Random(7);
        var shuffled = samples.OrderBy(_ => rng.Next()).ToList();
        var lookup = shuffled.ToDictionary(t => t, t => timeline.Evaluate(t));

        for (int i = 0; i < samples.Count; i++)
        {
            AssertSameState(forward[i], reverse[i]);
            AssertSameState(forward[i], lookup[samples[i]]);
        }
    }

    private static void AssertSameState(SceneSnapshot a, SceneSnapshot b)
    {
        Assert.Equal(a.StageIndex, b.StageIndex);
        Assert.Equal(a.Camera.Azimuth, b.Camera.Azimuth, 5);
        Assert.Equal(a.Camera.Distance, b.Camera.Distance, 5);
        Assert.Equal(a.Camera.Elevation, b.Camera.Elevation, 5);
        foreach (var (key, pose) in a.Poses)
        {
            var other = b.Poses[key];
            Assert.Equal(pose.Extract, other.Extract, 5);
            Assert.Equal(pose.Spin, other.Spin, 5);
            Assert.Equal(pose.Tilt, other.Tilt, 5);
            Assert.Equal(pose.Lift, other.Lift, 5);
            Assert.Equal(pose.ShellOpen, other.ShellOpen, 5);
            Assert.Equal(pose.Emphasis, other.Emphasis, 5);
        }
        Assert.Equal(a.Links.Count, b.Links.Count);
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Machine_starts_and_ends_fully_seated(string id)
    {
        var timeline = Timeline(id);
        var start = timeline.Evaluate(0);
        var end = timeline.Evaluate(timeline.Duration);
        foreach (var module in OperationsCore.Modules)
        {
            Assert.Equal(0f, start.Poses[module.Id].Extract, 4);
            Assert.Equal(0f, start.Poses[module.Id].ShellOpen, 4);
            Assert.Equal(0f, end.Poses[module.Id].Extract, 4);
            Assert.Equal(0f, end.Poses[module.Id].ShellOpen, 4);
            Assert.Equal(0f, end.Poses[module.Id].Lift, 4);
        }
        Assert.True(start.Expansion < 0.02f);
        Assert.True(end.Expansion < 0.02f);
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Machine_actually_opens_in_the_middle(string id)
    {
        var timeline = Timeline(id);
        float peak = 0;
        for (double t = 0; t <= timeline.Duration; t += 0.25)
            peak = Math.Max(peak, timeline.Evaluate(t).Expansion);
        Assert.True(peak > 0.2f, $"{id}: machine never meaningfully disassembles (peak expansion {peak:F3})");
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Motion_is_continuous_with_no_teleporting_parts(string id)
    {
        var timeline = Timeline(id);
        const double step = 1.0 / 60.0;
        SceneSnapshot? previous = null;
        for (double t = 0; t <= timeline.Duration; t += step)
        {
            var snap = timeline.Evaluate(t);
            if (previous is not null)
            {
                foreach (var module in OperationsCore.Modules)
                {
                    float delta = Math.Abs(snap.Poses[module.Id].Extract - previous.Poses[module.Id].Extract);
                    Assert.True(delta < 0.28f,
                        $"{id}: {module.Id} jumped {delta:F3} units in one frame at t={t:F2}");
                }
                float camDelta = Math.Abs(snap.Camera.Distance - previous.Camera.Distance);
                Assert.True(camDelta < 0.6f, $"{id}: camera distance jumped {camDelta:F3} at t={t:F2}");
            }
            previous = snap;
        }
    }

    [Theory]
    [MemberData(nameof(AllJourneys))]
    public void Stage_boundaries_map_back_to_the_stage_they_start(string id)
    {
        var timeline = Timeline(id);
        for (int i = 0; i < timeline.Journey.Stages.Count; i++)
        {
            double start = timeline.SeekToStage(i);
            Assert.Equal(i, timeline.StageAt(start));
            Assert.Equal(i, timeline.Evaluate(start).StageIndex);
        }
    }

    [Fact]
    public void Clamping_is_safe_outside_the_timeline()
    {
        var timeline = Timeline("journey.dns");
        Assert.Equal(0, timeline.Evaluate(-99).StageIndex);
        Assert.Equal(timeline.Journey.Stages.Count - 1, timeline.Evaluate(timeline.Duration + 99).StageIndex);
        Assert.Equal(timeline.Duration, timeline.Evaluate(1e6).Time);
    }
}
