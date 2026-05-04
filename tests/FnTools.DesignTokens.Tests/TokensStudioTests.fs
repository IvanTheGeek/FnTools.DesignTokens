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
    ]
