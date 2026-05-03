---
area: Experiment
status: living document — updated as findings accumulate
purpose: What an AI agent needs to know to use Penpot efficiently across all
         three surfaces, in a form that makes first-run attempts successful.
---

# Penpot AI Operations Reference

Operational knowledge for AI agents working with Penpot 2.14.4 (self-hosted Docker).
Distinct from `penpot-api.md` (protocol reference) — this document is about what
works, what fails first-time, and how to approach each surface efficiently.

---

## Surface selection

Choose before writing a single line of code:

| Goal | Use |
|---|---|
| Read shape tree, tokens, or component structure from an archive | Extract `.penpot` zip, read JSON directly — no API needed |
| Push token changes headlessly (CI, scripts) | REST `update-file` with transit+json |
| Read current live file state | REST `get-file` |
| Interactive shape creation/inspection with a file open | MCP `execute_code` |
| Quick visual inspection or one-off automation | Claude browser extension |

**Never mix surfaces unnecessarily** — MCP requires browser open, REST does not.
If the file is open anyway, MCP is more ergonomic. If it is not, REST is the only
option.

---

## REST API — what works and what fails first-time

### Authentication

```bash
# CORRECT — uses "Token" not "Bearer"
curl -H "Authorization: Token $PENPOT_TOKEN" ...

# WRONG — returns HTTP 200 but authenticates as anonymous (all-zero UUID)
curl -H "Authorization: Bearer $PENPOT_TOKEN" ...
```

Load token safely (no trailing newline):
```bash
tok=$(tr -d '[:space:]' < "$HOME/.config/penpot-claude.token")
```

`$PENPOT_TOKEN` is exported in `~/.bashrc` and available in Claude Code's environment.

### Finding files

There is one team (Default) and one project (Drafts). All files are in Drafts.
`get-project-files` returns transit+json with cached keys after the first entry —
later entries use `^0`, `^1`, etc. instead of repeating full key strings. Parse
the first entry fully; subsequent entries reuse those key positions.

Known instance IDs:
- Team: `637c8a56-1cd8-812f-8007-f5caba4278ee`
- Project: `637c8a56-1cd8-812f-8007-f5caba450606`
- TokenExperiments: `637c8a56-1cd8-812f-8007-f5cafb98e360` (our test file)

Laura files — IDs change on each import. Use `get-project-files` to find the current IDs.
After 2026-05-03 re-import (two copies of each exist in Drafts):

| File | ID (re-import) | ID (working copy) |
|---|---|---|
| System library | `11baa5c9-2a66-8156-8007-f74800eaf8c8` | `11baa5c9-2a66-8156-8007-f71618d1eabd` |
| Design mocks | `11baa5c9-2a66-8156-8007-f74800eaf8c9` | `11baa5c9-2a66-8156-8007-f71618d1eabe` |

Working copy = the one with our experiment changes (renamed frames, etc.).

### `get-file` — features string is mandatory

```bash
FEATURES="fdata/path-data,variants/v1,layout/grid,components/v2,fdata/shape-data-type,fdata/objects-map,design-tokens/v1,styles/v2,plugins/runtime"
curl -s -H "Authorization: Token $tok" \
  "http://localhost:9001/api/rpc/command/get-file?id=<uuid>&features=$FEATURES"
```

Without `features`, you get a restriction error. The response is 800KB+ for the
Design mocks file and uses doubly-encoded transit — shapes are stored as
JSON strings embedded within the transit payload (`fdata/objects-map` feature).

**Better approach for reading shapes**: use the `.penpot` archive (see below).

### Parsing transit+json responses

Transit uses a key caching scheme: after the first occurrence, a key like `"~:name"`
becomes `"^="` (or similar cached reference). Regex on the raw string misses
subsequent entries. For simple cases, extract all occurrences of a known first-entry
key-value pair, then fall back to raw string search for page names and IDs.

Python snippet for extracting all strings after a known position:
```python
# Don't try to parse transit as JSON — it's not JSON.
# Use string search + re.findall for specific known keys.
page_names = re.findall(r'"~:name","([^"]+)"', data)  # only catches first occurrence per cached key set
```

For robust transit parsing, install the Python `transit` library and use it properly.

### Token change types in 2.14.4

The REST API docstring (and older community posts) show `add-token`, `mod-token`,
`del-token`, `add-token-set`, `mod-token-set`. **These do not work in 2.14.4** and
return HTTP 400 validation errors.

Working types (upsert semantics — `attrs: null` deletes):
- `set-token-set` — create or update a token set
- `set-token` — create or update a token within a set

Full request body must use `Content-Type: application/transit+json`. Get `revn`
and `vern` from the previous `get-file` or `update-file` response. See
`penpot-api.md` for the full transit+json request shape.

---

## `.penpot` archive — best for reading shape data

A `.penpot` file is a zip archive. Extract it with standard tools:

```bash
unzip "Design mocks.penpot" -d extracted/
```

Structure:
```
files/
  <file-id>.json              ← file manifest (name, features, migrations, no pages list)
  <file-id>/
    pages/
      <page-id>/
        <shape-id>.json       ← one JSON per shape, clean, no transit encoding
    components/
      <component-id>.json
    tokens.json               ← Tokens Studio multi-set format
```

**Note**: the file manifest's `pages` array is empty in the archive — page IDs are
discovered by listing the `pages/` directory. Page names are not in the manifest;
they come from the shape JSON of the root frame (`00000000-0000-0000-0000-000000000000`)
or from searching the REST `get-file` response.

Page IDs for Design mocks (archive ID `5de02dba-212a-8144-8007-7dc064438707`):
- `32e906fd-569f-8017-8007-7eebe49699ef` — Dashboard
- `5de02dba-212a-8144-8007-7dc064438708` — Landing page
- `a8814cc1-e125-80ae-8007-963014046197` — Email
- `ed1a03b9-0fff-80f7-8007-9e1cd777dd5f` — Thumbnail

### Reading shapes from archive

Each shape JSON contains:
- `id`, `name`, `type` — identity
- `parentId`, `childrenIds` — tree structure
- `appliedTokens` — `{ "fill": "color.background.default", ... }` (string → string)
- `componentId`, `componentFile` — if this shape is a component instance
- `width`, `height`, `x`, `y` — geometry
- `interactions` — prototype connections (array, empty if none)

**`appliedTokens` keys** and their CSS equivalents:

| Key | CSS |
|---|---|
| `fill` | `background-color` or `color` |
| `strokeColor` | `border-color` |
| `strokeWidth` | `border-width` |
| `r1/r2/r3/r4` | `border-radius` (tl/tr/br/bl) |
| `p1/p2/p3/p4` | `padding` (top/right/bottom/left) |
| `m1/m2/m3/m4` | `margin` (top/right/bottom/left) |
| `columnGap` | `column-gap` |
| `rowGap` | `row-gap` |
| `width` | `width` |
| `height` | `height` |
| `typography` | composite (font-family, size, weight, line-height) |

---

## MCP server — what works and what fails first-time

### Setup sequence (must be done in order)

1. Start the MCP server: `penpot-mcp` (or `npx -y @penpot/mcp@2.14.1`)
2. Start Claude Code **after** the MCP server is running — tools are registered at
   session start. If Claude Code starts before the server, MCP tools will not appear
   in the session even if the server starts later.
3. Open Penpot in browser, open a design file
4. Load plugin: Plugins menu → Add → `http://localhost:4400/manifest.json`
5. Open the plugin panel, click "Connect to MCP server"

Verify: `claude mcp list` should show `penpot ✓ Connected`.

### MCP tool availability in Claude Code

MCP tools from the Penpot HTTP server are available during the session **only if the
server was running when Claude Code started**. They do not appear in ToolSearch as
deferred tools — they load at session initialization.

If MCP tools are not available, check:
```bash
claude mcp list   # Is penpot connected?
penpot-mcp        # Start the server if not running, then restart Claude Code
```

### Primary MCP tool: `execute_code`

Runs arbitrary Plugin API JavaScript against the currently open file. This is the
main way to read and write shape data interactively.

Example — list all shapes on the current page with their token bindings:
```javascript
const page = penpot.currentPage;
const shapes = page.findShapes();
shapes.map(s => ({
  name: s.name,
  type: s.type,
  appliedTokens: s.getPluginData('appliedTokens')  // may vary by API version
}));
```

Other tools: `high_level_overview`, `penpot_api_info` (type docs), `export_shape`
(PNG/SVG), `import_image`.

### MCP limitations

- Only works against the **currently open file** in the browser
- Requires browser + plugin panel to stay open
- Not usable headlessly or from CI
- `execute_code` runs in plugin context — some operations require user confirmation

---

## Claude browser extension — when to use

Use for:
- Quick one-off inspection of an open file
- Injecting token JSON via DataTransfer API (file upload workaround)
- Clicking through the Penpot UI when the MCP isn't wired up

Do not use for:
- Headless automation
- Anything requiring a local file path (extension cannot access local files)

Token import via browser extension (workaround for native file picker):
```javascript
// First open: TOOLS → Import dialog
// Then inject the file:
const tokenJson = JSON.stringify({ /* ... */ });
const file = new File([tokenJson], 'mytokens.json', { type: 'application/json' });
const input = Array.from(document.querySelectorAll('input[type="file"]'))
  .find(el => el.accept === '.json');
const dt = new DataTransfer();
dt.items.add(file);
input.files = dt.files;
input.dispatchEvent(new Event('change', { bubbles: true }));
```

CSP blocks `fetch()` across ports — embed the token JSON inline rather than
fetching from a local server.

---

## Tokens Studio theme model

A **theme** is a named preset that specifies which token sets are active — nothing more.
Activating a theme activates its sets; all shapes in the file resolve token paths against
the union of all currently active sets.

Laura's system library has 12 themes in 5 groups. Pick one from each group to form
a complete active state:

| Group | Options |
|---|---|
| Always-on | Global (always active — base sets for typography, spacing, radius, sizing) |
| Color mode | Light, Dark |
| Brand | NeonBooks, Eco Tools, Core |
| Breakpoint | **Mobile** (`Breakpoints/Mobile`), **Tablet** (`Breakpoints/Tablet`), **Desktop** (`Breakpoints/Desktop`) |
| Text zoom | 100%, 150%, 200% |

Example active combination: Always-on + Light + Core + Mobile + 100%

**Themes are file-wide in Penpot 2.14.4.** There is no per-frame theme activation.
All shapes on all pages resolve tokens against the same active theme set. There is no
mechanism to have frame A show Mobile breakpoint values while frame B shows Desktop —
you switch the active Breakpoint theme to view each breakpoint version.

### Breakpoint theme switching workflow

To view/export each breakpoint version of a design:
1. Open the TOKENS panel (left sidebar → Tokens tab)
2. Under the Breakpoint group, activate "Mobile" → file shows mobile token values
3. Inspect / export the "Landing page — Mobile" frame
4. Switch to "Tablet" → inspect the Tablet frame
5. Switch to "Desktop" → inspect the Desktop frame

**The 3 breakpoint frame copies are organisational labels.** They don't hold different
token states — they mark which frame is the reference for each breakpoint version.
All three always reflect the currently active breakpoint theme.

### active-themes storage location

Stored in the **consuming file** (Design mocks), not the System library.
Inside the file's `tokens-lib` → `~:active-themes`. The System library file has no
`active-themes` field — it only holds the token sets and theme definitions.

Current active state in the fresh Design mocks re-import (verified 2026-05-03):
```
["Brand/Core", "Global/Always-on", "Color mode/Dark", "Breakpoint/Tablet",
 "/__PENPOT__HIDDEN__TOKEN__THEME__", "Text zoom/100%"]
```
The hidden theme (`/__PENPOT__HIDDEN__TOKEN__THEME__`) appears automatically and its
group is `""` (empty string). It tracks a snapshot of previously active sets.

Theme path format: `<group>/<name>` — e.g. `"Breakpoint/Mobile"`, `"Color mode/Light"`.
Group is the string from the theme's `~:group` field; name is `~:name`.

### Setting active themes via REST

```
["^ ",
  "~:type", "~:set-active-token-themes",
  "~:theme-paths", ["~#set", ["Brand/Core", "Global/Always-on", "Color mode/Dark",
                              "Breakpoint/Mobile", "/__PENPOT__HIDDEN__TOKEN__THEME__",
                              "Text zoom/100%"]]
]
```

Apply to the **consuming file** (Design mocks), not the System library.

**Critical finding**: REST `set-active-token-themes` persists to the database correctly
(verified: revn increments, `~:active-themes` updates on next `get-file`) but does **not**
push a live update to the Penpot editor. The canvas does not re-render on page refresh
either — the Penpot editor must reload the file from scratch to pick up the change.

Use MCP `execute_code` (Plugin API) for real-time theme switching — the plugin runs
inside the browser context and updates the canvas immediately.

### What the `breakpoint` token actually controls

On the Landing page (confirmed from archive):
- **"Landing page" frame**: `appliedTokens.width = "breakpoint"`, stored width = 1020px
- **"Swatches" frame**: `appliedTokens.width = "breakpoint"`, stored width = 1020px

The `breakpoint` token path resolves to the value in the active `Breakpoints/*` set:
- `Breakpoints/Mobile.breakpoint = 360`
- `Breakpoints/Tablet.breakpoint = 768`
- `Breakpoints/Desktop.breakpoint = 1200`

So switching the active breakpoint theme changes the resolved width of these full-width
container frames. The stored width (1020) in the archive is the baked value from when
the file was exported — it reflects whatever theme was active at export time.

### Per-frame token resolution (not yet in Penpot)

Feature request is open. As of 2.14.4, not available. The CSS emitter (Phase 4)
handles per-breakpoint resolution correctly by outputting separate `:root` + `@media`
blocks — the canvas frames are documentation, not the mechanism.

---

## Common pitfalls

| Pitfall | Correct approach |
|---|---|
| Using `Bearer` auth header | Use `Authorization: Token <token>` |
| Parsing transit+json with JSON parser | Use string search or transit library |
| Using `add-token-set` / `mod-token` in 2.14.4 | Use `set-token-set` / `set-token` |
| Starting Claude Code before MCP server | Start MCP server first, then Claude Code |
| Trying to read shape JSON from `get-file` transit response | Extract `.penpot` archive and read individual JSON files |
| Expecting page list in archive file manifest | List the `pages/` directory instead |
| Using `fdata/objects-map` and expecting inline shapes | Shapes are doubly-encoded strings; use archive JSON instead |
| Re-importing a file expecting old IDs to remain valid | IDs change on each import; use `get-project-files` to find current IDs |
| Expecting per-frame theme activation | Themes are file-wide in 2.14.4; switch theme to view each breakpoint version |
| Manually setting frame width for breakpoint copies | Width of a breakpoint frame is driven by the `breakpoint` token when the correct theme is active; don't hardcode it |

---

## Efficient first-run checklist for Penpot work

Before starting any Penpot task:

- [ ] Is the MCP server running? (`penpot-mcp` if not)
- [ ] Did Claude Code start after the MCP server?
- [ ] Is Penpot open in browser with the target file?
- [ ] Is the plugin panel open and connected?
- [ ] Do I have the file ID? (check this doc or use `get-project-files`)
- [ ] Am I using `Authorization: Token` (not `Bearer`)?
- [ ] Do I need live data (use MCP/REST) or archive data (extract zip)?
