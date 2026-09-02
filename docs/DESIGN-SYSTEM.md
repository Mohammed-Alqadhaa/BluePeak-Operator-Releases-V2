# Design system

## The rule the system is built around

**Colour carries state. Structure carries hierarchy. Nothing is decorative.**

Every saturated colour on screen means something is healthy, degraded, critical, under
maintenance or unknown. The accent blue means *selected* or *this is the action*. There is no
sixth use of colour, which is why a red edge anywhere in the product can be trusted.

## What is deliberately absent

This product is composed from workspaces, rails, inspectors, section rules, data rows, canvases
and command bars. It contains no card grids, no KPI tile rows, no glassmorphism, no gradients
used as decoration, no glow, and no container that exists only to hold one number.

A container appears only where the hierarchy genuinely needs one. Sections are announced by a
label sitting on a hairline rule — a device that costs 26 px and no visual noise — rather than by
wrapping content in a box.

## Surfaces

Five steps of value, no shadows except on floating popups.

| Token | Value | Used for |
|---|---|---|
| `B.Rail` | `#090C11` | Navigation rail, title bar, status bar |
| `B.Base` | `#0E1218` | Workspace background |
| `B.Canvas` | `#12171F` | Secondary regions, section bars, drawing canvases |
| `B.Raised` | `#171D26` | Inspectors |
| `B.Overlay` | `#1C232E` | Popups, inspector headers, chips |

Separation is by value and a 1 px hairline (`B.Hairline` `#222933`), never by shadow.

## State

| State | Token | Shape |
|---|---|---|
| Healthy | `#3FB98A` | circle |
| Degraded | `#E0A33E` | diamond |
| Critical / Offline | `#E5544B` | square |
| Maintenance | `#8A7BD1` | ring |
| Unknown | `#6B7686` | circle |

`StatePip` draws a different **shape** per state as well as a different colour, so the product
survives colour-blindness and greyscale printing.

In the 3D simulator the same rule is inverted for brightness: healthy indicators are scaled to
34 % intensity while faults render at full. A machine where everything glows tells the operator
nothing.

## Typography

| Role | Family | Size | Weight |
|---|---|---|---|
| Display | Segoe UI Variable Display | 21 | SemiBold |
| Title | Segoe UI Variable Display | 15 | SemiBold |
| Subtitle | Segoe UI Variable Text | 13.5 | Medium |
| Body | Segoe UI Variable Text | 12.5 | Regular |
| Data | Cascadia Mono | 12 | Regular |
| Caption | Segoe UI Variable Text | 11 | Regular |
| Section label | Segoe UI Variable Text | 10 | SemiBold, tracked, small caps |

Every identifier, timestamp, measurement and count is monospaced. Numbers that appear in a column
must align, and an ID the operator will read aloud in a handover should be unambiguous.

Section labels use `TrackedText`, a custom element that lays out glyphs individually, because WPF
has no letter-spacing and a 10 px uppercase label without tracking reads as noise.

## Density

| Element | Height |
|---|---|
| Title bar | 38 |
| Command bar | 40 |
| Section bar / column header | 26–27 |
| Standard data row | 28 |
| Two-line data row | 40–46 |
| Rich row (with reason and next action) | 58–66 |
| Status bar | 24 |

Spacing is on an 8 px rhythm: 16–20 px workspace gutters, 10–12 px cell padding.

## Composition patterns

Every workspace is built from the same four devices, which is what makes thirteen screens feel
like one product:

1. **Command bar** — title, filters, search, and at most one primary action.
2. **Section rule** — a tracked label on a hairline, optionally with a right-aligned count.
3. **Data rows** — a hairline, a hover, a 2 px left marker when selected. Never a card.
4. **Inspector** — a fixed right column with a header block and a scrolling body of
   label/value fields at a fixed label width, so values align down the panel.

Three-column workspaces (NOC, SOC, Incidents, Automation, Changes) run **list → canvas → inspector**
with draggable splitters. The middle column is where reasoning happens; the outer columns are
selection and detail.

## Interaction

- Selection is a 2 px accent marker plus a value shift. Never a pill, never a fill.
- Hover always changes something. If it does not, the thing is not interactive.
- Keyboard focus is visible on every focusable control.
- Scrollbars are 11 px, thumb-only, and themed. No default Windows chrome is reachable anywhere.
- Anything that looks interactive is interactive. Every filter filters, every search searches,
  every button does something, and every cross-reference navigates and carries its subject.

## Motion

| Duration | Used for |
|---|---|
| 120 ms | Hover, switch knobs |
| 180–220 ms | Panel fades, mode changes |
| 360 ms | Larger transitions |

Easing is cubic in-out for travel and quartic-out for anything that seats or docks. Nothing
bounces. Nothing pulses without meaning.

The simulator's mechanical motion uses its own curves in `Easing`: `Mechanical` for part travel,
`Seat` for docking, `Camera` for framing. Ambient motion — idle camera drift, dependency flow
markers — can be switched off in Settings; journey choreography cannot, because that motion is
information.

## Icons

A stroke-only set on a 16×16 grid, built from `GeometryGroup` primitives rather than a font, so
nothing depends on a glyph being installed and every line lands on the same rhythm.

## Custom-drawn elements

Where a templated control would have been the wrong tool, the element draws itself:

| Element | Why it is drawn rather than composed |
|---|---|
| `Sparkline` | At row density this is a shape, not a chart, and appears ~100 times |
| `StatePip` | Shape varies with state, not just colour |
| `DistributionBar` | Replaces the reflex to show a population as three separate tiles |
| `JourneyScrubBar` | Segments sized to real stage durations, draggable, with verdict ticks |
| `DependencyCanvas` | Lane layout with protocol-labelled edges and a first-failure badge |
| `EntityGraph` | Deterministic radial layout so a case always draws the same picture |
| `LayerMap` | The whole estate at once, lighting requirements and blast radius on selection |
| `TrackedText` | WPF has no letter-spacing |

## The simulator's separate register

The simulator deliberately breaks the product's surface language, because it is a different kind
of instrument. Panels float over the scene instead of dividing it; the background is darker
(`#06080B`) with a radial vignette; labels are monospaced and technical; the palette is almost
monochrome so that state is the only saturated thing in the frame.

It is still the same design system — same tokens, same state colours, same density — used with a
different composition rule.
