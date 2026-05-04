// phase4-verify.fsx
// Phase 4: CSS emission verification.
//
// Goals:
//   1. Resolve Laura's file as Light + Desktop + 100% zoom + Core brand using
//      importTokensStudioCombined (with "Always-on" to include global foundation sets).
//   2. Emit CSS for :root (Light+Desktop) and @media mobile/tablet overrides.
//   3. Compare key token values against the known-correct values from the
//      laura-light-desktop push documented in phase2-findings.md Part 2.
//
// Usage:
//   cd /home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens
//   dotnet fsi scripts/phase4-verify.fsx

#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Foundation/bin/Debug/net10.0/FnTools.DesignTokens.Foundation.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Format/bin/Debug/net10.0/FnTools.DesignTokens.Format.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Validation/bin/Debug/net10.0/FnTools.DesignTokens.Validation.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Resolver/bin/Debug/net10.0/FnTools.DesignTokens.Resolver.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.TokensStudio/bin/Debug/net10.0/FnTools.DesignTokens.TokensStudio.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens/bin/Debug/net10.0/FnTools.DesignTokens.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Css/bin/Debug/net10.0/FnTools.DesignTokens.Css.dll"

open System
open System.IO
open FnTools.DesignTokens
open FnTools.DesignTokens.Css

let lauraJson = File.ReadAllText "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/samples/laura-system-library.tokens.json"
let config    = ShimConfig.defaults

// ── 1. Theme names in Laura's file ────────────────────────────────────────────
// Discover available theme names before resolving.

let themeNames =
    match TokensStudio.shimSingleFile config lauraJson with
    | Error e -> failwithf "shim failed: %s" e
    | Ok sr   -> sr.Themes |> List.map (fun t -> sprintf "  group=%-12s  name=%s" t.Group t.Name)

printfn "=== Laura theme inventory ==="
for t in themeNames do printfn "%s" t
printfn ""

// ── 2. Light + Desktop + 100% zoom + Core brand ───────────────────────────────
// "Always-on" includes the foundation sets (Foundations/*, Palettes, Typography,
// Components/Button). Without it, those sets are classified as theme-owned (they
// appear in Always-on's selectedTokenSets) and excluded from the combined set list.

let lightDesktopThemes = ["Always-on"; "Light"; "Desktop"; "100%"; "Core"]

printfn "=== importTokensStudioCombined %A ===" lightDesktopThemes

let lightDesktopTokens =
    match Api.importTokensStudioCombined config lightDesktopThemes lauraJson with
    | Error es -> failwithf "import failed: %A" es
    | Ok r     ->
        let warns = r.Warnings |> List.length
        printfn "  %d tokens resolved, %d warnings" (List.length r.Tokens) warns
        let unresolved = r.Warnings |> List.choose (function TokenUnresolved (p, ref) -> Some (p, ref) | _ -> None)
        if unresolved.Length > 0 then
            printfn "  %d unresolved:" unresolved.Length
            for (p, ref) in unresolved |> List.truncate 5 do
                printfn "    %s → %s" p ref
            if unresolved.Length > 5 then printfn "    ... and %d more" (unresolved.Length - 5)
        r.Tokens

// ── 3. Key value comparison (against phase2-findings.md Part 2 push data) ──────

let findToken (path: string) (tokens: (string list * ResolvedToken) list) =
    let parts = path.Split('.') |> Array.toList
    tokens |> List.tryFind (fun (p, _) -> p = parts)

let checkColor (path: string) (expected: string) (tokens: (string list * ResolvedToken) list) =
    match findToken path tokens with
    | None -> printfn "  MISSING  %s" path
    | Some (_, rt) ->
        let actual =
            match rt.Value with
            | ResolvedColor c ->
                match c.Hex with
                | Some h -> h.ToLowerInvariant()
                | None   -> colorToCss c
            | other -> sprintf "%A" other
        let ok = actual.Contains(expected.ToLower())
        printfn "  %s  %-40s  expected=%s  actual=%s" (if ok then "OK" else "!!") path expected actual

let checkDim (path: string) (expectedPx: float) (tokens: (string list * ResolvedToken) list) =
    match findToken path tokens with
    | None -> printfn "  MISSING  %s" path
    | Some (_, rt) ->
        let actual =
            match rt.Value with
            | ResolvedDimension d -> d.Value, (match d.Unit with Px -> "px" | Rem -> "rem")
            | ResolvedNumber n    -> n, ""
            | other               -> 0.0, sprintf "%A" other
        let ok = fst actual = expectedPx
        printfn "  %s  %-40s  expected=%.0fpx  actual=%g%s" (if ok then "OK" else "!!") path expectedPx (fst actual) (snd actual)

printfn ""
printfn "=== Value verification (vs phase2-findings.md push data) ==="
printfn "-- Colors (Light mode) --"
checkColor "color.background.default"  "#fafafa" lightDesktopTokens
checkColor "color.border.default"      "#c2bcc1" lightDesktopTokens
checkColor "color.background.body"     "#f3f2f3" lightDesktopTokens

printfn "-- Dimensions (Desktop + 100%% zoom, multiplier from Breakpoints/Desktop) --"
checkDim "breakpoint"    1200.0 lightDesktopTokens
checkDim "spacing.3xs"      8.0 lightDesktopTokens
checkDim "spacing.sm"      16.0 lightDesktopTokens
checkDim "radius.sm"       16.0 lightDesktopTokens

// ── 4. Emit CSS for Light+Desktop (:root) ─────────────────────────────────────

printfn ""
printfn "=== CSS :root emission ==="
let rootCss = CssEmitter.emit lightDesktopTokens
printfn "  %d characters, first 300:" rootCss.Length
printfn "%s" (rootCss.[..min 299 (rootCss.Length - 1)])

// ── 5. Breakpoint overrides: Mobile (360px) and Tablet (1020px) ───────────────

printfn ""
printfn "=== importTokensStudioCombined (Mobile theme) ==="
let mobileThemes = ["Always-on"; "Light"; "Mobile"; "100%"; "Core"]

let mobileTokens =
    match Api.importTokensStudioCombined config mobileThemes lauraJson with
    | Error es -> failwithf "mobile import failed: %A" es
    | Ok r     ->
        printfn "  %d tokens resolved, %d warnings" (List.length r.Tokens) (List.length r.Warnings)
        r.Tokens

printfn ""
printfn "-- Mobile breakpoint values --"
checkDim "breakpoint" 360.0 mobileTokens

printfn ""
printfn "=== importTokensStudioCombined (Tablet theme) ==="
let tabletThemes = ["Always-on"; "Light"; "Tablet"; "100%"; "Core"]

let tabletTokens =
    match Api.importTokensStudioCombined config tabletThemes lauraJson with
    | Error es -> failwithf "tablet import failed: %A" es
    | Ok r     ->
        printfn "  %d tokens resolved, %d warnings" (List.length r.Tokens) (List.length r.Warnings)
        r.Tokens

printfn ""
printfn "-- Tablet breakpoint values --"
checkDim "breakpoint" 1020.0 tabletTokens

// ── 6. Emit full responsive CSS: :root + @media mobile + @media tablet ────────

printfn ""
printfn "=== Responsive CSS emission (Desktop base + Mobile @media + Tablet @media) ==="

let responsiveCss =
    CssEmitter.emitThemed
        (fun n ->
            match n with
            | "Mobile" -> "@media (max-width: 360px)"
            | "Tablet" -> "@media (max-width: 1020px)"
            | other    -> sprintf "[data-theme=\"%s\"]" other)
        lightDesktopTokens
        (seq {
            yield "Tablet", (tabletTokens |> List.toSeq)
            yield "Mobile", (mobileTokens |> List.toSeq)
        })

printfn "  :root block:                       %s" (if responsiveCss.Contains ":root {"               then "present" else "MISSING")
printfn "  @media (max-width: 1020px) block:  %s" (if responsiveCss.Contains "@media (max-width: 1020px)" then "present" else "MISSING")
printfn "  @media (max-width: 360px) block:   %s" (if responsiveCss.Contains "@media (max-width: 360px)"  then "present" else "MISSING")

let tabletIdx = responsiveCss.IndexOf "@media (max-width: 1020px)"
if tabletIdx >= 0 then
    let block = responsiveCss.[tabletIdx..]
    printfn "  --breakpoint: 1020px in @media tablet: %s" (if block.Contains "--breakpoint: 1020px;" then "yes" else "NO")
    printfn "  nested :root in @media tablet: %s"         (if block.Contains "  :root {"             then "yes" else "NO")

let mobileIdx = responsiveCss.IndexOf "@media (max-width: 360px)"
if mobileIdx >= 0 then
    let block = responsiveCss.[mobileIdx..]
    printfn "  --breakpoint: 360px in @media mobile: %s"  (if block.Contains "--breakpoint: 360px;"  then "yes" else "NO")
    printfn "  nested :root in @media mobile: %s"         (if block.Contains "  :root {"             then "yes" else "NO")

// Write the full CSS to a file for manual inspection
let outPath = "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/scripts/phase4-output.css"
File.WriteAllText(outPath, responsiveCss)
printfn ""
printfn "Full responsive CSS written to: %s  (%d chars)" outPath responsiveCss.Length
