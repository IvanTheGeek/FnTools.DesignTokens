/// CSS rule auditor — surfaces hardcoded design values in regular CSS rules.
///
/// Complements CssIngest: CssIngest handles :root custom properties; CssAudit
/// scans every other rule for values that are candidates for tokenisation.
/// Use together as step 1 + step 2 of the CSS Bootstrap / Migration workflow.
module FnTools.DesignTokens.CssAudit

open System
open System.Text
open System.Text.RegularExpressions


// ─── Public types ─────────────────────────────────────────────────────────────

type AuditValueType =
    | Color
    | Dimension
    | Duration
    | FontFamily
    | FontWeight
    | Shadow
    | Unknown

type AuditOccurrence = {
    /// CSS selector where this value was found, with media-query context if applicable.
    /// e.g. ".button" or "@media (max-width: 600px) → .button"
    Selector : string
    Property : string
}

type AuditEntry = {
    RawValue    : string
    ValueType   : AuditValueType
    Count       : int
    Occurrences : AuditOccurrence list
}

type AuditResult = {
    /// Unique design values found in rules, sorted by Count descending.
    Entries   : AuditEntry list
    /// Number of leaf CSS rules scanned (excludes :root and skipped at-rules).
    RuleCount : int
}


// ─── CSS parsing helpers ──────────────────────────────────────────────────────

let private stripBlockComments (s: string) : string =
    let sb = StringBuilder(s.Length)
    let mutable i = 0
    while i < s.Length do
        if i + 1 < s.Length && s.[i] = '/' && s.[i + 1] = '*' then
            i <- i + 2
            while i + 1 < s.Length && not (s.[i] = '*' && s.[i + 1] = '/') do
                i <- i + 1
            i <- i + 2
        else
            sb.Append(s.[i]) |> ignore
            i <- i + 1
    sb.ToString()

/// Split s on sep only when not inside parentheses.
let private splitDepth0 (sep: char) (s: string) : string list =
    let mutable depth = 0
    let parts = ResizeArray<string>()
    let cur   = StringBuilder()
    for c in s do
        if   c = '(' then depth <- depth + 1; cur.Append c |> ignore
        elif c = ')' then depth <- depth - 1; cur.Append c |> ignore
        elif c = sep && depth = 0 then
            parts.Add(cur.ToString().Trim())
            cur.Clear() |> ignore
        else
            cur.Append c |> ignore
    if cur.Length > 0 then parts.Add(cur.ToString().Trim())
    List.ofSeq parts

/// Parse a flat (no nested braces) CSS block into (property, value) pairs.
let private extractPropValues (block: string) : (string * string) list =
    splitDepth0 ';' block
    |> List.choose (fun segment ->
        let segment = segment.Trim()
        if segment.Length = 0 then None
        else
            let colonIdx = segment.IndexOf(':')
            if colonIdx <= 0 then None
            else
                let prop  = segment.[..colonIdx-1].Trim().ToLowerInvariant()
                let value = segment.[colonIdx+1..].Trim()
                if prop.Length > 0 && value.Length > 0 then Some (prop, value)
                else None)

/// Recursively extract all leaf CSS rules as (selector, blockContent) pairs.
/// Container rules (media queries etc.) are unwrapped — inner selectors carry
/// the parent context as a prefix: "@media (...) → .selector".
/// Skips :root, @keyframes, @font-face, @charset.
let rec private extractAllRules (context: string) (text: string) : (string * string) list =
    let results = ResizeArray<string * string>()
    let mutable i = 0
    while i < text.Length do
        let braceOpen = text.IndexOf('{', i)
        if braceOpen < 0 then i <- text.Length
        else
            let rawSel = text.[i..braceOpen-1].Trim()
            let selector =
                if context = "" then rawSel
                else sprintf "%s → %s" context rawSel
            // find matching close brace
            let mutable j = braceOpen + 1
            let mutable depth = 1
            while j < text.Length && depth > 0 do
                if   text.[j] = '{' then depth <- depth + 1
                elif text.[j] = '}' then depth <- depth - 1
                j <- j + 1
            let blockEnd = j - 2
            let blockContent =
                if blockEnd >= braceOpen + 1 then text.[braceOpen+1..blockEnd]
                else ""
            let skipRule =
                rawSel = ":root" ||
                rawSel.TrimStart().StartsWith "@keyframes" ||
                rawSel.TrimStart().StartsWith "@font-face"  ||
                rawSel.TrimStart().StartsWith "@charset"
            if not skipRule then
                if blockContent.Contains '{' then
                    results.AddRange(extractAllRules selector blockContent)
                else
                    results.Add((selector, blockContent))
            i <- j
    List.ofSeq results


// ─── Value classification ─────────────────────────────────────────────────────

/// CSS properties whose values are typically design tokens.
let private designProperties =
    Collections.Generic.HashSet<string>([|
        // color
        "color"; "background-color"; "background"
        "border-color"; "border-top-color"; "border-right-color"
        "border-bottom-color"; "border-left-color"
        "border"; "border-top"; "border-right"; "border-bottom"; "border-left"
        "outline"; "outline-color"; "box-shadow"; "text-shadow"
        "fill"; "stroke"; "caret-color"; "accent-color"
        "text-decoration-color"; "column-rule-color"
        // typography
        "font-family"; "font-size"; "font-weight"; "line-height"; "letter-spacing"
        "word-spacing"
        // spacing / layout
        "padding"; "padding-top"; "padding-right"; "padding-bottom"; "padding-left"
        "margin"; "margin-top"; "margin-right"; "margin-bottom"; "margin-left"
        "gap"; "row-gap"; "column-gap"
        // border
        "border-radius"
        "border-top-left-radius"; "border-top-right-radius"
        "border-bottom-left-radius"; "border-bottom-right-radius"
        // sizing
        "width"; "height"; "min-width"; "max-width"; "min-height"; "max-height"
        // animation
        "transition"; "transition-duration"; "transition-timing-function"; "transition-delay"
        "animation-duration"; "animation-timing-function"; "animation-delay"
        // misc
        "opacity"
    |])

/// Values that convey no design information — skip them.
let private trivialValues =
    Collections.Generic.HashSet<string>([|
        "inherit"; "initial"; "revert"; "revert-layer"; "unset"
        "auto"; "none"; "normal"; "0"; "transparent"; "currentColor"; "currentcolor"
    |])

let private isColorValue (v: string) : bool =
    v.StartsWith "#"      ||
    v.StartsWith "oklch(" ||
    v.StartsWith "oklab(" ||
    v.StartsWith "rgb("   ||
    v.StartsWith "rgba("  ||
    v.StartsWith "hsl("   ||
    v.StartsWith "hsla("  ||
    v.StartsWith "color("

let private isDimensionValue (v: string) : bool =
    Regex.IsMatch(v,
        @"^-?\d+(?:\.\d+)?(px|rem|em|%|vw|vh|dvh|dvw|vmin|vmax|fr|ch|ex)$")

let private isDurationValue (v: string) : bool =
    Regex.IsMatch(v, @"^\d+(?:\.\d+)?(ms|s)$")

let private classifyValue (prop: string) (v: string) : AuditValueType =
    if prop = "font-family" then FontFamily
    elif prop = "font-weight" then FontWeight
    elif prop = "box-shadow" || prop = "text-shadow" then Shadow
    elif isColorValue v    then Color
    elif isDurationValue v then Duration
    elif isDimensionValue v then Dimension
    else Unknown


// ─── Main entry point ─────────────────────────────────────────────────────────

/// Audit a CSS or HTML string for hardcoded design values in regular rules.
///
/// Returns one AuditEntry per unique raw value, sorted by Count descending.
/// Excludes:
///   - :root custom-property declarations (use CssIngest for those)
///   - var() references (already resolved through the token system)
///   - Trivial values: inherit, auto, none, 0, transparent, etc.
///   - Unknown-typed values (non-design properties, complex shorthands)
let audit (cssText: string) : AuditResult =
    let cleaned  = stripBlockComments cssText
    let allRules = extractAllRules "" cleaned

    // rawValue → (valueType, occurrences)
    let occMap =
        Collections.Generic.Dictionary<string,
            AuditValueType * ResizeArray<AuditOccurrence>>()

    for (selector, block) in allRules do
        for (prop, value) in extractPropValues block do
            if designProperties.Contains prop &&
               not (prop.StartsWith "--") &&
               not (value.StartsWith "var(") &&
               not (trivialValues.Contains value) then
                let vt = classifyValue prop value
                if vt <> Unknown then
                    let occ = { Selector = selector; Property = prop }
                    match occMap.TryGetValue value with
                    | true, (_, list) -> list.Add occ
                    | false, _        ->
                        let list = ResizeArray()
                        list.Add occ
                        occMap.[value] <- (vt, list)

    let entries =
        occMap
        |> Seq.map (fun kv ->
            let (vt, occs) = kv.Value
            { RawValue    = kv.Key
              ValueType   = vt
              Count       = occs.Count
              Occurrences = List.ofSeq occs })
        |> Seq.sortByDescending (fun e -> e.Count)
        |> List.ofSeq

    { Entries = entries; RuleCount = allRules.Length }
