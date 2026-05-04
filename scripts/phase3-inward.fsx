// phase3-inward.fsx
// Phase 3: Token flow inward — characterise what the shim produces from Laura's
// Tokens Studio export and what DTCG Format.parse accepts vs rejects.
//
// Usage: dotnet fsi scripts/phase3-inward.fsx

#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Foundation/bin/Debug/net10.0/FnTools.DesignTokens.Foundation.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Format/bin/Debug/net10.0/FnTools.DesignTokens.Format.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Validation/bin/Debug/net10.0/FnTools.DesignTokens.Validation.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Resolver/bin/Debug/net10.0/FnTools.DesignTokens.Resolver.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.TokensStudio/bin/Debug/net10.0/FnTools.DesignTokens.TokensStudio.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens/bin/Debug/net10.0/FnTools.DesignTokens.dll"

open System.IO
open FnTools.DesignTokens

let lauraJson = File.ReadAllText "samples/laura-system-library.tokens.json"

// ── 1. Shim all sets ──────────────────────────────────────────────────────────

let shimResult =
    match TokensStudio.shimSingleFile ShimConfig.defaults lauraJson with
    | Error e -> failwithf "shim failed: %s" e
    | Ok r    -> r

printfn "=== SHIM RESULT ==="
printfn "Sets in tokenSetOrder: %d" (List.length shimResult.Metadata.TokenSetOrder)
printfn "Sets produced:         %d" (shimResult.Sets.Count)
printfn "Shim warnings:         %d" (List.length shimResult.Warnings)
printfn "Themes:                %d" (List.length shimResult.Themes)
printfn ""

// ── 2. Per-set parse status ───────────────────────────────────────────────────

printfn "=== PER-SET PARSE STATUS ==="
let mutable totalOk = 0
let mutable totalFail = 0
let mutable totalTokens = 0

let flattenCount (file: TokenFile) =
    Api.Primitives.flatten file |> Seq.length

for setName in shimResult.Metadata.TokenSetOrder do
    match shimResult.Sets |> Map.tryFind setName with
    | None ->
        printfn "  %-40s  MISSING from shim output" setName
    | Some setJson ->
        match Api.Primitives.parse setJson with
        | Error es ->
            totalFail <- totalFail + 1
            printfn "  %-40s  FAIL  (%d errors)" setName (List.length es)
            for e in es |> List.truncate 2 do
                printfn "    ↳ %s" (ParseError.format e)
        | Ok file ->
            let count = flattenCount file
            totalOk    <- totalOk + 1
            totalTokens <- totalTokens + count
            printfn "  %-40s  OK    (%d tokens)" setName count

printfn ""
printfn "  Parse OK: %d/%d sets, %d total tokens" totalOk (totalOk + totalFail) totalTokens
printfn ""

// ── 3. Type distribution across all parsed sets ───────────────────────────────

printfn "=== TOKEN TYPE DISTRIBUTION (shimmed, parsed) ==="
let typeCounts = System.Collections.Generic.Dictionary<string, int>()

for (_, setJson) in shimResult.Sets |> Map.toList do
    match Api.Primitives.parse setJson with
    | Error _ -> ()
    | Ok file ->
        for (_, t) in Api.Primitives.flatten file do
            let typeName =
                match t.Type with
                | Some ColorType      -> "color"
                | Some DimensionType  -> "dimension"
                | Some FontFamilyType -> "fontFamily"
                | Some FontWeightType -> "fontWeight"
                | Some NumberType     -> "number"
                | Some TypographyType -> "typography"
                | Some t              -> sprintf "%A" t
                | None                -> "(untyped)"
            let cur = if typeCounts.ContainsKey typeName then typeCounts.[typeName] else 0
            typeCounts.[typeName] <- cur + 1

for kvp in typeCounts |> Seq.sortByDescending (fun kv -> kv.Value) do
    printfn "  %-16s  %d" kvp.Key kvp.Value
printfn ""

// ── 4. Shim warnings breakdown ────────────────────────────────────────────────

printfn "=== SHIM WARNINGS BREAKDOWN ==="
let mathFailed    = shimResult.Warnings |> List.choose (function MathEvalFailed    (p, e) -> Some (p, e) | _ -> None)
let mathSkipped   = shimResult.Warnings |> List.choose (function SkippedMathExpression (p, e) -> Some (p, e) | _ -> None)
let hslUnresolved = shimResult.Warnings |> List.choose (function UnresolvedHslAlias (p, a) -> Some (p, a) | _ -> None)

printfn "  MathEvalFailed:        %d" (List.length mathFailed)
printfn "  SkippedMathExpression: %d" (List.length mathSkipped)
printfn "  UnresolvedHslAlias:    %d" (List.length hslUnresolved)

if not (List.isEmpty mathFailed) then
    printfn ""
    printfn "  MathEvalFailed samples (first 5):"
    for (path, expr) in mathFailed |> List.truncate 5 do
        printfn "    %s  →  %s" path expr
if not (List.isEmpty hslUnresolved) then
    printfn ""
    printfn "  UnresolvedHslAlias samples (first 5):"
    for (path, alias) in hslUnresolved |> List.truncate 5 do
        printfn "    %s  →  {%s}" path alias
printfn ""

// ── 5. Full import (non-themed): resolved vs unresolved ───────────────────────

printfn "=== FULL IMPORT (non-themed, EvaluateMath) ==="
let importResult =
    match Api.importTokensStudio ShimConfig.defaults lauraJson with
    | Error es -> failwithf "import failed: %A" es
    | Ok r     -> r

let setSkipped   = importResult.Warnings |> List.choose (function SetSkipped n      -> Some n      | _ -> None)
let unresolved   = importResult.Warnings |> List.choose (function TokenUnresolved (p,r) -> Some (p,r) | _ -> None)

printfn "  Resolved tokens:  %d" (List.length importResult.Tokens)
printfn "  Sets skipped:     %d  %A" (List.length setSkipped) setSkipped
printfn "  Tokens unresolved:%d" (List.length unresolved)

if not (List.isEmpty unresolved) then
    printfn ""
    printfn "  Unresolved samples (first 5):"
    for (path, ref) in unresolved |> List.truncate 5 do
        printfn "    %s  →  %s" path ref
printfn ""

// ── 6. What is structurally lost ─────────────────────────────────────────────

printfn "=== STRUCTURAL LOSSES (not recoverable from shimmed output) ==="

// Theme structure: present in shimResult but not expressible as single-set DTCG
printfn "  Themes: %d theme definitions" (List.length shimResult.Themes)
for t in shimResult.Themes do
    let sets = t.SelectedTokenSets |> Map.toList |> List.map (fun (n, s) -> sprintf "%s=%s" n s) |> String.concat "; "
    printfn "    [%s] %s  → %s" t.Group t.Name sets

printfn ""
printfn "  tokenSetOrder (%d sets): multi-set cascade semantics → lost in flat DTCG resolve" (List.length shimResult.Metadata.TokenSetOrder)
printfn "  Math expression strings → evaluated to concrete float; original expr discarded"
printfn "  HSL(a) strings → converted to #rrggbb hex; colorSpace=sRGB forced"
printfn "  transparent keyword → converted to DTCG sRGB 0/0/0 alpha=0 object"
printfn "  fontFamilies array → unwrapped to single fontFamily string (first element)"
printfn "  Legacy TS type names → renamed to DTCG types (spacing→dimension, fontSizes→dimension, etc.)"
printfn "  Cross-set alias chains → resolved to concrete value; alias path discarded"
