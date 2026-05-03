module FnTools.DesignTokens.Tests.VersionTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Tests.Fixtures


let allTests =
    testList "Versions" [

        testCase "First ED: type/value renamed; hex string upgraded" <| fun () ->
            match Format.parse FirstED.colorJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                Expect.equal file.Version FirstEditorsDraft "detected as First ED"
                let brand =
                    file.Children
                    |> List.find (fun (n, _) -> TokenName.value n = "color")
                    |> snd
                    |> function
                       | Group g ->
                           g.Children
                           |> List.find (fun (n, _) -> TokenName.value n = "brand")
                           |> snd
                       | _ -> failwith "expected group"
                match brand with
                | TokenLeaf t ->
                    match t.Value with
                    | TokenValue.Color c ->
                        Expect.equal c.ColorSpace SRGB "upgraded to sRGB"
                        Expect.equal c.Hex (Some "#ff0000") "hex preserved"
                    | other -> failtestf "expected Color, got %A" other
                | _ -> failtest "expected leaf"

        testCase "First ED: dimension string '8px' upgraded to object" <| fun () ->
            match Format.parse FirstED.dimensionJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                Expect.equal file.Version FirstEditorsDraft "detected as First ED"
                let small =
                    file.Children
                    |> List.find (fun (n, _) -> TokenName.value n = "spacing")
                    |> snd
                    |> function
                       | Group g ->
                           g.Children
                           |> List.find (fun (n, _) -> TokenName.value n = "small")
                           |> snd
                       | _ -> failwith "expected group"
                match small with
                | TokenLeaf t ->
                    match t.Value with
                    | TokenValue.Dimension d ->
                        Expect.equal d.Value 8.0 "value"
                        Expect.equal d.Unit Px "unit"
                    | other -> failtestf "expected Dimension, got %A" other
                | _ -> failtest "expected leaf"

        testCase "Second ED: hex string color upgraded" <| fun () ->
            match Format.parse SecondED.colorJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                Expect.equal file.Version SecondEditorsDraft "detected as Second ED"
                let brand =
                    file.Children
                    |> List.find (fun (n, _) -> TokenName.value n = "color")
                    |> snd
                    |> function
                       | Group g ->
                           g.Children
                           |> List.find (fun (n, _) -> TokenName.value n = "brand")
                           |> snd
                       | _ -> failwith "expected group"
                match brand with
                | TokenLeaf t ->
                    match t.Value with
                    | TokenValue.Color c ->
                        Expect.equal c.Hex (Some "#0000ff") "hex preserved"
                    | _ -> failtest "expected Color"
                | _ -> failtest "expected leaf"

        testCase "Second ED: 8-digit hex (#RRGGBBAA) upgraded with alpha channel" <| fun () ->
            match Format.parse SecondED.colorAlphaJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                Expect.equal file.Version SecondEditorsDraft "detected as Second ED"
                let brand =
                    file.Children
                    |> List.find (fun (n, _) -> TokenName.value n = "color")
                    |> snd
                    |> function
                       | Group g ->
                           g.Children
                           |> List.find (fun (n, _) -> TokenName.value n = "brand")
                           |> snd
                       | _ -> failwith "expected group"
                match brand with
                | TokenLeaf t ->
                    match t.Value with
                    | TokenValue.Color c ->
                        Expect.equal c.ColorSpace SRGB "upgraded to sRGB"
                        Expect.equal c.Hex (Some "#0000ff80") "hex preserved"
                        let expectedAlpha = 0x80 |> float |> (fun x -> x / 255.0)
                        Expect.floatClose Accuracy.high (Option.defaultValue -1.0 c.Alpha) expectedAlpha "alpha extracted from hex"
                    | _ -> failtest "expected Color"
                | _ -> failtest "expected leaf"

        testCase "Third ED: color object recognized" <| fun () ->
            match Format.parse ThirdED.colorJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                let brand =
                    file.Children
                    |> List.find (fun (n, _) -> TokenName.value n = "color")
                    |> snd
                    |> function
                       | Group g ->
                           g.Children
                           |> List.find (fun (n, _) -> TokenName.value n = "brand")
                           |> snd
                       | _ -> failwith "expected group"
                match brand with
                | TokenLeaf t ->
                    match t.Value with
                    | TokenValue.Color c ->
                        let (a, _, _) = c.Components
                        match a with
                        | Channel f -> Expect.floatClose Accuracy.high f 0.5 "first component"
                        | _ -> failtest "expected channel value"
                    | _ -> failtest "expected Color"
                | _ -> failtest "expected leaf"

        testCase "V2025_10: schema URL preserved" <| fun () ->
            match Format.parse V2025_10.colorBrandJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                Expect.isSome file.Schema "schema present"


        // ─── serializeAs / IAcceptDataLoss ────────────────────────────────────

        testCase "serializeAs SecondED: SRGB color written as hex string" <| fun () ->
            match Format.parse V2025_10.colorBrandJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                let json = Format.serializeAs SecondEditorsDraft IAcceptDataLoss file
                Expect.stringContains json "\"#" "hex string in output"
                Expect.isFalse (json.Contains "\"colorSpace\"") "no colorSpace object"

        testCase "serializeAs SecondED: $schema absent" <| fun () ->
            match Format.parse V2025_10.colorBrandJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                let json = Format.serializeAs SecondEditorsDraft IAcceptDataLoss file
                Expect.isFalse (json.Contains "$schema") "$schema omitted"

        testCase "serializeAs SecondED: alpha channel preserved in hex (#rrggbbaa)" <| fun () ->
            match Format.parse SecondED.colorAlphaJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                let json = Format.serializeAs SecondEditorsDraft IAcceptDataLoss file
                // #0000ff80 — 9-char hex with alpha
                Expect.stringContains json "#0000ff80" "alpha hex preserved"

        testCase "serializeAs SecondED: output round-trips through parse" <| fun () ->
            match Format.parse V2025_10.colorBrandJson with
            | Error es -> failtestf "parse failed (input): %A" es
            | Ok file ->
                let secondEdJson = Format.serializeAs SecondEditorsDraft IAcceptDataLoss file
                match Format.parse secondEdJson with
                | Error es -> failtestf "re-parse of Second ED output failed: %A" es
                | Ok reparsed ->
                    Expect.equal reparsed.Version SecondEditorsDraft "detected as Second ED"

        testCase "serializeAs V2025_10: identical to serialize" <| fun () ->
            match Format.parse V2025_10.colorBrandJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                let via_serialize   = Format.serialize file
                let via_serializeAs = Format.serializeAs V2025_10 IAcceptDataLoss file
                Expect.equal via_serializeAs via_serialize "V2025_10 output identical"
    ]
