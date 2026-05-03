/// CSS custom-property ingestion — converts --prefix-* declarations to DTCG 2025.10 JSON.
///
/// Supports: color (oklch, rgba), dimension (px, rem), duration (ms, s),
/// cubicBezier, fontFamily, fontWeight, number, shadow, and intra-file aliases.
/// Skips values it cannot classify with an entry in IngestWarning list.
module FnTools.DesignTokens.CssIngest

open System
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions


// ─── Public types ─────────────────────────────────────────────────────────────

type IngestWarning =
    | Skipped of name: string * reason: string

type IngestResult = {
    Json       : string
    TokenCount : int
    Warnings   : IngestWarning list
}


// ─── Low-level helpers ────────────────────────────────────────────────────────

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

let private inv = Globalization.CultureInfo.InvariantCulture

let private parseF (s: string) = Double.Parse(s, inv)

let private tryParseF (s: string) =
    match Double.TryParse(s, Globalization.NumberStyles.Float, inv) with
    | true, v -> Some v
    | _ -> None

/// { "value": n, "unit": u } — fresh object each call.
let private dimNode (v: float) (u: string) : JsonNode =
    let o = JsonObject()
    o.["value"] <- JsonValue.Create(v)
    o.["unit"]  <- JsonValue.Create(u)
    o :> JsonNode

/// { "$type": t, "$value": v }
let private tokenLeaf (t: string) (v: JsonNode) : JsonNode =
    let o = JsonObject()
    o.["$type"]  <- JsonValue.Create(t)
    o.["$value"] <- v
    o :> JsonNode

/// { "$value": "{path}" }
let private aliasLeaf (path: string) : JsonNode =
    let o = JsonObject()
    o.["$value"] <- JsonValue.Create(sprintf "{%s}" path)
    o :> JsonNode


// ─── Name → DTCG path ─────────────────────────────────────────────────────────

/// Strip --{prefix}- and split on the first hyphen → (group, leaf).
/// "accent" (no hyphen) → ("accent", "default").
let private toGroupLeaf (prefix: string) (propName: string) : (string * string) option =
    let pfxDash = prefix + "-"
    if not (propName.StartsWith pfxDash) then None
    else
        let remaining = propName.Substring(pfxDash.Length)
        let idx = remaining.IndexOf('-')
        if idx < 0 then Some (remaining, "default")
        else
            let grp  = remaining.Substring(0, idx)
            let leaf = remaining.Substring(idx + 1)
            Some (grp, leaf)

/// var(--{prefix}-a[-b]) → "a.b" or "a.default".  Returns None for cross-prefix refs.
let private varToPath (prefix: string) (varExpr: string) : string option =
    let m = Regex.Match(varExpr.Trim(), @"^var\(--([a-z0-9-]+)(?:,.*?)?\)$")
    if not m.Success then None
    else
        let full    = m.Groups.[1].Value
        let pfxDash = prefix + "-"
        if not (full.StartsWith pfxDash) then None
        else
            let remaining = full.Substring(pfxDash.Length)
            let idx = remaining.IndexOf('-')
            if idx < 0 then Some (remaining + ".default")
            else Some (remaining.Substring(0, idx) + "." + remaining.Substring(idx + 1))


// ─── Value parsers ────────────────────────────────────────────────────────────

let private tryParseOklch (s: string) : JsonNode option =
    let m = Regex.Match(s.Trim(),
        @"^oklch\(\s*(-?\d+(?:\.\d+)?)(%?)\s+(-?\d+(?:\.\d+)?)\s+(-?\d+(?:\.\d+)?)\s*\)$")
    if not m.Success then None
    else
        let lRaw = parseF m.Groups.[1].Value
        let l    = if m.Groups.[2].Value = "%" then lRaw / 100.0 else lRaw
        let c    = parseF m.Groups.[3].Value
        let h    = parseF m.Groups.[4].Value
        let comps = JsonArray()
        comps.Add(JsonValue.Create(l))
        comps.Add(JsonValue.Create(c))
        comps.Add(JsonValue.Create(h))
        let v = JsonObject()
        v.["colorSpace"] <- JsonValue.Create("oklch")
        v.["components"] <- comps
        Some (tokenLeaf "color" (v :> JsonNode))

let private tryParseRgba (s: string) : JsonNode option =
    let m = Regex.Match(s.Trim(),
        @"^rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(-?\d+(?:\.\d+)?)\s*\)$")
    if not m.Success then None
    else
        let ch (i: int) = parseF m.Groups.[i].Value / 255.0
        let r = ch 1
        let g = ch 2
        let b = ch 3
        let a = parseF m.Groups.[4].Value
        let comps = JsonArray()
        comps.Add(JsonValue.Create(r))
        comps.Add(JsonValue.Create(g))
        comps.Add(JsonValue.Create(b))
        let v = JsonObject()
        v.["colorSpace"] <- JsonValue.Create("srgb")
        v.["components"] <- comps
        if a <> 1.0 then v.["alpha"] <- JsonValue.Create(a)
        Some (tokenLeaf "color" (v :> JsonNode))

let private tryParseDimension (s: string) : JsonNode option =
    let m = Regex.Match(s.Trim(), @"^(-?\d+(?:\.\d+)?)(px|rem)$")
    if not m.Success then None
    else
        Some (tokenLeaf "dimension" (dimNode (parseF m.Groups.[1].Value) m.Groups.[2].Value))

let private tryParseDuration (s: string) : JsonNode option =
    let m = Regex.Match(s.Trim(), @"^(-?\d+(?:\.\d+)?)(ms|s)$")
    if not m.Success then None
    else
        Some (tokenLeaf "duration" (dimNode (parseF m.Groups.[1].Value) m.Groups.[2].Value))

let private tryParseCubicBezier (s: string) : JsonNode option =
    let m = Regex.Match(s.Trim(),
        @"^cubic-bezier\(\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\)$")
    if not m.Success then None
    else
        let p (i: int) = parseF m.Groups.[i].Value
        let arr = JsonArray()
        arr.Add(JsonValue.Create(p 1))
        arr.Add(JsonValue.Create(p 2))
        arr.Add(JsonValue.Create(p 3))
        arr.Add(JsonValue.Create(p 4))
        Some (tokenLeaf "cubicBezier" (arr :> JsonNode))

let private tryParseFontFamily (s: string) : JsonNode option =
    if not (s.Contains "'") then None
    else
        let families =
            splitDepth0 ',' s
            |> List.map (fun f ->
                let t = f.Trim()
                if t.StartsWith "'" && t.EndsWith "'" then t.[1..t.Length - 2]
                elif t.StartsWith "\"" && t.EndsWith "\"" then t.[1..t.Length - 2]
                else t)
            |> List.filter (fun f -> f.Length > 0)
        if families.IsEmpty then None
        else
            let arr = JsonArray()
            for f in families do arr.Add(JsonValue.Create(f))
            Some (tokenLeaf "fontFamily" (arr :> JsonNode))

/// Handles bare numbers (line-height, letter-spacing) and numbers with em (stripped).
/// nameParts used to detect fontWeight context.
let private tryParseNumber (nameParts: string list) (s: string) : JsonNode option =
    let stripped = if s.EndsWith "em" then s.[..s.Length - 3] else s
    match tryParseF stripped with
    | None -> None
    | Some v ->
        let t =
            if nameParts |> List.exists (fun p -> p.Contains "weight") then "fontWeight"
            else "number"
        Some (tokenLeaf t (JsonValue.Create(v) :> JsonNode))


// ─── Shadow parsing ───────────────────────────────────────────────────────────

/// Parse a color expression from inside a shadow layer (rgba, oklch, or var alias).
/// Returns the color node suitable for shadow["color"] — an object or alias string.
let private parseShadowColor (prefix: string) (s: string) : JsonNode option =
    let s = s.Trim()
    // Extract $value object from a color tokenLeaf
    let extractValue (leafNode: JsonNode) =
        match leafNode with
        | :? JsonObject as o -> o.["$value"] |> Option.ofObj |> Option.map (fun n -> n.DeepClone())
        | _ -> None
    if s.StartsWith "rgba" then
        tryParseRgba s |> Option.bind extractValue
    elif s.StartsWith "oklch" then
        tryParseOklch s |> Option.bind extractValue
    elif s.StartsWith "var(" then
        varToPath prefix s |> Option.map (fun path -> JsonValue.Create(sprintf "{%s}" path) :> JsonNode)
    else None

/// Parse "0" or "Npx" → dimension node.
let private parseShadowDim (s: string) : JsonNode option =
    let s = s.Trim()
    if s = "0" then Some (dimNode 0.0 "px")
    else
        let m = Regex.Match(s, @"^(-?\d+(?:\.\d+)?)px$")
        if m.Success then Some (dimNode (parseF m.Groups.[1].Value) "px")
        else None

/// Parse one shadow layer: "0 1px 2px rgba(...)" or "0 0 0 3px var(...)".
let private parseShadowLayer (prefix: string) (layer: string) : JsonNode option =
    let parts = splitDepth0 ' ' layer |> List.filter (fun s -> s <> "")
    // Need at least 4 parts: offsetX offsetY blur color  (spread is optional between blur and color)
    if parts.Length < 4 then None
    else
        let colorPart = List.last parts
        let dimParts  = List.take (parts.Length - 1) parts
        match parseShadowColor prefix colorPart with
        | None -> None
        | Some colorNode ->
            let dims = dimParts |> List.map parseShadowDim
            if dims |> List.exists Option.isNone then None
            else
                let d = dims |> List.map Option.get
                let dimResult =
                    match d with
                    | [x; y; b]    -> Some (x, y, b, dimNode 0.0 "px")
                    | [x; y; b; s] -> Some (x, y, b, s)
                    | _            -> None  // unexpected dim count; skip
                match dimResult with
                | None -> None
                | Some (ox, oy, bl, sp) ->
                let o = JsonObject()
                o.["color"]   <- colorNode
                o.["offsetX"] <- ox
                o.["offsetY"] <- oy
                o.["blur"]    <- bl
                o.["spread"]  <- sp
                Some (o :> JsonNode)

let private tryParseShadow (prefix: string) (s: string) : JsonNode option =
    let layers = splitDepth0 ',' s |> List.filter (fun l -> l <> "")
    if layers.IsEmpty then None
    else
        let parsed = layers |> List.choose (parseShadowLayer prefix)
        if parsed.Length <> layers.Length then None  // some layers failed
        else
            let v : JsonNode =
                if parsed.Length = 1 then parsed.[0]
                else
                    let arr = JsonArray()
                    for p in parsed do arr.Add(p)
                    arr :> JsonNode
            Some (tokenLeaf "shadow" v)


// ─── Token inference ──────────────────────────────────────────────────────────

let private inferToken (prefix: string) (grp: string) (leaf: string) (value: string)
    : JsonNode option * IngestWarning list =
    let nameParts = [grp; leaf]
    let fullName  = sprintf "--%s-%s-%s" prefix grp leaf
    let result =
        tryParseOklch value
        |> Option.orElseWith (fun () -> tryParseRgba value)
        |> Option.orElseWith (fun () -> tryParseDimension value)
        |> Option.orElseWith (fun () -> tryParseDuration value)
        |> Option.orElseWith (fun () -> tryParseCubicBezier value)
        |> Option.orElseWith (fun () -> tryParseFontFamily value)
        |> Option.orElseWith (fun () ->
            if value.TrimStart().StartsWith "var(" then
                varToPath prefix value |> Option.map aliasLeaf
            else None)
        |> Option.orElseWith (fun () -> tryParseShadow prefix value)
        |> Option.orElseWith (fun () -> tryParseNumber nameParts value)
    match result with
    | Some node -> Some node, []
    | None ->
        None, [Skipped (fullName, sprintf "unrecognized value: %s" (value.[..min 60 (value.Length - 1)]))]


// ─── CSS extraction ──────────────────────────────────────────────────────────

/// Extract the text content of the first block matching `selector { ... }`.
let private extractBlockContent (selector: string) (text: string) : string =
    let idx = text.IndexOf(selector)
    if idx < 0 then ""
    else
        let mutable i = idx + selector.Length
        while i < text.Length && text.[i] <> '{' do i <- i + 1
        if i >= text.Length then ""
        else
            i <- i + 1
            let start = i
            let mutable depth = 1
            while i < text.Length && depth > 0 do
                if   text.[i] = '{' then depth <- depth + 1
                elif text.[i] = '}' then depth <- depth - 1
                i <- i + 1
            text.Substring(start, i - start - 1)

/// Remove all /* ... */ block comments from s.
let private stripBlockComments (s: string) : string =
    let sb = StringBuilder(s.Length)
    let mutable i = 0
    while i < s.Length do
        if i + 1 < s.Length && s.[i] = '/' && s.[i + 1] = '*' then
            i <- i + 2
            while i + 1 < s.Length && not (s.[i] = '*' && s.[i + 1] = '/') do
                i <- i + 1
            i <- i + 2  // skip */
        else
            sb.Append(s.[i]) |> ignore
            i <- i + 1
    sb.ToString()

/// Extract all --name: value; pairs from a CSS block string.
/// Comments are stripped first; values are collected until `;` at parenthesis depth 0.
/// Only names whose every character is alphanumeric or hyphen are kept (rejects
/// comment-artifact pseudo-names that survived stripping).
let private isValidCssPropName (s: string) : bool =
    s.Length > 0 && s |> Seq.forall (fun c -> Char.IsAsciiLetterOrDigit c || c = '-')

let private extractDeclarations (block: string) : (string * string) list =
    let block = stripBlockComments block
    let results = ResizeArray<string * string>()
    let mutable i = 0
    while i < block.Length do
        let dashIdx = block.IndexOf("--", i)
        if dashIdx < 0 then i <- block.Length
        else
            let colonIdx = block.IndexOf(':', dashIdx)
            if colonIdx < 0 then i <- block.Length
            else
                let name = block.Substring(dashIdx + 2, colonIdx - dashIdx - 2).Trim()
                if not (isValidCssPropName name) then
                    i <- colonIdx + 1
                else
                    let mutable j     = colonIdx + 1
                    let mutable depth = 0
                    let sb = StringBuilder()
                    while j < block.Length && not (block.[j] = ';' && depth = 0) do
                        if   block.[j] = '(' then depth <- depth + 1
                        elif block.[j] = ')' then depth <- depth - 1
                        sb.Append(block.[j]) |> ignore
                        j <- j + 1
                    let value = sb.ToString().Trim()
                    if value.Length > 0 then
                        results.Add((name, value))
                    i <- j + 1
    List.ofSeq results


// ─── Main entry point ─────────────────────────────────────────────────────────

/// Ingest CSS custom properties with the given prefix from :root { } declarations.
///
/// prefix  — the CSS variable prefix without dashes, e.g. "cb" for --cb-*
///
/// Name mapping: --{prefix}-group[-leaf] → DTCG path group.leaf
/// (no leaf → uses "default"; e.g. --cb-accent → accent.default)
///
/// Cross-prefix var() references (e.g. --cb-* referenced from --ll-*) are skipped
/// with a warning — they require a resolver document to express correctly.
let ingest (cssText: string) (prefix: string) : IngestResult =
    let root = JsonObject()
    root.["$schema"] <- JsonValue.Create("https://design-tokens.org/schemas/2025.10/format.schema.json")

    let block        = extractBlockContent ":root" cssText
    let declarations = extractDeclarations block

    let mutable tokenCount = 0
    let warnings = ResizeArray<IngestWarning>()

    for (propName, value) in declarations do
        match toGroupLeaf prefix propName with
        | None -> ()
        | Some (grp, lf) ->
            match inferToken prefix grp lf value with
            | None, warns ->
                warnings.AddRange warns
            | Some tokenNode, warns ->
                warnings.AddRange warns
                let groupObj =
                    match root.[grp] with
                    | :? JsonObject as o -> o
                    | _ ->
                        let o = JsonObject()
                        root.[grp] <- o
                        o
                groupObj.[lf] <- tokenNode
                tokenCount <- tokenCount + 1

    let opts = JsonSerializerOptions(WriteIndented = true)
    { Json = root.ToJsonString(opts); TokenCount = tokenCount; Warnings = List.ofSeq warnings }
