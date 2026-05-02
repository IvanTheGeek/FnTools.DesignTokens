module FnTools.DesignTokens.Tests.SimpleApiTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Tests.Fixtures


let private noLoad (_: string) : Result<string, string> = Error "no file loading in tests"


let allTests =
    testList "SimpleApi" [

        testCase "import: returns ResolvedToken seq for valid file" <| fun () ->
            match FnTools.DesignTokens.Api.import V2025_10.colorBrandJson with
            | Error es -> failtestf "import failed: %A" es
            | Ok seq ->
                let xs = seq |> List.ofSeq
                Expect.equal (List.length xs) 1 "one resolved token"
                let path, rt = xs.[0]
                Expect.equal path ["color"; "brand"] "path"
                match rt.Value with
                | ResolvedColor _ -> ()
                | _ -> failtest "expected ResolvedColor"

        testCase "import: invalid JSON yields ParseFailed" <| fun () ->
            match FnTools.DesignTokens.Api.import "not json" with
            | Error errs ->
                let isParseFailed =
                    errs |> List.exists (function ParseFailed _ -> true | _ -> false)
                Expect.isTrue isParseFailed "expected ParseFailed"
            | Ok _ -> failtest "expected error"

        testCase "import: alias resolved to terminal literal" <| fun () ->
            match FnTools.DesignTokens.Api.import V2025_10.aliasJson with
            | Error es -> failtestf "import failed: %A" es
            | Ok seq ->
                let xs = seq |> List.ofSeq
                Expect.equal (List.length xs) 2 "two tokens"
                for (_, rt) in xs do
                    match rt.Value with
                    | ResolvedColor _ -> ()
                    | _ -> failtest "expected ResolvedColor"

        testCase "export: round-trips through resolved tokens" <| fun () ->
            match FnTools.DesignTokens.Api.import V2025_10.colorBrandJson with
            | Error es -> failtestf "import failed: %A" es
            | Ok seq ->
                let json = FnTools.DesignTokens.Api.export seq
                match Format.parse json with
                | Error es -> failtestf "re-parse failed: %A" es
                | Ok _ -> ()

        testCase "importWithResolver: applies default context" <| fun () ->
            match FnTools.DesignTokens.Api.importWithResolver noLoad Map.empty Resolver.basicResolverJson with
            | Error es -> failtestf "importWithResolver failed: %A" es
            | Ok seq ->
                let xs = seq |> List.ofSeq
                Expect.isGreaterThan (List.length xs) 0 "at least one token"
    ]
