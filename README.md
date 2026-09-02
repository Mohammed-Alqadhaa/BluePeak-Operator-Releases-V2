# BluePeak Operator

**Observe. Diagnose. Act. Verify.**

A Windows desktop operations console for enterprise IT: one estate model, thirteen workspaces,
and a real-time 3D simulator that takes the estate apart to show where a request actually broke.

Built with C# / .NET 10 / WPF. No web view, no embedded browser, no imported 3D assets — every
surface is WPF, and every piece of geometry in the simulator is generated in code.

---

## What it is

Most operations tools answer *what is red*. This one is built around the harder question:
**is this fault mine, or did I inherit it?** That question shapes the whole product.

- The estate is a single dependency graph. Everything else is a view onto it.
- Selecting a subject anywhere carries it everywhere. An incident chosen on the Overview board
  is already selected in NOC, Diagnostics, Changes and Evidence when you get there.
- Diagnosis is an ordered walk with **expected against actual at every hop**. The first hop where
  they diverge is the cause; everything above it is labelled consequence, so you do not act on it.
- Automation is gated. The engine's job is to refuse: policy, pre-check, simulation and an
  authorisation record all have to clear before anything is permitted to write.
- Evidence states its own authority. A record produced on this workstation is *local operator*
  authority and the product will not let that quietly become a project position.

## Running it

### From a package

Two Windows x64 packages are produced under `artifacts/publish/`:

| Package | Requires | Size |
|---|---|---|
| `framework-dependent/BluePeakOperator.exe` | .NET 10 Desktop Runtime installed | ~1 MB |
| `self-contained-x64/BluePeakOperator.exe` | nothing — runtime is included | ~133 MB |

Double-click either executable. There is no installer, no configuration file, no first-run
setup and nothing is written outside the folder you run it from.

### From source

```bash
dotnet run --project src/BluePeak.App/BluePeak.App.csproj
```

### Build, test and package everything

```bash
pwsh scripts/build.ps1 -Package -Capture
```

That restores, builds Release with warnings-as-errors, runs both test suites, produces both
packages, and writes design captures of every workspace to `artifacts/captures/`.

## Getting around

| Key | Goes to |
|---|---|
| `Ctrl+1` … `Ctrl+9`, `Ctrl+0` | Overview, NOC, SOC, Service Desk, Tickets, Incidents, Diagnostics, Infrastructure, Simulator, Automation |

Inside the simulator:

| Key | Does |
|---|---|
| `Space` | Play / pause |
| `,` and `.` | Previous / next stage |
| `R` | Replay |
| `Esc` | Back to the journey list |
| `←` `→` on the timeline | Scrub by one second (hold `Shift` for five) |

The timeline is draggable anywhere along its length. Every segment is one stage, sized to its
real duration, and clicking a segment seeks to it.

## The workspaces

Navigation is grouped by the operator loop rather than by product module, because that is the
order the questions actually arrive in.

**Observe** — Overview, NOC, SOC
**Respond** — Service Desk, Tickets, Incidents
**Diagnose** — Diagnostics, Infrastructure, Simulator
**Act** — Automation, Changes
**Verify** — Evidence

| Workspace | The question it answers |
|---|---|
| Overview | What needs attention, ranked by consequence, with the next action attached |
| NOC | Is this fault mine or inherited, and what is exposed if it gets worse |
| SOC | Are these separate alerts, or one actor on one subject |
| Service Desk | Does this contact already have a cause somebody else owns |
| Tickets | Where is the SLA position and what is this contact linked to |
| Incidents | What is the impact, the cause, and the correction in flight |
| Diagnostics | Where exactly did expected and actual diverge |
| Infrastructure | What breaks if this fails, and what is it degraded by |
| Simulator | Show me the machine and take it apart along the failing path |
| Automation | Can this be run safely, and what will stop it if not |
| Changes | What are the consequences before I approve this |
| Evidence | What was claimed, what was checked, and who may assert it |
| Settings | What this build is permitted to do |

## The simulator

The estate is rendered as one engineered machine — the **BluePeak Operations Core**. A central
spine carries four stacked decks of chassis wedges; each wedge is a functional subsystem and
each docks to the spine through a visible connector.

When a journey runs, the machine opens along the dependency path that journey exercises. Wedges
disengage, clamshells split, and the mechanism inside becomes visible — a resolver is an indexed
drum, identity is a sealed vault behind a keyed collar, switching is a crossbar lattice, and so
on. At the end the machine reassembles and locks.

Six journeys ship: Service desk contact, Name resolution failure, Authentication and trust,
Network path failure, Security detection and response, and Gated automation run. Each has its
own dependency story and its own choreography — they are not one animation with different labels.

See [docs/SIMULATOR.md](docs/SIMULATOR.md) for the design and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
for why scene state is a pure function of playhead position.

## Documentation

- [Architecture](docs/ARCHITECTURE.md) — layers, the timeline model, why the simulator scrubs exactly
- [Design system](docs/DESIGN-SYSTEM.md) — tokens, composition rules, and what is deliberately absent
- [Simulator](docs/SIMULATOR.md) — the machine, the journeys, the motion language
- [Testing](docs/TESTING.md) — what is covered and the evidence
- [Known limitations](docs/KNOWN-LIMITATIONS.md) — what this build does not do

## Safety

This build observes and simulates. It holds no credentials, has no configured endpoint and
contains no network client. Every action described as an execution runs against the in-memory
estate model inside the process. The Settings workspace states this contract in full and the
Evidence workspace enforces the authority boundary on every record.
