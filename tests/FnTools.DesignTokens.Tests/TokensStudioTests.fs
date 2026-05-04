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


/// Two mutually-exclusive zoom sets and a base set with a math expression.
/// tokenSetOrder: zoom/small, zoom/large, base  (zoom/large is LAST).
/// With the old global index: base.size = round(10 * {zoom}) → {zoom} = 2 → size=20 for all.
/// With per-theme fix: each theme uses its own zoom set → size=10 or 20.
/// With shimSingleFile auto-detection: zoom/* are variant → math fails with MathEvalFailedVariantAlias.
let private mathBleedJson = """
{
  "zoom/small": {
    "zoom": { "$type": "number", "$value": "1" }
  },
  "zoom/large": {
    "zoom": { "$type": "number", "$value": "2" }
  },
  "base": {
    "size": { "$type": "number", "$value": "round(10 * {zoom})" }
  },
  "$themes": [
    {
      "id": "1", "name": "Small", "group": "Scale",
      "selectedTokenSets": { "zoom/small": "enabled", "base": "source" }
    },
    {
      "id": "2", "name": "Large", "group": "Scale",
      "selectedTokenSets": { "zoom/large": "enabled", "base": "source" }
    }
  ],
  "$metadata": {
    "tokenSetOrder": ["zoom/small", "zoom/large", "base"]
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

        // ─── Alias type coercion ──────────────────────────────────────────────
        // When a dimension token aliases to a number token, the alias token's own
        // $type annotation (dimension) must take precedence over the target's type
        // (number). Without this, the px unit is lost and the token resolves to a
        // bare number, which is not a valid CSS length.
        testList "alias $type annotation takes precedence over target type" [

            testCase "dimension token aliasing to number resolves as ResolvedDimension with px unit" <| fun () ->
                // Set "base" contains a "scale" group; set "semantic" contains a "spacing" group.
                // The alias {scale.sm} correctly resolves to scale.sm in the merged namespace.
                // (Set names are stripped by the shim — only group names form the token path.)
                let json = """
{
  "base":     { "scale":   { "sm": { "$type": "number",    "$value": "16" } } },
  "semantic": { "spacing": { "sm": { "$type": "dimension", "$value": "{scale.sm}" } } },
  "$metadata": { "tokenSetOrder": ["base", "semantic"] }
}"""
                match Api.importTokensStudio ShimConfig.defaults json with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let sm = result.Tokens |> List.tryFind (fun (p, _) -> p = ["spacing"; "sm"])
                    match sm with
                    | Some (_, { Value = ResolvedDimension d; Type = DimensionType }) ->
                        Expect.equal d.Value 16.0 "value = 16"
                        Expect.equal d.Unit  Px    "unit = px"
                    | Some (_, rt) -> failtestf "expected ResolvedDimension, got %A" rt
                    | None -> failtest "spacing.sm not found"

            testCase "number token without annotation stays ResolvedNumber" <| fun () ->
                // When the alias token has no $type annotation, the target's type is used.
                let json = """
{
  "base":     { "scale": { "sm": { "$type": "number", "$value": "16" } } },
  "semantic": { "alias": { "sm": { "$value": "{scale.sm}" } } },
  "$metadata": { "tokenSetOrder": ["base", "semantic"] }
}"""
                match Api.importTokensStudio ShimConfig.defaults json with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let sm = result.Tokens |> List.tryFind (fun (p, _) -> p = ["alias"; "sm"])
                    match sm with
                    | Some (_, { Value = ResolvedNumber v }) -> Expect.equal v 16.0 "value = 16"
                    | Some (_, rt) -> failtestf "expected ResolvedNumber, got %A" rt
                    | None -> failtest "alias.sm not found"

            testCase "dimension annotation wins even when JSON property order puts scale set last" <| fun () ->
                // Regression: buildFlatIndex used JSON property order, not tokenSetOrder.
                // In Laura's file Foundations/Base is last in JSON order — this test ensures
                // the tokenSetOrder sort fix is in effect by using reversed JSON order.
                // "semantic" appears before "base" in JSON but after it in tokenSetOrder.
                let json = """
{
  "semantic": { "spacing": { "sm": { "$type": "dimension", "$value": "{scale.sm}" } } },
  "base":     { "scale":   { "sm": { "$type": "number",    "$value": "8" } } },
  "$metadata": { "tokenSetOrder": ["base", "semantic"] }
}"""
                match Api.importTokensStudio ShimConfig.defaults json with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let sm = result.Tokens |> List.tryFind (fun (p, _) -> p = ["spacing"; "sm"])
                    match sm with
                    | Some (_, { Value = ResolvedDimension d }) ->
                        Expect.equal d.Value 8.0 "value = 8"
                        Expect.equal d.Unit  Px   "unit = px"
                    | Some (_, rt) -> failtestf "expected ResolvedDimension, got %A" rt
                    | None -> failtest "spacing.sm not found"

        ]

        testList "Laura system library integration" [

            let lauraPath = "samples/laura-system-library.tokens.json"
            let lauraJson = System.IO.File.ReadAllText lauraPath

            let result =
                match Api.importTokensStudio ShimConfig.defaults lauraJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok r -> r

            testCase "produces 204 resolved tokens (variant math filtering prevents Foundations/Base scale tokens from resolving without zoom context)" <| fun () ->
                // Variant sets (zoom/*, breakpoints/*, brand/*, color-mode/*) are excluded
                // from the math index in shimSingleFile. Foundations/Base scale tokens that
                // reference {zoom} fail with MathEvalFailedVariantAlias. Their dependents
                // (spacing/radius/sizing tokens referencing {scale.*}) become unresolved.
                // Use Api.importTokensStudioThemed for correct per-theme resolution.
                Expect.equal (List.length result.Tokens) 204 "token count"

            testCase "no sets skipped (all 22 sets shim to valid DTCG)" <| fun () ->
                let skipped = result.Warnings |> List.choose (function SetSkipped n -> Some n | _ -> None)
                Expect.isEmpty skipped "no sets skipped"

            testCase "36 tokens unresolved: spacing/sizing/radius reference Foundations/Base scale tokens that failed variant math" <| fun () ->
                // Foundations/Spacing, Foundations/Radius, Foundations/Sizing tokens
                // reference {scale.*} paths. scale.* tokens were dropped from the shim
                // output because they depend on {zoom} (a theme-variant alias). The
                // resolver can't find them → TokenUnresolved warnings.
                let unresolved = result.Warnings |> List.choose (function TokenUnresolved _ -> Some () | _ -> None)
                Expect.equal (List.length unresolved) 36 "36 unresolved (spacing/radius/sizing depend on failed scale tokens)"

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

            // ─── Math-evaluator theme-bleed fix ──────────────────────────────────
            // Verifies that importTokensStudioThemed evaluates math expressions
            // using a per-theme index, not the global full-set index. With the bug,
            // the last zoom set in tokenSetOrder wins for all themes (zoom=2 bleeds
            // into zoom/small, making size=20 instead of 10).
            testList "math-bleed fix: per-theme math index" [

                testCase "Small theme: size = round(10 * zoom=1) = 10" <| fun () ->
                    match Api.importTokensStudioThemed ShimConfig.defaults ["Small"; "Large"] mathBleedJson with
                    | Error es -> failtestf "import failed: %A" es
                    | Ok result ->
                        let smallTheme = result.Themes |> List.tryFind (fun t -> t.ThemeName = "Small")
                        match smallTheme with
                        | None -> failtest "Small theme not found"
                        | Some t ->
                            let size = t.Tokens |> List.tryFind (fun (p, _) -> p = ["size"])
                            match size with
                            | Some (_, { Value = ResolvedNumber v }) ->
                                Expect.equal v 10.0 "Small theme: size = round(10 * 1) = 10"
                            | Some (_, rt) -> failtestf "expected ResolvedNumber for size, got %A" rt
                            | None -> failtest "size token not found in Small theme"

                testCase "Large theme: size = round(10 * zoom=2) = 20" <| fun () ->
                    match Api.importTokensStudioThemed ShimConfig.defaults ["Small"; "Large"] mathBleedJson with
                    | Error es -> failtestf "import failed: %A" es
                    | Ok result ->
                        let largeTheme = result.Themes |> List.tryFind (fun t -> t.ThemeName = "Large")
                        match largeTheme with
                        | None -> failtest "Large theme not found"
                        | Some t ->
                            let size = t.Tokens |> List.tryFind (fun (p, _) -> p = ["size"])
                            match size with
                            | Some (_, { Value = ResolvedNumber v }) ->
                                Expect.equal v 20.0 "Large theme: size = round(10 * 2) = 20"
                            | Some (_, rt) -> failtestf "expected ResolvedNumber for size, got %A" rt
                            | None -> failtest "size token not found in Large theme"

                testCase "themes differ: Small=10, Large=20 (not both 20 as with the bug)" <| fun () ->
                    match Api.importTokensStudioThemed ShimConfig.defaults ["Small"; "Large"] mathBleedJson with
                    | Error es -> failtestf "import failed: %A" es
                    | Ok result ->
                        let getSize (themeName: string) =
                            result.Themes
                            |> List.tryFind (fun t -> t.ThemeName = themeName)
                            |> Option.bind (fun t -> t.Tokens |> List.tryFind (fun (p, _) -> p = ["size"]))
                            |> Option.bind (fun (_, rt) ->
                                match rt.Value with ResolvedNumber v -> Some v | _ -> None)
                        match getSize "Small", getSize "Large" with
                        | Some small, Some large ->
                            Expect.notEqual small large "Small and Large have different size values"
                            Expect.equal small 10.0 "Small size = 10"
                            Expect.equal large 20.0 "Large size = 20"
                        | _ -> failtest "could not find size token in both themes"
            ]

            // ─── MathEvalFailedVariantAlias auto-detection ───────────────────────
            // shimSingleFile auto-detects theme-variant sets from $themes and excludes
            // them from the math index. Math expressions that reference variant aliases
            // fail with MathEvalFailedVariantAlias (not MathEvalFailed), which includes
            // a hint to use importTokensStudioThemed. Files without $themes keep the
            // old MathEvalFailed behavior.
            testList "MathEvalFailedVariantAlias: auto-detect variant math aliases" [

                testCase "shimSingleFile emits MathEvalFailedVariantAlias when math references a theme-variant alias" <| fun () ->
                    // mathBleedJson has zoom/small and zoom/large as variant sets (each
                    // enabled in exactly one Scale-group theme). base.size = round(10 * {zoom})
                    // references {zoom}, which lives in a variant set → excluded from math
                    // index → evaluation fails → MathEvalFailedVariantAlias.
                    match TokensStudio.shimSingleFile ShimConfig.defaults mathBleedJson with
                    | Error e -> failtestf "shim failed: %s" e
                    | Ok sr ->
                        let variantFailed =
                            sr.Warnings |> List.choose (function
                                | MathEvalFailedVariantAlias (p, _) -> Some p | _ -> None)
                        Expect.contains variantFailed "size" "size token failed with variant alias warning"

                testCase "MathEvalFailedVariantAlias formatted message includes importTokensStudioThemed hint" <| fun () ->
                    match TokensStudio.shimSingleFile ShimConfig.defaults mathBleedJson with
                    | Error e -> failtestf "shim failed: %s" e
                    | Ok sr ->
                        let w =
                            sr.Warnings |> List.tryPick (function
                                | MathEvalFailedVariantAlias _ as w -> Some w | _ -> None)
                        match w with
                        | None -> failtest "expected MathEvalFailedVariantAlias warning"
                        | Some w ->
                            let msg = TokensStudio.formatWarning w
                            Expect.isTrue (msg.Contains "importTokensStudioThemed") "hint references importTokensStudioThemed"
                            Expect.isTrue (msg.Contains "theme-variant alias") "mentions theme-variant alias"

                testCase "file without $themes uses MathEvalFailed (not variant alias warning)" <| fun () ->
                    // Without $themes there is no variant set detection — math failures
                    // emit MathEvalFailed as before.
                    let noThemesJson = """
{
  "zoom": { "$type": "number", "$value": "1" },
  "base": { "size": { "$type": "number", "$value": "round(10 * {unknown})" } },
  "$metadata": { "tokenSetOrder": ["zoom", "base"] }
}
"""
                    match TokensStudio.shimSingleFile ShimConfig.defaults noThemesJson with
                    | Error e -> failtestf "shim failed: %s" e
                    | Ok sr ->
                        let plain =
                            sr.Warnings |> List.choose (function MathEvalFailed (p, _) -> Some p | _ -> None)
                        let variant =
                            sr.Warnings |> List.choose (function MathEvalFailedVariantAlias (p, _) -> Some p | _ -> None)
                        Expect.isNonEmpty plain "MathEvalFailed emitted"
                        Expect.isEmpty variant "no MathEvalFailedVariantAlias when no $themes"
            ]
        ]

        // ─── importTokensStudioCombined ──────────────────────────────────────
        // Verifies that combining themes from different modifier groups gives
        // correct math results by using ALL themes (not just active) for the
        // allThemeSets computation (ADR-025 cross-group bleed fix).
        //
        // Contrast with importTokensStudioThemed ["Small"]:
        //   allThemeSets = Small's sets only → zoom/large treated as base → bleeds in
        //   → size = round(10 * zoom=2) = 20  (WRONG)
        //
        // importTokensStudioCombined ["Small"]:
        //   allThemeSets = all themes' sets → zoom/large excluded → math index = [zoom/small, base]
        //   → size = round(10 * zoom=1) = 10  (correct)
        testList "importTokensStudioCombined" [

            testCase "Small theme: size = round(10 * zoom=1) = 10" <| fun () ->
                match Api.importTokensStudioCombined ShimConfig.defaults ["Small"] mathBleedJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let size = result.Tokens |> List.tryFind (fun (p, _) -> p = ["size"])
                    match size with
                    | Some (_, { Value = ResolvedNumber v }) ->
                        Expect.equal v 10.0 "size = round(10 * 1) = 10"
                    | Some (_, rt) -> failtestf "expected ResolvedNumber, got %A" rt
                    | None -> failtest "size token not found"

            testCase "Large theme: size = round(10 * zoom=2) = 20" <| fun () ->
                match Api.importTokensStudioCombined ShimConfig.defaults ["Large"] mathBleedJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let size = result.Tokens |> List.tryFind (fun (p, _) -> p = ["size"])
                    match size with
                    | Some (_, { Value = ResolvedNumber v }) ->
                        Expect.equal v 20.0 "size = round(10 * 2) = 20"
                    | Some (_, rt) -> failtestf "expected ResolvedNumber, got %A" rt
                    | None -> failtest "size token not found"

            testCase "importTokensStudioThemed [Small] gives 20 (cross-group bleed)" <| fun () ->
                // Demonstrates the bug that importTokensStudioCombined fixes.
                // When only Small is active, allThemeSets = Small's sets only, so
                // zoom/large is treated as a base set and bleeds into the math index.
                match Api.importTokensStudioThemed ShimConfig.defaults ["Small"] mathBleedJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let smallTheme = result.Themes |> List.tryFind (fun t -> t.ThemeName = "Small")
                    match smallTheme with
                    | None -> failtest "Small theme not found"
                    | Some theme ->
                        let size = theme.Tokens |> List.tryFind (fun (p, _) -> p = ["size"])
                        match size with
                        | Some (_, { Value = ResolvedNumber v }) ->
                            Expect.equal v 20.0 "themed gives 20 (zoom/large bleeds in as base)"
                        | Some (_, rt) -> failtestf "expected ResolvedNumber, got %A" rt
                        | None -> failtest "size token not found in Small theme"

            testCase "unknown theme: ThemeNotFound warning, still returns remaining tokens" <| fun () ->
                match Api.importTokensStudioCombined ShimConfig.defaults ["Small"; "NoSuch"] mathBleedJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    let notFound = result.Warnings |> List.choose (function ThemeNotFound n -> Some n | _ -> None)
                    Expect.equal notFound ["NoSuch"] "ThemeNotFound warning for unknown theme"
                    Expect.isTrue (List.length result.Tokens > 0) "tokens resolved for valid theme"

            testCase "empty theme list: resolves base sets only (global, variant excluded)" <| fun () ->
                // No themes active → allThemeSets = all themes' sets = {zoom/small, zoom/large, base}
                // combinedOwn = {} → combinedSets = sets not in any theme = []
                // (All sets in mathBleedJson are owned by some theme, so result is empty)
                match Api.importTokensStudioCombined ShimConfig.defaults [] mathBleedJson with
                | Error es -> failtestf "import failed: %A" es
                | Ok result ->
                    Expect.isEmpty result.Tokens "no tokens when all sets are theme-owned and none active"

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

            testCase "exportToTokensStudio: original TS type names are recovered on export (ADR-026)" <| fun () ->
                // When the shim renames a TS type (spacing→dimension, fontSizes→dimension),
                // it records the original TS name in $extensions so exportToTokensStudio can
                // restore it. A pure DTCG round-trip still emits "dimension".
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
                Expect.stringContains outJson "\"spacing\""   "original 'spacing' type recovered"
                Expect.stringContains outJson "\"fontSizes\"" "original 'fontSizes' type recovered"
                Expect.isFalse (outJson.Contains("\"dimension\"")) "DTCG 'dimension' not emitted for renamed types"

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

            // ─── ADR-026: shim-annotation recovery ────────────────────────────

            testCase "ADR-026: tsType round-trip — spacing/fontSizes restored on TS export" <| fun () ->
                // Shim renames "spacing"→"dimension" and "fontSizes"→"dimension"; the
                // original TS type names are stored in $extensions so export can restore them.
                let json = """
{
  "tokens": {
    "gap":  { "$type": "spacing",   "$value": "16" },
    "text": { "$type": "fontSizes", "$value": "14" }
  },
  "$metadata": { "tokenSetOrder": ["tokens"] }
}"""
                let sr, sets = shimAndParse json
                let outJson, _ = TokensStudio.exportToTokensStudio sr sets
                Expect.stringContains outJson "\"spacing\""   "spacing type restored"
                Expect.stringContains outJson "\"fontSizes\"" "fontSizes type restored"
                Expect.isFalse (outJson.Contains "\"dimension\"") "DTCG dimension not emitted for renamed types"

            testCase "ADR-026: tsType not added for non-renamed types" <| fun () ->
                // A pure-DTCG "dimension" token (not renamed from a TS legacy type) must
                // export as "dimension" — the extension should not be set.
                let json = """
{
  "tokens": {
    "size": { "$type": "dimension", "$value": "16px" }
  },
  "$metadata": { "tokenSetOrder": ["tokens"] }
}"""
                let sr, sets = shimAndParse json
                let outJson, _ = TokensStudio.exportToTokensStudio sr sets
                Expect.stringContains outJson "\"dimension\"" "DTCG dimension emitted as-is"
                Expect.isFalse (outJson.Contains "tsType")    "tsType extension absent for non-renamed type"

            testCase "ADR-026: originalHsl round-trip — hsl expression restored on TS export" <| fun () ->
                // Shim evaluates hsl(240, 50, 60) → hex; the original expression is
                // stored in $extensions so export restores it exactly.
                // hslRx matches bare-number arguments (no % signs); literal values are
                // used so the resolution succeeds and the hex is actually stored in $value.
                let json = """
{
  "tokens": {
    "fill": { "$type": "color", "$value": "hsl(240, 50, 60)" }
  },
  "$metadata": { "tokenSetOrder": ["tokens"] }
}"""
                let sr, sets = shimAndParse json
                let outJson, _ = TokensStudio.exportToTokensStudio sr sets
                Expect.stringContains outJson "hsl(240, 50, 60)" "original HSL expression restored"
                Expect.isFalse (outJson.Contains "originalHsl") "originalHsl extension stripped from TS output"

            testCase "ADR-026: shim-internal keys stripped from TS export" <| fun () ->
                // After shimming, originalHsl and tsType annotations live in DTCG $extensions.
                // They must not appear in Tokens Studio output — they are DTCG-only carriers.
                // originalColor IS kept (ADR-023 TS-side recovery carrier).
                let json = """
{
  "tokens": {
    "gap":   { "$type": "spacing", "$value": "8"  },
    "fill":  { "$type": "color",   "$value": "hsl(120, 40, 50)" }
  },
  "$metadata": { "tokenSetOrder": ["tokens"] }
}"""
                let sr, sets = shimAndParse json
                let outJson, _ = TokensStudio.exportToTokensStudio sr sets
                Expect.isFalse (outJson.Contains "originalHsl")        "originalHsl absent from TS output"
                Expect.isFalse (outJson.Contains "originalFontWeight") "originalFontWeight absent from TS output"
                Expect.isFalse (outJson.Contains "tsType")             "tsType absent from TS output"
        ]
    ]
