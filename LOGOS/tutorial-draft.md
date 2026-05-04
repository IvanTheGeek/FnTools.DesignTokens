---
status: seed — rough notes, not yet shaped for publication
intent: tutorial / blog post for developers learning Penpot + design tokens
audience: developers who know CSS and code but have not used Penpot or design tokens before
---

# Penpot + Design Tokens: What Is Actually Going On

*Working title. Written as findings accumulate — will be shaped into a proper tutorial later.*

---

## The thing everyone gets wrong first

When you open Penpot's Tokens panel and see "THEMES" at the top with a dropdown,
you think: themes are the thing. Activate a theme, get different colors.

That is not what is happening.

The theme dropdown is a shortcut for toggling checkboxes. The checkboxes are the thing.

---

## Sets: the actual primitive

A **token set** is a named list of design decisions:

```
Color/Dark Core:
  color.background.body  = #181a1b
  color.text.main        = #bcbfc2
  color.border.default   = #303336

Color/Light Core:
  color.background.body  = #f2f3f2
  color.text.main        = #303632
  color.border.default   = #bcc2be
```

Each set has one state: **active or inactive**. When a set is active, its token
values participate in resolution. When it is not, they don't exist.

You can have multiple sets active at the same time. If two active sets define the
same token name, the one that comes later in the priority order wins.

That's the whole engine. Everything else is built on top of this.

---

## Themes: saved checkbox states

A **theme** is nothing more than a saved preset of which sets should be active.

Selecting "Light mode" from the Themes dropdown in Penpot does exactly this:
- deactivates `Color/Dark Core`, `Color/Dark Accent`, `Color/Dark Component`
- activates `Color/Light Core`, `Color/Light Accent`, `Color/Light Component`

You could do the same thing by manually toggling six checkboxes. The theme just
saves you the clicks.

This means:
- You can have a completely working design token system with zero themes defined.
  Just toggle sets manually.
- Themes are a UX concern, not a token format concern. The token values themselves
  don't know about themes.
- Adding a new theme costs nothing and breaks nothing — it is just a new preset.

---

## The design file told us all along

The system library file contains this, written directly on the canvas:

> *"When you have various themes inside a group, only one of the themes in this group can be active."*
> *"Having your sets clubbed under groups makes it more accessible to switch from a matrix of themes."*

**Matrix of themes** is the exact right mental model.

Groups are the axes. Themes within a group are the options on that axis. Your
active configuration is one selection per axis — one coordinate per dimension.
The system enforces that you can't have two themes from the same group active
simultaneously, because that would mean two conflicting sets of values for the
same token names.

The design file documented its own architecture. Worth reading what is on the canvas
before reverse-engineering from the API.

**The API does not enforce group mutual exclusion.** You can check multiple themes
from the same group simultaneously — nothing stops you. When you do, the set that
appears lowest in the list wins, because token resolution uses last-write semantics:
later sets override earlier ones, the same way later CSS rules override earlier ones.
If `Breakpoints/Mobile`, `Breakpoints/Tablet`, and `Breakpoints/Desktop` are all
active, `Breakpoints/Desktop` wins because it is last. The frame shows 1200px and
the others are silently ignored.

---

## Why this matters: brand + mode + breakpoint are orthogonal

Because themes are just set presets, you can layer them independently.

Laura's design system (the one we used to explore this) has 5 independent groups:

| Group | Options |
|---|---|
| Always-on | Global (typography, spacing, radius) |
| Color mode | Light, Dark |
| Brand | NeonBooks, Eco Tools, Core |
| Breakpoint | Mobile (360px), Tablet (1020px), Desktop (1200px) |
| Text zoom | 100%, 150%, 200% |

To see the Eco Tools brand in light mode on a tablet, you activate one from each group.
They don't interfere. The sets for brand colors don't overlap with the sets for
breakpoints. Activating "Eco Tools" changes `color.button.primary.background.default`
from blue (`#1259a1`) to green (`#12a112`). Activating "Tablet" changes the frame
width from 1200px to 1020px. Neither touches the other.

We verified this live: switching from Core/Dark to EcoTools/Light changed the
button from blue to green and the background from near-black to near-white in a
single operation.

---

## Semantic tokens: why the names don't mention colors

A beginner's instinct: name a token `dark-background` or `light-text-color`.

The problem: that name only makes sense in one mode. In light mode, `dark-background`
is not the background — your dark background token is now wrong by name.

The solution: name tokens after their **role**, not their **value**.

```
color.background.body     -- the main page background, whatever shade that is
color.text.main           -- primary readable text
color.border.default      -- standard UI border
```

In Dark Core: `color.background.body = #181a1b` (near-black)
In Light Core: `color.background.body = #f2f3f2` (near-white)

Same name. Different set. Different resolved value. The component that uses
`color.background.body` doesn't know or care which mode is active — it just
uses the token path. The set swap does the rest.

---

## Accent colors and why some tokens don't change between modes

We observed that `color.button.primary.icon` and `color.text.link` stayed at
`#5ea6ed` (a medium blue) in both dark and light mode.

Why? Because those token paths are defined in `Color/Palettes and Scales` — a set
that is always active — not in the Dark or Light Core sets. The Dark/Light Core
sets map surface/background/border tokens onto different palette values. They don't
remap accent and brand colors.

This is intentional architecture: some tokens are **mode-invariant**. Brand colors
and interactive accents stay consistent across modes. Only surface, text, and border
tokens invert. This is what makes a design system feel coherent across modes rather
than just "inverted".

The structure tells you the intent:
- "This token lives in a mode-specific set" → it flips between modes
- "This token lives in the always-on palette set" → it's the same everywhere

---

## How shapes use tokens (and what the API actually gives you)

Every shape in Penpot can have **token bindings**: a mapping from a CSS property
to a token path.

```json
{
  "fill":  "color.background.body",
  "width": "breakpoint"
}
```

This is stored on the shape as `shape.tokens` in the Plugin API. It does not change
when you switch themes. It's the wiring diagram.

The **resolved value** — the actual color or pixel count — is stored separately on
the shape geometry (`shape.width`, `shape.fills[0].fillColor`, etc.). This changes
when you activate or deactivate sets. Penpot resolves the token paths against the
active sets and writes the result into the shape's geometry properties.

So when you switch from Tablet to Mobile by toggling the breakpoint set:
1. `breakpoint` token resolves to `360` instead of `1020`
2. Every shape with `shape.tokens.width = "breakpoint"` gets `shape.width = 360`
3. That resolved `360` is saved to the database

The token path binding (`"breakpoint"`) stays on the shape permanently.
The resolved geometry value (`360` or `1020`) is what the canvas renders and
what other tools read.

---

## The two APIs and what they see

Penpot exposes two ways to interact with a file programmatically:

**REST API** (`get-file`, `update-file`):
- Talks directly to the database
- Returns the last-saved state
- Shape geometry (widths, colors) reflects whatever the last token resolution wrote
- `active-themes` field reflects which named theme presets were last saved —
  but only when changed via the REST `set-active-token-themes` operation

**MCP Plugin API** (`execute_code`):
- Runs JavaScript inside the browser's plugin context, against the live open file
- `set.active = true/false` immediately resolves tokens and updates the canvas
- Changes propagate to the database (shape geometry is saved)
- Does NOT update the `active-themes` field in the database

This creates an interesting split: after switching themes via the Plugin API,
the shape widths and fill colors in REST match the new state — but `active-themes`
still shows the old configuration. The geometry is ground truth; `active-themes`
is a label.

The Penpot UI's Themes dropdown does the same thing: it toggles `set.active` and
the canvas updates, but it does not write to `active-themes` either. This is why
the dropdown shows "No theme active" even when sets are active and the canvas looks
correct.

---

## What a breakpoint really is in this system

The word "breakpoint" in CSS means a media query threshold — the screen width at
which layout changes.

In Penpot's token system (via Tokens Studio), `breakpoint` is a plain dimension
token. The design file has three sets:

```
Breakpoints/Mobile:   breakpoint = 360
Breakpoints/Tablet:   breakpoint = 1020
Breakpoints/Desktop:  breakpoint = 1200
```

The "Landing page" frame has `shape.tokens.width = "breakpoint"`. So whichever
breakpoint set is active, the frame's width resolves to that value.

There are three frame copies on the page — one labelled Mobile, one Tablet, one
Desktop. They are not mechanically linked to their breakpoint. They are
documentation labels. All three always show the currently active breakpoint width.

To see the Mobile layout, activate `Breakpoints/Mobile`. All three frames shrink
to 360px. The "Mobile" label is just there to tell you which one to look at.

The actual responsive behavior in a shipped application comes from CSS `@media`
queries in the emitted stylesheet — not from having three frame copies in the
design file.

---

## Design mocks as application states

The screens in a design mocks file are not just illustrations. Each screen is a
**specific application state** — what the user sees at a particular moment, with
particular data, after a particular sequence of actions.

A dashboard screen is the state after a successful login. An email confirmation
screen is the state after a form is submitted. A landing page at mobile breakpoint
is the state of the same page for a different device context.

Prototype connections between screens express **transitions** — what happens when
the user clicks a button or link. Frame A → Frame B on click is an intent: "from
this state, this action takes you to that state."

This maps directly onto any state machine or routing model:
- Screens = states
- Prototype connections = transitions
- The full prototype graph = the navigable application

When you build the application from the design:
- Each distinct screen becomes a route or a component state
- Each prototype connection becomes a navigation event or state transition
- Token values become CSS custom properties
- Component instances become the component tree

The design file is not a picture of the app. It is a specification of the app's
state space, expressed visually.

**What Penpot prototype connections do and don't carry**:

They carry: source frame, target frame, trigger type (on click, on hover, etc.),
animation type, overlay settings.

They don't carry: guard conditions ("only if logged in"), data bindings ("pass this
user ID"), error states, back-navigation intent, loading states. Those require a
separate translation step from designer intent to developer implementation.

## The three-layer stack

This is the complete picture once the pieces are connected:

```
System Library
  Tokens (values) + Components (primitives and patterns)
  — defines WHAT exists and WHAT it looks like per theme

Design Mocks
  Screens (specific application states) + Prototype connections (transitions)
  — defines WHICH states the application has and HOW they connect
  — also defines WHICH components and token combinations each state uses

Implementation (Fun.Blazor / CSS / Router)
  Component tree per state + CSS custom properties + navigation events
  — the working application built FROM the above two layers
```

The System Library answers: "what building blocks do we have?"
The Design Mocks answer: "what do we build with them, and in what states?"
The implementation answers: "how does it run?"

**The mocks file does not duplicate the library — it composes it.**
We verified this directly: zero token diffs, zero local components between
the Design Mocks file and the System Library. Every token and component the
mocks use comes from the library unchanged. The mocks file's contribution is
entirely in how those pieces are arranged into states.

This clean boundary is what makes the stack maintainable. Change a token value
in the library → every screen that uses it updates. Add a new component variant
to the library → the mocks can use it without touching any token definitions.
Build a new Fun.Blazor component → the mocks file told you exactly what it needs.

---

## The scale is not a number — it is a function

A common assumption: a spacing token like `spacing.md` has a value. `16px`. Done.

Laura's system does not work that way. The spacing scale is computed:

```
zoom        = 1  (100%) | 1.5 (150%) | 2 (200%)    ← Text zoom set
base        = 16 × zoom
multiplier  = 1.1 (Mobile) | 1.125 (Tablet) | 1.25 (Desktop)   ← Breakpoint set
spacing.md  = round(base × multiplier¹)
```

`spacing.md` is not `16px`. It is a function of two theme axes. There are **9 distinct
resolved values** for that one token — one for each combination of breakpoint and zoom:

| | Mobile (×1.1) | Tablet (×1.125) | Desktop (×1.25) |
|---|---|---|---|
| 100% zoom (base=16) | 18px | 18px | 20px |
| 150% zoom (base=24) | 26px | 27px | 30px |
| 200% zoom (base=32) | 35px | 36px | 40px |

This is the mechanism behind **responsive + accessible typography and spacing**. The
same semantic token name (`spacing.md`) produces the right value for every combination
of screen size and user text zoom preference — not by having 9 separate tokens, but by
having a scale that is mathematically derived from two active-set inputs.

**What this means for CSS emission:**

You cannot emit a single `:root { --spacing-md: 18px }`. You need a matrix:

```css
:root                          { --spacing-md: 20px; }  /* Desktop, 100% */
@media (max-width: 1020px)     { --spacing-md: 18px; }  /* Tablet, 100% */
@media (max-width: 360px)      { --spacing-md: 18px; }  /* Mobile, 100% */
/* repeat for each zoom level — or use CSS clamp() */
```

The token system encodes the design intent mathematically. The CSS emitter's job is
to evaluate that math across all relevant theme combinations and emit the right
override structure. This is not a limitation of the token system — it is the feature.

---

## Things still to figure out

- Does the CSS emitter need to know about sets and themes, or just token values and
  their resolved outputs at each breakpoint?
- How do you emit a complete stylesheet that handles all three breakpoints without
  re-running the whole resolution three times?
- What is the right abstraction for a Fun.Blazor component that references token paths —
  does it know token names, or only CSS variable names?
- Can prototype connections (frame A → frame B on click) express enough navigation
  intent to inform a router, or is that always going to need a human translation step?

---

*More to add as the experiment continues.*
