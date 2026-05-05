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
    /// Valid CSS but not DTCG-tokenisable: relative units (em, %, vw, vh, ch, ex,
    /// fr, lh, svh, dvh, vmin, vmax) and CSS math functions (clamp(), calc()).
    /// Included in audit results so the bootstrap workflow can route them to the
    /// component layer explicitly, rather than silently discarding them as Unknown.
    | CssNative
    | Unknown

type AuditOccurrence = {
    /// CSS selector where this value was found, with media-query context if applicable.
    /// e.g. ".button" or "@media (max-width: 600px) → .button"
    Selector : string
    Property : string
}

type AuditEntry = {
    RawValue     : string
    ValueType    : AuditValueType
    Count        : int
    Occurrences  : AuditOccurrence list
    /// Path of a matching token (e.g. <c>"color.accent.subtle"</c>) when this
    /// hardcoded value is already covered by a token in the file passed to
    /// <see cref="auditAgainst"/>. Always <c>None</c> from plain <see cref="audit"/>.
    MatchedToken : string option
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
    Regex.IsMatch(v, @"^-?\d+(?:\.\d+)?(px|rem)$")

let private isDurationValue (v: string) : bool =
    Regex.IsMatch(v, @"^\d+(?:\.\d+)?(ms|s)$")

/// Relative or viewport-relative dimension units that are valid CSS but cannot be
/// represented as a DTCG dimension token (which requires px or rem).
let private isCssNativeDimension (v: string) : bool =
    Regex.IsMatch(v, @"^-?\d+(?:\.\d+)?(em|%|vw|vh|ch|ex|fr|lh|svh|dvh|svw|dvw|vmin|vmax)$")

/// CSS math functions that are valid as property values but cannot be reduced to a
/// single DTCG token value without runtime evaluation.
let private isCssNativeExpression (v: string) : bool =
    v.StartsWith "clamp(" || v.StartsWith "calc("

let private classifyValue (prop: string) (v: string) : AuditValueType =
    if prop = "font-family" then FontFamily
    elif prop = "font-weight" then FontWeight
    elif prop = "box-shadow" || prop = "text-shadow" then Shadow
    elif isColorValue v          then Color
    elif isDurationValue v       then Duration
    elif isDimensionValue v      then Dimension
    elif isCssNativeDimension v  then CssNative
    elif isCssNativeExpression v then CssNative
    else Unknown


// ─── Main entry point ─────────────────────────────────────────────────────────

/// Audit a CSS or HTML string for hardcoded design values in regular rules.
///
/// Returns one AuditEntry per unique raw value, sorted by Count descending.
/// Includes <see cref="AuditValueType.CssNative"/> values (relative units, clamp/calc)
/// so the bootstrap workflow can route them to the component layer explicitly.
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
            { RawValue     = kv.Key
              ValueType    = vt
              Count        = occs.Count
              Occurrences  = List.ofSeq occs
              MatchedToken = None })
        |> Seq.sortByDescending (fun e -> e.Count)
        |> List.ofSeq

    { Entries = entries; RuleCount = allRules.Length }


// ─── Duplication detector ─────────────────────────────────────────────────────

/// Expand 3- or 4-char hex to 6- or 8-char form: #rgb → #rrggbb, #rgba → #rrggbbaa.
let private expandShortHex (s: string) : string =
    match s.Length with
    | 4 -> sprintf "#%c%c%c%c%c%c" s.[1] s.[1] s.[2] s.[2] s.[3] s.[3]
    | 5 -> sprintf "#%c%c%c%c%c%c%c%c" s.[1] s.[1] s.[2] s.[2] s.[3] s.[3] s.[4] s.[4]
    | _ -> s

/// Normalise an rgba/rgb string to "rgba(r, g, b, a)" canonical form.
let private normalizeRgbaStr (s: string) : string =
    let m = Regex.Match(s,
        @"^rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?\s*\)$",
        RegexOptions.IgnoreCase)
    if not m.Success then s
    else
        let r = int m.Groups.[1].Value
        let g = int m.Groups.[2].Value
        let b = int m.Groups.[3].Value
        if m.Groups.[4].Success then
            sprintf "rgba(%d, %d, %d, %g)" r g b (float m.Groups.[4].Value)
        else
            sprintf "rgb(%d, %d, %d)" r g b

/// Return the canonical CSS string(s) to look up for an audit entry.
/// Short hex is expanded so #fff matches #ffffff; rgba spacing is normalised.
let private normalizeCssEntry (e: AuditEntry) : string list =
    match e.ValueType with
    | Color ->
        let s = e.RawValue.Trim().ToLowerInvariant()
        if s.StartsWith "#" then
            let expanded = expandShortHex s
            if expanded = s then [s] else [s; expanded]
        else
            let norm = normalizeRgbaStr s
            if norm = s then [s] else [s; norm]
    | _ -> [e.RawValue.Trim()]

/// Compute all canonical CSS string forms a token value could appear as in CSS.
/// Returns [] for types that cannot be meaningfully compared (Shadow, Alias, etc.).
let private tokenValueToCssStrings (v: TokenValue) : string list =
    match v with
    | TokenValue.Color c ->
        let alpha = Option.defaultValue 1.0 c.Alpha
        match c.Hex with
        | Some hex -> [hex.ToLowerInvariant()]
        | None ->
            match c.ColorSpace with
            | SRGB ->
                match c.Components with
                | Channel r, Channel g, Channel b ->
                    let rb = int (Math.Round(r * 255.0))
                    let gb = int (Math.Round(g * 255.0))
                    let bb = int (Math.Round(b * 255.0))
                    if alpha = 1.0 then [sprintf "#%02x%02x%02x" rb gb bb]
                    else [sprintf "rgba(%d, %d, %d, %g)" rb gb bb alpha]
                | _ -> []
            | _ -> []
    | TokenValue.Dimension d ->
        let u = match d.Unit with Px -> "px" | Rem -> "rem" | Em -> "em"
        [sprintf "%g%s" d.Value u]
    | TokenValue.FontFamily f ->
        let q (s: string) = if s.Contains ' ' then sprintf "'%s'" s else s
        match f with
        | Single s  -> [q s]
        | Stack lst -> [lst |> List.map q |> String.concat ", "]
    | _ -> []

/// Walk the token tree and build a dictionary: canonical CSS value → first token path.
let private buildTokenLookup (file: TokenFile) : Collections.Generic.Dictionary<string, string> =
    let lookup = Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
    let rec collect (path: string) (nodes: (TokenName * TokenNode) list) =
        for (name, node) in nodes do
            let p =
                if path = "" then TokenName.value name
                else sprintf "%s.%s" path (TokenName.value name)
            match node with
            | TokenLeaf t ->
                for s in tokenValueToCssStrings t.Value do
                    if not (lookup.ContainsKey s) then lookup.[s] <- p
            | Group g -> collect p g.Children
    collect "" file.Children
    lookup

/// Audit CSS text and annotate each entry with the path of a matching token in
/// <paramref name="file"/>, if any. Entries without a match have
/// <c>MatchedToken = None</c>.
///
/// Use after bootstrapping a tokens file to see which hardcoded values are already
/// covered and which still need to be migrated to token references.
let auditAgainst (cssText: string) (file: TokenFile) : AuditResult =
    let baseResult = audit cssText
    let lookup     = buildTokenLookup file
    let entries =
        baseResult.Entries |> List.map (fun e ->
            let matched =
                normalizeCssEntry e
                |> List.tryPick (fun s ->
                    match lookup.TryGetValue s with
                    | true, path -> Some path
                    | _          -> None)
            { e with MatchedToken = matched })
    { baseResult with Entries = entries }
