Working. Let me read the current state of `emit-tokens.fsx` before making changes, then do the full 0.10.0 update:

The version is still at 0.8.0 and using the wrong composition — the 0.9.0 version bump didn't land in this file. Fixing everything now in one pass for 0.10.0: version bump, remove the `resolverDoc` parse block, replace `resolveWith` with the convenience wrapper.

Clean, no warnings. `tokens.css` grew 27881 → 29411 chars (distinct scale values throughout). Let me verify:

Values are distinct but unitless again — `--spacing-x1: 20` not `1.25rem`. Something about the new path is bypassing the dimension→number coercion the emitter introduced in 0.6.0. Let me check:

All dimension-alias tokens are now unitless — `resolve`/`resolveAll` path was preserving `DimensionType` on the `ResolvedToken`, but `importWithResolverEvaluatingExtensionsWith` with `permissive` seems to be coercing the type to `NumberType`. Let me diagnose before deciding on the fix:

Both paths return `NumberType` — it's not the convenience wrapper specifically, it's `flattenResolved` using the target token's type rather than the aliasing token's declared type. This is the same `DimensionType` loss that existed before 0.6.0 — it was masked in the 0.8.0 path because `resolveAll` internally used `flattenAliases` (which was preserving declared type) before `flattenResolved` ran. Now that `flattenAliases` is public, let me check if inserting it explicitly restores the correct types:

`flattenAliases` preserves `DimensionType`. The four-step composition `resolve → evaluateMathExtensionsInFile → flattenAliases → flattenResolved` gives correct values AND correct types. The convenience wrapper skips `flattenAliases` and goes directly to `flattenResolved`, which takes the target token's type (`NumberType`) instead of the aliasing token's declared type (`DimensionType`). That's a bug in the wrapper.

Updating `emit-tokens.fsx` to the four-step Primitives composition:

Correct values with correct units — `--spacing-x1: 1.25rem` (20px / 16 = 1.25), distinct scale throughout. Clean build, no warnings.

---

**Summary for the library session** (next outside conversation):

`importWithResolverEvaluatingExtensionsWith` with `ValidateOptions.permissive` has a type-loss bug: dimension tokens aliasing number tokens lose their declared `DimensionType` in the output, becoming `NumberType`. The emitter then emits bare numbers with no unit. The cause is that the wrapper goes directly from `evaluateMathExtensionsInFile` to `flattenResolved` — `flattenResolved` uses the alias target's type (`NumberType` from `scale.x1`) rather than the aliasing token's declared type (`DimensionType` from `spacing.x1`).

The fix is either in `flattenResolved` (use the aliasing token's declared `$type`) or in the convenience wrapper (insert `flattenAliases` before `flattenResolved`, since `flattenAliases` correctly preserves declared types). We're working around it with the four-step manual composition — `Primitives.resolve` → `evaluateMathExtensionsInFile` → `Primitives.flattenAliases` → `Primitives.flattenResolved` — which produces both correct values and correct types.
