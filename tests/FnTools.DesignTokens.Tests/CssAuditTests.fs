module FnTools.DesignTokens.Tests.CssAuditTests

open Expecto
open FnTools.DesignTokens


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

    ]
