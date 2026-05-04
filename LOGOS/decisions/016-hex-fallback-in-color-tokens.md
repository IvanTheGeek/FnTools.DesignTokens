---
id: 016
title: Include hex fallback in color token values for tooling compatibility
status: accepted
date: 2026-05-04
---

## Context

DTCG 2025.10 requires color `$value` to be a structured object:
```json
{ "colorSpace": "oklch", "components": [L, C, H] }
```

Penpot's `design-tokens/v1` requires color `$value` to be a hex string:
```json
"#1a6e1a"
```

There is no single format that is simultaneously valid DTCG 2025.10 and natively accepted
by Penpot's token import. The gap exists because Penpot targets the DTCG second editor's
draft, which used hex strings as the required color format.

The DTCG 2025.10 spec provides a path out: the `hex` field inside a color object is an
**optional sRGB fallback** explicitly designed for tooling compatibility:

```json
{
  "$type": "color",
  "$value": {
    "colorSpace": "oklch",
    "components": [0.560, 0.140, 200],
    "hex": "#0d9488"
  }
}
```

Both fields are present and correct. A DTCG-compliant consumer reads `components`;
a Penpot adapter reads `hex`.

## Decision

Include `hex` in every color token `$value` where the color can be represented without
information loss as a 6-digit hex string (i.e., no alpha channel, or alpha = 1.0). For
colors with alpha, omit `hex` — there is no lossless 6-digit representation.

`hex` is a precomputed sRGB gamut-mapped approximation of the authoritative `components`
value. It is not the canonical color — `components` is. The `hex` field exists for
Penpot and any other tooling that requires hex input.

The CSS emitter reads `components` and emits the appropriate CSS function (`oklch(...)`,
`color(srgb ...)`). The Penpot adapter reads `hex` and emits `"$value": "#1a6e1a"`.

## Consequences

- Token files remain DTCG 2025.10 compliant. Validators that check the spec will accept
  the format.
- Penpot import works without a format conversion step for opaque colors. Alpha-channel
  colors (e.g., surface overlays at 8% opacity) are not Penpot-importable via hex — they
  require a separate adapter decision (skip, compute nearest hex with alpha channel, or
  convert to `rgba()` string).
- The authoritative color is always `components`. If `hex` is stale or approximate, the
  CSS emitter produces correct output regardless.
- OKLCH colors require gamut-mapping before a hex value can be computed. Wide-gamut colors
  with `colorSpace: "oklch"` should have `hex` reflect the sRGB-clipped approximation, with
  that approximation acknowledged as lossy.
- Colors without `hex` (alpha-channel colors) will be skipped by a Penpot adapter that only
  reads `hex`. This is visible behavior — the caller knows which tokens did not push.
- ADR 013 (library scope) is not violated: the `hex` field is inside the DTCG color object
  and is part of the format spec. Computing its value belongs to whichever layer creates
  the token (e.g., `CssIngest` when converting from a `#RRGGBB` CSS variable).
