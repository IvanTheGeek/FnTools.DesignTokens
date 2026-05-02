## Active / Next

> Phases 1–4 (parse / validate / serialize / resolve as a single assembly) are complete — see `tasks-completed.md`. Detailed plan for the items below: `planned-work.md`.

### Phase 5 — Namespace rename + layer split

- [ ] Rename `NEXUS.DesignTokens` → `FnTools.DesignTokens` across all `.fs` files, `.fsproj` files, and project directories; update test `open`s; verify build + 50/50 tests
- [ ] Decide on `.sln` (lean: yes, after the layer split)
- [ ] Layer split into Foundation / Format / Validation / Resolver / facade meta-package

### Future

- [ ] NuGet packaging — metadata + publication target decision
- [ ] Reference smoke test against community-group example tokens (round-trip with zero errors)
