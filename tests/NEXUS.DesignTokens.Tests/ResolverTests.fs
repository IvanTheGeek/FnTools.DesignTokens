module NEXUS.DesignTokens.Tests.ResolverTests

open Expecto
open NEXUS.DesignTokens
open NEXUS.DesignTokens.Tests.Fixtures


let private noLoad (_: string) : Result<string, string> = Error "no file loading in tests"


let allTests =
    testList "Resolver" [

        testCase "parseResolver: basic structure parses" <| fun () ->
            match Resolver.parseResolver Resolver.basicResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                Expect.equal (List.length doc.Sets) 1 "one set"
                Expect.equal (List.length doc.Modifiers) 1 "one modifier"
                Expect.equal (List.length doc.ResolutionOrder) 2 "resolution items"

        testCase "resolve: applies default context for theme" <| fun () ->
            match Resolver.parseResolver Resolver.basicResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                match Resolver.resolve noLoad Map.empty doc with
                | Error es -> failtestf "resolve failed: %A" es
                | Ok merged ->
                    let bg =
                        merged.Children
                        |> List.find (fun (n, _) -> TokenName.value n = "color")
                        |> snd
                        |> function
                           | Group g ->
                               g.Children
                               |> List.tryFind (fun (n, _) -> TokenName.value n = "background")
                           | _ -> None
                    Expect.isSome bg "background present after merge"

        testCase "resolve: dark context overrides light" <| fun () ->
            match Resolver.parseResolver Resolver.basicResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                let ctx = Map [ "theme", "dark" ]
                match Resolver.resolve noLoad ctx doc with
                | Error es -> failtestf "resolve failed: %A" es
                | Ok merged ->
                    let bg =
                        merged.Children
                        |> List.find (fun (n, _) -> TokenName.value n = "color")
                        |> snd
                        |> function
                           | Group g ->
                               g.Children
                               |> List.find (fun (n, _) -> TokenName.value n = "background")
                               |> snd
                           | _ -> failwith "expected group"
                    match bg with
                    | TokenLeaf t ->
                        match t.Value with
                        | TokenValue.Color c ->
                            let (r, _, _) = c.Components
                            match r with
                            | Channel 0.0 -> ()
                            | other -> failtestf "expected dark (0,0,0), got %A" other
                        | _ -> failtest "expected Color"
                    | _ -> failtest "expected leaf"

        testCase "resolve: unknown modifier in input fails" <| fun () ->
            match Resolver.parseResolver Resolver.basicResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                let ctx = Map [ "nonexistent", "x" ]
                match Resolver.resolve noLoad ctx doc with
                | Error _ -> ()
                | Ok _ -> failtest "expected unknown modifier error"

        testCase "resolve: unknown context for known modifier fails" <| fun () ->
            match Resolver.parseResolver Resolver.basicResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                let ctx = Map [ "theme", "midnight" ]
                match Resolver.resolve noLoad ctx doc with
                | Error _ -> ()
                | Ok _ -> failtest "expected unknown context error"
    ]
