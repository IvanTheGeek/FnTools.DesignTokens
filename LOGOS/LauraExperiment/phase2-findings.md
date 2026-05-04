---
area: Experiment
status: complete — 2026-05-04
phase: 2 — Token flow outward (our tokens → Penpot)
---

# Phase 2 Findings — Token Flow Outward

Goal: push `ivanthegeek.tokens.json` (DTCG 2025.10) into Penpot and read it back.
File: `samples/ivanthegeek.tokens.json` — 27 tokens across color, dimension, fontFamily, shadow.

---

## Result

**All 27 tokens pushed and read back correctly via REST.** MCP Plugin API provides read
verification only — token write is not available in the Plugin API (see below).

---

## REST push — what works

### Endpoint

```
POST /api/rpc/command/update-file
Content-Type: application/transit+json
Authorization: Token <token>
```

### Payload structure

```json
["^ ",
  "~:id",         "~u<file-uuid>",
  "~:revn",       <current-revn>,
  "~:vern",       <current-vern>,
  "~:session-id", "~u<any-uuid>",
  "~:changes",    [<set-change>, <token-change-1>, ...]
]
```

One `set-token-set` change creates the set. One `set-token` change per token. All in a single request — no per-token round-trips needed.

### Type name correction (required)

The transit type names do NOT match DTCG type names. The correct mapping:

| DTCG `$type` | Transit keyword | Plugin API `.type` |
|---|---|---|
| `color`      | `~:color`        | `color`            |
| `dimension`  | `~:dimensions`   | `dimension`        |
| `fontFamily` | `~:font-family`  | `fontFamilies`     |
| `shadow`     | `~:shadow`       | `shadow`           |
| `fontWeight` | `~:font-weight`  | `fontWeight`       |
| `number`     | `~:number`       | `number`           |
| `typography` | `~:typography`   | `typography`       |

First attempt used `~:fontFamily` and `~:dimension` — both rejected with `400 params-validation`.
Second attempt with corrected names: HTTP 200.

### Value formats accepted by Penpot REST

| Token type | Value format sent | Confirmed round-trip |
|---|---|---|
| color (opaque)  | `"#e8f0e8"` (6-char hex) | ✓ |
| color (alpha)   | `"#f0f7f0ef"` (8-char hex) | ✓ stored, ✓ readable |
| dimension       | `"0.78rem"` / `"1rem"` | ✓ (note: `1.0rem` normalized to `1rem`) |
| fontFamily      | `"Exo 2, sans-serif"` (comma-joined string) | ✓ |
| shadow          | `"0px 18px 40px 0px rgba(18,42,18,0.08)"` (CSS-like string) | ✓ |

**`$description` is preserved.** Values longer than 50 chars stored intact.

### Response format

```json
["^ ", "~:revn", 4, "~:lagged", [<echo of applied changes>]]
```

`~:lagged` is the server's confirmation — it echoes the change ops that were applied.
The `revn` in the top-level response is the file's revision after applying changes.

---

## MCP Plugin API — read only

`penpot.library.local.tokens.sets` provides full **read** access:

```javascript
const sets = [...penpot.library.local.tokens.sets];
const itk  = sets.find(s => s.name === 'ivanthegeek');
const tokens = [...itk.tokens].map(t => ({ name: t.name, type: t.type, value: t.value }));
```

Read result for all 27 tokens: **exact match** with pushed values. Names, types, values, and descriptions all present.

**Write surface: does not exist.** `penpot.tokens` is undefined. `penpot.library` is read-only
(only `local` and `connected` properties, no mutation methods). The token write path via
Plugin API was noted as "coming soon" in Penpot docs — confirmed still absent in 2.14.4.

**Implication for the round-trip task**: The two paths are not symmetric. REST is read+write;
MCP Plugin API is read-only. For pushing tokens headlessly (CI, scripted), REST is the only
option. For reading tokens back interactively (while a file is open), MCP is the ergonomic path.

---

## Type name map across three surfaces

Three distinct type name spaces exist:

| DTCG `$type`  | REST transit   | Plugin API   | Penpot UI label  |
|---|---|---|---|
| `color`       | `~:color`      | `color`      | Color            |
| `dimension`   | `~:dimensions` | `dimension`  | Dimensions       |
| `fontFamily`  | `~:font-family`| `fontFamilies`| Font Family     |
| `shadow`      | `~:shadow`     | `shadow`     | Shadow           |
| `fontWeight`  | `~:font-weight`| `fontWeight` | Font Weight      |
| `number`      | `~:number`     | `number`     | Number           |
| `typography`  | `~:typography` | `typography` | Typography       |

No single type name works in all three contexts. A Penpot adapter layer must translate.

---

## Alpha-channel colors

Colors with alpha (surface tokens at 0.94, 0.92, 0.78 opacity; accent.subtle at 0.08):
- Stored as 8-char hex `#rrggbbaa` — accepted and readable by both REST and Plugin API
- Visible in the Penpot TOKENS panel under the color type
- Penpot renders 8-char hex correctly in the UI (color swatch shows alpha)

However: **8-char hex is NOT valid per DTCG 2025.10** — the spec requires either a 6-char
hex (no alpha) or the structured object with an explicit `alpha` field. Using 8-char hex as
a Penpot adapter output is an acknowledged lossy encoding. See ADR 016.

---

## What the Penpot TOKENS panel shows

After push, the TOKENS panel shows the `ivanthegeek` set with tokens organized by type:
Color, Dimensions, Font Family, Shadow — matching the Penpot internal type categories.
The panel UI displays each token with its resolved value (hex swatch for colors, etc.).

The set appears alongside the pre-existing `lltokens-hex` set with no conflicts.

---

## MCP coverage query

After applying `appliedTokens` to shapes via REST `mod-obj`, the Plugin API can query
which shapes reference a given token path:

```javascript
function findShapesUsingToken(tokenPath) {
  return penpot.currentPage.findShapes()
    .filter(s => s.tokens && Object.values(s.tokens).includes(tokenPath))
    .map(s => ({
      id:       s.id,
      name:     s.name,
      type:     s.type,
      property: Object.entries(s.tokens).find(([, v]) => v === tokenPath)?.[0]
    }));
}
```

To build a full coverage map (token path → shapes using it):

```javascript
const allWithTokens = penpot.currentPage.findShapes()
  .filter(s => s.tokens && Object.keys(s.tokens).length > 0);
const coverageMap = {};
for (const shape of allWithTokens) {
  for (const [prop, tokenPath] of Object.entries(shape.tokens)) {
    if (!coverageMap[tokenPath]) coverageMap[tokenPath] = [];
    coverageMap[tokenPath].push({ shape: shape.name, property: prop });
  }
}
```

Both patterns verified on the test shapes in TokenExperiments. The Plugin API reads
`appliedTokens` as `shape.tokens` — a plain object with CSS property → token-path entries.

### Plugin API write path for appliedTokens — applyToShapes is a no-op

`token.applyToShapes('fill', [shape])` and `token.applyToSelected('fill')` both execute
without error but make no persistent change. `shape.tokens` remains `{}` and fills stay
unchanged. The function dispatches internally but nothing reaches the server.

**Working write path**: REST `mod-obj` with `~:applied-tokens` set operation:

```json
["^ ",
  "~:type",    "~:mod-obj",
  "~:id",      "~u<shape-uuid>",
  "~:page-id", "~u<page-uuid>",
  "~:operations", [["^ ",
    "~:type", "~:set",
    "~:attr", "~:applied-tokens",
    "~:val",  ["^ ", "fill", "color.accent.default", "borderRadius", "spacing.sm"]
  ]]
]
```

`appliedTokens` value is a transit map with **plain string keys** (not keywords) and token
path strings as values.

**Caveat**: setting `appliedTokens` via REST does not update the shape's rendered fill color.
The shape retains its existing fill data (`#B1B2B5` default). Penpot resolves token values
into shape properties when you interact with the shape in the UI (or when the Tokens panel
re-applies). For headless coverage tracking, the `appliedTokens` binding is sufficient; for
visual correctness, the fill must be updated separately to match the token's resolved value.

---

## Open questions from Phase 2

- Can REST push tokens into a specific position in the set order (affecting resolution
  precedence)? The `set-token-set` change has only `name` and `description` — no `position`
  field was observed. Order may be append-only.
- How does Penpot handle tokens with duplicate names across sets? (Not tested here.)
- Shadow token value format: does Penpot parse and validate the `rgba()` syntax, or store
  it as an opaque string? The round-trip confirms storage but not semantic validation.
- `$description` is preserved through REST and Plugin API. Is it shown in the UI? (Not
  verified — the tokens panel may not surface descriptions in the token list view.)
- REST write path discovered for themes (`set-token-theme`) — not tested. Phase 6 will
  need this for bidirectional validation.
