module FnTools.DesignTokens.Bindings

open System
open System.Text
open FnTools.DesignTokens


// ─── F# identifier conversion ────────────────────────────────────────────────

/// <summary>
/// Converts a single DTCG token path segment to a valid PascalCase F# identifier.
/// </summary>
/// <remarks>
/// Rules applied in order:
/// <list type="number">
///   <item>Split on hyphens — each part is processed independently, then joined.</item>
///   <item>Parts starting with a digit receive an <c>N</c> prefix (<c>500</c> → <c>N500</c>).</item>
///   <item>All other parts have their first character uppercased.</item>
/// </list>
/// The result is always a valid F# identifier. Because F# keywords are all lowercase
/// and this rule always capitalises the first character, no backtick-escaping is needed
/// in practice (e.g. <c>default</c> → <c>Default</c>, which is not a reserved word).
/// </remarks>
let toFsharpIdent (segment: string) : string =
    segment.Split('-')
    |> Array.map (fun part ->
        if part.Length = 0 then part
        elif Char.IsDigit part.[0] then "N" + part
        else Char.ToUpperInvariant(part.[0]).ToString() + part.[1..])
    |> String.concat ""


// ─── Binding tree ────────────────────────────────────────────────────────────

/// Internal token hierarchy used during code generation.
type private BindingNode =
    | Leaf   of varRef: string
    | Branch of children: Map<string, BindingNode>

// Sub-property suffixes that typography tokens expand to, mirroring the CSS emitter.
let private typographySubs =
    [ "font-family",    "FontFamily"
      "font-size",      "FontSize"
      "font-weight",    "FontWeight"
      "letter-spacing", "LetterSpacing"
      "line-height",    "LineHeight" ]

let private insertAt (fsPath: string list) (leaf: BindingNode) (root: Map<string, BindingNode>) =
    let rec go segs (m: Map<string, BindingNode>) =
        match segs with
        | []         -> m
        | [last]     -> Map.add last leaf m
        | head :: tl ->
            let child = match Map.tryFind head m with Some (Branch b) -> b | _ -> Map.empty
            Map.add head (Branch (go tl child)) m
    go fsPath root

let private makeCssVarRef (originalPath: string list) (suffix: string option) : string =
    let base' = "--" + String.concat "-" originalPath
    let full  = match suffix with Some s -> base' + "-" + s | None -> base'
    "var(" + full + ")"

let private buildTree (tokens: (string list * ResolvedToken) seq) : Map<string, BindingNode> =
    tokens
    |> Seq.fold (fun root (path, token) ->
        let fsPath = path |> List.map toFsharpIdent
        match token.Value with
        | ResolvedTypography _ ->
            typographySubs
            |> List.fold (fun r (cssSuf, fsIdent) ->
                let leaf = Leaf (makeCssVarRef path (Some cssSuf))
                insertAt (fsPath @ [fsIdent]) leaf r) root
        | _ ->
            insertAt fsPath (Leaf (makeCssVarRef path None)) root
    ) Map.empty


// ─── Rendering ───────────────────────────────────────────────────────────────

let rec private renderNode (sb: StringBuilder) (indent: int) (name: string) (node: BindingNode) =
    let pad = String.replicate indent "    "
    match node with
    | Leaf varRef ->
        sb.AppendLine(sprintf "%slet %s = \"%s\"" pad name varRef) |> ignore
    | Branch children ->
        sb.AppendLine(sprintf "%smodule %s =" pad name) |> ignore
        for KeyValue(childName, child) in children do
            renderNode sb (indent + 1) childName child
        if indent = 0 then
            sb.AppendLine() |> ignore


// ─── Public API ──────────────────────────────────────────────────────────────

/// <summary>
/// Emits a resolved DTCG token tree as F# source code with nested <c>module</c> declarations.
/// </summary>
/// <remarks>
/// <para>
/// Each token path segment is converted to a PascalCase F# identifier (see <see cref="toFsharpIdent"/>).
/// The generated module contains only <c>let</c> bindings and nested <c>module</c> blocks — it has
/// no runtime dependency on <c>FnTools.DesignTokens</c> or any other library.
/// </para>
/// <para>
/// Each token value becomes a <c>string</c> constant holding the CSS <c>var()</c> reference,
/// e.g. <c>"var(--color-text-primary)"</c>. Use these directly as values in Fun.Css property builders
/// or any CSS-in-F# approach.
/// </para>
/// <para>
/// <see cref="ResolvedTokenValue.ResolvedTypography"/> tokens expand to five sub-properties
/// (<c>FontFamily</c>, <c>FontSize</c>, <c>FontWeight</c>, <c>LetterSpacing</c>, <c>LineHeight</c>),
/// mirroring the CSS emitter expansion.
/// </para>
/// </remarks>
/// <param name="moduleName">Top-level F# module name for the generated file (e.g. <c>"Tokens"</c>).</param>
/// <param name="tokens">Flat resolved token pairs — output of <c>Api.import</c> or <c>Api.importWithResolver</c>.</param>
/// <returns>F# source text ready to write to a <c>.fs</c> file.</returns>
let emit (moduleName: string) (tokens: (string list * ResolvedToken) seq) : string =
    let tree = buildTree tokens
    let sb   = StringBuilder()
    sb.AppendLine "// <auto-generated/>" |> ignore
    sb.AppendLine (sprintf "module %s" moduleName) |> ignore
    sb.AppendLine() |> ignore
    for KeyValue(name, node) in tree do
        renderNode sb 0 name node
    sb.ToString()
