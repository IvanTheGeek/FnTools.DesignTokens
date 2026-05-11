module FnTools.DesignTokens.FSharp

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

/// Compute every F# identifier path that a token will occupy in the
/// generated bindings, paired with the CSS var() reference that path will
/// hold. For most tokens this is a single (fsPath, varRef); for
/// <c>ResolvedTypography</c> it expands to five entries (one per
/// sub-property — FontFamily, FontSize, FontWeight, LetterSpacing,
/// LineHeight) matching the CSS emitter expansion.
///
/// Shared by <see cref="buildTree"/> and
/// <see cref="checkIdentifierSafety"/> (ADR-038, 0.11.0) so the two
/// functions stay structurally aligned — no risk of one knowing about
/// the typography expansion and the other not.
let private expandedFsPaths
    (originalPath: string list)
    (token: ResolvedToken)
    : (string list * string) list =
    let fsPath = originalPath |> List.map toFsharpIdent
    match token.Value with
    | ResolvedTypography _ ->
        typographySubs
        |> List.map (fun (cssSuf, fsIdent) ->
            fsPath @ [fsIdent], makeCssVarRef originalPath (Some cssSuf))
    | _ ->
        [ fsPath, makeCssVarRef originalPath None ]

let private buildTree (tokens: ResolvedTokens) : Map<string, BindingNode> =
    tokens
    |> Seq.fold (fun root (path, token) ->
        expandedFsPaths path token
        |> List.fold (fun r (fsPath, varRef) ->
            insertAt fsPath (Leaf varRef) r) root
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
/// <remarks>
/// <para>
/// <b>Silent data loss warning</b>: when two tokens transform to the same F# identifier path
/// (e.g. <c>color.dark</c> and <c>color.Dark</c> both → <c>Color.Dark</c>) the underlying
/// <c>Map.add</c> silently keeps the last-encountered one and drops the rest. To detect
/// this before generation, call <see cref="checkIdentifierSafety"/> first, or use
/// <see cref="emitChecked"/> which composes both steps. ADR-038.
/// </para>
/// </remarks>
let emit (moduleName: string) (tokens: ResolvedTokens) : string =
    let tree = buildTree tokens
    let sb   = StringBuilder()
    sb.AppendLine "// <auto-generated/>" |> ignore
    sb.AppendLine (sprintf "module %s" moduleName) |> ignore
    sb.AppendLine() |> ignore
    for KeyValue(name, node) in tree do
        renderNode sb 0 name node
    sb.ToString()


// ─── Identifier-safety check (ADR-038, 0.11.0) ───────────────────────────────
//
// `emit` is structurally fragile to two patterns of DTCG token authoring that
// produce silent data loss in the generated F#:
//
//   1. Identifier collision — two DTCG paths transforming to the same F# path.
//      Example: color.dark and color.Dark both → ["Color"; "Dark"]. Map.add
//      silently keeps only the last one.
//
//   2. Leaf/branch conflict — one DTCG path produces a Leaf at an F# path that
//      another path extends as a Branch (or vice versa). Example: a token at
//      `font` (Leaf) plus a token at `font.size.sm` (extends Font as Branch).
//      insertAt sees the existing Leaf, falls through to `_ -> Map.empty`, and
//      overwrites the Leaf with the Branch.
//
// Both are silent in the current emitter and worth catching before generation.
// Non-ASCII identifiers and module nesting depth are accepted as-is in v1
// (F# handles Unicode fine; real-world nesting depth is well below F#'s limits).

/// Issues that <see cref="checkIdentifierSafety"/> reports. All currently
/// represent silent data loss in <see cref="emit"/>; neither variant is
/// informational.
type IdentifierIssue =
    /// Two or more DTCG token paths transform to the same F# identifier path.
    /// Generated F# would silently keep only the last-encountered token at
    /// that path; the others would be missing from the bindings module.
    | IdentifierCollision of fsharpPath: string list * tokenPaths: string list list
    /// A DTCG token path's F# identifier path is a strict prefix of one or
    /// more other paths' — one would be a Leaf, others extend it as a Branch.
    /// Generated F# would silently overwrite the Leaf with the Branch and
    /// lose the Leaf's value.
    | LeafBranchConflict
        of leafFsharpPath: string list
         * leafTokenPath: string list
         * extendingTokenPaths: string list list

module IdentifierIssue =
    /// One-line human-readable description of an issue.
    let format (i: IdentifierIssue) : string =
        let fmtFs (p: string list)  = String.concat "." p
        let fmtTok (p: string list) = String.concat "." p
        let fmtTokList (ps: string list list) =
            ps |> List.map fmtTok |> String.concat ", "
        match i with
        | IdentifierCollision (fs, paths) ->
            sprintf "F# identifier %s collides — %d DTCG tokens transform to the same path: %s"
                (fmtFs fs) (List.length paths) (fmtTokList paths)
        | LeafBranchConflict (fs, leafPath, extending) ->
            sprintf "F# identifier %s is both a Leaf (from %s) and a module prefix (extended by: %s)"
                (fmtFs fs) (fmtTok leafPath) (fmtTokList extending)

/// Check token paths against the F# identifier transformation that
/// <see cref="emit"/> applies, and report any patterns that would cause
/// silent data loss in the generated bindings. Returns an empty list if
/// <see cref="emit"/> is safe to call (every token will produce exactly
/// the binding the consumer expects).
///
/// Detects two kinds of issue:
/// <list type="bullet">
///   <item><see cref="IdentifierCollision"/> — multiple DTCG paths
///         producing the same F# path.</item>
///   <item><see cref="LeafBranchConflict"/> — one DTCG path produces a
///         Leaf at an F# path that another path extends as a Branch
///         (or vice versa).</item>
/// </list>
///
/// Run this before <see cref="emit"/>, or use <see cref="emitChecked"/>
/// to compose the check and emission in one call. ADR-038.
let checkIdentifierSafety
    (tokens: ResolvedTokens)
    : IdentifierIssue list =

    // For each token, materialise the (fsPath, originalDtcgPath) pairs the
    // emit pipeline would attempt to insert. Typography tokens contribute
    // five entries; everything else contributes one.
    let occupations : (string list * string list) list =
        tokens
        |> Seq.collect (fun (path, token) ->
            expandedFsPaths path token
            |> List.map (fun (fsPath, _varRef) -> fsPath, path))
        |> List.ofSeq

    // (1) Collisions — same fsPath from more than one source path.
    //     Dedupe sources per fsPath; an identical (path, token) producing
    //     the same fsPath twice doesn't count (typography token producing
    //     five distinct sub-paths is the normal case, not a collision).
    let collisions =
        occupations
        |> List.groupBy fst
        |> List.choose (fun (fsPath, occs) ->
            let distinctSources = occs |> List.map snd |> List.distinct
            if distinctSources.Length >= 2 then
                Some (IdentifierCollision (fsPath, distinctSources))
            else
                None)

    // (2) Leaf/branch conflicts — one fsPath is a strict prefix of another.
    //     Build the set of (fsPath → source paths) first so we know which
    //     fsPaths are Leaves and which are mentioned at all.
    let fsPathSet = occupations |> List.map fst |> Set.ofList
    let sourcesByFsPath =
        occupations
        |> List.groupBy fst
        |> List.map (fun (fs, occs) -> fs, occs |> List.map snd |> List.distinct)
        |> Map.ofList

    // For each fsPath that exists, check if any longer fsPath has it as a
    // strict prefix. If so, the shorter one is a Leaf that conflicts with
    // a longer Branch path.
    let isStrictPrefix (shorter: string list) (longer: string list) =
        shorter.Length < longer.Length
        && List.take shorter.Length longer = shorter

    let conflicts =
        fsPathSet
        |> Set.toList
        |> List.choose (fun leafFs ->
            let extenders =
                fsPathSet
                |> Set.toList
                |> List.filter (isStrictPrefix leafFs)
            if List.isEmpty extenders then None
            else
                let leafSources = Map.find leafFs sourcesByFsPath
                let extendingSources =
                    extenders
                    |> List.collect (fun e -> Map.find e sourcesByFsPath)
                    |> List.distinct
                // Each Leaf at leafFs is in conflict with every extending path.
                // Report once per leaf source (typically one).
                match leafSources with
                | [single] ->
                    Some (LeafBranchConflict (leafFs, single, extendingSources))
                | many ->
                    // The leafFs itself has a collision too, but report this as
                    // a conflict separately using the first source for the leaf
                    // path; the IdentifierCollision will cover the rest.
                    Some (LeafBranchConflict (leafFs, List.head many, extendingSources)))

    collisions @ conflicts

/// Emit + identifier-safety check in one call. If the check finds any
/// issues, returns <c>Error</c> with the issue list (and does not emit).
/// Otherwise returns <c>Ok</c> with the generated F# source — identical to
/// what <see cref="emit"/> would have produced.
let emitChecked
    (moduleName: string)
    (tokens: ResolvedTokens)
    : Result<string, IdentifierIssue list> =
    let tokenList = List.ofSeq tokens
    match checkIdentifierSafety tokenList with
    | [] -> Ok (emit moduleName tokenList)
    | issues -> Error issues
