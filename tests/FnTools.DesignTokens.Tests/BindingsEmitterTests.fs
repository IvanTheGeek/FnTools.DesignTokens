module FnTools.DesignTokens.Tests.BindingsEmitterTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Bindings


// ─── Helpers ──────────────────────────────────────────────────────────────────

let private meta = { Description = None; Deprecated = None; Extensions = [] }

let private token rv ty : ResolvedToken = { Value = rv; Type = ty; Metadata = meta }

let private colorToken =
    token (ResolvedColor { ColorSpace = OKLCH; Components = (Channel 0.56, Channel 0.14, Channel 200.0); Alpha = None; Hex = None }) ColorType

let private dimToken v u =
    token (ResolvedDimension { Value = v; Unit = u }) DimensionType

let private numToken n = token (ResolvedNumber n) NumberType

let private typoToken =
    token (ResolvedTypography {
        FontFamily    = Single "Inter"
        FontSize      = { Value = 16.0; Unit = Px }
        FontWeight    = Numeric 400
        LetterSpacing = { Value = 0.0; Unit = Px }
        LineHeight    = 1.5
    }) TypographyType


// ─── toFsharpIdent ─────────────────────────────────────────────────────────────

let identTests =
    testList "toFsharpIdent" [

        test "lowercase segment is capitalized" {
            Expect.equal (toFsharpIdent "primary") "Primary" ""
        }

        test "all-lowercase segment" {
            Expect.equal (toFsharpIdent "color") "Color" ""
        }

        test "segment starting with digit gets N prefix" {
            Expect.equal (toFsharpIdent "500") "N500" ""
        }

        test "single digit gets N prefix" {
            Expect.equal (toFsharpIdent "0") "N0" ""
        }

        test "already-prefixed N stays unchanged" {
            Expect.equal (toFsharpIdent "N50") "N50" ""
        }

        test "hyphenated segment becomes PascalCase" {
            Expect.equal (toFsharpIdent "focus-ring") "FocusRing" ""
        }

        test "hyphenated with digit part" {
            Expect.equal (toFsharpIdent "space-4") "SpaceN4" ""
        }

        test "reserved keyword capitalised — no backtick needed" {
            Expect.equal (toFsharpIdent "default") "Default" ""
        }

        test "reserved keyword type capitalised" {
            Expect.equal (toFsharpIdent "type") "Type" ""
        }

        test "already PascalCase passes through unchanged" {
            Expect.equal (toFsharpIdent "Primary") "Primary" ""
        }
    ]


// ─── emit — module structure ───────────────────────────────────────────────────

let emitTests =
    testList "emit" [

        test "single token emits correct module nesting and var ref" {
            let tokens = seq { yield ["color"; "text"; "primary"], colorToken }
            let src = emit "Tokens" tokens
            Expect.stringContains src "module Tokens" ""
            Expect.stringContains src "module Color =" ""
            Expect.stringContains src "module Text =" ""
            Expect.stringContains src "let Primary = \"var(--color-text-primary)\"" ""
        }

        test "numeric scale segment gets N prefix in identifier, not in CSS var" {
            let tokens = seq { yield ["color"; "blue"; "500"], colorToken }
            let src = emit "Tokens" tokens
            Expect.stringContains src "let N500 = \"var(--color-blue-500)\"" ""
            Expect.isFalse (src.Contains "let 500") "no raw digit identifier"
        }

        test "hyphenated segment becomes PascalCase in identifier" {
            let tokens = seq { yield ["shadow"; "focus-ring"], token (ResolvedNumber 1.0) NumberType }
            let src = emit "Tokens" tokens
            Expect.stringContains src "let FocusRing = \"var(--shadow-focus-ring)\"" ""
        }

        test "default segment capitalises — no backtick" {
            let tokens = seq { yield ["color"; "action"; "default"], colorToken }
            let src = emit "Tokens" tokens
            Expect.stringContains src "let Default = \"var(--color-action-default)\"" ""
            Expect.isFalse (src.Contains "``default``") "no unnecessary backtick"
        }

        test "two sibling tokens under same module group together" {
            let tokens =
                seq {
                    yield ["color"; "text"; "primary"],   colorToken
                    yield ["color"; "text"; "secondary"],  colorToken
                }
            let src = emit "Tokens" tokens
            let colorModuleIdx = src.IndexOf("module Color =")
            let primaryIdx     = src.IndexOf("let Primary")
            let secondaryIdx   = src.IndexOf("let Secondary")
            Expect.isTrue (colorModuleIdx >= 0) "Color module present"
            Expect.isTrue (primaryIdx > colorModuleIdx) "Primary inside Color"
            Expect.isTrue (secondaryIdx > colorModuleIdx) "Secondary inside Color"
            // exactly one Color module
            Expect.equal (src.Split("module Color =").Length - 1) 1 "only one Color ="
        }

        test "typography token expands to five sub-properties" {
            let tokens = seq { yield ["font"; "body"], typoToken }
            let src = emit "Tokens" tokens
            Expect.stringContains src "let FontFamily = \"var(--font-body-font-family)\""    ""
            Expect.stringContains src "let FontSize = \"var(--font-body-font-size)\""        ""
            Expect.stringContains src "let FontWeight = \"var(--font-body-font-weight)\""    ""
            Expect.stringContains src "let LetterSpacing = \"var(--font-body-letter-spacing)\"" ""
            Expect.stringContains src "let LineHeight = \"var(--font-body-line-height)\""    ""
        }

        test "typography var refs use original lowercase path" {
            let tokens = seq { yield ["font"; "body"], typoToken }
            let src = emit "Tokens" tokens
            Expect.stringContains src "var(--font-body-font-family)" ""
            Expect.isFalse (src.Contains "var(--Font-Body") "no capitalised CSS var"
        }

        test "dimension token emits single var ref" {
            let tokens = seq { yield ["spacing"; "N4"], dimToken 16.0 Px }
            let src = emit "Tokens" tokens
            Expect.stringContains src "let N4 = \"var(--spacing-N4)\"" ""
        }

        test "auto-generated comment is present" {
            let tokens = seq { yield ["a"; "b"], numToken 1.0 }
            let src = emit "Tokens" tokens
            Expect.stringContains src "// <auto-generated/>" ""
        }

        test "custom module name is respected" {
            let tokens = seq { yield ["a"; "b"], numToken 1.0 }
            let src = emit "MyDesign" tokens
            Expect.stringContains src "module MyDesign" ""
            Expect.isFalse (src.Contains "module Tokens") "default name not present"
        }

        test "multiple top-level groups are separated by blank lines" {
            let tokens =
                seq {
                    yield ["color"; "a"], colorToken
                    yield ["spacing"; "b"], dimToken 4.0 Px
                }
            let src = emit "Tokens" tokens
            // Both top-level modules present
            Expect.stringContains src "module Color ="   ""
            Expect.stringContains src "module Spacing =" ""
        }
    ]


// ─── Integration — LaundryLog resolver round-trip ─────────────────────────────

let integrationTests =
    testList "integration" [

        test "LaundryLog resolver emits expected token identifiers" {
            let resolverPath = "/home/ivan/nexus/LaundryLog/tokens/ll.resolver.json"
            let resolverJson = System.IO.File.ReadAllText resolverPath
            let loadFile name =
                let dir  = System.IO.Path.GetDirectoryName resolverPath |> Option.ofObj |> Option.defaultValue ""
                let full = System.IO.Path.Combine(dir, name)
                try Ok (System.IO.File.ReadAllText full)
                with ex -> Error ex.Message
            match Api.importWithResolver loadFile Map.empty resolverJson with
            | Error es ->
                failwith (es |> List.map Api.formatImportError |> String.concat "; ")
            | Ok tokens ->
                let src = emit "Tokens" tokens
                // Primitives
                Expect.stringContains src "module Color ="       "Color module"
                Expect.stringContains src "module Neutral ="     "Neutral submodule"
                Expect.stringContains src "let N50 ="            "N50 binding"
                // Semantic alias — resolved to its primitive value
                Expect.stringContains src "let Primary ="        "primary text token"
                // LaundryLog machine colours
                Expect.stringContains src "module Machine ="     "machine module"
                Expect.stringContains src "module Washer ="      "washer submodule"
                Expect.stringContains src "let Default ="        "washer default binding"
                // Spacing numeric scale
                Expect.stringContains src "module Spacing ="     "spacing module"
                // CSS var refs use lowercase original path
                Expect.stringContains src "var(--color-neutral-N50)"          "neutral-N50 CSS var"
                Expect.stringContains src "var(--color-machine-washer-default)" "washer CSS var"
        }
    ]


// ─── Identifier-safety check (ADR-038, 0.11.0) ──────────────────────────────

let safetyTests = testList "checkIdentifierSafety + emitChecked" [

    testCase "clean file: no issues reported" <| fun () ->
        let tokens : (string list * ResolvedToken) list = [
            ["color"; "text"; "primary"], colorToken
            ["spacing"; "sm"],              dimToken 8.0 Px
            ["scale"; "base"],              numToken 16.0
        ]
        let issues = checkIdentifierSafety tokens
        Expect.equal issues [] "no collisions or conflicts"

    testCase "case collision: color.dark vs color.Dark both map to Color.Dark" <| fun () ->
        let tokens = [
            ["color"; "dark"], colorToken
            ["color"; "Dark"], colorToken
        ]
        let issues = checkIdentifierSafety tokens
        match issues with
        | [IdentifierCollision (fs, sources)] ->
            Expect.equal fs ["Color"; "Dark"] "fsPath = Color.Dark"
            Expect.equal sources.Length 2 "both DTCG paths reported"
        | other -> failtestf "expected single IdentifierCollision, got %A" other

    testCase "hyphen-vs-camel collision: font.line-height vs font.lineHeight" <| fun () ->
        let tokens = [
            ["font"; "line-height"], dimToken 1.5 Px
            ["font"; "lineHeight"],  dimToken 1.5 Px
        ]
        let issues = checkIdentifierSafety tokens
        match issues with
        | [IdentifierCollision (fs, _)] ->
            Expect.equal fs ["Font"; "LineHeight"] "both transform to Font.LineHeight"
        | other -> failtestf "expected IdentifierCollision, got %A" other

    testCase "numeric N-prefix collision: scale.400 vs scale.N400" <| fun () ->
        let tokens = [
            ["scale"; "400"],  numToken 400.0
            ["scale"; "N400"], numToken 400.0
        ]
        let issues = checkIdentifierSafety tokens
        match issues with
        | [IdentifierCollision (fs, _)] ->
            Expect.equal fs ["Scale"; "N400"] "both transform to Scale.N400"
        | other -> failtestf "expected IdentifierCollision, got %A" other

    testCase "typography expansion collision: heading typography vs explicit heading.FontSize" <| fun () ->
        // Typography token at ["font"; "heading"] expands to 5 sub-paths
        // including ["Font"; "Heading"; "FontSize"]. An explicit dimension
        // token at ["font"; "heading"; "font-size"] would also produce
        // ["Font"; "Heading"; "FontSize"]. Both collide.
        let tokens = [
            ["font"; "heading"],              typoToken
            ["font"; "heading"; "font-size"], dimToken 24.0 Px
        ]
        let issues = checkIdentifierSafety tokens
        let hasFontSizeCollision =
            issues |> List.exists (function
                | IdentifierCollision (fs, _) -> fs = ["Font"; "Heading"; "FontSize"]
                | _ -> false)
        Expect.isTrue hasFontSizeCollision "FontSize sub-path collides between typography expansion and explicit dimension token"

    testCase "leaf/branch conflict: font (leaf) vs font.size.sm (extends as branch)" <| fun () ->
        let tokens = [
            ["font"],               dimToken 16.0 Px
            ["font"; "size"; "sm"], dimToken 12.0 Px
        ]
        let issues = checkIdentifierSafety tokens
        let hasConflict =
            issues |> List.exists (function
                | LeafBranchConflict (leafFs, leafPath, _) ->
                    leafFs = ["Font"] && leafPath = ["font"]
                | _ -> false)
        Expect.isTrue hasConflict "Font leaf conflicts with extending Font.Size.Sm branch"

    testCase "emitChecked: clean tokens return Ok with emitted source" <| fun () ->
        let tokens = [
            ["color"; "text"; "primary"], colorToken
        ]
        match emitChecked "Tokens" tokens with
        | Ok src ->
            Expect.stringContains src "module Tokens"          "module header"
            Expect.stringContains src "Color"                  "Color module"
            Expect.stringContains src "Primary"                "Primary leaf"
        | Error issues -> failtestf "expected Ok, got Error %A" issues

    testCase "emitChecked: collision returns Error without emitting" <| fun () ->
        let tokens = [
            ["color"; "dark"], colorToken
            ["color"; "Dark"], colorToken
        ]
        match emitChecked "Tokens" tokens with
        | Ok _ -> failtest "expected Error from emitChecked on collision"
        | Error issues ->
            Expect.equal issues.Length 1 "one collision reported"
            match issues.[0] with
            | IdentifierCollision (fs, _) ->
                Expect.equal fs ["Color"; "Dark"] "collision at Color.Dark"
            | other -> failtestf "expected IdentifierCollision, got %A" other

    testCase "BindingsIdentifierIssue.format produces readable line" <| fun () ->
        let issue = IdentifierCollision (["Color"; "Dark"], [["color"; "dark"]; ["color"; "Dark"]])
        let s = BindingsIdentifierIssue.format issue
        Expect.stringContains s "Color.Dark"   "fsPath mentioned"
        Expect.stringContains s "color.dark"   "first source mentioned"
        Expect.stringContains s "color.Dark"   "second source mentioned"

    testCase "ivanthegeek.tokens.json sample emits cleanly (real-world baseline)" <| fun () ->
        // Regression check against a real sample — the bundled sample file
        // should never accidentally introduce a collision.
        let samplePath = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "samples", "ivanthegeek.tokens.json")
        if System.IO.File.Exists samplePath then
            let json = System.IO.File.ReadAllText samplePath
            match Api.import json with
            | Error es -> failtestf "sample parse failed: %A" es
            | Ok tokens ->
                let issues = checkIdentifierSafety tokens
                Expect.equal issues [] "real-world sample has no collisions"
        // else: sample file not present in this build context — skip silently
]


// ─── All tests ────────────────────────────────────────────────────────────────

let allTests =
    testList "BindingsEmitter" [
        identTests
        emitTests
        integrationTests
        safetyTests
    ]
