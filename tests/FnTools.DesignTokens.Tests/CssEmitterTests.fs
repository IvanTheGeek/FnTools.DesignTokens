module FnTools.DesignTokens.Tests.CssEmitterTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Css


// ─── Helpers ──────────────────────────────────────────────────────────────────

let private resolvedColor cs c1 c2 c3 alpha =
    { Value    = ResolvedColor { ColorSpace = cs; Components = (Channel c1, Channel c2, Channel c3); Alpha = alpha; Hex = None }
      Type     = ColorType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private resolvedDim v u =
    { Value    = ResolvedDimension { Value = v; Unit = u }
      Type     = DimensionType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private resolvedDur v u =
    { Value    = ResolvedDuration { Value = v; Unit = u }
      Type     = DurationType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private resolvedNum n =
    { Value    = ResolvedNumber n
      Type     = NumberType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private resolvedFw fw =
    { Value    = ResolvedFontWeight fw
      Type     = FontWeightType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private resolvedFF f =
    { Value    = ResolvedFontFamily f
      Type     = FontFamilyType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private resolvedCb p1x p1y p2x p2y =
    { Value    = ResolvedCubicBezier { P1x = p1x; P1y = p1y; P2x = p2x; P2y = p2y }
      Type     = CubicBezierType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private shadowObj ox oy blur spread (r, g, b) alpha =
    { Color   = { ColorSpace = SRGB; Components = (Channel r, Channel g, Channel b); Alpha = Some alpha; Hex = None }
      OffsetX = { Value = ox;   Unit = Px }
      OffsetY = { Value = oy;   Unit = Px }
      Blur    = { Value = blur; Unit = Px }
      Spread  = { Value = spread; Unit = Px }
      Inset   = None }

let private resolvedShadowSingle obj =
    { Value    = ResolvedShadow (ResolvedShadowSingle obj)
      Type     = ShadowType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private resolvedShadowMulti xs =
    { Value    = ResolvedShadow (ResolvedShadowMultiple xs)
      Type     = ShadowType
      Metadata = { Description = None; Deprecated = None; Extensions = [] } }

let private containsDecl (name: string) (value: string) (css: string) =
    css.Contains(sprintf "%s: %s;" name value)


// ─── colorToCss ───────────────────────────────────────────────────────────────

let colorTests = testList "colorToCss" [

    testCase "oklch no alpha" <| fun () ->
        let c = { ColorSpace = OKLCH; Components = (Channel 0.56, Channel 0.14, Channel 200.0); Alpha = None; Hex = None }
        Expect.equal (colorToCss c) "oklch(0.56 0.14 200)" "oklch without alpha"

    testCase "oklch with alpha" <| fun () ->
        let c = { ColorSpace = OKLCH; Components = (Channel 0.56, Channel 0.14, Channel 200.0); Alpha = Some 0.5; Hex = None }
        Expect.equal (colorToCss c) "oklch(0.56 0.14 200 / 0.5)" "oklch with alpha"

    testCase "srgb uses color() function" <| fun () ->
        let c = { ColorSpace = SRGB; Components = (Channel 0.0, Channel 0.0, Channel 0.0); Alpha = Some 0.06; Hex = None }
        Expect.equal (colorToCss c) "color(srgb 0 0 0 / 0.06)" "srgb with alpha"

    testCase "srgb no alpha omits slash" <| fun () ->
        let c = { ColorSpace = SRGB; Components = (Channel 1.0, Channel 0.0, Channel 0.0); Alpha = None; Hex = None }
        Expect.equal (colorToCss c) "color(srgb 1 0 0)" "srgb no alpha"

    testCase "missing channel emits none" <| fun () ->
        let c = { ColorSpace = OKLCH; Components = (Channel 0.5, Channel 0.2, Missing); Alpha = None; Hex = None }
        Expect.equal (colorToCss c) "oklch(0.5 0.2 none)" "missing hue channel"

    testCase "alpha = 1.0 is not emitted" <| fun () ->
        let c = { ColorSpace = OKLCH; Components = (Channel 0.7, Channel 0.185, Channel 70.0); Alpha = Some 1.0; Hex = None }
        let result = colorToCss c
        Expect.isFalse (result.Contains " / ") "alpha 1.0 should be omitted"

    testCase "display-p3 color space" <| fun () ->
        let c = { ColorSpace = DisplayP3; Components = (Channel 0.5, Channel 0.3, Channel 0.1); Alpha = None; Hex = None }
        Expect.stringStarts (colorToCss c) "color(display-p3 " "display-p3 prefix"
]


// ─── Scalar emitters ──────────────────────────────────────────────────────────

let scalarTests = testList "scalar emitters" [

    testCase "dimension px" <| fun () ->
        Expect.equal (dimensionToCss { Value = 4.0; Unit = Px }) "4px" "4px"

    testCase "dimension rem" <| fun () ->
        Expect.equal (dimensionToCss { Value = 0.6875; Unit = Rem }) "0.6875rem" "0.6875rem"

    testCase "dimension zero" <| fun () ->
        Expect.equal (dimensionToCss { Value = 0.0; Unit = Px }) "0px" "0px"

    testCase "duration ms" <| fun () ->
        Expect.equal (durationToCss { Value = 200.0; Unit = Milliseconds }) "200ms" "200ms"

    testCase "duration s" <| fun () ->
        Expect.equal (durationToCss { Value = 0.35; Unit = Seconds }) "0.35s" "0.35s"

    testCase "cubicBezier" <| fun () ->
        Expect.equal (cubicBezierToCss { P1x = 0.4; P1y = 0.0; P2x = 0.2; P2y = 1.0 })
                     "cubic-bezier(0.4, 0, 0.2, 1)" "standard easing"

    testCase "fontWeight numeric" <| fun () ->
        Expect.equal (fontWeightToCss (Numeric 450)) "450" "numeric 450"

    testCase "fontWeight keyword bold" <| fun () ->
        Expect.equal (fontWeightToCss (Keyword Bold)) "700" "bold → 700"

    testCase "fontFamily stack — generics unquoted, names quoted" <| fun () ->
        let ff = Stack ["DM Sans"; "Helvetica Neue"; "Arial"; "sans-serif"]
        Expect.equal (fontFamilyToCss ff)
                     "\"DM Sans\", \"Helvetica Neue\", \"Arial\", sans-serif"
                     "font family stack"

    testCase "fontFamily single generic" <| fun () ->
        Expect.equal (fontFamilyToCss (Single "monospace")) "monospace" "generic unquoted"
]


// ─── Shadow ───────────────────────────────────────────────────────────────────

let shadowTests = testList "shadowToCss" [

    testCase "single shadow" <| fun () ->
        let s = shadowObj 0.0 1.0 2.0 0.0 (0.0, 0.0, 0.0) 0.06
        let result = shadowToCss (ResolvedShadowSingle s)
        Expect.equal result "0px 1px 2px 0px color(srgb 0 0 0 / 0.06)" "xs shadow"

    testCase "multiple shadows — comma-separated" <| fun () ->
        let s1 = shadowObj 0.0 1.0 4.0 0.0 (0.0, 0.0, 0.0) 0.08
        let s2 = shadowObj 0.0 1.0 2.0 0.0 (0.0, 0.0, 0.0) 0.05
        let result = shadowToCss (ResolvedShadowMultiple [s1; s2])
        Expect.isTrue (result.Contains ",") "comma separates shadows"
        Expect.stringStarts result "0px 1px 4px" "first shadow first"

    testCase "inset shadow" <| fun () ->
        let base' = shadowObj 0.0 0.0 0.0 3.0 (0.0, 0.0, 0.0) 0.5
        let s = { base' with Inset = Some true }
        let result = shadowToCss (ResolvedShadowSingle s)
        Expect.stringStarts result "inset " "inset prefix"
]


// ─── cssVarName ───────────────────────────────────────────────────────────────

let varNameTests = testList "cssVarName" [
    testCase "single segment" <| fun () ->
        Expect.equal (cssVarName ["spacing"]) "--spacing" "single"
    testCase "three segments" <| fun () ->
        Expect.equal (cssVarName ["color"; "action"; "default"]) "--color-action-default" "three"
    testCase "numeric segment" <| fun () ->
        Expect.equal (cssVarName ["color"; "amber"; "N300"]) "--color-amber-N300" "N prefix"
]


// ─── emit (integration) ───────────────────────────────────────────────────────

let emitTests = testList "emit" [

    testCase "produces :root block" <| fun () ->
        let tokens = seq { yield ["spacing"; "N1"], resolvedDim 4.0 Px }
        let css = emit tokens
        Expect.stringStarts css ":root {" "starts with :root"
        Expect.isTrue (css.Contains "--spacing-N1: 4px;") "contains declaration"

    testCase "all token types emit without error" <| fun () ->
        let tokens = [
            ["color"; "primary"],   resolvedColor OKLCH 0.56 0.14 200.0 None
            ["spacing"; "base"],    resolvedDim 4.0 Px
            ["duration"; "fast"],   resolvedDur 100.0 Milliseconds
            ["number"; "ratio"],    resolvedNum 1.5
            ["weight"; "bold"],     resolvedFw (Numeric 700)
            ["font"; "sans"],       resolvedFF (Stack ["DM Sans"; "sans-serif"])
            ["easing"; "std"],      resolvedCb 0.4 0.0 0.2 1.0
            ["shadow"; "xs"],       resolvedShadowSingle (shadowObj 0.0 1.0 2.0 0.0 (0.0, 0.0, 0.0) 0.06)
        ]
        let css = emit (List.toSeq tokens)
        Expect.isTrue (css.Contains "--color-primary: oklch(") "color var present"
        Expect.isTrue (css.Contains "--spacing-base: 4px;")    "dimension var present"
        Expect.isTrue (css.Contains "--duration-fast: 100ms;") "duration var present"
        Expect.isTrue (css.Contains "--number-ratio: 1.5;")    "number var present"
        Expect.isTrue (css.Contains "--easing-std: cubic-bezier(") "cubicBezier var present"

    testCase "LaundryLog token files parse and emit valid CSS" <| fun () ->
        let tokenDir = "/home/ivan/nexus/LaundryLog/tokens"
        let resolverPath = System.IO.Path.Combine(tokenDir, "ll.resolver.json")
        if System.IO.File.Exists resolverPath then
            let loadFile path =
                let full = System.IO.Path.Combine(tokenDir, path)
                if System.IO.File.Exists full then Ok (System.IO.File.ReadAllText full)
                else Error (sprintf "missing: %s" full)
            let resolverJson = System.IO.File.ReadAllText resolverPath
            match Api.importWithResolver loadFile Map.empty resolverJson with
            | Error errs -> failtestf "import failed: %A" errs
            | Ok tokens ->
                let css = emit tokens
                Expect.stringStarts css ":root {" "starts with :root"
                Expect.isTrue (css.Contains "--color-machine-washer-default: oklch(") "washer color present"
                Expect.isTrue (css.Contains "--color-text-primary: oklch(")           "text.primary resolved"
                Expect.isTrue (css.Contains "--shadow-focusRing: ")                   "focusRing shadow present"
                Expect.isTrue (css.Contains "--spacing-N1: 4px;")                     "spacing N1 present"
                Expect.isTrue (css.Contains "--easing-standard: cubic-bezier(")       "easing present"
        else
            skiptest "LaundryLog token files not found — skipping integration test"
]


// ─── All ──────────────────────────────────────────────────────────────────────

let allTests =
    testList "CSS emitter" [
        colorTests
        scalarTests
        shadowTests
        varNameTests
        emitTests
    ]
