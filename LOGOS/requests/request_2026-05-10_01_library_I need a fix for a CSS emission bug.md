You are working on the FnTools.DesignTokens F# library. I need a fix for a CSS emission bug.

## Context

The library emits CSS custom property declarations from resolved DTCG token graphs.
The relevant pipeline:

1. Token SoT (DTCG JSON) contains tokens of $type "dimension" whose $value fields
   are references to other tokens of $type "number". Example:
   
   Foundations/Spacing set:
     spacing.x1 = { "$type": "dimension", "$value": "{scale.x1}" }
   
   Foundations/Base set:
     scale.x1   = { "$type": "number",    "$value": 16 }

2. After resolution and flattening, spacing.x1 has resolved value 16 (a bare integer,
   no unit — inherited from the number-type scale token).

3. CssEmitter.emitThemedWith is called with a DimensionUnitPolicy that returns Rem
   for all paths except "stroke.*" and "breakpoint.*".

4. The emitted CSS is:
     --spacing-x1: 16;        ← WRONG — no unit, invalid as CSS <length>
   
   Expected:
     --spacing-x1: 1rem;      ← correct (16px ÷ 16px base = 1rem)
   or at minimum:
     --spacing-x1: 16px;      ← valid CSS

## Bug

When a $type "dimension" token resolves to a bare integer (because it referenced
a $type "number" token), the CSS emitter emits the raw integer without any unit.
The DimensionUnitPolicy is not applied. The resulting custom property is invalid
as a CSS <length> value — browsers silently ignore it.

DTCG spec says dimension values always carry a px unit. A dimension token that
resolves to the integer 16 should be treated as 16px, then the unit policy applied
(16px → 1rem if policy says Rem, or 16px if policy says Px).

## What I currently have as a workaround

In emit-tokens.fsx (our consumer script) I post-process the CSS string with a
regex that appends "px" to bare numeric values for --spacing-*, --radius-*, --size-*,
and --stroke-* custom properties. This is fragile and doesn't apply the Rem policy.

## What I need

The CssEmitter should, for a $type "dimension" token:

- If the resolved value already carries a unit (e.g. "16px", "2.5rem") → apply the
  unit policy as today
- If the resolved value is a bare number (integer or float, no unit) → treat it as
  Npx, then apply the unit policy (so 16 → 16px → 1rem with Rem policy)

This fixes the root cause without requiring consumer-side workarounds.

## To reproduce

Use a resolver document where a "dimension" token references a "number" token.
Resolve and flatten, then call CssEmitter.emitThemedWith with a Rem policy.
Assert that the emitted custom property value is "1rem" (or "16px" if Px policy),
not "16".

## Affected emit path

CssEmitter.emitThemedWith (and likely emitCalcPreserving as well, where the same
bare-number case would produce a unitless value in the :root block).

Please fix the emission logic and add a test covering a dimension→number reference
chain with a Rem unit policy.
