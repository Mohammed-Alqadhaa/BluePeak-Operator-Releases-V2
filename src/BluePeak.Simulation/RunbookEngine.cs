using BluePeak.Domain;

namespace BluePeak.Simulation;

public enum RunMode
{
    /// <summary>Read-only: gates evaluate and simulate, execution stages are skipped.</summary>
    DryRun,
    /// <summary>Full sequence including execution, which still requires an authorisation gate.</summary>
    Gated
}

public enum RunOutcome { Idle, Running, WaitingApproval, Blocked, Completed, Aborted }

/// <summary>
/// Executes a runbook as a sequence of gates against the local simulation. The engine's
/// purpose is to refuse: it will not run a mutating step without an authorisation, and it
/// halts the whole run when a pre-check assertion fails rather than continuing past it.
/// </summary>
public sealed class RunbookEngine
{
    private readonly Runbook _runbook;
    private CancellationTokenSource? _cancellation;
    private int _index;
    private bool _authorised;

    public RunbookEngine(Runbook runbook)
    {
        _runbook = runbook;
        Reset();
    }

    public Runbook Runbook => _runbook;
    public RunOutcome Outcome { get; private set; } = RunOutcome.Idle;
    public RunMode Mode { get; private set; } = RunMode.DryRun;
    public int Index => _index;
    public bool IsAuthorised => _authorised;
    public List<string> Log { get; } = new();

    public event Action? Changed;

    public void Reset()
    {
        _cancellation?.Cancel();
        _cancellation = null;
        _index = 0;
        _authorised = false;
        Outcome = RunOutcome.Idle;
        Log.Clear();
        foreach (var step in _runbook.Steps) step.State = GateState.Pending;
        foreach (var step in _runbook.Steps) step.Output = "";
        Changed?.Invoke();
    }

    public void Abort()
    {
        _cancellation?.Cancel();
        _cancellation = null;
        if (Outcome is RunOutcome.Running or RunOutcome.WaitingApproval)
        {
            foreach (var step in _runbook.Steps.Where(s => s.State == GateState.Running))
                step.State = GateState.Pending;
            Outcome = RunOutcome.Aborted;
            Log.Add("Run aborted by the operator. No further step was attempted.");
            Changed?.Invoke();
        }
    }

    public void Authorise()
    {
        if (Outcome != RunOutcome.WaitingApproval) return;
        _authorised = true;
        var step = _runbook.Steps[_index];
        step.State = GateState.Passed;
        step.Output = "Authorised by the operator against an approved change record.";
        Log.Add($"{step.Gate} · {step.Name} — authorised");
        _index++;
        Changed?.Invoke();
        _ = RunAsync(Mode);
    }

    public async Task RunAsync(RunMode mode)
    {
        Mode = mode;
        _cancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        Outcome = RunOutcome.Running;
        Changed?.Invoke();

        try
        {
            while (_index < _runbook.Steps.Count)
            {
                if (cancellation.IsCancellationRequested) return;
                var step = _runbook.Steps[_index];

                // Approval gate: stop and wait. The engine does not proceed on its own.
                if (step.RequiresApproval && !_authorised)
                {
                    step.State = GateState.WaitingApproval;
                    step.Output = "Held. Execution requires an authorisation that has not been given.";
                    Outcome = RunOutcome.WaitingApproval;
                    Log.Add($"{step.Gate} · {step.Name} — held for authorisation");
                    Changed?.Invoke();
                    return;
                }

                // Mutating step under a dry run: skipped, never silently executed.
                if (step.Mutating && mode == RunMode.DryRun)
                {
                    step.State = GateState.Skipped;
                    step.Output = "Skipped. A dry run never writes.";
                    Log.Add($"{step.Gate} · {step.Name} — skipped, dry run");
                    _index++;
                    Changed?.Invoke();
                    await Task.Delay(180, cancellation.Token);
                    continue;
                }

                step.State = GateState.Running;
                Changed?.Invoke();
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(step.EstimatedSeconds * 0.28, 0.25, 1.1)), cancellation.Token);
                if (cancellation.IsCancellationRequested) return;

                // The seeded failure: this runbook's drift pre-check is designed to catch a real
                // condition in the estate, and catching it must stop the run.
                bool fails = ShouldFail(step);
                if (fails)
                {
                    step.State = GateState.Failed;
                    step.Output = FailureDetail(step);
                    Outcome = RunOutcome.Blocked;
                    Log.Add($"{step.Gate} · {step.Name} — FAILED, run halted");
                    Changed?.Invoke();
                    return;
                }

                step.State = GateState.Passed;
                step.Output = SuccessDetail(step, mode);
                Log.Add($"{step.Gate} · {step.Name} — passed");
                _index++;
                Changed?.Invoke();
            }

            Outcome = RunOutcome.Completed;
            _runbook.LastRunAt = DateTime.Now;
            _runbook.RunCount++;
            _runbook.LastRunResult = mode == RunMode.DryRun
                ? "Dry run completed — no state changed"
                : "Completed and verified";
            Log.Add(mode == RunMode.DryRun
                ? "Dry run complete. Nothing was written; the estate is unchanged."
                : "Run complete. Verification passed and the record is sealed.");
            Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Abort() has already recorded the outcome.
        }
    }

    /// <summary>
    /// Drift is only reported the first time RB-014 is exercised, which is the behaviour an
    /// operator would see: reconcile the drift, re-run, and the gate then clears.
    /// </summary>
    private bool ShouldFail(RunbookStep step) =>
        _runbook.Id == "RB-014" &&
        step.Name.Contains("configuration drift", StringComparison.OrdinalIgnoreCase) &&
        _runbook.RunCount == 7;

    private static string FailureDetail(RunbookStep step) =>
        "Resolver 2 carries an undocumented manual edit to its forwarder list. Applying the template "
        + "would destroy it silently. Reconcile the drift into source of truth, then run again.";

    private static string SuccessDetail(RunbookStep step, RunMode mode) => step.Gate switch
    {
        "Request" => "Inputs captured and bound to the initiating record.",
        "Policy" => "Operator role verified, target in scope, no change freeze active.",
        "Pre-check" => "Assertion held against the live estate.",
        "Simulate" => "Modelled without writing. Predicted outcome recorded for comparison.",
        "Execute" => mode == RunMode.DryRun ? "Skipped in dry run." : "Applied, and the control query confirmed the result.",
        "Verify" => "Asserted from the consumer's position, not from the target's own status.",
        "Evidence" => "Inputs, outputs and verification hashed into the ledger.",
        _ => "Completed."
    };

    public IReadOnlyList<GateGroup> Groups()
    {
        var groups = new List<GateGroup>();
        foreach (var step in _runbook.Steps)
        {
            var group = groups.FirstOrDefault(g => g.Name == step.Gate);
            if (group is null)
            {
                group = new GateGroup { Name = step.Gate };
                groups.Add(group);
            }
            group.Steps.Add(step);
        }
        return groups;
    }
}

public sealed class GateGroup
{
    public required string Name { get; init; }
    public List<RunbookStep> Steps { get; } = new();

    public GateState State
    {
        get
        {
            if (Steps.Any(s => s.State == GateState.Failed)) return GateState.Failed;
            if (Steps.Any(s => s.State == GateState.Blocked)) return GateState.Blocked;
            if (Steps.Any(s => s.State == GateState.WaitingApproval)) return GateState.WaitingApproval;
            if (Steps.Any(s => s.State == GateState.Running)) return GateState.Running;
            if (Steps.All(s => s.State is GateState.Passed or GateState.Skipped)) return GateState.Passed;
            return GateState.Pending;
        }
    }

    public string Progress => $"{Steps.Count(s => s.State is GateState.Passed or GateState.Skipped)}/{Steps.Count}";
}
