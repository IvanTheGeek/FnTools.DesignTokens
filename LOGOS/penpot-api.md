---
area: Integration
status: reference — 2026-05-03
---

# Penpot Integration Reference

Technical reference for interacting with the self-hosted Penpot instance at
`http://localhost:9001`. Covers the three interaction surfaces, API authentication,
token import/export format, internal storage structure, and setup notes.

---

## Three interaction surfaces

| | REST API | Penpot MCP server | Claude browser extension |
|---|---|---|---|
| **Needs browser open** | No | Yes (plugin must be loaded) | Yes |
| **Needs Penpot file open** | No | Yes | Yes |
| **Scope** | Any file in the instance | Currently open file only | Currently open file only |
| **Token read/write** | Yes — `get-file` returns full token data; `update-file` with `set-token` / `set-token-set` / `set-token-theme` change types (verified 2.14.4) | **Read only** — `penpot.library.local.tokens.sets` (no write surface in Plugin API as of 2.14.4) | Yes (direct DOM/JS access) |
| **Shape create/modify** | Yes via `update-file` change ops | Full | Full |
| **Export shape as image** | Via exporter service | Yes (`export_shape` tool) | Yes (screenshot) |
| **File management** | Yes (create/rename/delete files, projects) | No | No |
| **Multi-file operations** | Yes | No | No |
| **Webhooks** | Yes (outbound on file changes) | No | No |
| **Token format** | transit+json (verbose, parseable) | Tokens Studio / hex strings | Tokens Studio / hex strings |
| **Auth** | Personal access token | Browser session (no extra auth) | Browser session |
| **Headless / CI** | Yes | No | No |

**Note on REST token support**: claims in community posts that REST token support is broken
are outdated. Verified working in Penpot 2.14.4 — see [REST token change types](#rest-token-change-types).
The complexity is the transit+json format, not missing functionality.

### REST API — use for
Token and file operations from F# scripts or CI, headless automation, multi-file queries,
webhooks. Verbose format but complete functionality.

### MCP server — use for
Token and shape work from Claude Code while a file is open. More ergonomic than REST for
interactive AI sessions — Plugin API JavaScript is easier to compose than transit+json.
Requires: MCP server running + browser open with Penpot + plugin connected. See setup below.

### Claude browser extension — use for
Interactive design work: exploring an open file, ad-hoc automation while designing, visual
inspection without writing any code. No setup beyond having the extension installed.

**Key constraint for MCP and browser extension**: Both require a design file to be open.
Neither can be used headlessly — a human must have Penpot open in a browser.

---

## MCP server setup

### Architecture

```
Claude Code ──(HTTP MCP)──► MCP server (port 4401)
                                  │
                           WebSocket (port 4402)
                                  │
                          Penpot Plugin (browser)
                                  │
                           Penpot Plugin API
                                  │
                           Penpot (localhost:9001)
```

The MCP server does not talk to Penpot directly. It sends JavaScript code to a plugin
running inside the browser; the plugin executes that code via the Penpot Plugin API.
The primary MCP tool is `execute_code` — arbitrary Plugin API JavaScript.

Other tools: `high_level_overview`, `penpot_api_info` (type docs), `export_shape` (PNG/SVG),
`import_image` (file → rectangle).

### Start the MCP server

```bash
penpot-mcp          # ~/.local/bin/penpot-mcp — runs @penpot/mcp@2.14.1
# or directly:
npx -y @penpot/mcp@2.14.1
```

Ports: `4400` (plugin web server), `4401` (MCP HTTP + SSE), `4402` (WebSocket for plugin).

The server is added to Claude Code's global user config:
```bash
claude mcp list    # should show: penpot ✓ Connected (when server is running)
```

### Load the plugin in Penpot (one-time per browser session)

1. Open `http://localhost:9001` and open a design file
2. Plugins menu (top bar) → Add plugin → paste `http://localhost:4400/manifest.json`
3. Open the plugin panel → click "Connect to MCP server"
4. Status changes to "Connected" — Claude Code can now use MCP tools against that file

The plugin connection must stay open (don't close the plugin panel) while using MCP tools.

### Claude Code MCP config

Stored in `~/.claude.json` (managed by `claude mcp add`). Entry:
```json
{ "penpot": { "type": "http", "url": "http://localhost:4401/mcp" } }
```

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

Penpot exports in a **Tokens Studio variant**, not DTCG 2025.10:

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
| Aliases | `{ "$value": "{other.token}" }` | Supported (Tokens Studio syntax) |

**Why Penpot uses hex strings**: This is not a custom Tokens Studio format — it's the
DTCG 2nd editor's draft format (the pre-stable version the ecosystem built against), which
required color `$value` to be a hex string: "The value MUST be a string containing a hex
triplet/quartet including the preceding `#` character." The 2025.10 stable release replaced
this with the object format. See https://second-editors-draft.tr.designtokens.org/format/
for the earlier spec text.

Penpot GitHub issue tracking this gap: https://github.com/penpot/penpot/issues/9305

**Next step for `ll.tokens.json`**: Add `"hex"` fallback to each color token. The emitter
already reads `components` for CSS output. A Penpot adapter reads `$value.hex` to produce
the Penpot-compatible format. `Format.parse` validates the full color object (both are
present and correct per schema).

---

## Token type name map

Three distinct type name spaces exist across the three surfaces:

| DTCG `$type`  | REST transit keyword | Plugin API `.type` | Penpot UI label |
|---|---|---|---|
| `color`       | `~:color`            | `color`            | Color           |
| `dimension`   | `~:dimensions`       | `dimension`        | Dimensions      |
| `fontFamily`  | `~:font-family`      | `fontFamilies`     | Font Family     |
| `shadow`      | `~:shadow`           | `shadow`           | Shadow          |
| `fontWeight`  | `~:font-weight`      | `fontWeight`       | Font Weight     |
| `number`      | `~:number`           | `number`           | Number          |
| `typography`  | `~:typography`       | `typography`       | Typography      |

**Critical**: using DTCG type names directly in REST transit produces `400 params-validation`.
Use the `~:dimensions` and `~:font-family` forms shown above.

---

## REST token change types

Verified working in Penpot 2.14.4. All use `update-file` with `Content-Type: application/transit+json`.

### Create / update a token set

```
["^ ",
  "~:type", "~:set-token-set",
  "~:id",   "~u<set-uuid>",
  "~:attrs", ["^ ",
    "~:id",          "~u<set-uuid>",
    "~:name",        "<set-name>",
    "~:description", ""
  ]
]
```

Pass `"~:attrs", null` to delete the set.

### Create / update a token

```
["^ ",
  "~:type",     "~:set-token",
  "~:set-id",   "~u<set-uuid>",
  "~:token-id", "~u<token-uuid>",
  "~:attrs", ["^ ",
    "~:id",          "~u<token-uuid>",
    "~:name",        "dot.separated.path",
    "~:type",        "~:color",
    "~:value",       "#1a6e1a",
    "~:description", ""
  ]
]
```

Pass `"~:attrs", null` to delete the token. Token `~:type` values match Penpot's type set
(not DTCG): `~:color`, `~:number`, `~:dimension`, `~:fontFamily`, `~:fontWeight`,
`~:fontSizes`, `~:spacing`, `~:borderRadius`, etc.

### Other token change types (from source)

- `set-token-theme` — create/update/delete a theme (`~:id`, `~:attrs`)
- `set-active-token-themes` — set which themes are active (`~:theme-paths` as a set of strings)
- `rename-token-set-group` — rename a group prefix
- `move-token-set` / `move-token-set-group` — reorder sets

### Full update-file request shape

```
["^ ",
  "~:id",         "~u<file-uuid>",
  "~:revn",       <current-revn>,
  "~:vern",       <current-vern>,
  "~:session-id", "~u<any-uuid>",
  "~:changes",    [<change-1>, <change-2>, ...]
]
```

`revn` and `vern` come from the last `get-file` or `update-file` response. `session-id` can
be any UUID — it identifies the editing session for conflict tracking.

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
