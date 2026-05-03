module FnTools.DesignTokens.Tests.CssIngestTests

open Expecto
open FnTools.DesignTokens


// ─── Inline CSS fixtures ──────────────────────────────────────────────────────

let private cbSample = """
:root {
  --cb-neutral-50:  oklch(97.5% 0.006 75);
  --cb-neutral-900: oklch(18%   0.008 75);
  --cb-amber-300: oklch(82%  0.140 78);
  --cb-amber-500: oklch(70%  0.185 70);
  --cb-text-primary:   var(--cb-neutral-900);
  --cb-accent:         var(--cb-amber-500);
  --cb-accent-hover:   var(--cb-amber-300);
  --cb-font-body: 'DM Sans', 'Helvetica Neue', Arial, sans-serif;
  --cb-text-sm:   0.8125rem;
  --cb-text-base: 0.9375rem;
  --cb-space-2:  8px;
  --cb-space-4: 16px;
  --cb-weight-semibold: 600;
  --cb-leading-normal: 1.5;
  --cb-tracking-tight: -0.02em;
  --cb-radius-md: 12px;
  --cb-shadow-xs: 0 1px 2px rgba(0,0,0,0.06);
  --cb-shadow-sm: 0 1px 4px rgba(0,0,0,0.08), 0 1px 2px rgba(0,0,0,0.05);
  --cb-duration-fast: 100ms;
  --cb-ease-standard: cubic-bezier(0.4, 0, 0.2, 1);
}
"""

let private llSample = """
:root {
  --ll-washer-color: oklch(56% 0.14 200);
  --ll-washer-subtle: oklch(94% 0.04 200);
  --ll-header-text: oklch(100% 0 0);
  --ll-header-sub: rgba(255,255,255,0.9);
  --ll-coin-size: 64px;
  --ll-app-accent: var(--cb-accent);
}
"""


// ivanthegeek.com inline :root — no prefix, flat names
let private itkSample = """
:root {
  --bg: #e8f0e8;
  --surface: rgba(240, 247, 240, 0.94);
  --border: #a8c8a8;
  --border-strong: #6a9e6a;
  --accent: #1a6e1a;
  --accent-hover: #0f4e0f;
  --text: #0a1a0a;
  --shadow: 0 18px 40px rgba(18, 42, 18, 0.08);
}
"""


// ─── Helpers ──────────────────────────────────────────────────────────────────

let private ingestAndParse css prefix =
    let result = CssIngest.ingest css prefix
    let parseResult = Format.parse result.Json
    result, parseResult

let private findGroup name (file: TokenFile) =
    file.Children
    |> List.tryFind (fun (n, _) -> TokenName.value n = name)
    |> Option.bind (fun (_, node) -> match node with Group g -> Some g | _ -> None)

let private findLeaf groupName leafName (file: TokenFile) =
    findGroup groupName file
    |> Option.bind (fun g ->
        g.Children
        |> List.tryFind (fun (n, _) -> TokenName.value n = leafName))
    |> Option.bind (fun (_, node) -> match node with TokenLeaf t -> Some t | _ -> None)


// ─── Unit tests ───────────────────────────────────────────────────────────────

let allTests =
    testList "CssIngest" [

        testCase "cb: ingests OKLCH color tokens with zero parse errors" <| fun () ->
            let result, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "parse errors: %A" es
            | Ok file ->
                Expect.isTrue (result.TokenCount > 0) "should have tokens"
                match findLeaf "neutral" "50" file with
                | None -> failtest "missing neutral.50"
                | Some t ->
                    match t.Value with
                    | TokenValue.Color c ->
                        Expect.equal c.ColorSpace OKLCH "neutral.50 is OKLCH"
                        let (l, _, _) = c.Components
                        match l with
                        | Channel lv -> Expect.floatClose Accuracy.medium lv 0.975 "L≈0.975"
                        | _ -> failtest "expected channel"
                    | other -> failtestf "expected Color, got %A" other

        testCase "cb: intra-file var() aliases produce TokenValue.Alias" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "text" "primary" file with
                | None -> failtest "missing text.primary"
                | Some t ->
                    match t.Value with
                    | TokenValue.Alias (CurlyBrace segs) ->
                        Expect.isTrue (segs |> List.exists (fun s -> s = "neutral")) "alias points to neutral"
                    | other -> failtestf "expected CurlyBrace alias, got %A" other

        testCase "cb: dimension px token parsed correctly" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "space" "2" file with
                | None -> failtest "missing space.2"
                | Some t ->
                    match t.Value with
                    | TokenValue.Dimension d ->
                        Expect.equal d.Unit Px "px unit"
                        Expect.floatClose Accuracy.high d.Value 8.0 "8px"
                    | other -> failtestf "expected Dimension, got %A" other

        testCase "cb: dimension rem token parsed correctly" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "text" "sm" file with
                | None -> failtest "missing text.sm"
                | Some t ->
                    match t.Value with
                    | TokenValue.Dimension d ->
                        Expect.equal d.Unit Rem "rem unit"
                        Expect.floatClose Accuracy.high d.Value 0.8125 "0.8125rem"
                    | other -> failtestf "expected Dimension, got %A" other

        testCase "cb: fontFamily stack parsed" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "font" "body" file with
                | None -> failtest "missing font.body"
                | Some t ->
                    match t.Value with
                    | TokenValue.FontFamily (Stack fs) ->
                        Expect.isTrue (fs.Length > 1) "stack has multiple families"
                        Expect.stringContains (List.head fs) "DM Sans" "first is DM Sans"
                    | other -> failtestf "expected Stack FontFamily, got %A" other

        testCase "cb: fontWeight parsed as Numeric" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "weight" "semibold" file with
                | None -> failtest "missing weight.semibold"
                | Some t ->
                    match t.Value with
                    | TokenValue.FontWeight (Numeric n) ->
                        Expect.equal n 600 "weight.semibold = 600"
                    | other -> failtestf "expected Numeric FontWeight, got %A" other

        testCase "cb: letter-spacing em parsed as Number" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "tracking" "tight" file with
                | None -> failtest "missing tracking.tight"
                | Some t ->
                    match t.Value with
                    | TokenValue.Number n -> Expect.floatClose Accuracy.high n -0.02 "-0.02"
                    | other -> failtestf "expected Number, got %A" other

        testCase "cb: single-layer shadow parses" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "shadow" "xs" file with
                | None -> failtest "missing shadow.xs"
                | Some t ->
                    match t.Value with
                    | TokenValue.Shadow (ShadowSingle _) -> ()
                    | other -> failtestf "expected ShadowSingle, got %A" other

        testCase "cb: multi-layer shadow parses as ShadowMultiple" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "shadow" "sm" file with
                | None -> failtest "missing shadow.sm"
                | Some t ->
                    match t.Value with
                    | TokenValue.Shadow (ShadowMultiple layers) ->
                        Expect.equal layers.Length 2 "two shadow layers"
                    | other -> failtestf "expected ShadowMultiple, got %A" other

        testCase "cb: duration ms token parsed" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "duration" "fast" file with
                | None -> failtest "missing duration.fast"
                | Some t ->
                    match t.Value with
                    | TokenValue.Duration d ->
                        Expect.equal d.Unit Milliseconds "ms unit"
                        Expect.floatClose Accuracy.high d.Value 100.0 "100ms"
                    | other -> failtestf "expected Duration, got %A" other

        testCase "cb: cubicBezier parsed" <| fun () ->
            let _, parsed = ingestAndParse cbSample "cb"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "ease" "standard" file with
                | None -> failtest "missing ease.standard"
                | Some t ->
                    match t.Value with
                    | TokenValue.CubicBezier cb ->
                        Expect.floatClose Accuracy.high cb.P1x 0.4 "P1x"
                        Expect.floatClose Accuracy.high cb.P2y 1.0 "P2y"
                    | other -> failtestf "expected CubicBezier, got %A" other

        testCase "ll: direct OKLCH colors ingest with zero parse errors" <| fun () ->
            let result, parsed = ingestAndParse llSample "ll"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok _ ->
                Expect.isTrue (result.TokenCount >= 3) "at least 3 ll tokens"
                let skipped = result.Warnings |> List.length
                Expect.isTrue (skipped >= 1) "cross-prefix alias produces at least 1 warning"

        testCase "ll: rgba color (header.sub) parsed as sRGB with alpha" <| fun () ->
            let _, parsed = ingestAndParse llSample "ll"
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "header" "sub" file with
                | None -> failtest "missing header.sub"
                | Some t ->
                    match t.Value with
                    | TokenValue.Color c ->
                        Expect.equal c.ColorSpace SRGB "rgba → sRGB"
                        Expect.isSome c.Alpha "has alpha"
                        Expect.floatClose Accuracy.high (Option.get c.Alpha) 0.9 "alpha 0.9"
                    | other -> failtestf "expected Color, got %A" other


        // ─── EXP-02: LaundryLog design system handoff HTML ────────────────────

        testCase "EXP-02: CB tokens from LaundryLog handoff HTML — zero parse errors" <| fun () ->
            let htmlPath =
                "/home/ivan/nexus/LaundryLog/artifacts/LaundryLog_Design_System_Handoff/LaundryLog Design System.html"
            if not (System.IO.File.Exists htmlPath) then
                skiptest "LaundryLog handoff HTML not found — skipping EXP-02"
            let html   = System.IO.File.ReadAllText htmlPath
            let result = CssIngest.ingest html "cb"
            match Format.parse result.Json with
            | Error es ->
                failtestf "EXP-02 CB: Format.parse failed with %d errors:\n%A" es.Length es
            | Ok _ ->
                printfn "EXP-02 CB: %d tokens ingested, %d skipped" result.TokenCount result.Warnings.Length
                for w in result.Warnings do
                    match w with
                    | CssIngest.Skipped (name, reason) ->
                        printfn "  SKIP  %s  (%s)" name reason

        testCase "EXP-02: LL tokens from LaundryLog handoff HTML — zero parse errors" <| fun () ->
            let htmlPath =
                "/home/ivan/nexus/LaundryLog/artifacts/LaundryLog_Design_System_Handoff/LaundryLog Design System.html"
            if not (System.IO.File.Exists htmlPath) then
                skiptest "LaundryLog handoff HTML not found — skipping EXP-02"
            let html   = System.IO.File.ReadAllText htmlPath
            let result = CssIngest.ingest html "ll"
            match Format.parse result.Json with
            | Error es ->
                failtestf "EXP-02 LL: Format.parse failed with %d errors:\n%A" es.Length es
            | Ok _ ->
                printfn "EXP-02 LL: %d tokens ingested, %d skipped" result.TokenCount result.Warnings.Length
                for w in result.Warnings do
                    match w with
                    | CssIngest.Skipped (name, reason) ->
                        printfn "  SKIP  %s  (%s)" name reason


        // ─── Prefix-less ingestion (ivanthegeek.com) ─────────────────────────

        testCase "prefix-less: empty prefix ingests all custom properties" <| fun () ->
            let result, parsed = ingestAndParse itkSample ""
            match parsed with
            | Error es -> failtestf "parse errors: %A" es
            | Ok _ ->
                Expect.isTrue (result.TokenCount >= 7) "at least 7 tokens"
                Expect.isEmpty result.Warnings "no warnings"

        testCase "prefix-less: flat name (--bg) maps to bg.default color" <| fun () ->
            let _, parsed = ingestAndParse itkSample ""
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "bg" "default" file with
                | None -> failtest "missing bg.default"
                | Some t ->
                    match t.Value with
                    | TokenValue.Color c -> Expect.equal c.ColorSpace SRGB "hex → sRGB"
                    | other -> failtestf "expected Color, got %A" other

        testCase "prefix-less: compound name (--accent-hover) maps to accent.hover" <| fun () ->
            let _, parsed = ingestAndParse itkSample ""
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "accent" "hover" file with
                | None -> failtest "missing accent.hover"
                | Some t ->
                    match t.Value with
                    | TokenValue.Color c -> Expect.equal c.ColorSpace SRGB "hex → sRGB"
                    | other -> failtestf "expected Color, got %A" other

        testCase "prefix-less: --border and --border-strong both land under 'border' group" <| fun () ->
            let _, parsed = ingestAndParse itkSample ""
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                Expect.isSome (findLeaf "border" "default" file) "border.default present"
                Expect.isSome (findLeaf "border" "strong"  file) "border.strong present"

        testCase "prefix-less: --shadow maps to shadow token" <| fun () ->
            let _, parsed = ingestAndParse itkSample ""
            match parsed with
            | Error es -> failtestf "%A" es
            | Ok file ->
                match findLeaf "shadow" "default" file with
                | None -> failtest "missing shadow.default"
                | Some t ->
                    match t.Value with
                    | TokenValue.Shadow (ShadowSingle _) -> ()
                    | other -> failtestf "expected ShadowSingle, got %A" other
    ]
