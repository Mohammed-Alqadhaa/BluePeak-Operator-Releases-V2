using System.Reflection;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.App.Workspaces;
using BluePeak.Simulation;
using Xunit;

namespace BluePeak.UiTests;

/// <summary>
/// The lifecycle requirements the brief calls out: navigating away and back, repeated
/// unload/load cycles, and 3D recovery must never lose or corrupt playback state.
/// </summary>
[Collection("sta")]
public class SimulatorLifecycleTests
{
    private readonly StaHost _host;
    public SimulatorLifecycleTests(StaHost host) => _host = host;

    private static SimulatorView View => (SimulatorView)WorkspaceCatalog.ById("simulator")!.View;

    private static PlaybackController Controller(SimulatorView view) =>
        (PlaybackController)typeof(SimulatorView)
            .GetField("_controller", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(view)!;

    private static bool RenderLoopAttached(SimulatorView view) =>
        (bool)typeof(SimulatorView)
            .GetField("_renderLoopAttached", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(view)!;

    [Fact]
    public void The_simulator_constructs_with_a_scene_and_a_loaded_journey()
    {
        _host.Run(() =>
        {
            // The workspace view is a shared singleton, so this asserts what must be true of it
            // at any point in a session rather than assuming a pristine playhead.
            var view = View;
            var controller = Controller(view);
            Assert.True(controller.Duration > 20, "no journey loaded");
            Assert.InRange(controller.Position, 0, controller.Duration);

            var snapshot = controller.Snapshot();
            Assert.Equal(OperationsCore.Modules.Count, snapshot.Poses.Count);
            Assert.InRange(snapshot.StageIndex, 0, controller.Timeline.Journey.Stages.Count - 1);
        });
    }

    [Fact]
    public void Activation_attaches_the_render_loop_and_deactivation_detaches_it()
    {
        _host.Run(() =>
        {
            var view = View;
            view.OnDeactivated();
            Assert.False(RenderLoopAttached(view));
            view.OnActivated();
            Assert.True(RenderLoopAttached(view));
            view.OnDeactivated();
            Assert.False(RenderLoopAttached(view));
        });
    }

    [Fact]
    public void Repeated_activation_cycles_do_not_stack_render_subscriptions()
    {
        _host.Run(() =>
        {
            var view = View;
            for (int i = 0; i < 12; i++)
            {
                view.OnActivated();
                view.OnDeactivated();
            }
            view.OnActivated();
            Assert.True(RenderLoopAttached(view));
            view.OnDeactivated();
        });
    }

    [Fact]
    public void Navigating_away_and_back_preserves_the_playhead_exactly()
    {
        _host.Run(() =>
        {
            Navigator.Current.Navigate("simulator");
            var view = View;
            var controller = Controller(view);

            view.CaptureSeek(0.55);
            double position = controller.Position;
            int stage = controller.StageIndex;
            Assert.True(position > 1);

            Navigator.Current.Navigate("noc");
            Navigator.Current.Navigate("evidence");
            Navigator.Current.Navigate("simulator");

            Assert.Equal(position, Controller(View).Position, 6);
            Assert.Equal(stage, Controller(View).StageIndex);
        });
    }

    [Fact]
    public void The_scene_recovers_the_same_frame_after_a_lifecycle_cycle()
    {
        _host.Run(() =>
        {
            var view = View;
            var controller = Controller(view);
            view.CaptureSeek(0.42);

            var before = controller.Snapshot();
            view.OnDeactivated();
            view.OnActivated();
            var after = controller.Snapshot();

            Assert.Equal(before.StageIndex, after.StageIndex);
            Assert.Equal(before.Camera.Azimuth, after.Camera.Azimuth, 5);
            foreach (var (key, pose) in before.Poses)
            {
                Assert.Equal(pose.Extract, after.Poses[key].Extract, 6);
                Assert.Equal(pose.ShellOpen, after.Poses[key].ShellOpen, 6);
            }
        });
    }

    [Fact]
    public void Every_journey_can_be_selected_and_reaches_its_final_seated_frame()
    {
        _host.Run(() =>
        {
            var view = View;
            var controller = Controller(view);

            foreach (var journey in BluePeak.Simulation.Journeys.JourneyCatalog.All)
            {
                view.ApplyFocus(new FocusSubject(FocusKind.Journey, journey.Id, journey.Name));
                Assert.Equal(journey.Id, controller.Timeline.Journey.Id);

                view.CaptureSeek(1.0);
                var final = controller.Snapshot();
                foreach (var module in OperationsCore.Modules)
                {
                    Assert.Equal(0f, final.Poses[module.Id].Extract, 4);
                    Assert.Equal(0f, final.Poses[module.Id].ShellOpen, 4);
                }
            }
        });
    }

    [Fact]
    public void Changing_scenario_while_a_journey_is_open_resets_cleanly()
    {
        _host.Run(() =>
        {
            var view = View;
            var controller = Controller(view);

            view.ApplyFocus(new FocusSubject(FocusKind.Journey, "journey.dns", "DNS"));
            view.CaptureSeek(0.7);
            Assert.True(controller.Position > 1);

            view.ApplyFocus(new FocusSubject(FocusKind.Journey, "journey.network", "Network"));
            Assert.Equal("journey.network", controller.Timeline.Journey.Id);
            Assert.Equal(0, controller.Position);
            Assert.Equal(0, controller.StageIndex);
        });
    }

    [Fact]
    public void A_scrub_across_the_whole_timeline_never_throws_and_stays_in_bounds()
    {
        _host.Run(() =>
        {
            var view = View;
            var controller = Controller(view);
            view.ApplyFocus(new FocusSubject(FocusKind.Journey, "journey.soc", "SOC"));

            for (double f = -0.2; f <= 1.2; f += 0.017)
            {
                view.CaptureSeek(f);
                Assert.InRange(controller.Position, 0, controller.Duration);
                var snapshot = controller.Snapshot();
                Assert.Equal(OperationsCore.Modules.Count, snapshot.Poses.Count);
            }
        });
    }

    [Fact]
    public void The_simulator_survives_being_driven_through_every_journey_repeatedly()
    {
        _host.Run(() =>
        {
            var view = View;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var journey in BluePeak.Simulation.Journeys.JourneyCatalog.All)
                {
                    view.ApplyFocus(new FocusSubject(FocusKind.Journey, journey.Id, journey.Name));
                    view.OnDeactivated();
                    view.CaptureSeek(0.3);
                    view.OnActivated();
                    view.CaptureSeek(0.8);
                }
            }
            Assert.True(RenderLoopAttached(view));
            view.OnDeactivated();
        });
    }
}
