module FnTools.DesignTokens.Tests.TokensStudioTests

open Expecto
open FnTools.DesignTokens


// ─── Fixtures ────────────────────────────────────────────────────────────────

/// Minimal two-set Tokens Studio JSON: palette set + semantic set that aliases into it.
/// Note: the set name ("palette") is not part of the token path — only the content keys
/// determine the path. So the alias is "{blue}", not "{palette.blue}".
let private twoSetJson = """
{
  "palette": {
    "blue": { "$type": "color", "$value": "#0066cc" }
  },
  "semantic": {
    "primary": { "$type": "color", "$value": "{blue}" }
  },
  "$metadata": {
    "tokenSetOrder": ["palette", "semantic"]
  }
}
"""

/// Single-set Tokens Studio JSON with a number token stored as a string (shim should coerce).
let private numberCoercionJson = """
{
  "scale": {
    "base": { "$type": "number", "$value": "16" },
    "ratio": { "$type": "number", "$value": "1.25" }
  },
  "$metadata": {
    "tokenSetOrder": ["scale"]
  }
}
"""

/// Set with a math expression that PreserveMath keeps as a string — Format.parse will fail
/// for that set, so it should be recorded as SetSkipped.
let private mathExpressionJson = """
{
  "core": {
    "base": { "$type": "number", "$value": "16" }
  },
  "generated": {
    "lg": { "$type": "number", "$value": "round({base} * pow(1.25, 2))" }
  },
  "$metadata": {
    "tokenSetOrder": ["core", "generated"]
  }
}
"""


// ─── Tests ───────────────────────────────────────────────────────────────────

// ─── Theme-aware fixtures ─────────────────────────────────────────────────────

/// Minimal Tokens Studio file with two themes, each enabling different color sets.
/// Base (global) sets: "primitives"
/// Theme sets: "light-colors" (Light theme), "dark-colors" (Dark theme)
let private twoThemeJson = """
{
  "primitives": {
    "white": { "$type": "color", "$value": "#ffffff" },
    "black": { "$type": "color", "$value": "#000000" }
  },
  "light-colors": {
    "bg": { "$type": "color", "$value": "{white}" }
  },
  "dark-colors": {
    "bg": { "$type": "color", "$value": "{black}" }
  },
  "$themes": [
    {
      "id": "1", "name": "Light", "group": "Mode",
      "selectedTokenSets": { "light-colors": "enabled" }
    },
    {
      "id": "2", "name": "Dark", "group": "Mode",
      "selectedTokenSets": { "dark-colors": "enabled" }
    }
  ],
  "$metadata": {
    "tokenSetOrder": ["primitives", "light-colors", "dark-colors"]
  }
}
"""


/// Two-set JSON for testing EvaluateMath with alias-dependent expressions.
/// multiplier is in "scale" set; expression is in "generated" set.
let private evalMathAliasJson = """
{
  "scale": {
    "base":       { "$type": "number", "$value": "16" },
    "multiplier": { "$type": "number", "$value": "1.125" }
  },
  "generated": {
    "xs":  { "$type": "number", "$value": "round({base} * pow({multiplier}, -1))" },
    "3xs": { "$type": "number", "$value": "round({base} * pow({multiplier}, -3))" }
  },
  "$metadata": {
    "tokenSetOrder": ["scale", "generated"]
  }
}
"""


let allTests =
    testList "TokensStudio" [

        testList "EvaluateMath" [

            testCase "plain number alias chain resolves" <| fun () ->
                // base = 16, generated.xs = round(16 * pow(1.125, -1)) = round(14.22) = 14
                match Api.importTokensStudio ShimConfig.defaults evalMathAliasJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    Expect.isEmpty (result.Warnings |> List.choose (function SetSkipped n -> Some n | _ -> None))
                                   "no sets skipped"
                    let xs = result.Tokens |> List.tryFind (fun (p, _) -> p = ["xs"])
                    match xs with
                    | Some (_, { Value = ResolvedNumber v }) -> Expect.equal v 14.0 "xs = round(14.22) = 14"
                    | Some (_, rt) -> failtestf "expected ResolvedNumber for xs, got %A" rt
                    | None -> failtest "xs token not found"

            testCase "negative exponent: round(base * pow(multiplier, -3)) = 11" <| fun () ->
                // round(16 * pow(1.125, -3)) = round(16 * 0.7023) = round(11.237) = 11
                match Api.importTokensStudio ShimConfig.defaults evalMathAliasJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let t3xs = result.Tokens |> List.tryFind (fun (p, _) -> p = ["3xs"])
                    match t3xs with
                    | Some (_, { Value = ResolvedNumber v }) -> Expect.equal v 11.0 "3xs = round(11.237) = 11"
                    | Some (_, rt) -> failtestf "expected ResolvedNumber for 3xs, got %A" rt
                    | None -> failtest "3xs token not found"

            testCase "binary arithmetic without function call: a * b" <| fun () ->
                let json = """
{
  "s": { "a": { "$type": "number", "$value": "3" },
         "b": { "$type": "number", "$value": "4" } },
  "t": { "c": { "$type": "number", "$value": "{a} * {b}" } },
  "$metadata": { "tokenSetOrder": ["s", "t"] }
}"""
                match Api.importTokensStudio ShimConfig.defaults json with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let c = result.Tokens |> List.tryFind (fun (p, _) -> p = ["c"])
                    match c with
                    | Some (_, { Value = ResolvedNumber v }) -> Expect.equal v 12.0 "3 * 4 = 12"
                    | Some (_, rt) -> failtestf "expected 12.0, got %A" rt
                    | None -> failtest "c token not found"

            testCase "unresolvable alias produces MathEvalFailed warning, token omitted" <| fun () ->
                let json = """
{
  "t": { "x": { "$type": "number", "$value": "round({missing} * 2)" } },
  "$metadata": { "tokenSetOrder": ["t"] }
}"""
                match Api.importTokensStudio ShimConfig.defaults json with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    Expect.isEmpty result.Tokens "token omitted"
                    // The set parses fine; the token itself is omitted via MathEvalFailed warning
        ]

        testCase "importTokensStudio: two-set cross-set alias resolves" <| fun () ->
            match Api.importTokensStudio ShimConfig.defaults twoSetJson with
            | Error es -> failtestf "import failed: %A" es
            | Ok result ->
                Expect.equal (List.length result.Warnings) 0 "no warnings"
                Expect.equal (List.length result.Tokens) 2 "two tokens"
                // The set name is NOT a path prefix — only the content keys determine paths.
                // Set "palette" contains {"blue": ...} so the path is just "blue".
                let paths = result.Tokens |> List.map (fst >> String.concat ".")
                Expect.contains paths "blue"    "palette token present at path 'blue'"
                Expect.contains paths "primary" "semantic token present at path 'primary'"
                // Both should be color tokens with the same resolved value
                result.Tokens |> List.iter (fun (_, rt) ->
                    match rt.Value with
                    | ResolvedColor _ -> ()
                    | _ -> failtestf "expected ResolvedColor at %A" rt)

        testCase "importTokensStudio: number string coerced to numeric token" <| fun () ->
            match Api.importTokensStudio ShimConfig.defaults numberCoercionJson with
            | Error es -> failtestf "import failed: %A" es
            | Ok result ->
                Expect.equal (List.length result.Warnings) 0 "no warnings"
                Expect.equal (List.length result.Tokens) 2 "two number tokens"
                result.Tokens |> List.iter (fun (_, rt) ->
                    Expect.equal rt.Type NumberType "number type")
                let values =
                    result.Tokens
                    |> List.choose (fun (_, rt) ->
                        match rt.Value with ResolvedNumber n -> Some n | _ -> None)
                    |> List.sort
                Expect.equal values [1.25; 16.0] "correct numeric values"

        testCase "importTokensStudio: EvaluateMath resolves math expression to concrete value" <| fun () ->
            // round({base} * pow(1.25, 2)) where base=16 → round(16 * 1.5625) = 25
            match Api.importTokensStudio ShimConfig.defaults mathExpressionJson with
            | Error es -> failtestf "import failed: %A" es
            | Ok result ->
                let skipped = result.Warnings |> List.choose (function SetSkipped n -> Some n | _ -> None)
                Expect.isEmpty skipped "no sets skipped"
                Expect.equal (List.length result.Tokens) 2 "both tokens resolved"
                let lgToken =
                    result.Tokens |> List.tryFind (fun (p, _) -> p = ["lg"])
                match lgToken with
                | Some (_, { Value = ResolvedNumber v }) -> Expect.equal v 25.0 "lg = 25"
                | Some (_, rt) -> failtestf "expected ResolvedNumber for lg, got %A" rt
                | None -> failtest "lg token not found"

        testCase "importTokensStudio: PreserveMath leaves expression as string, set skipped" <| fun () ->
            match Api.importTokensStudio { MathPolicy = PreserveMath } mathExpressionJson with
            | Error es -> failtestf "import failed: %A" es
            | Ok result ->
                let skipped = result.Warnings |> List.choose (function SetSkipped n -> Some n | _ -> None)
                Expect.equal skipped ["generated"] "generated set skipped with PreserveMath"
                Expect.equal (List.length result.Tokens) 1 "one token from core set"

        testCase "importTokensStudio: empty tokenSetOrder returns empty result" <| fun () ->
            let json = """{"$metadata": {"tokenSetOrder": []}}"""
            match Api.importTokensStudio ShimConfig.defaults json with
            | Error es -> failtestf "import failed: %A" es
            | Ok result ->
                Expect.isEmpty result.Tokens "no tokens"
                Expect.isEmpty result.Warnings "no warnings"

        testCase "importTokensStudio: invalid JSON returns ParseFailed" <| fun () ->
            match Api.importTokensStudio ShimConfig.defaults "not json" with
            | Error es ->
                let isParseFailed = es |> List.exists (function ParseFailed _ -> true | _ -> false)
                Expect.isTrue isParseFailed "expected ParseFailed"
            | Ok _ -> failtest "expected error"

        testList "Laura system library integration" [

            let lauraPath = "samples/laura-system-library.tokens.json"
            let lauraJson = System.IO.File.ReadAllText lauraPath

            let result =
                match Api.importTokensStudio ShimConfig.defaults lauraJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok r -> r

            testCase "produces 250 resolved tokens (Foundations/Base + typography fontSize resolved)" <| fun () ->
                Expect.equal (List.length result.Tokens) 250 "token count"

            testCase "no sets skipped (Foundations/Base math expressions now evaluate)" <| fun () ->
                let skipped = result.Warnings |> List.choose (function SetSkipped n -> Some n | _ -> None)
                Expect.isEmpty skipped "no sets skipped"

            testCase "0 tokens unresolved (typography fontSize aliases resolved at shim time)" <| fun () ->
                let unresolved = result.Warnings |> List.choose (function TokenUnresolved _ -> Some () | _ -> None)
                Expect.isEmpty unresolved "no unresolved tokens"

            testCase "palette color tokens resolve to hex color values" <| fun () ->
                let paletteTokens =
                    result.Tokens
                    |> List.filter (fun (path, _) ->
                        match path with "palette" :: _ -> true | _ -> false)
                Expect.isGreaterThan (List.length paletteTokens) 0 "palette tokens present"
                paletteTokens |> List.iter (fun (path, rt) ->
                    match rt.Value with
                    | ResolvedColor _ -> ()
                    | _ -> failtestf "expected ResolvedColor at %s" (String.concat "." path))

            testCase "color tokens all resolve (palette + semantic theme sets)" <| fun () ->
                // Semantic sets (Light and Dark) share the same token paths — later set wins.
                // Dark sets are last in tokenSetOrder so Dark values survive. Total: 143.
                let colorTokens =
                    result.Tokens
                    |> List.filter (fun (_, rt) -> rt.Type = ColorType)
                Expect.equal (List.length colorTokens) 143 "143 color tokens"

            testCase "brand font-family tokens: last brand set (Eco Tools) wins" <| fun () ->
                // All three brand sets share the same paths (font-family.default/alt/code).
                // Brand/Eco Tools is last in tokenSetOrder so its values win — 3 tokens.
                let ffTokens =
                    result.Tokens
                    |> List.filter (fun (_, rt) -> rt.Type = FontFamilyType)
                Expect.equal (List.length ffTokens) 3 "3 font-family tokens (last brand set wins)"

            testCase "breakpoint dimension token: Desktop value wins" <| fun () ->
                // Three breakpoint sets all define path 'breakpoint' — Desktop is last so wins.
                let bpTokens =
                    result.Tokens
                    |> List.filter (fun (path, _) -> path = ["breakpoint"])
                Expect.equal (List.length bpTokens) 1 "1 breakpoint dimension token"
                match snd bpTokens.[0] with
                | { Value = ResolvedDimension { Value = 1200.0 } } -> ()
                | rt -> failtestf "expected breakpoint = 1200px, got %A" rt
        ]

        testList "importTokensStudioThemed" [

            testCase "empty theme list: base has all tokens, Themes is empty" <| fun () ->
                match Api.importTokensStudioThemed ShimConfig.defaults [] twoThemeJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    Expect.isEmpty result.Themes "no themes"
                    // All 4 tokens (white, black, bg×2) — but bg is defined twice
                    // (last-wins), so 3 unique paths: white, black, bg
                    Expect.isGreaterThan (List.length result.BaseTokens) 0 "base has tokens"

            testCase "unknown theme name: ThemeNotFound warning, still succeeds" <| fun () ->
                match Api.importTokensStudioThemed ShimConfig.defaults ["NoSuchTheme"] twoThemeJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let notFound = result.Warnings |> List.choose (function ThemeNotFound n -> Some n | _ -> None)
                    Expect.equal notFound ["NoSuchTheme"] "ThemeNotFound recorded"
                    Expect.isEmpty result.Themes "no themes resolved"

            testCase "single theme: base has global tokens, theme has full resolution" <| fun () ->
                match Api.importTokensStudioThemed ShimConfig.defaults ["Light"] twoThemeJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    // primitives (white, black) are global — not in any theme
                    let basePaths = result.BaseTokens |> List.map (fst >> String.concat ".")
                    Expect.contains basePaths "white" "white in base"
                    Expect.contains basePaths "black" "black in base"
                    // Light theme has base + light-colors
                    Expect.equal (List.length result.Themes) 1 "one theme"
                    let lightTokens = result.Themes.[0].Tokens
                    let lightPaths = lightTokens |> List.map (fst >> String.concat ".")
                    Expect.contains lightPaths "white" "white in light (base included)"
                    Expect.contains lightPaths "bg"    "bg in light (light-colors set)"

            testCase "two themes: base is global, each theme has its own semantic tokens" <| fun () ->
                match Api.importTokensStudioThemed ShimConfig.defaults ["Light"; "Dark"] twoThemeJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    Expect.equal (List.length result.Themes) 2 "two themes"
                    Expect.equal result.Themes.[0].ThemeName "Light" "first theme is Light"
                    Expect.equal result.Themes.[1].ThemeName "Dark"  "second theme is Dark"
                    // base: primitives only (light-colors and dark-colors are in allThemeSets)
                    let basePaths = result.BaseTokens |> List.map (fst >> String.concat ".")
                    Expect.contains basePaths "white" "white in base"
                    Expect.contains basePaths "black" "black in base"
                    Expect.isFalse (List.contains "bg" basePaths) "bg NOT in base (theme-specific)"
                    // Light theme: base + light-colors; bg resolves to white (#ffffff)
                    let lightBg =
                        result.Themes.[0].Tokens
                        |> List.tryFind (fun (p, _) -> p = ["bg"])
                    match lightBg with
                    | Some (_, { Value = ResolvedColor c }) when c.Hex = Some "#ffffff" -> ()
                    | Some (_, rt) -> failtestf "light bg wrong value: %A" rt
                    | None -> failtest "light bg token not found"
                    // Dark theme: base + dark-colors; bg resolves to black (#000000)
                    let darkBg =
                        result.Themes.[1].Tokens
                        |> List.tryFind (fun (p, _) -> p = ["bg"])
                    match darkBg with
                    | Some (_, { Value = ResolvedColor c }) when c.Hex = Some "#000000" -> ()
                    | Some (_, rt) -> failtestf "dark bg wrong value: %A" rt
                    | None -> failtest "dark bg token not found"

            testList "Laura Light+Dark integration" [

                let lauraPath = "samples/laura-system-library.tokens.json"
                let lauraJson = System.IO.File.ReadAllText lauraPath

                let result =
                    match Api.importTokensStudioThemed ShimConfig.defaults ["Light"; "Dark"] lauraJson with
                    | Error es -> failtestf "import failed: %A" es
                    | Ok r -> r

                testCase "two themes resolved" <| fun () ->
                    Expect.equal (List.length result.Themes) 2 "Light and Dark"
                    Expect.equal result.Themes.[0].ThemeName "Light" "first theme"
                    Expect.equal result.Themes.[1].ThemeName "Dark"  "second theme"

                testCase "base has global tokens (palette colors present)" <| fun () ->
                    let baseColors =
                        result.BaseTokens
                        |> List.filter (fun (_, rt) -> rt.Type = ColorType)
                    Expect.isGreaterThan (List.length baseColors) 0 "palette colors in base"

                testCase "each theme has more tokens than base (adds semantic colors)" <| fun () ->
                    let baseCount = List.length result.BaseTokens
                    for t in result.Themes do
                        Expect.isGreaterThan (List.length t.Tokens) baseCount
                            (sprintf "%s has more tokens than base" t.ThemeName)

                testCase "Light and Dark differ on at least one semantic color token" <| fun () ->
                    let lightMap =
                        result.Themes.[0].Tokens
                        |> List.map (fun (p, rt) -> String.concat "." p, rt.Value)
                        |> Map.ofList
                    let darkMap =
                        result.Themes.[1].Tokens
                        |> List.map (fun (p, rt) -> String.concat "." p, rt.Value)
                        |> Map.ofList
                    let diffs =
                        lightMap |> Map.filter (fun k lightVal ->
                            match Map.tryFind k darkMap with
                            | Some darkVal -> lightVal <> darkVal
                            | None -> false)
                    Expect.isGreaterThan (Map.count diffs) 0 "Light and Dark have different token values"
            ]
        ]

        testList "export" [

            let shimAndParse (json: string) : ShimResult * Map<string, TokenFile> =
                match TokensStudio.shim json with
                | Error e -> failwithf "shim failed: %s" e
                | Ok sr ->
                    let sets =
                        sr.Sets
                        |> Map.toList
                        |> List.choose (fun (n, j) ->
                            match Api.Primitives.parse j with
                            | Ok f -> Some (n, f)
                            | Error _ -> None)
                        |> Map.ofList
                    sr, sets

            testCase "toResolverDocument: two-theme group → one modifier, primitives in base" <| fun () ->
                let sr, sets = shimAndParse twoThemeJson
                let doc = TokensStudio.toResolverDocument sr sets
                Expect.equal (List.length doc.Modifiers) 1 "one modifier"
                let (mName, mDef) = doc.Modifiers.[0]
                Expect.equal mName "Mode" "modifier name is Mode"
                Expect.equal (List.length mDef.Contexts) 2 "two contexts"
                let ctxNames = mDef.Contexts |> List.map fst
                Expect.contains ctxNames "Light" "Light context"
                Expect.contains ctxNames "Dark"  "Dark context"
                Expect.equal mDef.Default (Some "Light") "default context is first (Light)"
                let baseSets =
                    doc.ResolutionOrder
                    |> List.choose (function SetRef n -> Some n | _ -> None)
                Expect.contains baseSets "primitives"    "primitives is a base set"
                Expect.isFalse (List.contains "light-colors" baseSets) "light-colors not in base"
                Expect.isFalse (List.contains "dark-colors"  baseSets) "dark-colors not in base"

            testCase "toResolverDocument: no themes → all parsed sets become base SetRefs" <| fun () ->
                let json = """
{
  "a": { "x": { "$type": "number", "$value": "1" } },
  "b": { "y": { "$type": "number", "$value": "2" } },
  "$metadata": { "tokenSetOrder": ["a", "b"] }
}"""
                let sr, sets = shimAndParse json
                let doc = TokensStudio.toResolverDocument sr sets
                Expect.isEmpty doc.Modifiers "no modifiers"
                let refs = doc.ResolutionOrder |> List.choose (function SetRef n -> Some n | _ -> None)
                Expect.equal (List.length refs) 2 "two base set refs"
                Expect.contains refs "a" "set a in resolution order"
                Expect.contains refs "b" "set b in resolution order"

            testCase "toResolverDocument: resolver resolves correct context via modifier" <| fun () ->
                let sr, sets = shimAndParse twoThemeJson
                let doc = TokensStudio.toResolverDocument sr sets
                let noLoad _ = Error "inline-only"
                let lightCtx = Map [ "Mode", "Light" ]
                match Resolver.resolve noLoad lightCtx doc with
                | Error es -> failtestf "resolve failed: %A" es
                | Ok merged ->
                    let bg =
                        merged.Children
                        |> List.tryPick (fun (n, node) ->
                            if TokenName.value n = "bg" then
                                match node with TokenLeaf t -> Some t | _ -> None
                            else None)
                    Expect.isSome bg "bg token present in Light resolution"

            testCase "exportToTokensStudio: sRGB color emitted as hex, no warnings" <| fun () ->
                let json = """
{
  "colors": { "brand": { "$type": "color", "$value": "#0066cc" } },
  "$metadata": { "tokenSetOrder": ["colors"] }
}"""
                let sr, sets = shimAndParse json
                let outJson, warnings = TokensStudio.exportToTokensStudio sr sets
                Expect.isEmpty warnings "no lossy warnings"
                Expect.stringContains outJson "\"#0066cc\"" "hex color in output"

            testCase "exportToTokensStudio: alias preserved as {path} string" <| fun () ->
                let sr, sets = shimAndParse twoSetJson
                let outJson, _ = TokensStudio.exportToTokensStudio sr sets
                Expect.stringContains outJson "{blue}" "alias {blue} in output"

            testCase "exportToTokensStudio: $themes and $metadata emitted" <| fun () ->
                let sr, sets = shimAndParse twoThemeJson
                let outJson, _ = TokensStudio.exportToTokensStudio sr sets
                Expect.stringContains outJson "\"$themes\""      "$themes key present"
                Expect.stringContains outJson "\"$metadata\""    "$metadata key present"
                Expect.stringContains outJson "\"tokenSetOrder\"" "tokenSetOrder in $metadata"
                Expect.stringContains outJson "\"primitives\""   "primitives in tokenSetOrder"
                Expect.stringContains outJson "\"Mode\""         "Mode group in $themes"

            testCase "exportToTokensStudio: wide-gamut color produces $extensions originalColor + $description companion" <| fun () ->
                let fakeShim : ShimResult = {
                    Sets     = Map.empty
                    Themes   = []
                    Metadata = { TokenSetOrder = ["tokens"]; ActiveThemes = []; ActiveSets = [] }
                    Warnings = []
                }
                let oklchJson = """
{
  "brand": {
    "$type": "color",
    "primary": {
      "$value": { "colorSpace": "oklch", "components": [0.7, 0.2, 120] }
    }
  }
}"""
                match Api.Primitives.parse oklchJson with
                | Error es -> failtestf "parse failed: %A" es
                | Ok file ->
                    let sets = Map [ "tokens", file ]
                    let outJson, warnings = TokensStudio.exportToTokensStudio fakeShim sets
                    Expect.equal (List.length warnings) 1 "one lossy warning"
                    let (LossyColorConversion (path, original)) = warnings.[0]
                    Expect.equal path "brand.primary" "warning path is brand.primary"
                    Expect.stringContains original "oklch"     "warning carries DTCG color string"
                    // Primary round-trip carrier: $extensions[com.fntools.designtokens.originalColor]
                    Expect.stringContains outJson "$extensions"               "$extensions present"
                    Expect.stringContains outJson "com.fntools.designtokens"  "vendor namespace present"
                    Expect.stringContains outJson "originalColor"             "originalColor key present"
                    Expect.stringContains outJson "oklch"                     "colorSpace preserved in payload"
                    // Companion human annotation in $description (Penpot-survival fallback)
                    Expect.stringContains outJson "$description"              "$description companion present"
                    Expect.stringContains outJson "Tokens Studio compatibility" "lossy annotation in $description"

            testCase "exportToTokensStudio: dimension emitted as string value" <| fun () ->
                let json = """
{
  "size": { "sm": { "$type": "dimension", "$value": "8px" } },
  "$metadata": { "tokenSetOrder": ["size"] }
}"""
                let sr, sets = shimAndParse json
                let outJson, _ = TokensStudio.exportToTokensStudio sr sets
                Expect.stringContains outJson "\"8px\"" "dimension emitted as string"

            testCase "exportToTokensStudio: type names are DTCG names, not TS legacy names" <| fun () ->
                let json = """
{
  "s": {
    "gap":  { "$type": "spacing",   "$value": "16" },
    "size": { "$type": "fontSizes", "$value": "14" }
  },
  "$metadata": { "tokenSetOrder": ["s"] }
}"""
                let sr, sets = shimAndParse json
                let outJson, _ = TokensStudio.exportToTokensStudio sr sets
                Expect.stringContains outJson "\"dimension\""     "DTCG 'dimension' type in output"
                Expect.isFalse (outJson.Contains("\"spacing\""))   "no TS legacy 'spacing' type"
                Expect.isFalse (outJson.Contains("\"fontSizes\"")) "no TS legacy 'fontSizes' type"

            // ─── Round-trip via $extensions (ADR-023) ─────────────────────────

            // Helper: strip the named keys from any JsonObject that has a "$value" sibling
            // (i.e. token leaf nodes) recursively. Replaces brittle regex-based stripping.
            // Nullness is threaded through walk so System.Text.Json's nullable returns flow
            // cleanly into the type system (no FS3261 escape valve).
            let stripLeafKeys (keys: string seq) (json: string) : string =
                let keyList = Seq.toList keys
                let rec walk (node: System.Text.Json.Nodes.JsonNode | null) =
                    match node with
                    | null -> ()
                    | :? System.Text.Json.Nodes.JsonObject as o ->
                        if o.ContainsKey("$value") then
                            for k in keyList do
                                if o.ContainsKey(k) then o.Remove(k) |> ignore
                        // Snapshot the kvp list so removals during walk don't perturb iteration.
                        for kv in Seq.toList (o :> seq<_>) do walk kv.Value
                    | :? System.Text.Json.Nodes.JsonArray as a ->
                        for i in 0 .. a.Count - 1 do walk a.[i]
                    | _ -> ()
                let root = System.Text.Json.Nodes.JsonNode.Parse(json)
                walk root
                match root with
                | null   -> json
                | nonNul -> nonNul.ToJsonString()

            // Helper to extract the first color token from a parsed file's tree.
            let firstColorLeaf (f: TokenFile) : Token =
                let rec scan (children: (TokenName * TokenNode) list) =
                    children
                    |> List.tryPick (fun (_, n) ->
                        match n with
                        | TokenLeaf t when (match t.Value with TokenValue.Color _ -> true | _ -> false) -> Some t
                        | TokenLeaf _ -> None
                        | Group g -> scan g.Children)
                match scan f.Children with
                | Some t -> t
                | None   -> failwith "no color leaf found"

            testCase "round-trip: oklch wide-gamut DTCG → TS export → TS shim → DTCG parse preserves ColorValue" <| fun () ->
                // Authored DTCG: oklch color, no Hex fallback.
                let originalJson = """
{
  "brand": {
    "$type": "color",
    "primary": {
      "$value": { "colorSpace": "oklch", "components": [0.7, 0.2, 120] }
    }
  }
}"""
                let originalFile =
                    match Api.Primitives.parse originalJson with
                    | Ok f    -> f
                    | Error e -> failtestf "initial parse: %A" e
                let originalColor =
                    match (firstColorLeaf originalFile).Value with
                    | TokenValue.Color c -> c
                    | _ -> failwith "expected color"

                // Export to Tokens Studio JSON (lossy).
                let fakeShim : ShimResult = {
                    Sets     = Map.empty
                    Themes   = []
                    Metadata = { TokenSetOrder = ["tokens"]; ActiveThemes = []; ActiveSets = [] }
                    Warnings = []
                }
                let tsJson, warnings =
                    TokensStudio.exportToTokensStudio fakeShim (Map [ "tokens", originalFile ])
                Expect.equal (List.length warnings) 1 "lossy warning on export"

                // Shim back to DTCG per-set JSON; parse it.
                let sr =
                    match TokensStudio.shim tsJson with
                    | Ok sr -> sr | Error e -> failtestf "shim: %s" e
                let setJson =
                    sr.Sets |> Map.toList |> List.head |> snd
                let roundTrippedFile =
                    match Api.Primitives.parse setJson with
                    | Ok f -> f | Error e -> failtestf "round-trip parse: %A" e
                let roundTrippedColor =
                    match (firstColorLeaf roundTrippedFile).Value with
                    | TokenValue.Color c -> c
                    | _ -> failwith "expected color after round-trip"

                Expect.equal roundTrippedColor.ColorSpace originalColor.ColorSpace "colorSpace preserved"
                Expect.equal roundTrippedColor.Components originalColor.Components "components preserved"
                Expect.equal roundTrippedColor.Alpha      originalColor.Alpha      "alpha preserved"

            testCase "round-trip recovery: extension-stripped TS JSON falls back to $description annotation" <| fun () ->
                // Simulate Penpot's behaviour: keep $description, strip $extensions.
                // The shim should still recover the wide-gamut color from the annotation.
                let originalJson = """
{
  "brand": {
    "$type": "color",
    "accent": {
      "$value": { "colorSpace": "display-p3", "components": [0.4, 0.8, 0.2], "alpha": 0.9 }
    }
  }
}"""
                let originalFile =
                    match Api.Primitives.parse originalJson with
                    | Ok f -> f | Error e -> failtestf "initial parse: %A" e
                let fakeShim : ShimResult = {
                    Sets     = Map.empty
                    Themes   = []
                    Metadata = { TokenSetOrder = ["tokens"]; ActiveThemes = []; ActiveSets = [] }
                    Warnings = []
                }
                let tsJson, _ = TokensStudio.exportToTokensStudio fakeShim (Map [ "tokens", originalFile ])
                // Strip $extensions block (Penpot-equivalent) but keep $description.
                let stripped = stripLeafKeys [ "$extensions" ] tsJson
                Expect.isFalse (stripped.Contains "$extensions") "extensions removed for the test"
                Expect.stringContains stripped "$description"     "description survives"

                let sr =
                    match TokensStudio.shim stripped with
                    | Ok sr -> sr | Error e -> failtestf "shim of stripped: %s" e
                let setJson = sr.Sets |> Map.toList |> List.head |> snd
                let recoveredFile =
                    match Api.Primitives.parse setJson with
                    | Ok f -> f | Error e -> failtestf "recovery parse: %A" e
                let recoveredColor =
                    match (firstColorLeaf recoveredFile).Value with
                    | TokenValue.Color c -> c
                    | _ -> failwith "expected color after recovery"

                Expect.equal recoveredColor.ColorSpace DisplayP3 "colorSpace recovered from $description"
                let (c1, c2, c3) = recoveredColor.Components
                Expect.equal c1 (Channel 0.4) "component[0] recovered"
                Expect.equal c2 (Channel 0.8) "component[1] recovered"
                Expect.equal c3 (Channel 0.2) "component[2] recovered"
                Expect.equal recoveredColor.Alpha (Some 0.9) "alpha recovered"

            testCase "round-trip recovery: both extensions and description stripped → falls back to lossy sRGB hex" <| fun () ->
                // Worst-case pipeline: any tool that strips both $extensions AND $description
                // (or whose export route emitted neither). The shim cannot recover the
                // wide-gamut original; the output should be a clean sRGB color from the hex.
                let originalJson = """
{
  "brand": {
    "$type": "color",
    "primary": {
      "$value": { "colorSpace": "oklch", "components": [0.7, 0.2, 120] }
    }
  }
}"""
                let originalFile =
                    match Api.Primitives.parse originalJson with
                    | Ok f -> f | Error e -> failtestf "initial parse: %A" e
                let fakeShim : ShimResult = {
                    Sets     = Map.empty
                    Themes   = []
                    Metadata = { TokenSetOrder = ["tokens"]; ActiveThemes = []; ActiveSets = [] }
                    Warnings = []
                }
                let tsJson, _ = TokensStudio.exportToTokensStudio fakeShim (Map [ "tokens", originalFile ])
                // Strip both $extensions and $description.
                let stripped = stripLeafKeys [ "$extensions"; "$description" ] tsJson
                Expect.isFalse (stripped.Contains "$extensions")  "extensions removed"
                Expect.isFalse (stripped.Contains "$description") "description removed"
                let sr =
                    match TokensStudio.shim stripped with
                    | Ok sr -> sr | Error e -> failtestf "shim of stripped: %s" e
                let setJson = sr.Sets |> Map.toList |> List.head |> snd
                let degradedFile =
                    match Api.Primitives.parse setJson with
                    | Ok f -> f | Error e -> failtestf "degraded parse: %A" e
                let degradedColor =
                    match (firstColorLeaf degradedFile).Value with
                    | TokenValue.Color c -> c
                    | _ -> failwith "expected color"
                // Lossy degradation: original colorSpace is unrecoverable; the parsed value
                // is the sRGB approximation that survived in $value.
                Expect.equal degradedColor.ColorSpace SRGB "falls back to sRGB"
                Expect.notEqual degradedColor.Components (let (c1,c2,c3) =
                                                            (Channel 0.7, Channel 0.2, Channel 120.0)
                                                          in (c1,c2,c3))
                                "components are NOT the wide-gamut originals (degraded)"

            testCase "import priority: $extensions wins over $description when both are present" <| fun () ->
                // Hand-craft TS JSON with conflicting carriers — extension says oklch,
                // description says display-p3. Verify the extension's data is used.
                let conflictingJson = """
{
  "brand": {
    "$type": "color",
    "primary": {
      "$value": "#aa3344",
      "$description": "source: color(display-p3 0.5 0.5 0.5) — converted to sRGB hex for Tokens Studio compatibility",
      "$extensions": {
        "com.fntools.designtokens": {
          "originalColor": {
            "colorSpace": "oklch",
            "components": [0.7, 0.2, 120]
          }
        }
      }
    }
  },
  "$metadata": { "tokenSetOrder": ["tokens"] }
}"""
                let sr =
                    match TokensStudio.shim conflictingJson with
                    | Ok sr -> sr | Error e -> failtestf "shim: %s" e
                let setJson = sr.Sets |> Map.toList |> List.head |> snd
                let parsed =
                    match Api.Primitives.parse setJson with
                    | Ok f -> f | Error e -> failtestf "parse: %A" e
                let color =
                    match (firstColorLeaf parsed).Value with
                    | TokenValue.Color c -> c
                    | _ -> failwith "expected color"
                Expect.equal color.ColorSpace OKLCH
                    "extension's oklch wins over description's display-p3"

            testCase "extensions passthrough: user-authored vendor extensions survive round-trip" <| fun () ->
                // A user-authored extension under a different vendor namespace must be
                // preserved on both the export and the import side per the DTCG spec.
                let json = """
{
  "$schema": "https://design-tokens.org/schemas/2025.10/tokens.json",
  "swatch": {
    "$type": "color",
    "$value": { "colorSpace": "srgb", "components": [0.1, 0.2, 0.3] },
    "$extensions": {
      "com.example.vendor": { "review": "approved" }
    }
  }
}"""
                let file =
                    match Api.Primitives.parse json with
                    | Ok f -> f | Error e -> failtestf "parse: %A" e
                let fakeShim : ShimResult = {
                    Sets     = Map.empty
                    Themes   = []
                    Metadata = { TokenSetOrder = ["tokens"]; ActiveThemes = []; ActiveSets = [] }
                    Warnings = []
                }
                let tsJson, _ = TokensStudio.exportToTokensStudio fakeShim (Map [ "tokens", file ])
                Expect.stringContains tsJson "com.example.vendor" "user vendor key emitted"
                Expect.stringContains tsJson "approved"          "user data emitted"

                let sr =
                    match TokensStudio.shim tsJson with
                    | Ok sr -> sr | Error e -> failtestf "shim: %s" e
                let setJson = sr.Sets |> Map.toList |> List.head |> snd
                Expect.stringContains setJson "com.example.vendor" "user vendor key survives shim"
                Expect.stringContains setJson "approved"          "user data survives shim"
        ]
    ]
