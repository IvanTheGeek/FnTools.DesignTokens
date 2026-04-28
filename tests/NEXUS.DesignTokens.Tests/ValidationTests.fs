module NEXUS.DesignTokens.Tests.ValidationTests

open Expecto
open NEXUS.DesignTokens
open NEXUS.DesignTokens.Tests.Fixtures


let private mkLeaf (v: TokenValue) (t: TokenType) =
    TokenLeaf {
        Value = v
        Type = Some t
        Metadata = emptyMeta
    }

let private fileWith (children: (TokenName * TokenNode) list) =
    { Version = V2025_10; Schema = None; Children = children }


let allTests =
    testList "Validation" [

        testCase "TokenName.tryCreate rejects '$' prefix" <| fun () ->
            match TokenName.tryCreate "$foo" with
            | Error _ -> ()
            | Ok _ -> failtest "expected error"

        testCase "TokenName.tryCreate rejects '.' in name" <| fun () ->
            match TokenName.tryCreate "a.b" with
            | Error _ -> ()
            | Ok _ -> failtest "expected error"

        testCase "TokenName.tryCreate accepts plain name" <| fun () ->
            match TokenName.tryCreate "brand" with
            | Ok _ -> ()
            | Error msg -> failtestf "unexpected error: %s" msg

        testCase "alpha out of range fails validation" <| fun () ->
            let bad : ColorValue = {
                ColorSpace = SRGB
                Components = (Channel 0.0, Channel 0.0, Channel 0.0)
                Alpha = Some 1.5
                Hex = None
            }
            let file = fileWith [ tn "c", mkLeaf (TokenValue.Color bad) ColorType ]
            match Validation.validate file with
            | Error _ -> ()
            | Ok () -> failtest "expected validation error for alpha=1.5"

        testCase "alpha at boundary 1.0 passes" <| fun () ->
            let ok : ColorValue = {
                ColorSpace = SRGB
                Components = (Channel 0.0, Channel 0.0, Channel 0.0)
                Alpha = Some 1.0
                Hex = None
            }
            let file = fileWith [ tn "c", mkLeaf (TokenValue.Color ok) ColorType ]
            match Validation.validate file with
            | Ok () -> ()
            | Error es -> failtestf "unexpected errors: %A" es

        testCase "fontWeight numeric out of range fails" <| fun () ->
            let bad = TokenValue.FontWeight (Numeric 1500)
            let file = fileWith [ tn "w", mkLeaf bad FontWeightType ]
            match Validation.validate file with
            | Error _ -> ()
            | Ok () -> failtest "expected error for weight 1500"

        testCase "fontWeight numeric in range [1,1000] passes" <| fun () ->
            let ok = TokenValue.FontWeight (Numeric 700)
            let file = fileWith [ tn "w", mkLeaf ok FontWeightType ]
            match Validation.validate file with
            | Ok () -> ()
            | Error es -> failtestf "unexpected errors: %A" es

        testCase "cubicBezier P1x out of [0,1] fails" <| fun () ->
            let bad = TokenValue.CubicBezier { P1x = 1.5; P1y = 0.0; P2x = 0.5; P2y = 1.0 }
            let file = fileWith [ tn "ease", mkLeaf bad CubicBezierType ]
            match Validation.validate file with
            | Error _ -> ()
            | Ok () -> failtest "expected error"

        testCase "cubicBezier P1y unbounded passes" <| fun () ->
            let ok = TokenValue.CubicBezier { P1x = 0.5; P1y = -2.0; P2x = 0.5; P2y = 5.0 }
            let file = fileWith [ tn "ease", mkLeaf ok CubicBezierType ]
            match Validation.validate file with
            | Ok () -> ()
            | Error es -> failtestf "unexpected errors: %A" es

        testCase "gradient with single stop fails" <| fun () ->
            let bad : GradientValue =
                [ { Color = Literal {
                        ColorSpace = SRGB
                        Components = (Channel 1.0, Channel 0.0, Channel 0.0)
                        Alpha = None; Hex = None }
                    Position = Literal 0.0 } ]
            let file = fileWith [ tn "g", mkLeaf (TokenValue.Gradient bad) GradientType ]
            match Validation.validate file with
            | Error _ -> ()
            | Ok () -> failtest "expected gradient stop count error"

        testCase "non-finite Number fails" <| fun () ->
            let bad = TokenValue.Number System.Double.PositiveInfinity
            let file = fileWith [ tn "n", mkLeaf bad NumberType ]
            match Validation.validate file with
            | Error _ -> ()
            | Ok () -> failtest "expected non-finite error"

        testCase "circular alias detected" <| fun () ->
            match Format.parse Invalid.circularAlias with
            | Error _ -> ()  // parse may surface it earlier
            | Ok file ->
                match Validation.validate file with
                | Error _ -> ()
                | Ok () -> failtest "expected circular reference"

        testCase "sRGB hex matching components passes" <| fun () ->
            let ok : ColorValue = {
                ColorSpace = SRGB
                Components = (Channel 1.0, Channel 0.5019607843137255, Channel 0.0)
                Alpha = None
                Hex = Some "#ff8000"
            }
            let file = fileWith [ tn "c", mkLeaf (TokenValue.Color ok) ColorType ]
            match Validation.validate file with
            | Ok () -> ()
            | Error es -> failtestf "unexpected errors: %A" es

        testCase "sRGB hex mismatching components fails" <| fun () ->
            let bad : ColorValue = {
                ColorSpace = SRGB
                Components = (Channel 1.0, Channel 0.0, Channel 0.0)  // pure red
                Alpha = None
                Hex = Some "#00ff00"  // pure green — mismatch
            }
            let file = fileWith [ tn "c", mkLeaf (TokenValue.Color bad) ColorType ]
            match Validation.validate file with
            | Error _ -> ()
            | Ok () -> failtest "expected hex/components mismatch error"
    ]
