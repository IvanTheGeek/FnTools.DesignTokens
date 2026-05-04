---
area: Experiment
status: complete — 2026-05-04 (extended 2026-05-04)
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

---

## Part 2 — Laura semantic token push (2026-05-04)

Goal: push a DTCG-resolved token set mirroring Laura's Dashboard semantic structure
(`color.*`, `spacing.*`, `radius.*`, `typography.*`, `breakpoint`) and verify
that Dashboard shapes resolve against it.

File: Design mocks (`11baa5c9-2a66-8156-8007-f7969761f14d`) — ID changed since
2026-05-03; use `get-project-files` to find current IDs (they change on each re-import).

---

### Resolution approach

`Api.importTokensStudioThemed` was used with ALL 11 theme names to prevent mutually
exclusive sets (Dark, Mobile/Tablet, 150%/200% zoom) from bleeding into the base.
Colors extracted from the "Light" + "Core" theme resolved tokens; spacing/radius/size
computed directly from the Desktop scale formula (`round(16 * 1.25^N)`).

**Shim math evaluator theme-bleed bug discovered**: `EvaluateMath` evaluates math
expressions at shim time against the full multi-set token index. The last set in
`tokenSetOrder` wins for each path — `Text zoom/200%` (zoom=2) is last, so
`base = 16 * {zoom} = 32` regardless of which theme is being resolved. This makes
spacing and radius values wrong (all 32 instead of the correct scale) when using
`importTokensStudioThemed` for themes that include `Foundations/Base`.

**Workaround**: compute scale values directly using `round(base * pow(multiplier, N))`.
**This is a bug to fix**: math should be re-evaluated per-theme, not at shim time.

Correct Desktop + 100% zoom scale values:
| scale | value |
|---|---|
| hairline | 1 |
| micro | 2 |
| 3xs | 8 |
| 2xs | 10 |
| xs | 13 |
| sm | 16 |
| md | 20 |
| lg | 25 |
| xl | 31 |
| 2xl | 39 |
| 3xl | 49 |

---

### REST API change — new push format (breaking change from 2026-05-03)

The `set-token-set` and `set-token` change types have a new schema in this Penpot version.

**`set-token-set` (now):**
```json
["^ ",
  "~:type", "~:set-token-set",
  "~:id",   "~u<SET_UUID>",
  "~:attrs", ["^ ",
    "~:id",          "~u<SET_UUID>",
    "~:name",        "laura-light-desktop",
    "~:description", "...",
    "~:modified-at", "~m<epoch-ms>"
  ]
]
```
- `~:id` on the change itself is now required (was absent before)
- `~:attrs.tokens` works (inline token map) BUT the `token?` predicate rejects all tokens — use individual `set-token` changes instead

**`set-token` (now):**
```json
["^ ",
  "~:type",     "~:set-token",
  "~:set-id",   "~u<SET_UUID>",
  "~:token-id", "~u<TOKEN_UUID>",
  "~:attrs", ["^ ",
    "~:id",          "~u<TOKEN_UUID>",
    "~:name",        "color.background.body",
    "~:type",        "~:color",
    "~:value",       "#f3f2f3",
    "~:description", "..."
  ]
]
```
- `~:token-set-name` + `~:name` replaced by `~:set-id` (UUID) + `~:token-id` (UUID)
- `~:modified-at` is optional in `set-token.attrs`

**Old format still rejected** — both the old `set-token-set` (without `:id`) and old
`set-token` (with `token-set-name` instead of `set-id`) return HTTP 400 params-validation.

The old format worked during Phase 2 Part 1. This is a schema change between Penpot versions
or possibly between the initial push and this follow-up push.

---

### Push result

Set `laura-light-desktop` with 35 tokens pushed in one `update-file` request:
- 1 `set-token-set` change (create the set)
- 35 `set-token` changes (one per token)

Verified via MCP Plugin API:
- 35/35 tokens readable with correct names, types, and values
- Types stored as: `"color"`, `"dimension"`, `"typography"` (transit keywords decoded)
- Color values stored as hex strings: `"#f28ce1"`, `"#fafafa"`, etc.
- Dimension values stored as px strings: `"16px"`, `"13px"`, `"1200px"`, etc.
  - **Resolved numeric** (unit stripped): `radius.sm` resolves to `16`, not `"16px"`

---

### Token precedence — local set wins over System Library

`laura-light-desktop` was appended to the end of the set list (position 23 of 23).
**Last set in order always wins** (same as CSS cascade — later declaration overrides).

With our set active alongside `Breakpoints/Tablet` (System Library, active):

| Token path | System Library active | Our set (last) | Resolved |
|---|---|---|---|
| `breakpoint` | 1020 (Tablet) | 1200px (Desktop) | **1200** |
| `color.background.default` | `{palette.default.800}` dark (Dark Core active) | `#fafafa` | **#fafafa** |
| `color.border.default` | `{palette.default.700}` dark | `#c2bcc1` light | **#c2bcc1** |
| `radius.sm` | `{scale.sm}` = 32 (wrong, zoom-bleed) | `16px` | **16** |
| `spacing.3xs` | `{scale.3xs}` = 32 (wrong, zoom-bleed) | `8px` | **8** |

Our set overwrites the System Library's dark-mode + wrong-scale values entirely.
This is the mechanism to use for scripted theme switching without activating/deactivating
individual Library sets.

---

### Dashboard shape verification

All 120 shapes with `appliedTokens` confirmed present. Sample of shapes bound to our tokens:

| Shape | Token binding | Resolved value |
|---|---|---|
| Swatches frame | `width: breakpoint` | 1200 (was 1020) |
| pattern / card | `fill: color.background.default` | #fafafa (was dark) |
| pattern / card | `strokeColor: color.border.default` | #c2bcc1 |
| pattern / card | `r1-r4: radius.sm` | 16 |
| pattern / card | `strokeWidth: stroke.hairline` | 1 |
| pattern / card | `columnGap/rowGap: spacing.3xs` | 8 |

Typography token paths (`typography.heading.level-2`, `typography.default`, etc.) are
present in the set and bound by shapes — visual update in the editor depends on the font
being available in Penpot's font registry.

---

### What broke / gaps

| Gap | Detail |
|---|---|
| `token?` predicate blocks inline `attrs.tokens` embed | Embedding tokens directly in `set-token-set.attrs.tokens` fails the custom `token_QMARK_` predicate — root cause unknown; individual `set-token` changes work |
| Math-evaluator theme-bleed | Shim evaluates math at full-index time; spacing/radius values are wrong per-theme when multiple zoom sets exist |
| Typography font availability | Typography tokens push correctly but Penpot only applies them visually if the font is loaded in its font registry |
| Dimension `value` vs `resolvedValue` | `token.value` stores the string as-is (`"16px"` stays `"16px"`). `token.resolvedValue` is what Penpot applies to shapes — it parses the px string to a bare number (`16`). Both formats accepted on push. |
| Set position is append-only | No `position` field on `set-token-set` — new sets are always appended; cannot control insertion position |
| Local vs Library precedence | Local sets always win over connected Library sets when placed later in order. This could cause unintended overrides if a local set is accidentally active |

---

### Updated known IDs

| File | ID |
|---|---|
| System library | `11baa5c9-2a66-8156-8007-f7969761f14c` |
| Design mocks | `11baa5c9-2a66-8156-8007-f7969761f14d` |

(Unchanged since 2026-05-03 re-import — confirm via `get-project-files` before use.)
