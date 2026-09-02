# Architecture

## Projects

```
src/BluePeak.Domain        net10.0           Estate model, entities, graph analysis, seed fixture
src/BluePeak.Simulation    net10.0           Scene model, journeys, timeline, playback, runbook engine
src/BluePeak.App           net10.0-windows   WPF: design system, controls, workspaces, 3D renderer
tests/BluePeak.Tests       net10.0           Domain and simulation engine (96 tests)
tests/BluePeak.UiTests     net10.0-windows   Views, navigation, lifecycle (24 tests)
```

The dependency direction is strict and one-way: `App → Simulation → Domain`. Neither the domain
nor the simulation layer references WPF, which is what makes the timeline, the scene evaluation
and the runbook engine testable without a UI thread.

## The estate is one graph

`EstateModel` holds every managed element and every dependency edge, plus the operational records
that hang off them — tickets, incidents, alerts, cases, entities, changes, runbooks, evidence and
diagnostic paths. Adjacency is indexed once at load.

Three graph operations carry most of the product's reasoning:

| Operation | Answers |
|---|---|
| `DependenciesOf` / `DependentsOf` | What does this need, what needs this |
| `BlastRadius(id)` | Transitive closure over consumers — what breaks if this fails |
| `FirstFailure(id)` | Deepest unhealthy element on this node's own dependency path |

`FirstFailure` is the one that changes behaviour. When a service is unhealthy it walks *down*
through what that service requires and returns the deepest unhealthy element found. If that is
the node itself, the fault originates there. If it is something below, the node is a victim and
NOC says so in as many words, because the difference decides whether you act here or somewhere else.

The seed is deterministic: `EstateSeed.Build` snaps its clock anchor to the minute and drives a
fixed-seed RNG, so every launch produces the same estate, the same evidence digests and therefore
reproducible captures.

## The simulator's central decision

**Scene state is a pure function of playhead position.**

```csharp
SceneSnapshot Evaluate(double time)
```

A journey is a list of stages. Each stage declares a camera pose, a set of module poses, the
links visible during it, and the inspection detail. `JourneyTimeline` resolves every stage to a
*complete* pose set at construction, then evaluation interpolates between stage `i-1` and stage
`i` with an easing curve.

Nothing about the result depends on how the playhead arrived at that instant. That single
property gives, for free:

- **Scrubbing backwards** reconstructs the exact earlier state — there is no accumulated state to unwind.
- **Scrubbing forwards** reconstructs the exact later state — no fast-forward simulation needed.
- **Resume after a scrub** continues correctly, because position is the only thing that changed.
- **Lifecycle recovery** is trivial: detaching and re-attaching the render loop re-derives the
  frame from the controller, so navigating away and back cannot corrupt or lose playback.
- **Determinism under test** — the engine tests assert that forward, reverse and random-access
  evaluation of the same timeline produce identical poses, and that no part moves more than a
  threshold between adjacent frames (no teleporting geometry).

`PlaybackController` owns time and nothing else: a position, a rate and a mode. It clamps a single
`Advance` to 0.25 s so a stalled render loop — a minimised window, a slow frame — cannot skip the
timeline. `BeginScrub`/`EndScrub` remember the pre-drag mode so releasing the playhead resumes
only if it was running when the drag started.

## The 3D scene

`OperationsCore` defines the machine: eleven functional modules placed by ring, azimuth and
height around a shared spine. It is pure data with no rendering knowledge, so the same definition
drives the renderer, the inspector, the journey authoring helpers and the tests.

Rendering follows the WPF 3D performance guidance closely:

- **One `Viewport3D`**, `IsHitTestVisible=false`, `ClipToBounds=false`. Hit-testing 3D is a
  software path, and the inspector selects modules by identity rather than by picking.
- **Geometry is built once and frozen.** Per frame, only `Transform3D` values and
  `SolidColorBrush.Color` change. No mesh is rebuilt during playback and no geometry is allocated.
- **Links are pooled.** A dependency is a shared unit cylinder positioned by scale, rotation and
  translation, so showing and hiding relationships costs transform writes, not allocations.
- **Dimming is by value, not alpha.** A recessive module lerps toward the ambient background
  colour rather than becoming transparent, which avoids WPF's transparency sorting entirely
  while keeping depth readable.

`MeshFactory` generates every form: chamfered boxes, cylinders, annular tubes, arc wedges,
prisms, tori and domes. The chamfer matters — it is the difference between a machine and a set
of cuboids, because it gives every edge a highlight to catch.

### Camera framing

WPF measures `PerspectiveCamera.FieldOfView` **horizontally**. On a wide viewport the vertical
extent is therefore much smaller than the number suggests, and a camera distance that frames
correctly on paper crops the machine top and bottom. `SceneRenderer.FramingDistance` converts an
authored distance — which journeys express against the vertical extent — into the real viewport,
scaling by measured aspect. This was a genuine bug found by looking at captures rather than XAML.

The 3D stage is also inset to the area the overlay panels leave free, so the machine is centred
in the space the operator can actually see rather than behind the inspector.

## Navigation and carried context

`Navigator` owns the current workspace. Views are created once and kept (`WorkspaceDefinition.View`),
so navigating away and back preserves scroll position, selection and — critically — the simulator's
playhead.

Two interfaces let a workspace participate:

```csharp
interface IFocusAware     { void ApplyFocus(FocusSubject subject); }
interface ILifecycleAware { void OnActivated(); void OnDeactivated(); }
```

`FocusService` holds the subject the operator is reasoning about. `NavigateWithSubject` sets the
subject, navigates, and pushes it into the destination, which selects the matching row. The
subject is visible in the title bar for the whole session and can be pushed into the current
workspace at any time by clicking it. Every workspace implements `IFocusAware`; the simulator
also implements `ILifecycleAware` to suspend its render loop when it is not on screen.

## The runbook engine

`RunbookEngine` executes a runbook as a sequence of gates. It is deliberately written to refuse:

- An approval gate **stops and returns**. It does not proceed on its own, and nothing past it is
  touched until `Authorise()` is called.
- A mutating step under `RunMode.DryRun` is **skipped and marked as skipped**, never silently run.
- A failed pre-check assertion **halts the whole run**. It does not warn and continue.

It lives in the simulation layer rather than the UI so its behaviour is asserted by tests, not by
clicking. The tests check the ordering invariant directly: in every mutating runbook, the approval
gate's index is lower than the first mutating step's index.

## Design system in code

Tokens live in XAML resource dictionaries (`Design/Tokens.xaml` and friends). Anything drawn with
a `DrawingContext` resolves the same tokens through `Design/Theme.cs`, so custom visuals and
templated controls cannot drift apart.

Custom-drawn elements exist where a templated control would have been the wrong tool:
`Sparkline`, `StatePip`, `DistributionBar`, `MeterBar`, `HeatStrip`, `TrackedText`,
`JourneyScrubBar`, `DependencyCanvas`, `EntityGraph` and `LayerMap`. At row density a sparkline is
a shape, not a chart, and must cost nothing to draw a hundred times.

`TrackedText` exists because WPF has no letter-spacing and the design system's tracked small-caps
section labels needed it. It lays out each glyph itself.
