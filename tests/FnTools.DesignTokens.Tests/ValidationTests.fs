module FnTools.DesignTokens.Tests.ValidationTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Tests.Fixtures


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

        // ─── Cross-type alias mismatch ────────────────────────────────────────

        testCase "dimension token aliasing number is flagged as TypeMismatch" <| fun () ->
            // spacing.x1 ($type:dimension) → scale.x1 ($type:number 16)
            let file = fileWith [
                tn "spacing", Group {
                    Type = None; Metadata = emptyMeta; Root = None; Extends = None
                    Children = [
                        tn "x1", mkLeaf (TokenValue.Alias (CurlyBrace ["scale"; "x1"])) DimensionType
                    ]
                }
                tn "scale", Group {
                    Type = None; Metadata = emptyMeta; Root = None; Extends = None
                    Children = [
                        tn "x1", mkLeaf (TokenValue.Number 16.0) NumberType
                    ]
                }
            ]
            match Validation.validate file with
            | Ok () -> failtest "expected TypeMismatch for dimension→number alias"
            | Error es ->
                let mismatch =
                    es |> List.tryPick (function
                        | TypeMismatch (p, exp, act) -> Some (p, exp, act)
                        | _ -> None)
                match mismatch with
                | None -> failtestf "no TypeMismatch in errors: %A" es
                | Some (path, expected, actual) ->
                    Expect.equal path     "spacing.x1" "path"
                    Expect.equal expected "dimension"  "expected type"
                    Expect.equal actual   "number"     "actual type"

        testCase "same-type alias passes (dimension → dimension)" <| fun () ->
            let file = fileWith [
                tn "base", mkLeaf (TokenValue.Dimension { Value = 8.0; Unit = Px }) DimensionType
                tn "alias", mkLeaf (TokenValue.Alias (CurlyBrace ["base"])) DimensionType
            ]
            match Validation.validate file with
            | Ok () -> ()
            | Error es -> failtestf "unexpected errors: %A" es

        testCase "alias chain dimension → dimension → number is flagged at the dimension origin" <| fun () ->
            // a ($type:dimension) → b ($type:dimension) → c ($type:number)
            // Following the chain, c is the ultimate type; a and b both disagree at the end.
            let file = fileWith [
                tn "a", mkLeaf (TokenValue.Alias (CurlyBrace ["b"])) DimensionType
                tn "b", mkLeaf (TokenValue.Alias (CurlyBrace ["c"])) DimensionType
                tn "c", mkLeaf (TokenValue.Number 16.0) NumberType
            ]
            match Validation.validate file with
            | Ok () -> failtest "expected TypeMismatch through chain"
            | Error es ->
                let mismatches =
                    es |> List.choose (function
                        | TypeMismatch (p, _, _) -> Some p
                        | _ -> None)
                Expect.contains mismatches "a" "a flagged"
                Expect.contains mismatches "b" "b flagged"

        testCase "circular alias does not produce a TypeMismatch (CircularReference instead)" <| fun () ->
            let file = fileWith [
                tn "a", mkLeaf (TokenValue.Alias (CurlyBrace ["b"])) DimensionType
                tn "b", mkLeaf (TokenValue.Alias (CurlyBrace ["a"])) DimensionType
            ]
            match Validation.validate file with
            | Ok () -> failtest "expected at least one error"
            | Error es ->
                let hasCycle = es |> List.exists (function CircularReference _ -> true | _ -> false)
                let hasMismatch = es |> List.exists (function TypeMismatch _ -> true | _ -> false)
                Expect.isTrue  hasCycle    "circular alias is flagged"
                Expect.isFalse hasMismatch "no spurious type mismatch from cycle"

        testCase "unresolved alias target does not produce a TypeMismatch" <| fun () ->
            let file = fileWith [
                tn "a", mkLeaf (TokenValue.Alias (CurlyBrace ["missing"])) DimensionType
            ]
            match Validation.validate file with
            | _ ->
                // No assertion on Ok/Error — main check is no spurious TypeMismatch
                let es =
                    match Validation.validate file with
                    | Ok () -> []
                    | Error xs -> xs
                let hasMismatch = es |> List.exists (function TypeMismatch _ -> true | _ -> false)
                Expect.isFalse hasMismatch "no spurious mismatch on unresolved alias"
    ]
