## Completed

### Research & planning
- [x] Read DTCG 2025.10 spec in full — Format, Color, and Resolver modules
- [x] Cloned community-group repo; JSON schemas at `VARIOUS/community-group/www/public/schemas/2025.10/`
- [x] Identified all 13 token types and their field constraints
- [x] Documented 14 color spaces and component value ranges
- [x] Understood two reference syntaxes: curly-brace and JSON Pointer (RFC 6901)
- [x] Understood four-stage resolver algorithm
- [x] Documented what is explicitly out of scope (file I/O, gamut mapping, JSON5, custom type plugins)
- [x] Decided on F# type design patterns: `ValueOrRef<'T>`, `ColorComponent`, mutual recursion via `and`, private `TokenName` constructor
- [x] Wrote full implementation plan: `~/.claude/plans/how-might-we-turn-toasty-deer.md`
- [x] Wrote spec research context: `docs/spec-context.md`
- [x] Initialized repo with plan context commit

### Phase 1 — domain + validation
- [x] `Errors.fs` — ParseError, ValidationError, ResolveError, LoadError, ImportError DUs + format functions
- [x] `Domain.fs` — SpecVersion, full type system (records, DUs, ValueOrRef, ColorComponent)
- [x] `Validation.fs` — all numeric range checks, alias cycle detection, hex/components consistency
- [x] `Json.fs` — `System.Text.Json.Nodes` read helpers (tryGet*, require*, readRef, readExtensions)

### Phase 2 — format
- [x] `Format.fs` — `parse` / `serialize` / `parseAuto` / `parseAs` / `serializeAs`
- [x] Type dispatch with `$type` inheritance down the group tree
- [x] All 13 token value parsers + writers
- [x] Error collection (not short-circuit)
- [x] Round-trip lossless serialization including `$extensions`
- [x] Version detection (FirstED, SecondED, ThirdED, V2025_10) and upgrade-on-parse
- [x] Bug fix: version detector no longer descends into `$value` content (avoided false First-ED detection from inner `value`/`unit` keys)

### Phase 3 — resolver
- [x] `Resolver.fs` — `parseResolver` / `resolve` / `resolveAll`
- [x] Four-stage algorithm with caller-supplied `loadFile`
- [x] Deep merge (group level) vs. replace (token level)
- [x] Alias chain flattening with cycle detection
- [x] Bug fix: `validateResolver` accepts `{"modifier":"name"}` shorthand (empty context = use default)

### Phase 4 — public façade + tests
- [x] `DesignTokens.fs` — `Api` module with `import`, `importWithResolver`, `export`, `Primitives` nested module
- [x] `Fixtures.fs`, `Generators.fs` (Hedgehog 0.x)
- [x] `ValidationTests.fs`, `ParseTests.fs`, `VersionTests.fs`, `FlattenTests.fs`, `ResolverTests.fs`, `SimpleApiTests.fs`, `PropertyTests.fs`
- [x] `Program.fs` — Expecto entry point
- [x] All 50 tests pass; library + tests build with 0 warnings, 0 errors

### Workspace move (2026-05-01)
- [x] Repo moved from `/home/ivan/nexus/NEXUS-Tokens` to `/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens`
- [x] Created `/home/ivan/DEVELOPMENT/FnTools/AGENTS.md` (shared FnTools agent guide)
- [x] Updated `CLAUDE.md` to reflect new path, layered architecture plan, and git workflow
- [x] Replaced project Stop hook with safety-net + push-current-branch (no more timestamp-only commits as the primary record)
- [x] Removed GitKraken hooks from global settings
- [x] Removed parent `nexus` Stop hook (duplicate auto-commit)
- [x] Created public GitHub repo `IvanTheGeek/FnTools.DesignTokens`, wired as `origin`, pushed `main`
- [x] Recorded the move in `/home/ivan/nexus` history (deletion commit)
- [x] Added `LICENSE` (AGPL-3.0)
