using BluePeak.Domain;
using BluePeak.Domain.Seed;
using BluePeak.Simulation;
using Xunit;

namespace BluePeak.Tests;

public class RunbookEngineTests
{
    private static Runbook Book(string id) =>
        EstateSeed.Build(new DateTime(2026, 9, 2, 9, 45, 0)).Runbook(id)
        ?? throw new InvalidOperationException(id);

    [Fact]
    public async Task A_dry_run_never_executes_a_mutating_step()
    {
        var runbook = Book("RB-007");
        var engine = new RunbookEngine(runbook);
        await engine.RunAsync(RunMode.DryRun);

        Assert.Equal(RunOutcome.WaitingApproval, engine.Outcome);
        engine.Authorise();
        await WaitUntilSettled(engine);

        Assert.Equal(RunOutcome.Completed, engine.Outcome);
        foreach (var step in runbook.Steps.Where(s => s.Mutating))
            Assert.Equal(GateState.Skipped, step.State);
    }

    [Fact]
    public async Task Execution_halts_at_an_authorisation_gate_and_goes_no_further()
    {
        var runbook = Book("RB-007");
        var engine = new RunbookEngine(runbook);
        await engine.RunAsync(RunMode.Gated);

        Assert.Equal(RunOutcome.WaitingApproval, engine.Outcome);

        int gateIndex = runbook.Steps.FindIndex(s => s.RequiresApproval);
        Assert.Equal(GateState.WaitingApproval, runbook.Steps[gateIndex].State);

        // Nothing past the gate has been touched.
        for (int i = gateIndex + 1; i < runbook.Steps.Count; i++)
            Assert.Equal(GateState.Pending, runbook.Steps[i].State);
    }

    [Fact]
    public async Task Authorising_lets_the_run_continue_to_completion()
    {
        var runbook = Book("RB-007");
        var engine = new RunbookEngine(runbook);
        await engine.RunAsync(RunMode.Gated);
        Assert.Equal(RunOutcome.WaitingApproval, engine.Outcome);

        engine.Authorise();
        await WaitUntilSettled(engine);

        Assert.Equal(RunOutcome.Completed, engine.Outcome);
        Assert.All(runbook.Steps, s => Assert.True(s.State is GateState.Passed or GateState.Skipped,
            $"{s.Name} ended {s.State}"));
    }

    [Fact]
    public async Task A_failed_precheck_halts_the_run_instead_of_warning_and_continuing()
    {
        // RB-014 is seeded to report configuration drift on its first exercise.
        var runbook = Book("RB-014");
        Assert.Equal(7, runbook.RunCount);

        var engine = new RunbookEngine(runbook);
        await engine.RunAsync(RunMode.Gated);

        Assert.Equal(RunOutcome.Blocked, engine.Outcome);

        var failed = runbook.Steps.Single(s => s.State == GateState.Failed);
        Assert.Contains("drift", failed.Name, StringComparison.OrdinalIgnoreCase);

        int failedIndex = runbook.Steps.IndexOf(failed);
        for (int i = failedIndex + 1; i < runbook.Steps.Count; i++)
            Assert.Equal(GateState.Pending, runbook.Steps[i].State);
        Assert.All(runbook.Steps.Where(s => s.Mutating), s => Assert.Equal(GateState.Pending, s.State));
    }

    [Fact]
    public async Task Reconciling_the_drift_lets_the_second_attempt_pass_the_gate()
    {
        var runbook = Book("RB-014");
        var engine = new RunbookEngine(runbook);
        await engine.RunAsync(RunMode.Gated);
        Assert.Equal(RunOutcome.Blocked, engine.Outcome);

        // The operator reconciles the drift into source of truth; the run count moves on.
        runbook.RunCount++;
        engine.Reset();
        await engine.RunAsync(RunMode.Gated);

        Assert.Equal(RunOutcome.WaitingApproval, engine.Outcome);
        Assert.DoesNotContain(runbook.Steps, s => s.State == GateState.Failed);
    }

    [Fact]
    public async Task Abort_stops_the_run_and_leaves_nothing_half_executed()
    {
        var runbook = Book("RB-058");
        var engine = new RunbookEngine(runbook);
        var run = engine.RunAsync(RunMode.Gated);
        await Task.Delay(220);
        engine.Abort();
        await run;

        Assert.Equal(RunOutcome.Aborted, engine.Outcome);
        Assert.DoesNotContain(runbook.Steps, s => s.State == GateState.Running);
    }

    [Fact]
    public async Task Reset_returns_every_step_to_pending()
    {
        var runbook = Book("RB-045");
        var engine = new RunbookEngine(runbook);
        await engine.RunAsync(RunMode.Gated);
        await WaitUntilSettled(engine);

        engine.Reset();
        Assert.Equal(RunOutcome.Idle, engine.Outcome);
        Assert.All(runbook.Steps, s => Assert.Equal(GateState.Pending, s.State));
        Assert.Empty(engine.Log);
    }

    [Fact]
    public async Task A_read_only_runbook_completes_without_any_authorisation()
    {
        var runbook = Book("RB-045");
        Assert.DoesNotContain(runbook.Steps, s => s.Mutating);

        var engine = new RunbookEngine(runbook);
        await engine.RunAsync(RunMode.Gated);
        await WaitUntilSettled(engine);

        Assert.Equal(RunOutcome.Completed, engine.Outcome);
    }

    [Fact]
    public void Every_runbook_declares_the_gates_the_product_promises()
    {
        var model = EstateSeed.Build(new DateTime(2026, 9, 2, 9, 45, 0));
        foreach (var runbook in model.Runbooks)
        {
            var gates = runbook.Steps.Select(s => s.Gate).Distinct().ToList();
            Assert.Contains("Request", gates);
            Assert.Contains("Policy", gates);
            Assert.Contains("Pre-check", gates);
            Assert.Contains("Simulate", gates);
            Assert.Contains("Verify", gates);
            Assert.Contains("Evidence", gates);

            // A mutating runbook must have an approval gate, and it must come before every write.
            if (runbook.Steps.Any(s => s.Mutating))
            {
                int approval = runbook.Steps.FindIndex(s => s.RequiresApproval);
                Assert.True(approval >= 0, $"{runbook.Id} mutates without an approval gate");
                int firstMutation = runbook.Steps.FindIndex(s => s.Mutating);
                Assert.True(approval < firstMutation,
                    $"{runbook.Id} writes at step {firstMutation} before its gate at {approval}");
            }

            // Evidence is always sealed last.
            Assert.Equal("Evidence", runbook.Steps[^1].Gate);
        }
    }

    private static async Task WaitUntilSettled(RunbookEngine engine)
    {
        for (int i = 0; i < 300 && engine.Outcome == RunOutcome.Running; i++)
            await Task.Delay(50);
    }
}
