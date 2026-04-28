module NEXUS.DesignTokens.Tests.VersionTests

open Expecto
open NEXUS.DesignTokens
open NEXUS.DesignTokens.Tests.Fixtures


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
    ]
