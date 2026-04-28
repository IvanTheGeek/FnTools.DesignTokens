## Active / Next

### Project scaffold
- [ ] Create `src/NEXUS.DesignTokens/NEXUS.DesignTokens.fsproj`
- [ ] Create `tests/NEXUS.DesignTokens.Tests/NEXUS.DesignTokens.Tests.fsproj`
- [ ] Create solution file `NEXUS.DesignTokens.sln`

### Implementation — Phase 1 (core domain)
- [ ] `Errors.fs` — ParseError, ValidationError, ResolveError, LoadError DUs
- [ ] `Domain.fs` — all type definitions per plan
- [ ] `Validation.fs` — constraint checks, returns `ValidationError list`
- [ ] `Json.fs` — low-level System.Text.Json read helpers

### Implementation — Phase 2 (format + resolver)
- [ ] `Format.fs` — parse + serialize `.tokens.json`
- [ ] `Resolver.fs` — parse + four-stage resolution algorithm
- [ ] `DesignTokens.fs` — public façade: primitives + convenience tier (load, flattenResolved, resolveAll)

### Tests
- [ ] `Fixtures.fs` — JSON strings for all token types (valid + invalid)
- [ ] `ValidationTests.fs`
- [ ] `ParseTests.fs`
- [ ] `FlattenTests.fs`
- [ ] `ResolverTests.fs`
- [ ] `Program.fs`
