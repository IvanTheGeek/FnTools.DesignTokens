## Active / Next

### Project scaffold
- [ ] Create `src/NEXUS.DesignTokens/NEXUS.DesignTokens.fsproj`
- [ ] Create `tests/NEXUS.DesignTokens.Tests/NEXUS.DesignTokens.Tests.fsproj`
- [ ] Create solution file `NEXUS.DesignTokens.sln`

### Implementation — Phase 1 (core domain)
- [ ] `Errors.fs` — ParseError (+ UnknownSpecVersion), ValidationError, ResolveError, LoadError, ImportError DUs + companion modules with `format` functions
- [ ] `Domain.fs` — SpecVersion DU (FirstEditorsDraft | SecondEditorsDraft | ThirdEditorsDraft | V2025_10)
- [ ] `Domain.fs` — all type definitions per plan
- [ ] `Validation.fs` — constraint checks, returns `ValidationError list`
- [ ] `Json.fs` — low-level System.Text.Json read helpers

### Implementation — Phase 2 (format + resolver)
- [ ] `Format.fs` — version detection + upgrade pass + parse + serialize `.tokens.json`
- [ ] `Resolver.fs` — parse + four-stage resolution algorithm
- [ ] `DesignTokens.fs` — simple API (import, importWithResolver, export) + Primitives nested module (all raw functions)

### Tests
Test frameworks: **Expecto** + **Hedgehog 2.x** (property-based). Verify rejected — see insights.

- [ ] `Fixtures.fs` — JSON strings for all token types (valid + invalid) + older version format examples
- [ ] `Generators.fs` — Hedgehog generators for all domain types (`tokenName`, `colorValue`, `dimensionValue`, `tokenValue`, `aliasToken`, `tokenFile`)
- [ ] `ValidationTests.fs`
- [ ] `ParseTests.fs` — includes `parseAuto` token-file vs resolver detection
- [ ] `VersionTests.fs` — upgrade path tests for First ED, Second ED, Third ED
- [ ] `FlattenTests.fs` — includes `flattenResolved` returning `ResolvedToken` with no `Alias`
- [ ] `SimpleApiTests.fs` — end-to-end tests for `import`, `importWithResolver`, `export` (simple API tier)
- [ ] `PropertyTests.fs` — Hedgehog properties: round-trip, flattenResolved Alias guarantee, error collection completeness, DAG invariant, merge order
- [ ] `ResolverTests.fs`
- [ ] `Program.fs`
