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

        // ─── serializeResolver ────────────────────────────────────────────────

        testCase "serializeResolver: round-trip preserves sets, modifiers, resolution order" <| fun () ->
            match Resolver.parseResolver Resolver.basicResolverJson with
            | Error es -> failtestf "initial parse failed: %A" es
            | Ok doc ->
                let json2 = Resolver.serializeResolver doc
                match Resolver.parseResolver json2 with
                | Error es -> failtestf "re-parse failed: %A" es
                | Ok doc2 ->
                    Expect.equal doc2.Name               doc.Name               "name preserved"
                    Expect.equal doc2.Version            doc.Version            "version preserved"
                    Expect.equal (List.length doc2.Sets) (List.length doc.Sets) "set count preserved"
                    let setNames1 = doc.Sets  |> List.map fst |> List.sort
                    let setNames2 = doc2.Sets |> List.map fst |> List.sort
                    Expect.equal setNames2 setNames1 "set names preserved"
                    Expect.equal (List.length doc2.Modifiers) (List.length doc.Modifiers) "modifier count preserved"
                    let modNames1 = doc.Modifiers  |> List.map fst |> List.sort
                    let modNames2 = doc2.Modifiers |> List.map fst |> List.sort
                    Expect.equal modNames2 modNames1 "modifier names preserved"
                    let (_, theme2) = doc2.Modifiers |> List.find (fun (n, _) -> n = "theme")
                    Expect.equal theme2.Default (Some "light") "modifier default preserved"
                    let ctxNames = theme2.Contexts |> List.map fst |> List.sort
                    Expect.equal ctxNames ["dark"; "light"] "modifier contexts preserved"
                    Expect.equal (List.length doc2.ResolutionOrder) 2 "resolution order length preserved"

        testCase "serializeResolver: Inline sources embedded without $ref" <| fun () ->
            match Resolver.parseResolver Resolver.basicResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                let json = Resolver.serializeResolver doc
                Expect.stringContains json "\"inline\"" "inline key present"
                Expect.isFalse (json.Contains "\"$ref\"") "$ref not emitted"

        testCase "serializeResolver: FileRef source round-trips as path object" <| fun () ->
            let json = """
{
  "version": "2025.10",
  "sets": {
    "base": { "sources": [{ "path": "tokens/base.json" }] }
  },
  "resolutionOrder": [{ "set": "base" }]
}"""
            match Resolver.parseResolver json with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                let serialized = Resolver.serializeResolver doc
                match Resolver.parseResolver serialized with
                | Error es -> failtestf "re-parse failed: %A" es
                | Ok doc2 ->
                    let (_, sd) = doc2.Sets.[0]
                    match sd.Sources.[0] with
                    | FileRef p -> Expect.equal p "tokens/base.json" "path preserved"
                    | Inline _  -> failtest "expected FileRef"

        testCase "serializeResolver: optional name/description omitted when None" <| fun () ->
            let json = """
{
  "version": "2025.10",
  "sets": { "s": { "sources": [{ "inline": {} }] } },
  "resolutionOrder": [{ "set": "s" }]
}"""
            match Resolver.parseResolver json with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                Expect.isNone doc.Name "no name"
                let serialized = Resolver.serializeResolver doc
                Expect.isFalse (serialized.Contains "\"name\"")        "name key absent"
                Expect.isFalse (serialized.Contains "\"description\"") "description key absent"

        testCase "serializeResolver: set and modifier extensions round-trip" <| fun () ->
            // Build a document with extensions directly and verify they survive
            // serialize → parse. Extensions are (string * JsonNode) list.
            match Resolver.parseResolver Resolver.basicResolverJson with
            | Error es -> failtestf "parse failed: %A" es
            | Ok doc ->
                // Inject an extension onto the "core" set
                let (coreName, coreDef) = doc.Sets.[0]
                let extNode : System.Text.Json.Nodes.JsonNode =
                    System.Text.Json.Nodes.JsonValue.Create "test"
                    |> Option.ofObj
                    |> Option.defaultWith (fun () -> failwith "JsonValue.Create returned null")
                    :> System.Text.Json.Nodes.JsonNode
                let coreWithExt = { coreDef with Extensions = ["x-tag", extNode] }
                let docWithExt  = { doc with Sets = [(coreName, coreWithExt)] }
                let serialized  = Resolver.serializeResolver docWithExt
                match Resolver.parseResolver serialized with
                | Error es -> failtestf "re-parse failed: %A" es
                | Ok doc2  ->
                    let (_, sd) = doc2.Sets.[0]
                    let extVal = sd.Extensions |> List.tryFind (fun (k, _) -> k = "x-tag")
                    Expect.isSome extVal "extension key present after round-trip"
                    match extVal with
                    | Some (_, v) -> Expect.equal (v.ToString()) "test" "extension value preserved"
                    | None -> ()
    ]
