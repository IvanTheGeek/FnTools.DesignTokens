---
area: Penpot interop
status: complete — 2026-05-04
question: Does Penpot preserve DTCG `$extensions` through import → store → export?
informs: ADR-023 (Tokens Studio export uses $extensions as primary lossy-metadata carrier)
upstream: https://github.com/penpot/penpot/issues/9307
---

# Penpot `$extensions` preservation test

Empirical test prior to ADR-023. Question: can we use `$extensions` as the round-trip
carrier for lossy export metadata (e.g. wide-gamut original color values), instead of
the `$description` annotation chosen in ADR-022?

## Result

**No. Penpot does not preserve `$extensions`.** They are stripped at import time, do
not exist in Penpot's internal token model, and cannot be reconstructed on export.

`$description` remains the only round-trip-stable metadata carrier through Penpot.
ADR-022 stands; ADR-023 would only achieve Tokens-Studio-side round-trip, not
Penpot-side.

## Evidence

### 1. Plugin API Token type has no extensions field

```js
const t = penpot.library.local.tokens.sets[0].tokens[0];
Object.keys(t)
// → ["id", "name", "type", "value", "description"]
```

No `extensions`, `$extensions`, or any vendor-data field. The five fields above are
the entire surface.

### 2. `.extensions` assignment does not persist

Setting `t.extensions = { 'com.fntools.test': { foo: 'bar' } }` succeeds (JS allows
arbitrary property assignment on the proxy object), but re-fetching the token via
`penpotUtils.findTokenByName(t.name)` returns a fresh object whose keys are still
`["id", "name", "type", "value", "description"]`. The assignment is dropped — Penpot's
backing data model has no slot for it.

### 3. Internal transit storage has no extensions slot

Per `penpot-api.md`:

```
["~#penpot/token", ["^ ",
  "~:id", "<uuid>",
  "~:name", "<dot.path>",
  "~:type", "~:color",
  "~:value", "#RRGGBB",
  "~:description", ""
]]
```

Five fields, all five accounted for. No extensions key in the schema.

### 4. Export format omits extensions

Per `penpot-api.md` Tokens-Studio-variant export format (Penpot 2.14.4):

```json
{
  "<set-name>": {
    "<group>": { "<leaf>": {
      "$value": "...",
      "$type": "...",
      "$description": ""
    }}
  }
}
```

Only `$value`, `$type`, `$description`. Even if extensions had been present in the
import file, Penpot would not write them back out.

### 5. `addToken` signature

```fsharp
set.addToken(type: TokenType, name: string, value: TokenValueString): Token
```

Three parameters. No way to pass extensions or any vendor metadata at creation.
`description` is settable separately on the resulting `Token` instance and survives;
`extensions` does not.

## Implications for ADR-023

ADR-023 was proposed as: use `$extensions[com.fntools.designtokens.originalColor]`
as a structured carrier for wide-gamut color data, instead of the human-readable
`$description` annotation chosen in ADR-022.

Given the findings:

- **Tokens-Studio-only round-trip**: `$extensions` would survive (Tokens Studio web
  app and pure DTCG tooling both preserve unknown extensions per spec). For workflows
  that never touch Penpot, ADR-023 is implementable and structurally cleaner than
  string-parsing `$description` annotations.
- **Penpot round-trip**: `$extensions` is lost. The wide-gamut original cannot be
  recovered from a Penpot-exported file regardless of carrier choice. The sRGB hex
  fallback (already lossless under the ADR-016 strategy when `Hex` is set) is the
  only data that survives.
- **Cross-tool**: any pipeline that includes a Penpot stage loses extensions.
  `$description` annotation survives Penpot but is harder to parse mechanically.

## Decision update

ADR-022's preserve-aliases choice still stands. The lossy-metadata carrier choice
is **superseded by [ADR-023](decisions/023-tokens-studio-export-extensions-carrier.md)**:
the exporter emits **both** carriers — `$extensions[com.fntools.designtokens][originalColor]`
as the structured payload and the `$description` annotation as the Penpot-survival
companion. The importer favours `$extensions` when present, falls back to parsing
the `$description` annotation, and finally to the lossy sRGB hex when neither
survives. Pipelines that don't pass through Penpot get an exact round-trip;
pipelines that do still recover the wide-gamut original via the description.

## Test method

- Connected Penpot file: Laura's mocks (22 sets, 12 themes, 305+ tokens).
- Plugin API surface inspection via `mcp__penpot__execute_code`.
- Internal storage format read from prior phase 2 work (`penpot-api.md`).
- No REST round-trip test executed: the Plugin-API and storage-format evidence is
  conclusive — there is no path by which `$extensions` could enter Penpot's model
  in the first place.
