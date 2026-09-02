# Testing

```bash
pwsh scripts/build.ps1            # build Release with warnings-as-errors, run both suites
pwsh scripts/build.ps1 -Package   # also produce both Windows packages
pwsh scripts/build.ps1 -Capture   # also write design captures of every workspace
```

Test result files are written to `artifacts/engine-tests.trx` and `artifacts/ui-tests.trx`.

## Coverage

**120 tests. 96 engine, 24 user interface. All passing.**

### Simulation engine — `tests/BluePeak.Tests/TimelineTests.cs`

| Assertion | Why it matters |
|---|---|
| Catalog contains the six required journeys, with unique ids | The brief's minimum set |
| Every journey has a complete arc — Establish first, Reassemble last, with Disassemble, Inspect and Verify present | A journey that never opens the machine is not a journey |
| Every stage carries inspection detail | Protocol, expected, actual, evidence and impact must be readable at every beat |
| Every pose, link and focus reference resolves to a real module | Catches an authoring typo before it becomes a silently missing part |
| Evaluation covers every module at every instant | No part can be undefined mid-journey |
| **Evaluation is identical forward, in reverse, and in random access** | This is the scrubbing guarantee, asserted directly |
| The machine starts and ends fully seated | Reassembly genuinely completes |
| The machine actually opens in the middle | Catches a journey that forgets to disassemble |
| **No part moves more than 0.28 units between adjacent 60 fps frames** | No teleporting geometry, no camera jump cuts |
| Stage boundaries map back to the stage they start | Step controls land exactly |
| Clamping is safe outside the timeline | Scrubbing past either end cannot throw |

### Playback transport — `tests/BluePeak.Tests/PlaybackTests.cs`

Play, pause, resume-from-pause, completion and clamping, play-after-completion restart, replay,
step forward and back, step-back-from-mid-stage returning to that stage's start, rate scaling,
stage-changed firing exactly once per stage across a full run, and scenario change resetting cleanly.

Scrubbing is covered specifically:

- Scrub backwards reconstructs the earlier state exactly
- Scrub forwards reconstructs the later state exactly
- Resume after a scrub continues from the release point *and only if it was playing before the drag*
- Scrub from paused stays paused on release
- Scrub to the very end while playing reports completion
- Scrubbing never leaves the timeline bounds

Plus **a stalled render loop cannot skip the timeline** — a single 30-second `Advance` moves the
playhead at most 0.25 s — and a full transport exercise (play, pause, scrub both ways, resume,
step, replay, run to completion) is run against **all six journeys**, asserting each ends fully seated.

### Estate model — `tests/BluePeak.Tests/EstateTests.cs`

| Assertion | Why it matters |
|---|---|
| Every dependency edge resolves to real nodes and declares a protocol | No dangling graph |
| **The dependency graph is acyclic** | A cycle would make first-failure analysis meaningless |
| Every cross-reference resolves — incidents to services, tickets, evidence and changes; alerts to entities and cases; changes to services; runbooks to targets; diagnostic hops to services | The product's "one platform" claim is a data claim first |
| Blast radius walks consumers transitively and excludes providers and itself | Correctness of the core question |
| Dependency closure reaches the foundation | Depth is real, not one level |
| First failure finds the deepest unhealthy component, is idempotent at the origin, and is null for a healthy node | The reasoning that changes operator behaviour |
| Each diagnostic path declares exactly one first failure, that hop is genuinely unhealthy, and no hop before it has hard-failed | A degraded-but-answering hop *is* allowed before it — that is the masked-fault case |
| Every evidence record states its authority and carries a digest; no local record is project-authoritative | The authority boundary the product promises |
| The seed is deterministic for the same clock, including evidence digests | Reproducible captures |
| Layer rollups account for every element | No element falls out of the estate |
| The seeded situation is coherent across workspaces | One fault visible from every discipline |

### Runbook engine — `tests/BluePeak.Tests/RunbookEngineTests.cs`

| Assertion | Why it matters |
|---|---|
| A dry run never executes a mutating step — every one is marked skipped | "Dry run" means it |
| Execution halts at an authorisation gate and **nothing past it is touched** | The gate is real, not advisory |
| Authorising lets the run continue to completion | The gate opens |
| **A failed pre-check halts the run**; no mutating step is left pending-executed | It refuses rather than warning and continuing |
| Reconciling the drift lets the second attempt pass | The failure is a condition, not a hardcoded refusal |
| Abort leaves nothing half-executed | |
| Reset returns every step to pending and clears the log | |
| A read-only runbook completes without any authorisation | Gates are proportionate |
| Every runbook declares Request, Policy, Pre-check, Simulate, Verify and Evidence gates, seals evidence last, and — in every mutating runbook — **the approval gate's index is lower than the first mutating step's** | The safety ordering invariant, asserted structurally |

### User interface and lifecycle — `tests/BluePeak.UiTests/`

Run on a dedicated STA thread hosting a real WPF `Application` with the product's actual resource
dictionaries merged, so views are exercised exactly as they are at runtime.

| Assertion | Why it matters |
|---|---|
| Every one of the 13 workspaces constructs without a markup failure | Catches a broken binding or missing resource |
| Every workspace lays out at the shipped window size (1342×838) | Catches a collapsed or zero-sized region |
| Views are created once and reused across navigation | State survives navigation |
| Navigating the whole rail three times over is stable | |
| Back navigation returns to the previous workspace | |
| A carried subject is adopted by the destination workspace — asserted for **nine** workspace/subject pairs | The context-carrying claim |
| The Service Desk triage decision actually changes the record | The interaction is real, not cosmetic |
| The simulator constructs with a scene and a loaded journey | |
| Activation attaches the render loop; deactivation detaches it | |
| **12 activation cycles do not stack render subscriptions** | The classic leak in this pattern |
| **Navigating away and back preserves the playhead exactly** | The lifecycle requirement, asserted numerically |
| The scene recovers the identical frame after a lifecycle cycle — camera and every module pose | 3D recovery |
| Every journey can be selected and reaches its final seated frame | All six reassemble |
| Changing scenario mid-journey resets cleanly | |
| A scrub across the whole timeline (including out of bounds) never throws | |
| The simulator survives being driven through every journey twice with lifecycle cycles interleaved | |

## Build verification

| Build | Verified |
|---|---|
| Debug | Builds, runs, captures all 17 surfaces |
| Release, warnings-as-errors | Builds clean — zero warnings |
| Framework-dependent win-x64 | Published, **launched, and captured all 17 surfaces** |
| Self-contained win-x64 | Published, **launched, and captured all 17 surfaces** |

Packaged startup is verified by actually running each package with `--capture`, not by checking
that a file exists. Both produce the full capture set, which means every workspace constructed,
laid out and rendered — including the 3D scene — from the packaged binaries.

## Design captures

`--capture <dir>` drives the running application through every workspace, writing a PNG of each,
then returns to the simulator and captures four points along a journey timeline (open, inspect,
diagnose, verify). This is how the design review was done: against rendered pixels, not XAML.

Several defects were found this way and only this way — a clipped clock column, camera framing
that cropped the machine because WPF measures field of view horizontally, off-axis dock centres
on the round modules, truncated labels in the dependency canvas, and a Settings page that stated
a dependency count contradicting the one the same page reported from the live model.
