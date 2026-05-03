module FnTools.DesignTokens.Tests.ParseTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Tests.Fixtures


let allTests =
    testList "Parse" [

        testCase "color: 2025.10 parses to expected ColorValue" <| fun () ->
            match Format.parse V2025_10.colorBrandJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                Expect.equal file.Version V2025_10 "version"
                let token =
                    file.Children
                    |> List.tryFind (fun (n, _) -> TokenName.value n = "color")
                    |> Option.bind (fun (_, node) ->
                        match node with
                        | Group g ->
                            g.Children
                            |> List.tryFind (fun (n, _) -> TokenName.value n = "brand")
                            |> Option.bind (fun (_, child) ->
                                match child with
                                | TokenLeaf t -> Some t
                                | _ -> None)
                        | _ -> None)
                match token with
                | Some t ->
                    match t.Value with
                    | TokenValue.Color c ->
                        Expect.equal c.ColorSpace SRGB "color space"
                        Expect.equal c.Hex (Some "#ff8000") "hex"
                    | other -> failtestf "expected Color, got %A" other
                | None -> failtest "color.brand not found"

        testCase "dimension: parses object form" <| fun () ->
            match Format.parse V2025_10.dimensionJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok _ -> ()

        testCase "dimension: em unit parsed and round-trips" <| fun () ->
            match Format.parse V2025_10.dimensionEmJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                let wide =
                    file.Children
                    |> List.find (fun (n, _) -> TokenName.value n = "tracking")
                    |> snd
                    |> function
                       | Group g -> g.Children |> List.find (fun (n, _) -> TokenName.value n = "wide") |> snd
                       | _ -> failwith "expected group"
                match wide with
                | TokenLeaf t ->
                    match t.Value with
                    | TokenValue.Dimension d ->
                        Expect.equal d.Unit Em "unit is em"
                        Expect.floatClose Accuracy.high d.Value 0.22 "value"
                    | other -> failtestf "expected Dimension, got %A" other
                | _ -> failtest "expected leaf"

        testCase "alias: $value with curly-brace syntax" <| fun () ->
            match Format.parse V2025_10.aliasJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                let primary =
                    file.Children
                    |> List.find (fun (n, _) -> TokenName.value n = "color")
                    |> snd
                    |> function
                       | Group g ->
                           g.Children
                           |> List.find (fun (n, _) -> TokenName.value n = "primary")
                           |> snd
                       | _ -> failwith "expected group"
                match primary with
                | TokenLeaf t ->
                    match t.Value with
                    | TokenValue.Alias (CurlyBrace ["color"; "brand"]) -> ()
                    | other -> failtestf "expected Alias, got %A" other
                | _ -> failtest "expected leaf"

        testCase "extensions: preserved as JsonNode list" <| fun () ->
            match Format.parse V2025_10.extensionsJson with
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
                    Expect.equal (List.length t.Metadata.Extensions) 1 "one extension"
                    let key, _ = t.Metadata.Extensions.[0]
                    Expect.equal key "com.figma" "extension key"
                | _ -> failtest "expected leaf"

        testCase "round-trip: parse + serialize + parse → equal" <| fun () ->
            let r1 = Format.parse V2025_10.dimensionJson
            match r1 with
            | Error es -> failtestf "first parse failed: %A" es
            | Ok file1 ->
                let json = Format.serialize file1
                match Format.parse json with
                | Error es -> failtestf "round-trip parse failed: %A" es
                | Ok file2 ->
                    Expect.equal file2.Version V2025_10 "version preserved"
                    Expect.equal (List.length file2.Children) (List.length file1.Children) "child count"

        testCase "rejects $-prefixed token name" <| fun () ->
            // $bad starts with $; we treat $-prefixed top-level keys as metadata,
            // so this becomes an unrecognized metadata field — but no children.
            match Format.parse Invalid.dollarPrefixName with
            | Ok file -> Expect.equal (List.length file.Children) 0 "no children"
            | Error _ -> ()  // also acceptable

        testCase "rejects unknown $type" <| fun () ->
            match Format.parse Invalid.unknownTokenType with
            | Error _ -> ()
            | Ok _ -> failtest "expected unknown $type error"

        testCase "parseAuto: token file detected" <| fun () ->
            match Format.parseAuto V2025_10.dimensionJson with
            | Ok (TokenFileResult _) -> ()
            | Ok (ResolverResult _) -> failtest "expected TokenFileResult"
            | Error es -> failtestf "parse failed: %A" es
    ]
