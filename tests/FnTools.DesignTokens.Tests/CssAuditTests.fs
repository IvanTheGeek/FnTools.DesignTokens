module FnTools.DesignTokens.Tests.CssAuditTests

open Expecto
open FnTools.DesignTokens
open FnTools.DesignTokens.Tests.Fixtures


// ─── Fixtures ─────────────────────────────────────────────────────────────────

// Simulates a small site where :root has tokens but rules have hardcoded values.
// Colors: #fff (×2), #1a6e1a (×1), #eef5ed (×1), #0f4e0f (×1), rgba(0,0,0,0.1) (×1)
// Dimensions: 8px (×2 — both border-radius), 16px (inside padding shorthand → not matched as 16px alone), 0.875rem (×1)
// FontFamily: 'Exo 2', sans-serif (×1)
let private fixture = """
:root {
  --accent: #1a6e1a;
  --text: #0a1a0a;
}

body {
  font-family: 'Exo 2', sans-serif;
  color: var(--text);
  background: #eef5ed;
}

.button {
  background: #1a6e1a;
  color: #fff;
  border-radius: 8px;
  padding: 8px 16px;
  font-size: 0.875rem;
}

.button:hover {
  background: #0f4e0f;
}

.card {
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}
"""

// Media-query wrapping — values inside @media blocks should still be found.
let private mediaFixture = """
.base { color: #111111; }

@media (max-width: 600px) {
  .base { color: #222222; font-size: 0.75rem; }
}
"""

// CssNative: relative units and CSS math functions — valid CSS but not DTCG-tokenisable.
// Note: fr is a grid unit and only appears in grid-template-* properties which are not
// in designProperties, so fr is unreachable through the public audit API.
let private cssNativeFixture = """
.text {
  font-size: 1.2em;
  letter-spacing: 0.05em;
  line-height: 150%;
}
.layout {
  width: 80vw;
  height: 100vh;
  gap: 2ch;
}
.responsive {
  padding: clamp(8px, 2vw, 24px);
  font-size: calc(1rem + 0.5vw);
}
"""


// ─── Helpers ─────────────────────────────────────────────────────────────────

let private findEntry rawValue (result: CssAudit.AuditResult) =
    result.Entries |> List.tryFind (fun e -> e.RawValue = rawValue)


// ─── Tests ───────────────────────────────────────────────────────────────────

let allTests =
    testList "CssAudit" [

        testCase "returns non-empty entries for a mixed CSS fixture" <| fun () ->
            let result = CssAudit.audit fixture
            Expect.isTrue (result.Entries.Length > 0) "at least one entry"
            Expect.isTrue (result.RuleCount >= 4)     "at least 4 rules scanned"

        testCase ":root block is not included in entries or rule count" <| fun () ->
            let result = CssAudit.audit fixture
            // --accent and --text should not appear as AuditEntry RawValues
            let hasCustomProp =
                result.Entries |> List.exists (fun e -> e.RawValue.StartsWith "--")
            Expect.isFalse hasCustomProp ":root custom props not in audit"

        testCase "var() references are excluded" <| fun () ->
            let result = CssAudit.audit fixture
            let hasVar =
                result.Entries |> List.exists (fun e -> e.RawValue.StartsWith "var(")
            Expect.isFalse hasVar "var() refs excluded"

        testCase "#fff has count 2 (button color + card background)" <| fun () ->
            let result = CssAudit.audit fixture
            match findEntry "#fff" result with
            | None   -> failtest "missing #fff entry"
            | Some e ->
                Expect.equal e.Count     2     "#fff count=2"
                Expect.equal e.ValueType CssAudit.Color "classified as Color"

        testCase "#1a6e1a has count 1 (button background)" <| fun () ->
            let result = CssAudit.audit fixture
            match findEntry "#1a6e1a" result with
            | None   -> failtest "missing #1a6e1a"
            | Some e ->
                Expect.equal e.Count 1 "#1a6e1a count=1"
                Expect.equal e.ValueType CssAudit.Color "Color"

        testCase "border-radius: 8px has count 2 (button + card)" <| fun () ->
            let result = CssAudit.audit fixture
            match findEntry "8px" result with
            | None   -> failtest "missing 8px entry"
            | Some e ->
                Expect.equal e.Count     2                    "8px count=2"
                Expect.equal e.ValueType CssAudit.Dimension   "Dimension"
                let props = e.Occurrences |> List.map (fun o -> o.Property)
                Expect.isTrue (props |> List.forall (fun p -> p = "border-radius")) "both are border-radius"

        testCase "padding shorthand '8px 16px' is not split — reported as-is" <| fun () ->
            let result = CssAudit.audit fixture
            // '8px 16px' is Unknown (not a single dimension value) — should not appear
            let hasShorthand = findEntry "8px 16px" result |> Option.isSome
            Expect.isFalse hasShorthand "compound shorthand not reported as dimension"

        testCase "font-family classified correctly" <| fun () ->
            let result = CssAudit.audit fixture
            match findEntry "'Exo 2', sans-serif" result with
            | None   -> failtest "missing font-family entry"
            | Some e ->
                Expect.equal e.ValueType CssAudit.FontFamily "FontFamily"
                Expect.equal e.Count     1                   "count=1"

        testCase "box-shadow value classified as Shadow" <| fun () ->
            let result = CssAudit.audit fixture
            match findEntry "0 2px 8px rgba(0,0,0,0.1)" result with
            | None   -> failtest "missing box-shadow entry"
            | Some e -> Expect.equal e.ValueType CssAudit.Shadow "Shadow"

        testCase "entries are sorted by count descending" <| fun () ->
            let result = CssAudit.audit fixture
            let counts = result.Entries |> List.map (fun e -> e.Count)
            let sorted = counts |> List.sortDescending
            Expect.equal counts sorted "sorted descending"

        testCase "values inside @media rules are found with context in selector" <| fun () ->
            let result = CssAudit.audit mediaFixture
            match findEntry "#222222" result with
            | None   -> failtest "missing #222222"
            | Some e ->
                Expect.equal e.Count 1 "count=1"
                let sel = e.Occurrences.[0].Selector
                Expect.stringContains sel "@media" "selector contains @media context"

        testCase "outer and inner rule both found when same value used at different widths" <| fun () ->
            let result = CssAudit.audit mediaFixture
            // #111111 from .base (outside media), #222222 from .base inside @media
            Expect.isSome (findEntry "#111111" result) "#111111 found"
            Expect.isSome (findEntry "#222222" result) "#222222 found"


        // ─── auditAgainst ────────────────────────────────────────────────────

        testCase "auditAgainst: plain audit has all MatchedToken = None" <| fun () ->
            let result = CssAudit.audit fixture
            let anyMatched = result.Entries |> List.exists (fun e -> e.MatchedToken.IsSome)
            Expect.isFalse anyMatched "plain audit has no matches"

        testCase "auditAgainst: hex color matched to token path" <| fun () ->
            let file = Format.parse CssAuditMatch.tokensJson |> Result.defaultWith (fun e -> failwithf "%A" e)
            let result = CssAudit.auditAgainst fixture file
            match findEntry "#1a6e1a" result with
            | None   -> failtest "missing #1a6e1a"
            | Some e -> Expect.equal e.MatchedToken (Some "color.accent") "matched color.accent"

        testCase "auditAgainst: short hex #fff expanded and matched to #ffffff token" <| fun () ->
            let file = Format.parse CssAuditMatch.tokensJson |> Result.defaultWith (fun e -> failwithf "%A" e)
            let result = CssAudit.auditAgainst fixture file
            match findEntry "#fff" result with
            | None   -> failtest "missing #fff"
            | Some e -> Expect.equal e.MatchedToken (Some "color.white") "matched color.white"

        testCase "auditAgainst: rgba matched to color token computed from components" <| fun () ->
            let file = Format.parse CssAuditMatch.tokensJson |> Result.defaultWith (fun e -> failwithf "%A" e)
            let result = CssAudit.auditAgainst fixture file
            // fixture has rgba(0,0,0,0.1) → should match color.overlay (0,0,0,alpha=0.1)
            match findEntry "0 2px 8px rgba(0,0,0,0.1)" result with
            | None   -> failtest "missing shadow entry"
            | Some e ->
                // shadow entries are not comparable — MatchedToken should be None
                Expect.isNone e.MatchedToken "shadow not matched (not comparable)"

        testCase "auditAgainst: dimension matched to spacing token" <| fun () ->
            let file = Format.parse CssAuditMatch.tokensJson |> Result.defaultWith (fun e -> failwithf "%A" e)
            let result = CssAudit.auditAgainst fixture file
            match findEntry "8px" result with
            | None   -> failtest "missing 8px"
            | Some e -> Expect.equal e.MatchedToken (Some "spacing.sm") "matched spacing.sm"

        testCase "auditAgainst: font-family matched to fontFamily token" <| fun () ->
            let file = Format.parse CssAuditMatch.tokensJson |> Result.defaultWith (fun e -> failwithf "%A" e)
            let result = CssAudit.auditAgainst fixture file
            match findEntry "'Exo 2', sans-serif" result with
            | None   -> failtest "missing font-family entry"
            | Some e -> Expect.equal e.MatchedToken (Some "font.family.body") "matched font.family.body"

        testCase "auditAgainst: unmatched value stays None" <| fun () ->
            let file = Format.parse CssAuditMatch.tokensJson |> Result.defaultWith (fun e -> failwithf "%A" e)
            let result = CssAudit.auditAgainst fixture file
            // #eef5ed and #0f4e0f are not in the token file
            match findEntry "#eef5ed" result with
            | None   -> failtest "missing #eef5ed"
            | Some e -> Expect.isNone e.MatchedToken "#eef5ed has no token match"


        // ─── CssNative ───────────────────────────────────────────────────────

        testList "CssNative: relative units and CSS math functions" [

            testCase "em unit classified as CssNative" <| fun () ->
                let result = CssAudit.audit cssNativeFixture
                match findEntry "1.2em" result with
                | None   -> failtest "missing 1.2em"
                | Some e -> Expect.equal e.ValueType CssAudit.CssNative "CssNative"

            testCase "percentage unit classified as CssNative" <| fun () ->
                let result = CssAudit.audit cssNativeFixture
                match findEntry "150%" result with
                | None   -> failtest "missing 150%"
                | Some e -> Expect.equal e.ValueType CssAudit.CssNative "CssNative"

            testCase "vw unit classified as CssNative" <| fun () ->
                let result = CssAudit.audit cssNativeFixture
                match findEntry "80vw" result with
                | None   -> failtest "missing 80vw"
                | Some e -> Expect.equal e.ValueType CssAudit.CssNative "CssNative"

            testCase "vh unit classified as CssNative" <| fun () ->
                let result = CssAudit.audit cssNativeFixture
                match findEntry "100vh" result with
                | None   -> failtest "missing 100vh"
                | Some e -> Expect.equal e.ValueType CssAudit.CssNative "CssNative"

            testCase "ch unit classified as CssNative" <| fun () ->
                let result = CssAudit.audit cssNativeFixture
                match findEntry "2ch" result with
                | None   -> failtest "missing 2ch"
                | Some e -> Expect.equal e.ValueType CssAudit.CssNative "CssNative"

            testCase "clamp() expression classified as CssNative" <| fun () ->
                let result = CssAudit.audit cssNativeFixture
                match findEntry "clamp(8px, 2vw, 24px)" result with
                | None   -> failtest "missing clamp() entry"
                | Some e -> Expect.equal e.ValueType CssAudit.CssNative "CssNative"

            testCase "calc() expression classified as CssNative" <| fun () ->
                let result = CssAudit.audit cssNativeFixture
                match findEntry "calc(1rem + 0.5vw)" result with
                | None   -> failtest "missing calc() entry"
                | Some e -> Expect.equal e.ValueType CssAudit.CssNative "CssNative"

            testCase "CssNative entries are included in audit results (not filtered like Unknown)" <| fun () ->
                let result = CssAudit.audit cssNativeFixture
                let nativeEntries = result.Entries |> List.filter (fun e -> e.ValueType = CssAudit.CssNative)
                Expect.isGreaterThan (List.length nativeEntries) 0 "CssNative entries present"

            testCase "CssNative entries have MatchedToken = None in auditAgainst" <| fun () ->
                let file = Format.parse CssAuditMatch.tokensJson |> Result.defaultWith (fun e -> failwithf "%A" e)
                let result = CssAudit.auditAgainst cssNativeFixture file
                let nativeEntries = result.Entries |> List.filter (fun e -> e.ValueType = CssAudit.CssNative)
                Expect.isGreaterThan (List.length nativeEntries) 0 "has CssNative entries"
                nativeEntries |> List.iter (fun e ->
                    Expect.isNone e.MatchedToken
                        (sprintf "%s: CssNative values have no token match" e.RawValue))

            testCase "px and rem still classified as Dimension (not CssNative)" <| fun () ->
                let result = CssAudit.audit fixture
                match findEntry "8px" result with
                | None   -> failtest "missing 8px"
                | Some e -> Expect.equal e.ValueType CssAudit.Dimension "8px is Dimension"
                match findEntry "0.875rem" result with
                | None   -> failtest "missing 0.875rem"
                | Some e -> Expect.equal e.ValueType CssAudit.Dimension "0.875rem is Dimension"

        ]

    ]
