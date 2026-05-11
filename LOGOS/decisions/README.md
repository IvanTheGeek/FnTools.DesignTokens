# Architecture Decision Records

34 ADRs covering the library's architecture, scope, dependencies, and feature-level technical decisions. Each ADR is one numbered Markdown file in this directory. Several have addenda — call-outs flagged below.

**Latest ADR:** 034 (2026-05-10).
**Most-amended ADRs:** 003, 013, 018, 028 (all closed forward references in 2026-05-10).
**Superseded:** ADR-022 superseded in part by ADR-023.

---

## By topic

### Architecture & cross-cutting

| ID | Title | Date | Notes |
|---|---|---|---|
| [001](001-layer-split-architecture.md) | Layer split — Foundation / Format / Validation / Resolver / Css | 2026-05-02 | Foundation + 4 layers + Bindings + TokensStudio + meta = 8 packages today |
| [002](002-error-collection-not-short-circuit.md) | Parse and validate collect all errors — no short-circuit | 2026-05-02 | Drives `validation { }` CE adoption (ADR-005) |
| [003](003-io-belongs-to-caller.md) | All file I/O is provided by the caller via a load function | 2026-05-02 | **Addendum 2026-05-10**: closes the schema-validator forward reference; points at ADR-013 addendum |
| [004](004-convenience-tier-is-library-responsibility.md) | The library exposes a composed convenience tier alongside primitives | 2026-05-02 | **Addendum 2026-05-03**: F# nested module pattern as the tier signal |
| [011](011-extensions-round-trip-preservation.md) | `$extensions` values are always round-tripped without inspection | 2026-05-03 | Foundation for ADRs 023, 026, 031 |
| [012](012-structural-enforcement-over-documentation.md) | Footguns are eliminated structurally, not by documentation | 2026-05-03 | `ResolvedToken`, infallible `export`; cited by ADRs 028 addendum + 034 |
| [013](013-library-scope-dtcg-interchange-boundary.md) | Library scope ends at the DTCG interchange boundary | 2026-05-03 | **Addendum 2026-05-10**: schema-URL validation explicitly out of scope; suggests `FnTools.DesignTokens.SchemaCheck` companion package |
| [014](014-auto-upgrade-all-spec-versions-on-parse.md) | All four DTCG spec versions are auto-upgraded to 2025.10 on parse | 2026-05-03 | |

### Library tooling & dependencies

| ID | Title | Date | Notes |
|---|---|---|---|
| [005](005-fstoolkit-for-validation-ce.md) | FsToolkit.ErrorHandling for the validation computation expression | 2026-05-02 | Only non-BCL dependency in core layers |
| [006](006-expecto-hedgehog-no-verify.md) | Expecto + Hedgehog for tests; Verify (snapshot) explicitly rejected | 2026-05-02 | |
| [008](008-dtcg-jsonc-no-custom-format.md) | DTCG `.tokens.json` with JSONC comments — no custom authoring format needed | 2026-05-02 | |
| [009](009-toml-tomlyn-for-fnhci-tokens.md) | TOML via Tomlyn for FnHCI non-visual token files | 2026-05-02 | For the future FnHCI package |

### Branding & packaging

| ID | Title | Date |
|---|---|---|
| [007](007-fntools-designtokens-standalone.md) | FnTools.DesignTokens stays standalone — not renamed to FnTools.FnHCI.Tokens.Design | 2026-05-02 |

### Format & Validation

| ID | Title | Date | Notes |
|---|---|---|---|
| [016](016-hex-fallback-in-color-tokens.md) | Include hex fallback in color token values for tooling compatibility | 2026-05-04 | |
| [028](028-em-dimension-unit-extension.md) | `Em` dimension unit as deliberate spec extension | 2026-05-04 | **Addendum 2026-05-10**: strict-mode validator built as `Api.validateStrictDtcg` (v0.7.0) |
| [033](033-dimension-number-alias-handling.md) | Dimension token aliasing a number — validate as TypeMismatch, emit as Npx | 2026-05-10 | Dual-layer fix: validation surfaces, emitter coerces. Shipped v0.6.0 |

### Resolver semantics

| ID | Title | Date | Notes |
|---|---|---|---|
| [015](015-dtcg-resolver-for-design-system-inheritance.md) | Design system inheritance is modelled as a DTCG resolver document | 2026-05-03 | Cheddar → CheddarBooks → LaundryLog chain |
| [032](032-serialize-resolver-document.md) | `serializeResolver` — `ResolverDocument` serialization | 2026-05-08 | Closes the parse/validate/resolve/serialize lifecycle |
| [034](034-evaluate-math-extensions-post-resolve.md) | `tsMathExpression` evaluation is a post-resolve pass at the `Api` layer, not a Resolver change | 2026-05-10 | Closes request_2026-05-10_02. Shipped v0.8.0 |

### CSS emission & ingestion

| ID | Title | Date | Notes |
|---|---|---|---|
| [018](018-cssingest-skipped-for-non-dtcg-units.md) | `CssIngest` emits `Skipped` for non-DTCG units — never degrades silently | 2026-05-04 | **Addendum 2026-05-10**: `TokenValue.CssNative` permanently closed (no clean JSON shape) |
| [019](019-theme-aware-emission-caller-supplied-selector.md) | `emitThemed` accepts a caller-supplied selector function, not a fixed scheme | 2026-05-04 | |
| [027](027-calc-preserving-emission.md) | Calc-preserving CSS emission for the design-tool workbench | 2026-05-04 *(upgraded 2026-05-10)* | Reads `tsMathExpression` for scale detection |

### Bindings & component layer

| ID | Title | Date | Notes |
|---|---|---|---|
| [010](010-n-prefix-numeric-scales.md) | N-prefix for numeric token names in generated F# bindings | 2026-05-02 | `scale.400` → `Scale.N400` |
| [017](017-component-token-tier-lives-in-code.md) | Component tokens live in F# code, not in `.tokens.json` files | 2026-05-03 | Two-tier file model (primitive + semantic only); cited by ADR-018 addendum |

### Tokens Studio integration

| ID | Title | Date | Notes |
|---|---|---|---|
| [020](020-dimension-alias-resolution-at-shim-time.md) | Resolve number→dimension aliases in typography at shim time | 2026-05-04 | |
| [021](021-tokens-studio-metadata-to-resolver-document.md) | Map Tokens Studio `$themes`/`$metadata` to a DTCG `ResolverDocument` | 2026-05-04 | |
| [022](022-tokens-studio-export-preserve-aliases.md) | Tokens Studio export uses preserve-aliases path; `$description` carries lossy metadata | 2026-05-04 | *Carrier choice partially superseded by ADR-023* |
| [023](023-tokens-studio-export-extensions-carrier.md) | Tokens Studio export uses `$extensions` as the primary lossy-metadata carrier | 2026-05-04 | Supersedes ADR-022 for the carrier choice; uses ADR-011 |
| [024](024-variant-set-math-index-filtering.md) | Variant-set math index filtering with `MathEvalFailedVariantAlias` | 2026-05-04 | Theme-bleed fix |
| [025](025-combined-theme-resolution.md) | Combined theme resolution with `importTokensStudioCombined` | 2026-05-04 | |
| [026](026-shim-annotation-recovery-pattern.md) | Shim-annotation recovery pattern for non-color data losses | 2026-05-04 | Extends ADR-023 to typography/HSL/tsType keys |
| [029](029-import-tokens-studio-raw-roundtrip-api.md) | `importTokensStudioRaw` for discoverable round-trip workflow | 2026-05-04 | |
| [030](030-import-tokens-studio-combined-with-dtcg-base.md) | `importTokensStudioCombinedWith` — DTCG base sets alongside TS theme resolution | 2026-05-06 | Uses `DtcgSetRole = AsBasePrimitives` marker |
| [031](031-math-expression-round-trip.md) | Math expression round-trip via `tsMathExpression` annotation | 2026-05-08 | Read by ADR-027 (calc emission) and ADR-034 (resolve-time evaluation) |

---

## Cross-references at a glance

```
ADR-011 ($extensions round-trip)
  └─ enables ADR-023 → ADR-026 → ADR-031 (vendor-namespace recovery patterns)

ADR-013 (DTCG interchange-boundary scope)
  ├─ addendum closes ADR-003's schema-validator forward reference
  ├─ cited by ADR-018 addendum (CssNative out of scope)
  └─ cited by ADR-028 (Em is a deliberate, documented extension)

ADR-012 (structural enforcement)
  ├─ cited by ADR-028 addendum (why validator, not serializer)
  └─ cited by ADR-034 (Resolver stays strict; opt-in extension semantics)

ADR-022 (TS export preserve-aliases)
  └─ partly superseded by ADR-023 (extensions as primary carrier)

ADR-027 (calc-preserving emission)  ──┐
                                      ├─ both read `tsMathExpression`
ADR-031 (math expression round-trip)  │  written by the shim
                                      ├─ ADR-034 (post-resolve evaluation
                                      │  becomes a third reader)
                                      └─ ADR-033 (dimension→number alias
                                         coercion lets calc fire for
                                         alias-resolved scale tokens)
```

---

## Chronological master list

| ID | Date | Title (short) | Status |
|---|---|---|---|
| 001 | 2026-05-02 | Layer split | accepted |
| 002 | 2026-05-02 | Error collection (no short-circuit) | accepted |
| 003 | 2026-05-02 | I/O via load function | accepted + 2026-05-10 addendum |
| 004 | 2026-05-02 | Convenience tier | accepted + 2026-05-03 addendum |
| 005 | 2026-05-02 | FsToolkit.ErrorHandling | accepted |
| 006 | 2026-05-02 | Expecto + Hedgehog, no Verify | accepted |
| 007 | 2026-05-02 | Standalone (not FnHCI-namespaced) | accepted |
| 008 | 2026-05-02 | DTCG JSONC (no custom format) | accepted |
| 009 | 2026-05-02 | TOML via Tomlyn (for FnHCI) | accepted |
| 010 | 2026-05-02 | N-prefix numeric scales | accepted |
| 011 | 2026-05-03 | `$extensions` round-trip | accepted |
| 012 | 2026-05-03 | Structural enforcement | accepted |
| 013 | 2026-05-03 | DTCG interchange-boundary scope | accepted + 2026-05-10 addendum |
| 014 | 2026-05-03 | Auto-upgrade all spec versions | accepted |
| 015 | 2026-05-03 | DTCG resolver for inheritance | accepted |
| 016 | 2026-05-04 | Hex fallback in color tokens | accepted |
| 017 | 2026-05-03 | Component tokens in code | accepted |
| 018 | 2026-05-04 | CssIngest skip non-DTCG units | accepted + 2026-05-10 addendum |
| 019 | 2026-05-04 | `emitThemed` caller-supplied selector | accepted |
| 020 | 2026-05-04 | Dimension alias resolution at shim time | accepted |
| 021 | 2026-05-04 | TS `$themes`/`$metadata` → ResolverDocument | accepted |
| 022 | 2026-05-04 | TS export preserve-aliases | accepted (partly superseded by 023) |
| 023 | 2026-05-04 | TS export `$extensions` carrier | accepted |
| 024 | 2026-05-04 | Variant-set math index filtering | accepted |
| 025 | 2026-05-04 | `importTokensStudioCombined` | accepted |
| 026 | 2026-05-04 | Shim-annotation recovery pattern | accepted |
| 027 | 2026-05-04 | Calc-preserving emission | accepted + 2026-05-10 upgrade |
| 028 | 2026-05-04 | `Em` dimension unit extension | accepted + 2026-05-10 addendum |
| 029 | 2026-05-04 | `importTokensStudioRaw` | accepted |
| 030 | 2026-05-06 | `importTokensStudioCombinedWith` | accepted |
| 031 | 2026-05-08 | Math expression round-trip | accepted |
| 032 | 2026-05-08 | `serializeResolver` | accepted |
| 033 | 2026-05-10 | Dimension→number alias handling | accepted |
| 034 | 2026-05-10 | `tsMathExpression` post-resolve evaluation | accepted |

---

## Adding a new ADR

1. Pick the next sequence number (current latest: 034 → use 035).
2. Filename: `NNN-kebab-case-title.md`.
3. Frontmatter:
   ```markdown
   ---
   id: NNN
   title: One-line description
   status: accepted | proposed | superseded
   date: YYYY-MM-DD
   ---
   ```
4. Body sections: **Context · Decision · Consequences**. Add **Rationale** when the decision space is large. Add **Addendum** later (with a date subhead) when the original forward references close or the decision evolves.
5. Update this index — add a row in the chronological list, in the relevant topic section, and update the cross-references diagram if the new ADR relates to existing ones.
6. Cite the ADR from the relevant code with a comment, and from `LOGOS/work-completed.md` when shipped.
