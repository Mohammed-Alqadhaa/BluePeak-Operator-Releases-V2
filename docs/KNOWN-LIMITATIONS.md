# Known limitations

Stated plainly. Anything here is a real boundary of this build, not a defect list to be
discovered later.

## Scope of the build

**It does not connect to anything.** There is no collector, no credential store, no configured
endpoint and no network client compiled in. Every action described as an execution runs against
the in-memory estate model inside the process. This is deliberate and is stated on the Settings
workspace and in the title bar, not buried here.

**The estate is a fixture, not a discovery.** 35 elements and 58 dependencies are seeded from a
fixed seed at launch, so every run shows the same situation and captures are reproducible. Names,
people and addresses are fictional; addresses come from documentation ranges reserved for examples.

**Nothing persists.** Triage decisions, recorded approvals and runbook runs live in memory and are
discarded on exit. This is intentional — every launch starts from the same fixture — but it means
you cannot leave a session half-finished and return to it.

**Time does not advance.** The estate's clock is anchored at launch. Ages and SLA positions are
computed against that anchor, so a window left open for an hour will not show an hour of drift.

## Interaction

**Approvals and triage decisions are local.** Recording an approval in Changes or attaching a
contact in Service Desk mutates the session model and says so. It is not an authorisation for
anything outside the process, and the Evidence workspace will not let a locally produced record
present itself as project-authoritative.

**The simulator's 3D scene is not directly pickable.** Modules are selected by the journey and by
the inspector, not by clicking geometry. WPF hit-tests 3D in software and the meshes are large
enough that enabling it would cost frames for an interaction the product does not need. Camera
control is choreographed rather than free-orbit, for the same reason the brief gives: the operator
must not lose orientation.

**There is no free-form query language.** Filters and search are per-workspace and field-scoped.
A product at this maturity would eventually want a query surface; this build does not have one.

**No dark/light theming.** The product is dark only. A monitoring console viewed for hours in low
ambient light does not need a light mode, and shipping a half-tuned one would be worse than none.

## Data model

**The dependency graph is single-edged and acyclic.** Two services relate through one modelled
edge, and cycles are rejected by test. Real estates contain both multiple relationships between the
same pair and genuine cycles; first-failure analysis would need to change to handle them.

**Health is authored, not computed.** Node health is part of the fixture rather than derived from
the metric series. The propagation *reasoning* is computed — blast radius, first failure, closure —
but the leaf states are given.

**Metric series are synthetic.** Sparklines are generated with a drift model, not replayed from a
real time series. They are shaped correctly relative to each node's state but they are not data.

## Platform

**Windows x64 only.** WPF, and the packaging targets `win-x64`. There is no ARM64 package in this
build, though nothing in the code prevents one.

**Rendering tier 2 assumed.** The simulator is verified at 72–94 fps on hardware acceleration. On
a tier-1 or software-rendering machine — a remote session without GPU passthrough, for instance —
frame rate will drop. The frame readout in the simulator's top bar exists so this is visible
rather than hidden, and the ambient motion toggles in Settings reduce load.

**Fonts.** The design system asks for Segoe UI Variable and Cascadia Mono, both present on
Windows 11. The stacks fall back to Segoe UI and Consolas, which shifts metrics slightly but
breaks no layout.

**DPI.** Per-monitor v2 aware and verified at 96 dpi. Higher scaling factors lay out correctly but
have not been reviewed capture-by-capture at every scale.

## Things reviewed and deliberately left

**Some inspector panels scroll rather than fitting.** On a 900 px-high window the SOC, NOC and
Changes inspectors have more content than height. Scrolling a dense inspector is normal in this
class of tool; compressing the content to fit would have cost the detail that makes it useful.

**Short queues leave list space empty.** With three open incidents, the incident list does not
fill its column. A queue profile panel was added beneath it and four resolved incidents were added
to the fixture so the workspace has recent history, but an honest three-item queue is still a
three-item queue.

**The simulator's inspection panel can outrun its height** on the longest stages, so
"Visible relationships" sometimes needs a scroll. The alternative — truncating the expected/actual
detail — would remove the reason the panel exists.
