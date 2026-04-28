## Phase 1 — Project scaffold + domain types (current phase)

### Scaffold
- [ ] Solution file, two `.fsproj` files (library + tests)
- [ ] Verify `dotnet build` succeeds on empty projects

### Domain (files 1–2)
- [ ] `Errors.fs` — three error DUs
- [ ] `Domain.fs` — full type system per plan

### Constraint validation (file 3)
- [ ] `Validation.fs` — `validate` and `validateResolver` functions
- [ ] All numeric range checks (alpha, position, cubicBezier P1x/P2x, fontWeight, gradient stops)
- [ ] Circular reference detection (alias DFS, $extends chain)

### JSON helpers (file 4)
- [ ] `Json.fs` — tryGet* / require* helpers, readTokenName, readRef, readExtensions

## Phase 2 — Format parser + serializer

- [ ] `Format.fs` — `parse` (JSON → TokenFile) and `serialize` (TokenFile → JSON)
- [ ] Type dispatch: `$type` inheritance down the group tree
- [ ] All 13 token value parsers
- [ ] Error collection (not short-circuit)
- [ ] Round-trip lossless serialization including unknown `$extensions`

## Phase 3 — Resolver

- [ ] `Resolver.fs` — `parseResolver` and `resolve`
- [ ] Four-stage algorithm with caller-supplied `loadFile`
- [ ] Deep merge (group level) vs. replace (token level)
- [ ] Alias resolution with cycle detection

## Phase 4 — Public façade + tests

- [ ] `DesignTokens.fs` — clean public API surface
- [ ] All test files via Expecto
- [ ] Smoke test: parse spec's own example token file from community-group repo, zero errors

## Phase 5 — NuGet packaging (future)

- [ ] Package metadata in `.fsproj`
- [ ] Publish to NuGet or private feed
