# The simulator

## The metaphor

The estate is one engineered machine: the **BluePeak Operations Core**.

A hexagonal spine runs the full height of the assembly, carrying an emissive conduit — the bus
every subsystem docks into. Around it are four stacked decks:

```
        ┌─────────────┐
        │  EVD  crown │   Evidence Vault              ring 4   y = +3.16
        └─────────────┘
   ┌────┬─────────────┬────┐
   │CTL │     SOC     │AUT │   Control · Inspection · Automation    ring 3   y = +2.00
   └────┴─────────────┴────┘
   ┌────┬─────────────┬────┐
   │APP │     DNS     │IDT │   Workload · Resolution · Identity     ring 2   y = +0.74
   └────┴─────────────┴────┘
   ┌────┬─────────────┬────┐
   │IGR │     SWF     │RTG │   Ingress · Switching · Routing        ring 1   y = −0.52
   └────┴─────────────┴────┘
   ╔═══════════════════════╗
   ║   FND  foundation     ║   Foundation Platform         ring 0   y = −1.72
   ╚═══════════════════════╝
```

Rings 1–3 hold three chassis wedges each, 112° of sweep apart, so the sealed machine reads as one
enclosed body with visible seams rather than a stack of discs. Deck plates stop *inside* the wedge
silhouette for exactly that reason.

## Why every module looks different

Each wedge is a **clamshell**: an outer skin band, a top and bottom deck, and end walls, enclosing
a hollow bay. When the shell opens, the halves separate vertically and flare outward, exposing a
mechanism whose form is specific to what the subsystem does.

| Module | Code | Mechanism | Why that form |
|---|---|---|---|
| Request Ingress | IGR | Fan of connector blades behind a slotted faceplate | Requests arrive as discrete channels |
| Switching Fabric | SWF | Crossbar lattice over a backplane, with two separate bundle members | A failing *member* must be visible, not just the bundle |
| Routing and Delivery | RTG | Faceted prism with three directional vanes | A path is chosen from alternatives |
| Name Resolution | DNS | Indexed drum with radial fins on bearing housings | Zones are entries on a rotating index |
| Identity and Trust | IDT | Sealed cylinder behind a keyed collar | Signing material is locked, and the collar unlocks under inspection |
| Application Workload | APP | Stacked service plates on spacer posts | Instances behind one address |
| Observation and Control | CTL | Sensor dome on a gimbal ring, with a lens | It watches the rings beneath it and sweeps |
| Security Inspection | SOC | Concentric aperture rings and a six-blade iris | It closes in on what it is examining |
| Gated Automation | AUT | Cylinder, piston and guide rails, with four gate indicators | It only travels once the gates clear |
| Evidence Vault | EVD | Sealed block over a stack of ledger plates | Records are added, never rewritten |
| Foundation Platform | FND | Hexagonal plinth with six feet and a datum ring | Everything stands on it |

Every wedge also carries a **docking interface** — a pin block with five pins facing the spine —
which is what makes an extracted part read as *removed from* something rather than floating.

## The six journeys

| Journey | Discipline | The question |
|---|---|---|
| Service desk contact | Service Desk | Is this a new fault, or one we already own? |
| Name resolution failure | NOC | Every authenticated path failed at once. Which component actually broke? |
| Authentication and trust | Identity | Sign-in works for some people and not others. How long have we got? |
| Network path failure | NOC | Sessions drop every few minutes. Throughput looks fine. What is wrong? |
| Security detection and response | SOC | Four alerts on one account. One intruder, or four coincidences? |
| Gated automation run | Automation | Can I safely change this, and how will I know it worked? |

They are not one animation with different captions. Each opens a different set of modules, exposes
different relationships, and reaches a different conclusion:

- **DNS** puts the resolver drum at the centre and proves that the failing API is a victim.
- **Authentication** opens the trust vault to show it signing correctly from a cache it can no
  longer refresh, converting an intermittent symptom into a 41-minute deadline.
- **Network** opens the switching lattice down to the individual bundle member, because the
  aggregate is exactly what hides the fault.
- **SOC** opens the inspection aperture and binds four detections onto one entity, then opens the
  vault to show the unbound bearer token that made it possible.
- **Automation** opens the actuator's gate stack and shows a pre-check *failing* and blocking the run.
- **Service desk** barely opens the machine at all — that is the point. It asks the control ring
  what is already known and attaches the contact instead of diagnosing it again.

## Stage kinds and choreography

| Kind | What the machine does | Camera |
|---|---|---|
| Establish | Sealed, whole | Wide three-quarter |
| Disassemble | Every ring stands off its seat; connector buses become visible | Wider, raised |
| Inspect | One module extracts, rotates and opens; everything else recedes to ~15 % | Close three-quarter on the module |
| Trace | Two modules held apart with the dependency drawn between them | Framed on the gap between them |
| Diagnose | The failing module fully extracted and open at full emphasis | Tightest framing in the journey |
| Act | The actuator arms, the piston extends under gate control | On the automation module |
| Verify | Control reaches back to the subjects with check links | Between control and the target |
| Reassemble | Everything returns along its extraction axis and seats | Back to the establishing shot |

Camera framing is derived from module geometry (`B.Look`, `B.Between`) rather than hand-written
coordinates, so a module can be repositioned in the assembly without rewriting every journey.

Azimuth interpolation always takes the short way round, so a stage never spins the machine the
long way to reach a nearby angle.

## Motion language

`Easing.Mechanical` — symmetric cubic, used for part travel.
`Easing.Seat` — quartic-out, used when a part docks.
`Easing.Camera` — smoothstep, so framing never snaps at either end.

Nothing bounces, nothing pulses without meaning, and there are no particles, lasers or hologram
noise. The only ambient motion is the dependency flow marker travelling along a link — which
communicates *direction of dependency* — and a slow idle orbit on the journey list. Both can be
switched off in Settings.

## Transport

Play, pause, resume, replay, step back, step forward, rate (0.5× / 1× / 1.5× / 2×), scenario
change, and back to the journey list. The scrub bar is the real timeline: segments sized to real
stage durations, coloured by stage kind, with a verdict tick along the top edge in the stage's
outcome colour, and a stage number where the segment is wide enough to hold one.

Because scene state is a pure function of playhead position (see
[ARCHITECTURE.md](ARCHITECTURE.md)), dragging backwards reconstructs the exact earlier machine
state, dragging forwards reconstructs the exact later one, and releasing resumes only if playback
was running when the drag began.

## Handoff

Every stage offers "Carry this to" buttons built from the journey's own linkage — the incident,
the change, the runbook, the evidence record, the dependency walk, and the estate node the focused
module represents. The simulator is not a demo mode off to one side; it is another way into the
same records.

## Performance

Measured on the development machine at rendering tier 2 (hardware accelerated): **72–94 fps** at
1560×900, reported live in the top bar so a dropped frame is visible rather than hidden.

The scene is roughly 11 modules × (clamshell + frame + interface + mechanism) plus spine, decks
and ground — every mesh built once and frozen. Per frame the renderer writes transforms and brush
colours only.
