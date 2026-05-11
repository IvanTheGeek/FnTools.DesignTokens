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


// ─── Tests for deprecated post-flatten evaluateMathExtensions (0.8.0 API) ──
//
// The block below intentionally calls the deprecated function. F# 10 scoped
// warning suppression (RFC FS-1146) lets us narrow the FS0044 suppression
// to exactly these tests, with #warnon restoring normal behavior immediately
// after — strictly preferable to a file-wide #nowarn. The tests exist for
// regression coverage of the deprecated function until v1.0.0 removal.
#nowarn 44

let deprecatedFunctionTests =
    testList "evaluateMathExtensions (deprecated 0.9.0 — post-flatten, no alias propagation)" [

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

        testCase "deprecated function regression: dimension→number alias does NOT propagate (this is the documented limitation)" <| fun () ->
            // The whole reason the function was deprecated. After flatten,
            // spacing.x1 (which aliased scale.x1) is a baked ResolvedNumber 16.
            // Updating scale.x1 to 20 does not touch spacing.x1.
            let input = [
                ["base"],         numTok 16.0 emptyMd
                ["multiplier"],   numTok 1.25 emptyMd
                ["scale"; "x1"],  numTok 16.0 (mdWithMath "round({base} * pow({multiplier}, 1))")
                ["spacing"; "x1"],
                  // simulates a post-flatten alias to scale.x1
                  { Value = ResolvedNumber 16.0; Type = DimensionType; Metadata = emptyMd }
            ]
            let r = Api.evaluateMathExtensions input
            let getNum p =
                r.Tokens
                |> List.find (fun (path, _) -> path = p)
                |> snd
                |> fun t -> match t.Value with ResolvedNumber n -> n | _ -> failwith "expected number"
            Expect.equal (getNum ["scale"; "x1"])    20.0 "scale.x1 evaluated"
            Expect.equal (getNum ["spacing"; "x1"])  16.0 "spacing.x1 stays stale — alias info gone post-flatten"
    ]

// Re-enable FS0044 so any accidental call to a deprecated function below
// (or in code added later in this file) is caught.
#warnon 44


// ─── Tests for evaluateMathExtensionsInFile (canonical 0.9.0 API) ───────────

/// Parse a JSON fixture to a TokenFile, fail the test on parse error.
let private parseFile (json: string) : TokenFile =
    match Format.parse json with
    | Error es -> failtestf "fixture parse failed: %A" es
    | Ok f -> f

/// Get the resolved numeric value of a token at a path, fail if missing or non-numeric.
let private getResolvedNum (tokens: (string list * ResolvedToken) list) (path: string list) : float =
    match tokens |> List.tryFind (fun (p, _) -> p = path) with
    | None -> failtestf "token at %A not found" path
    | Some (_, token) ->
        match token.Value with
        | ResolvedNumber n -> n
        | ResolvedDimension d -> d.Value
        | ResolvedDuration d -> d.Value
        | other -> failtestf "expected numeric at %A, got %A" path other

let inFileTests =
    testList "evaluateMathExtensionsInFile (canonical — alias propagation works)" [

        testCase "token without math expression passes through unchanged" <| fun () ->
            let json = """
                { "x": { "$type": "number", "$value": 42 } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings [] "no warnings"
            // verify the file's value is unchanged
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                let tokensList = List.ofSeq tokens
                Expect.equal (getResolvedNum tokensList ["x"]) 42.0 "value unchanged"

        testCase "single formula token: stale $value replaced by evaluated result" <| fun () ->
            let json = """
                { "scale": { "x3": {
                    "$type": "number",
                    "$value": 16,
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "round(16 * pow(1.25, 3))" } } } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings [] "no warnings"
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                Expect.equal (getResolvedNum (List.ofSeq tokens) ["scale"; "x3"]) 31.0
                    "16 * 1.25^3 rounded = 31"

        testCase "{variable} references resolve through the alias-aware index" <| fun () ->
            let json = """
                { "base":       { "$type": "number", "$value": 16 },
                  "multiplier": { "$type": "number", "$value": 1.25 },
                  "scale": { "x3": {
                    "$type": "number",
                    "$value": 16,
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "round({base} * pow({multiplier}, 3))" } } } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings [] "no warnings"
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                Expect.equal (getResolvedNum (List.ofSeq tokens) ["scale"; "x3"]) 31.0
                    "evaluated against context"

        testCase "PROPAGATION: scale.x1 update flows to spacing.x1 (alias to scale.x1)" <| fun () ->
            // The user's bug case from request_2026-05-10_03.
            // Before this fix, spacing.x1 stayed at 16 even after scale.x1 → 20.
            let json = """
                { "base":       { "$type": "number", "$value": 16 },
                  "multiplier": { "$type": "number", "$value": 1.25 },
                  "scale": { "x1": {
                    "$type": "number",
                    "$value": 16,
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "round({base} * pow({multiplier}, 1))" } } } },
                  "spacing": { "x1": {
                    "$type": "dimension",
                    "$value": "{scale.x1}" } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings [] "no warnings"
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                let t = List.ofSeq tokens
                Expect.equal (getResolvedNum t ["scale"; "x1"])    20.0 "scale.x1 evaluated"
                Expect.equal (getResolvedNum t ["spacing"; "x1"])  20.0 "spacing.x1 PROPAGATED via alias"

        testCase "PROPAGATION: multi-hop alias chain A → B → C all pick up updated value" <| fun () ->
            let json = """
                { "base":       { "$type": "number", "$value": 16 },
                  "multiplier": { "$type": "number", "$value": 2 },
                  "scale": { "x2": {
                    "$type": "number",
                    "$value": 16,
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "{base} * pow({multiplier}, 2)" } } } },
                  "spacing": { "x2": { "$type": "dimension", "$value": "{scale.x2}" } },
                  "radius":  { "lg":  { "$type": "dimension", "$value": "{spacing.x2}" } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings [] "no warnings"
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                let t = List.ofSeq tokens
                Expect.equal (getResolvedNum t ["scale"; "x2"])    64.0 "16 * 2^2 = 64"
                Expect.equal (getResolvedNum t ["spacing"; "x2"])  64.0 "spacing → scale.x2 propagated"
                Expect.equal (getResolvedNum t ["radius";  "lg"])  64.0 "radius → spacing → scale.x2 propagated"

        testCase "PROPAGATION: formula referencing another formula via MathEval recursion" <| fun () ->
            // size.x3 = scale.x1 * 4; scale.x1 has its own formula. Both should evaluate correctly.
            let json = """
                { "base":       { "$type": "number", "$value": 4 },
                  "multiplier": { "$type": "number", "$value": 2 },
                  "scale": { "x1": {
                    "$type": "number",
                    "$value": 0,
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "{base} * {multiplier}" } } } },
                  "size":  { "x3": {
                    "$type": "number",
                    "$value": 0,
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "{scale.x1} * 4" } } } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings [] "no warnings"
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                let t = List.ofSeq tokens
                Expect.equal (getResolvedNum t ["scale"; "x1"])   8.0 "4 * 2"
                Expect.equal (getResolvedNum t ["size";  "x3"]) 32.0 "scale.x1 * 4 = 8 * 4 = 32"

        testCase "Dimension token: scalar updated, unit preserved" <| fun () ->
            let json = """
                { "d": {
                    "$type": "dimension",
                    "$value": { "value": 16, "unit": "rem" },
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "round(16 * pow(1.5, 2))" } } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings [] "no warnings"
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                let (_, token) = List.ofSeq tokens |> List.find (fun (p, _) -> p = ["d"])
                match token.Value with
                | ResolvedDimension d ->
                    Expect.equal d.Value 36.0 "value updated to round(16 * 1.5^2) = 36"
                    Expect.equal d.Unit  Rem  "unit preserved"
                | other -> failtestf "expected ResolvedDimension, got %A" other

        testCase "missing variable emits warning, leaves stale $value" <| fun () ->
            let json = """
                { "x": {
                    "$type": "number",
                    "$value": 99,
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "{ghost} + 1" } } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings.Length 1 "one warning"
            match r.Warnings.[0] with
            | MathExpressionFailed (path, expr, _) ->
                Expect.equal path "x" "path"
                Expect.equal expr "{ghost} + 1" "expression"
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                Expect.equal (getResolvedNum (List.ofSeq tokens) ["x"]) 99.0 "stale value kept"

        testCase "multiple evaluation failures collected, not short-circuited" <| fun () ->
            let json = """
                { "a": { "$type": "number", "$value": 1,
                    "$extensions": { "com.fntools.designtokens": { "tsMathExpression": "{ghost1} + 1" } } },
                  "b": { "$type": "number", "$value": 2,
                    "$extensions": { "com.fntools.designtokens": { "tsMathExpression": "{ghost2} * 2" } } },
                  "c": { "$type": "number", "$value": 3,
                    "$extensions": { "com.fntools.designtokens": { "tsMathExpression": "pow({ghost3}, 2)" } } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings.Length 3 "all three failures collected"

        testCase "non-numeric carrier with extension passes through unchanged, no warning" <| fun () ->
            // Color token with a (semantically nonsensical) math extension.
            // Math doesn't apply to non-numerics; not a failure either.
            let json = """
                { "c": {
                    "$type": "color",
                    "$value": { "colorSpace": "srgb", "components": [1, 0, 0] },
                    "$extensions": { "com.fntools.designtokens": {
                        "tsMathExpression": "1 + 2" } } } }
                """
            let file = parseFile json
            let r = Api.evaluateMathExtensionsInFile file
            Expect.equal r.Warnings.Length 0 "no warnings for non-numeric carrier"
            match Api.Primitives.flattenResolved r.File with
            | Error es -> failtestf "flatten failed: %A" es
            | Ok tokens ->
                let (_, token) = List.ofSeq tokens |> List.find (fun (p, _) -> p = ["c"])
                match token.Value with
                | ResolvedColor _ -> ()
                | other -> failtestf "expected ResolvedColor unchanged, got %A" other

        testCase "Primitives.evaluateMathExtensionsInFile matches Api.evaluateMathExtensionsInFile" <| fun () ->
            let json = """
                { "x": { "$type": "number", "$value": 0,
                    "$extensions": { "com.fntools.designtokens": { "tsMathExpression": "2 + 3" } } } }
                """
            let file = parseFile json
            let viaApi        = Api.evaluateMathExtensionsInFile          file
            let viaPrimitives = Api.Primitives.evaluateMathExtensionsInFile file
            Expect.equal viaPrimitives.Warnings viaApi.Warnings "warnings match"
    ]


let allTests =
    testList "Extension evaluation (ADR-034 + 2026-05-10 addendum)" [
        deprecatedFunctionTests
        inFileTests
    ]
