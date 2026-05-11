module FnTools.DesignTokens.Tests.ExtensionEvaluationTests

open System.Text.Json.Nodes
open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Tests.Fixtures


// ─── Helpers ─────────────────────────────────────────────────────────────────

let private emptyMd : Metadata =
    { Description = None; Deprecated = None; Extensions = [] }

let private mdWithMath (expr: string) : Metadata =
    let vendor = JsonObject()
    vendor.Add("tsMathExpression", JsonValue.Create<string>(expr))
    { Description = None
      Deprecated  = None
      Extensions  = [ "com.fntools.designtokens", vendor :> JsonNode ] }

let private numTok (n: float) (md: Metadata) : ResolvedToken =
    { Value = ResolvedNumber n; Type = NumberType; Metadata = md }

let private dimTok (v: float) (u: DimensionUnit) (md: Metadata) : ResolvedToken =
    { Value = ResolvedDimension { Value = v; Unit = u }
      Type  = DimensionType
      Metadata = md }


// ─── Tests ──────────────────────────────────────────────────────────────────

let allTests =
    testList "Extension evaluation (ADR-034)" [

        testCase "token without math expression passes through unchanged" <| fun () ->
            let input = [ ["x"], numTok 42.0 emptyMd ]
            let r = Api.evaluateMathExtensions input
            Expect.equal r.Warnings    [] "no warnings"
            Expect.equal r.Tokens.Length 1 "one token"
            match (snd r.Tokens.[0]).Value with
            | ResolvedNumber 42.0 -> ()
            | other -> failtestf "expected ResolvedNumber 42, got %A" other

        testCase "single literal expression evaluates and replaces value" <| fun () ->
            let input = [ ["scale"; "x3"], numTok 16.0 (mdWithMath "round(16 * pow(1.25, 3))") ]
            let r = Api.evaluateMathExtensions input
            Expect.equal r.Warnings [] "no warnings"
            match (snd r.Tokens.[0]).Value with
            | ResolvedNumber n -> Expect.equal n 31.0 "16 * 1.25^3 rounded = 31"
            | other -> failtestf "expected ResolvedNumber, got %A" other

        testCase "{variable} resolves against the resolved context" <| fun () ->
            // base=16, multiplier=1.25; scale.x3 = round({base} * pow({multiplier}, 3))
            let input = [
                ["base"],         numTok 16.0   emptyMd
                ["multiplier"],   numTok 1.25   emptyMd
                ["scale"; "x3"],  numTok 16.0   (mdWithMath "round({base} * pow({multiplier}, 3))")
            ]
            let r = Api.evaluateMathExtensions input
            Expect.equal r.Warnings [] "no warnings"
            let scaleX3 = r.Tokens |> List.find (fun (p, _) -> p = ["scale"; "x3"])
            match (snd scaleX3).Value with
            | ResolvedNumber n -> Expect.equal n 31.0 "evaluated against context"
            | other            -> failtestf "expected ResolvedNumber 31, got %A" other

        testCase "{variable} resolves a Dimension token's numeric value" <| fun () ->
            // base is a Dimension token; the expression still resolves its scalar.
            let input = [
                ["base"],        dimTok 16.0 Px emptyMd
                ["multiplier"],  numTok 1.25    emptyMd
                ["scale"; "x2"], dimTok 16.0 Px (mdWithMath "round({base} * pow({multiplier}, 2))")
            ]
            let r = Api.evaluateMathExtensions input
            Expect.equal r.Warnings [] "no warnings"
            let scaleX2 = r.Tokens |> List.find (fun (p, _) -> p = ["scale"; "x2"])
            match (snd scaleX2).Value with
            | ResolvedDimension d ->
                Expect.equal d.Value 25.0 "16 * 1.25^2 = 25"
                Expect.equal d.Unit  Px   "unit preserved across evaluation"
            | other -> failtestf "expected ResolvedDimension, got %A" other

        testCase "missing variable emits MathExpressionFailed, keeps stale $value" <| fun () ->
            let input = [
                ["scale"; "x3"], numTok 99.0 (mdWithMath "round({missing} * 2)")
            ]
            let r = Api.evaluateMathExtensions input
            Expect.equal r.Warnings.Length 1 "one warning"
            match r.Warnings.[0] with
            | MathExpressionFailed (path, expr, _) ->
                Expect.equal path "scale.x3"                  "path"
                Expect.equal expr "round({missing} * 2)"      "expression preserved"
            // stale value retained
            match (snd r.Tokens.[0]).Value with
            | ResolvedNumber n -> Expect.equal n 99.0 "stale value kept"
            | other -> failtestf "expected ResolvedNumber 99, got %A" other

        testCase "parse error emits MathExpressionFailed, keeps stale $value" <| fun () ->
            let input = [
                ["x"], numTok 7.0 (mdWithMath "this is not a valid expression $$$")
            ]
            let r = Api.evaluateMathExtensions input
            Expect.equal r.Warnings.Length 1 "one warning"
            match (snd r.Tokens.[0]).Value with
            | ResolvedNumber n -> Expect.equal n 7.0 "stale value kept"
            | other            -> failtestf "expected ResolvedNumber 7, got %A" other

        testCase "multiple failures collected, not short-circuited" <| fun () ->
            let input = [
                ["a"], numTok 1.0 (mdWithMath "{ghost1} + 1")
                ["b"], numTok 2.0 (mdWithMath "{ghost2} * 2")
                ["c"], numTok 3.0 (mdWithMath "pow({ghost3}, 2)")
            ]
            let r = Api.evaluateMathExtensions input
            Expect.equal r.Warnings.Length 3 "all three failures collected"
            let paths =
                r.Warnings |> List.map (function MathExpressionFailed (p, _, _) -> p)
            Expect.contains paths "a" "a flagged"
            Expect.contains paths "b" "b flagged"
            Expect.contains paths "c" "c flagged"

        testCase "Dimension preserves unit when value is updated" <| fun () ->
            let input = [
                ["d"], dimTok 16.0 Rem (mdWithMath "round(16 * pow(1.5, 2))")
            ]
            let r = Api.evaluateMathExtensions input
            match (snd r.Tokens.[0]).Value with
            | ResolvedDimension d ->
                Expect.equal d.Value 36.0 "value updated"
                Expect.equal d.Unit  Rem  "unit preserved"
            | other -> failtestf "expected ResolvedDimension, got %A" other

        testCase "non-numeric token with extension passes through unchanged" <| fun () ->
            // Color token carrying a (semantically nonsensical) math extension —
            // we don't apply math to non-numerics. No warning either — the
            // extension simply doesn't apply here.
            let color : ResolvedToken =
                { Value = ResolvedColor { ColorSpace = SRGB
                                          Components = (Channel 1.0, Channel 0.0, Channel 0.0)
                                          Alpha = None; Hex = None }
                  Type     = ColorType
                  Metadata = mdWithMath "round(1 + 2)" }
            let r = Api.evaluateMathExtensions [ ["c"], color ]
            Expect.equal r.Warnings.Length 0 "no warnings for non-numeric carrier"
            match (snd r.Tokens.[0]).Value with
            | ResolvedColor _ -> ()  // color unchanged
            | other -> failtestf "expected ResolvedColor, got %A" other

        testCase "tokens without the extension are interleaved correctly" <| fun () ->
            let input = [
                ["a"],            numTok 1.0  emptyMd
                ["scale"; "x1"],  numTok 0.0  (mdWithMath "{a} * 10")
                ["b"],            numTok 5.0  emptyMd
                ["scale"; "x2"],  numTok 0.0  (mdWithMath "{a} + {b}")
            ]
            let r = Api.evaluateMathExtensions input
            Expect.equal r.Warnings [] "no warnings"
            let getNum p =
                r.Tokens
                |> List.find (fun (path, _) -> path = p)
                |> snd
                |> fun t -> match t.Value with ResolvedNumber n -> n | _ -> failwith "expected number"
            Expect.equal (getNum ["a"])             1.0  "a unchanged"
            Expect.equal (getNum ["scale"; "x1"])  10.0  "x1 = a*10 = 10"
            Expect.equal (getNum ["b"])             5.0  "b unchanged"
            Expect.equal (getNum ["scale"; "x2"])   6.0  "x2 = a+b = 6"

        testCase "formatter produces readable line" <| fun () ->
            let w = MathExpressionFailed ("scale.x3", "{missing} + 1", "missing variable")
            let s = ExtensionEvaluationWarning.format w
            Expect.stringContains s "scale.x3"                 "path mentioned"
            Expect.stringContains s "{missing} + 1"            "expression mentioned"
            Expect.stringContains s "missing variable"         "reason mentioned"
            Expect.stringContains s "kept stale"               "fallback behavior mentioned"

        testCase "Primitives.evaluateMathExtensions matches Api.evaluateMathExtensions" <| fun () ->
            let input = [ ["x"], numTok 0.0 (mdWithMath "2 + 3") ]
            let viaApi        = Api.evaluateMathExtensions          input
            let viaPrimitives = Api.Primitives.evaluateMathExtensions input
            Expect.equal viaPrimitives.Warnings viaApi.Warnings "warnings match"
            Expect.equal viaPrimitives.Tokens.Length viaApi.Tokens.Length "token counts match"
    ]
