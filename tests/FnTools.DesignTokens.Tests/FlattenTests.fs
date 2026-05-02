module FnTools.DesignTokens.Tests.FlattenTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Tests.Fixtures


let allTests =
    testList "Flatten" [

        testCase "flatten yields all leaf paths" <| fun () ->
            match Format.parse V2025_10.aliasJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                let xs = FnTools.DesignTokens.Api.Primitives.flatten file |> List.ofSeq
                Expect.equal (List.length xs) 2 "two tokens"

        testCase "tryFind: existing path returns Some" <| fun () ->
            match Format.parse V2025_10.colorBrandJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                match FnTools.DesignTokens.Api.Primitives.tryFind ["color"; "brand"] file with
                | Some _ -> ()
                | None -> failtest "expected to find color.brand"

        testCase "tryFind: missing path returns None" <| fun () ->
            match Format.parse V2025_10.colorBrandJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                match FnTools.DesignTokens.Api.Primitives.tryFind ["color"; "missing"] file with
                | None -> ()
                | Some _ -> failtest "expected None"

        testCase "tryResolveAlias: follows curly-brace ref" <| fun () ->
            match Format.parse V2025_10.aliasJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                match FnTools.DesignTokens.Api.Primitives.tryResolveAlias (CurlyBrace ["color"; "primary"]) file with
                | Some t ->
                    match t.Value with
                    | TokenValue.Color _ -> ()  // followed alias to literal color
                    | other -> failtestf "expected Color, got %A" other
                | None -> failtest "expected to resolve alias"

        testCase "flattenResolved: no Alias values appear in output" <| fun () ->
            match Format.parse V2025_10.aliasJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok file ->
                match FnTools.DesignTokens.Api.Primitives.flattenResolved file with
                | Error es -> failtestf "flattenResolved failed: %A" es
                | Ok seq ->
                    let tokens = seq |> List.ofSeq
                    Expect.equal (List.length tokens) 2 "two resolved tokens"
                    // ResolvedTokenValue has no Alias case — structurally guaranteed
                    for (_, rt) in tokens do
                        match rt.Value with
                        | ResolvedColor _ -> ()
                        | other -> failtestf "expected ResolvedColor, got %A" other
    ]
