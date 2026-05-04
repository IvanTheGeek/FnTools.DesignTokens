module FnTools.DesignTokens.Tests.ResolverTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Tests.Fixtures


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

        testCase "parseResolver: $ref in set sources parses" <| fun () ->
            match Resolver.parseResolver Resolver.refResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                Expect.equal (List.length doc.Sets) 1 "one set"
                let (_, core) = doc.Sets.[0]
                Expect.equal (List.length core.Sources) 1 "one source in core"
                match core.Sources.[0] with
                | Inline _ -> ()
                | other -> failtestf "expected Inline source after $ref resolution; got %A" other

        testCase "parseResolver: $ref chain ($ref → $ref) resolves transitively" <| fun () ->
            // coreSource $ref → coreInline, so the set should see one Inline source
            match Resolver.parseResolver Resolver.refResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                let (_, core) = doc.Sets.[0]
                match core.Sources.[0] with
                | Inline f ->
                    let hasSpacing =
                        f.Children |> List.exists (fun (n, _) -> TokenName.value n = "spacing")
                    Expect.isTrue hasSpacing "resolved inline contains spacing group"
                | other -> failtestf "expected Inline; got %A" other

        testCase "parseResolver: $ref in modifier context sources parses" <| fun () ->
            match Resolver.parseResolver Resolver.refResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                let (_, density) = doc.Modifiers.[0]
                let (_, normalCtx) = density.Contexts |> List.find (fun (n, _) -> n = "normal")
                Expect.equal (List.length normalCtx.Sources) 1 "one source in normal context"
                match normalCtx.Sources.[0] with
                | Inline _ -> ()
                | other -> failtestf "expected Inline source in modifier context; got %A" other

        testCase "parseResolver: $ref to unknown pointer fails with clear error" <| fun () ->
            let bad = """
{
  "version": "2025.10",
  "sets": { "s": { "sources": [{ "$ref": "#/$defs/missing" }] } },
  "modifiers": {},
  "resolutionOrder": [{ "set": "s" }]
}"""
            match Resolver.parseResolver bad with
            | Error es ->
                let msg = es |> List.map (sprintf "%A") |> String.concat "; "
                Expect.stringContains msg "missing" "error mentions the missing segment"
            | Ok _ -> failtest "expected parse error for unknown $ref"

        testCase "parseResolver: external $ref (non-same-document) fails" <| fun () ->
            let bad = """
{
  "version": "2025.10",
  "sets": { "s": { "sources": [{ "$ref": "other-file.json#/foo" }] } },
  "modifiers": {},
  "resolutionOrder": [{ "set": "s" }]
}"""
            match Resolver.parseResolver bad with
            | Error es ->
                let msg = es |> List.map (sprintf "%A") |> String.concat "; "
                Expect.stringContains msg "same-document" "error explains same-document restriction"
            | Ok _ -> failtest "expected parse error for external $ref"
    ]
