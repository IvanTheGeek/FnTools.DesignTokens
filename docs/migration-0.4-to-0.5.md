# Migrating from 0.4 to 0.5

Released: 2026-05-07. **Pure version bump — no functional changes.**

---

## TL;DR

0.5.0 was the first NuGet-CI release. The library code and public API are
identical to 0.4.0. Bump the version and rebuild; nothing else to do.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.4.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.5.0" />
```

---

## What changed

Nothing in the library. The release exists to mark the moment at which
`.forgejo/workflows/publish-stable.yml` (CI publish on `v*` tag) became
the canonical release path. From 0.5.0 onward, every release is
deterministic — the Forgejo build runner checks out the tag SHA, builds,
tests, packs with `-p:Version="${TAG#v}"`, and pushes with
`--skip-duplicate`.

If you're upgrading from 0.4.0, you'll get an identical artifact under a
new version number. If you're starting fresh, 0.5.0 is fine as a baseline
— it just doesn't include the features added in 0.6.0+ (validation rules
and emitter behavior introduced in ADR-033, ADR-028 addendum, and ADR-034).

## Why not skip to 0.6.0?

You can. The 0.5.0 → 0.6.0 jump has real behavior changes
([`migration-0.5-to-0.6.md`](./migration-0.5-to-0.6.md)). 0.5.0 exists for
projects that pinned versions for reproducibility around the date of the
CI-publish transition.
