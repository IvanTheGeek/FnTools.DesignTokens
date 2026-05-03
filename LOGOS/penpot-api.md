---
area: Integration
status: reference — 2026-05-03
---

# Penpot Integration Reference

Technical reference for interacting with the self-hosted Penpot instance at
`http://localhost:9001`. Covers API authentication, token import/export format, internal
storage structure, and browser automation notes for AI agents.

---

## Instance setup

- Docker Compose: `/opt/penpot/docker-compose.yaml`
- Public URI: `http://localhost:9001`
- Required flags in `PENPOT_FLAGS`:
  ```
  disable-email-verification enable-smtp enable-prepl-server
  disable-secure-session-cookies enable-access-tokens
  ```
- `enable-access-tokens` is required for the token management UI at
  `/#/settings/access-tokens`. Without it the page is hidden but the direct URL still works.

---

## API authentication

Penpot access tokens use `Authorization: Token <token>`, **not** `Bearer`:

```bash
curl -s -H "Authorization: Token $PENPOT_TOKEN" \
  "http://localhost:9001/api/rpc/command/get-profile"
```

Using `Bearer` returns HTTP 200 but authenticates as the anonymous user (id all zeros).

**Token storage**: `~/.config/penpot-claude.token` (344 bytes, single line, no trailing
newline after stripping whitespace). Load safely:

```bash
tok=$(tr -d '[:space:]' < "$HOME/.config/penpot-claude.token")
```

**Shell env setup**: `~/.bashrc` exports `PENPOT_TOKEN` on login — Claude Code inherits it
from the shell that launched it. The `env` key in `.claude/settings.json` does NOT evaluate
shell substitutions — `"$(cat ...)"` is stored as a literal string.

---

## API response format

All responses are `transit+json`. Prefix meanings:
- `~:foo` → keyword `:foo`
- `~ufoo-uuid` → UUID value
- `~mNNN` → timestamp (milliseconds)
- `["^ ", ...]` → map literal
- `["~#ordered-map", [...]]` → ordered map (preserves insertion order)
- `["~#penpot/tokens-lib", ...]` → custom Penpot type

Parse responses with Python's transit library or extract with regex. For simple field
extraction, `python3 -c "import sys, re; ..."` is sufficient.

---

## Key API endpoints

All use GET with query parameters unless noted.

| Endpoint | Params | Notes |
|---|---|---|
| `get-profile` | — | Returns authenticated user or anonymous (all-zero UUID) |
| `get-teams` | — | Returns all teams including default "Default" team |
| `get-projects` | `team-id` | Returns projects in team |
| `get-project-files` | `project-id` | Returns files in project (not `get-files`) |
| `get-file` | `id`, `features` | Must declare all feature flags or gets restriction error |

**Features string** required for `get-file`:
```
fdata/path-data,variants/v1,layout/grid,components/v2,fdata/shape-data-type,fdata/objects-map,design-tokens/v1,styles/v2,plugins/runtime
```

**Instance IDs** (current self-hosted):
- Team: `637c8a56-1cd8-812f-8007-f5caba4278ee` (Default)
- Project: `637c8a56-1cd8-812f-8007-f5caba450606` (Drafts)
- File: `637c8a56-1cd8-812f-8007-f5cafb98e360` (TokenExperiments)
- Page: `637c8a56-1cd8-812f-8007-f5cafb98e361` (Page 1)

---

## Token import format

### What Penpot accepts

```json
{
  "<group>": {
    "<subgroup>": {
      "<leaf>": { "$type": "color", "$value": "#RRGGBB" }
    }
  }
}
```

Rules:
- File name (without extension) becomes the **set name** (`lltokens-hex.json` → `lltokens-hex`)
- Top-level JSON keys become token group prefixes
- `$type` must be on each **leaf** token — group-level `$type` is not supported
- Color values: **hex strings only** (`#RRGGBB`) — Penpot reads `$value` as a string
- DTCG 2025.10 object format `{ "colorSpace": "oklch", "components": [L, C, H] }` is NOT
  supported — produces 1 error token (the whole group parsed as invalid leaf)
- No `$schema` key required (ignored if present)
- Penpot feature supports: color, dimension, border-radius, shadow, spacing, opacity, sizing,
  rotation, font-size, font-family, font-weight, letter-spacing, number, text-case,
  text-decoration, stroke-width, typography

### Importing via browser (AI automation)

The Claude in Chrome extension **cannot** upload local files to web inputs (`file_upload` tool
returns "Not allowed"). Workaround: inject via JavaScript DataTransfer API.

```js
// 1. Open TOOLS → Import dialog first (click the TOOLS button, then Import)
// 2. Then run this in the browser console / javascript_tool:
const tokenJson = JSON.stringify({ /* your token object */ }, null, 2);
const file = new File([tokenJson], 'mytokens.json', { type: 'application/json' });
const input = Array.from(document.querySelectorAll('input[type="file"]'))
  .find(el => el.accept === '.json');
const dt = new DataTransfer();
dt.items.add(file);
input.files = dt.files;
input.dispatchEvent(new Event('change', { bubbles: true }));
```

The dialog's file input has `accept=".json"` — find it by that attribute to distinguish from
the image upload input (`accept="image/..."`) and zip input (`accept=".zip"`).

CSP blocks `fetch()` to cross-port localhost URLs from Penpot's origin. Embed token JSON
content directly in the script instead of fetching from a local server.

---

## Token export format

Penpot exports in a **Token Studio variant**, not DTCG 2025.10:

```json
{
  "<set-name>": {
    "<group>": {
      "<subgroup>": {
        "<leaf>": {
          "$value": "#RRGGBB",
          "$type": "color",
          "$description": ""
        }
      }
    }
  }
}
```

Differences from the import format:
- Set name added as extra top-level key (wraps the entire structure)
- `$description` always present, empty string when none
- No `$schema` declaration
- Color values remain hex regardless of original format

`Format.parse` (DTCG 2025.10) will **not** accept this export without transformation:
the extra set-name wrapper is not valid DTCG top-level structure.

---

## Internal token storage

Tokens are stored in `file.data.options.tokens-lib` as transit+json:

```
["~#penpot/tokens-lib", ["^ ",
  "~:sets", ["~#ordered-map", [
    ["<set-name>", ["~#penpot/token-set", ["^ ",
      "~:id", "<uuid>",
      "~:name", "<set-name>",
      "~:description", "",
      "~:tokens", ["~#ordered-map", [
        ["<dot.separated.path>", ["~#penpot/token", ["^ ",
          "~:id", "<uuid>",
          "~:name", "<dot.separated.path>",
          "~:type", "~:color",
          "~:value", "#RRGGBB",
          "~:description", ""
        ]]]
      ]]
    ]]]
  ]],
  "~:themes", ...
]]
```

Token paths use dot notation matching the nested JSON structure: `machine.washer.default`.

---

## Format gap: DTCG 2025.10 vs Penpot

**Critical finding from the spec**: DTCG 2025.10 does NOT allow hex strings as `$value` at
all. The color schema requires `{ "colorSpace": "...", "components": [...] }` — both fields
are marked `required`. A hex string is not a valid DTCG 2025.10 color `$value`. There is
therefore no format that is simultaneously DTCG 2025.10 spec-compliant AND natively accepted
by Penpot's `$value` field.

However, the spec provides the `hex` field inside the color object as an **optional sRGB
fallback**, explicitly designed for tooling compatibility:

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

This is the correct shape for our tokens:
- DTCG 2025.10 compliant — OKLCH is the authoritative color
- `hex` is a precomputed sRGB gamut-mapped approximation
- The CSS emitter reads `components` → emits `oklch(0.56 0.14 200)`
- The Penpot adapter reads `$value.hex` → emits `"$value": "#0d9488"` per leaf

| Feature | DTCG 2025.10 | Penpot `design-tokens/v1` |
|---|---|---|
| Color `$value`: object | `{ "colorSpace": "oklch", "components": [L,C,H] }` | NOT supported |
| Color `$value`: hex string | NOT valid (hex is an optional sub-field only) | Required |
| Color `$value.hex` fallback | Optional sRGB fallback within object (spec-designed for this) | Usable via adapter |
| `$type` on group | Valid DTCG | NOT supported (group parsed as error leaf) |
| `$schema` key | Standard | Ignored |
| Set wrapping in export | Not a DTCG concept | Added by Penpot |
| Aliases | `{ "$value": "{other.token}" }` | Supported (Token Studio syntax) |

**Next step for `ll.tokens.json`**: Add `"hex"` fallback to each color token. The emitter
already reads `components` for CSS output. A Penpot adapter reads `$value.hex` to produce
the Penpot-compatible format. `Format.parse` validates the full color object (both are
present and correct per schema).

---

## Workspace URL pattern

```
http://localhost:9001/#/workspace?team-id=<team-id>&file-id=<file-id>&page-id=<page-id>&layout=tokens
```

The `&layout=tokens` query param opens the TOKENS panel automatically.

Navigate directly to the TOKENS panel by appending `&layout=tokens` to any workspace URL.

---

## Notes for AI agents

- The TOKENS tab is in the left sidebar: LAYERS | ASSETS | TOKENS
- TOOLS button (bottom-left of TOKENS panel) opens Import / Export menu
- Import dialog has an "IMPORT SINGLE JSON FILE" button — do NOT click it; use the JS
  DataTransfer injection above instead, which avoids the native file picker
- Drag-and-drop on the canvas is not available via the `computer` browser tool; use
  JavaScript to create shapes programmatically if needed
- Right-clicking a token shows: Edit token / Duplicate token / Delete token
- Export dialog shows a live preview of the JSON before downloading
