module FnTools.DesignTokens.Api

open System
open FnTools.DesignTokens


// ─── Helpers ─────────────────────────────────────────────────────────────────

let private collect (xs: Result<'a, 'e list> seq) : Result<'a list, 'e list> =
    let oks = ResizeArray()
    let errs = ResizeArray()
    for x in xs do
        match x with Ok v -> oks.Add v | Error es -> errs.AddRange es
    if errs.Count > 0 then Error (List.ofSeq errs) else Ok (List.ofSeq oks)


// ─── Flatten + tryFind ──────────────────────────────────────────────────────

let private indexTokens (file: TokenFile) : Map<string, Token * string list> =
    let acc = ResizeArray<string * (Token * string list)>()
    let rec walk (path: string list) (node: TokenNode) =
        match node with
        | TokenLeaf t ->
            acc.Add (String.concat "." path, (t, path))
        | Group g ->
            match g.Root with
            | Some t -> acc.Add (String.concat "." path, (t, path))
            | None -> ()
            for (name, child) in g.Children do
                walk (path @ [TokenName.value name]) child
    for (name, node) in file.Children do
        walk [TokenName.value name] node
    Map.ofSeq acc

let private flattenFile (file: TokenFile) : (string list * Token) seq =
    let acc = ResizeArray<string list * Token>()
    let rec walk (inheritedType: TokenType option) (path: string list) (node: TokenNode) =
        match node with
        | TokenLeaf t ->
            let resolvedType = t.Type |> Option.orElse inheritedType
            let withType = { t with Type = resolvedType }
            acc.Add (path, withType)
        | Group g ->
            let groupType = g.Type |> Option.orElse inheritedType
            match g.Root with
            | Some t ->
                let rt = { t with Type = t.Type |> Option.orElse groupType }
                acc.Add (path, rt)
            | None -> ()
            for (name, child) in g.Children do
                walk groupType (path @ [TokenName.value name]) child
    for (name, node) in file.Children do
        walk None [TokenName.value name] node
    seq { for x in acc -> x }

let private tryFindIn (path: string list) (file: TokenFile) : Token option =
    let rec descend (segs: string list) (children: (TokenName * TokenNode) list) (inheritedType: TokenType option) =
        match segs with
        | [] -> None
        | [last] ->
            children
            |> List.tryFind (fun (n, _) -> TokenName.value n = last)
            |> Option.bind (fun (_, node) ->
                match node with
                | TokenLeaf t ->
                    Some { t with Type = t.Type |> Option.orElse inheritedType }
                | Group g ->
                    match g.Root with
                    | Some t -> Some { t with Type = t.Type |> Option.orElse (g.Type |> Option.orElse inheritedType) }
                    | None -> None)
        | head :: tail ->
            children
            |> List.tryFind (fun (n, _) -> TokenName.value n = head)
            |> Option.bind (fun (_, node) ->
                match node with
                | TokenLeaf _ -> None
                | Group g ->
                    let nextType = g.Type |> Option.orElse inheritedType
                    descend tail g.Children nextType)
    descend path file.Children None


// ─── Alias chain resolution (one chain) ─────────────────────────────────────

let private tryResolveAliasIn (ref: TokenRef) (file: TokenFile) : Token option =
    let rec follow (visiting: Set<string>) (r: TokenRef) =
        match r with
        | CurlyBrace target ->
            let key = String.concat "." target
            if Set.contains key visiting then None
            else
                match tryFindIn target file with
                | Some t ->
                    match t.Value with
                    | TokenValue.Alias r' -> follow (Set.add key visiting) r'
                    | _ -> Some t
                | None -> None
        | JsonPointer _ -> None
    follow Set.empty ref


// ─── Resolution to ResolvedToken ────────────────────────────────────────────

let private inferType = TokenValue.inferType

/// Resolve a ValueOrRef<'T> by extracting the literal — references resolved by lookup.
let private resolveValueOrRef
    (extract: TokenValue -> 'T option)
    (typeName: string)
    (path: string)
    (file: TokenFile)
    (vor: ValueOrRef<'T>)
    : Result<'T, ValidationError list> =
    match vor with
    | Literal v -> Ok v
    | Reference r ->
        match tryResolveAliasIn r file with
        | None ->
            let refStr =
                match r with
                | CurlyBrace path -> "{" + String.concat "." path + "}"
                | JsonPointer p -> p
            Error [UnresolvedReference (path, refStr)]
        | Some t ->
            match extract t.Value with
            | Some v -> Ok v
            | None ->
                Error [TypeMismatch (path, typeName, "different value type")]

let private extractColor v =
    match v with TokenValue.Color c -> Some c | _ -> None
let private extractDimension v =
    match v with TokenValue.Dimension d -> Some d | _ -> None
let private extractDuration v =
    match v with TokenValue.Duration d -> Some d | _ -> None
let private extractCubicBezier v =
    match v with TokenValue.CubicBezier c -> Some c | _ -> None
let private extractStrokeStyleRaw v =
    match v with TokenValue.StrokeStyle s -> Some s | _ -> None
let private extractFontFamily v =
    match v with TokenValue.FontFamily f -> Some f | _ -> None
let private extractFontWeight v =
    match v with TokenValue.FontWeight f -> Some f | _ -> None
let private extractNumber v =
    match v with TokenValue.Number n -> Some n | _ -> None
let private extractShadowObject v =
    match v with
    | TokenValue.Shadow (ShadowSingle s) -> Some s
    | _ -> None

/// Resolve a stroke-style value's references inside a custom dash array.
let rec private resolveStrokeStyle
    (path: string)
    (file: TokenFile)
    (s: StrokeStyleValue)
    : Result<ResolvedStrokeStyleValue, ValidationError list> =
    match s with
    | StrokeKeyword k -> Ok (ResolvedStrokeKeyword k)
    | StrokeCustom obj ->
        let dashesR =
            obj.DashArray
            |> List.mapi (fun i d ->
                resolveValueOrRef extractDimension "dimension" (sprintf "%s.dashArray[%d]" path i) file d)
            |> collect
        dashesR |> Result.map (fun das ->
            ResolvedStrokeCustom { DashArray = das; LineCap = obj.LineCap })

let private resolveBorder
    (path: string)
    (file: TokenFile)
    (b: BorderValue)
    : Result<ResolvedBorderValue, ValidationError list> =
    let colorR  = resolveValueOrRef extractColor "color" (path + ".color") file b.Color
    let widthR  = resolveValueOrRef extractDimension "dimension" (path + ".width") file b.Width
    let styleR =
        match b.Style with
        | Literal s -> resolveStrokeStyle (path + ".style") file s
        | Reference r ->
            match tryResolveAliasIn r file with
            | None ->
                let refStr =
                    match r with
                    | CurlyBrace p -> "{" + String.concat "." p + "}"
                    | JsonPointer p -> p
                Error [UnresolvedReference (path + ".style", refStr)]
            | Some t ->
                match t.Value with
                | TokenValue.StrokeStyle s -> resolveStrokeStyle (path + ".style") file s
                | _ -> Error [TypeMismatch (path + ".style", "strokeStyle", "different value type")]
    match colorR, widthR, styleR with
    | Ok c, Ok w, Ok s -> Ok { Color = c; Width = w; Style = s }
    | _ ->
        [ colorR |> Result.map ignore; widthR |> Result.map ignore; styleR |> Result.map ignore ]
        |> List.collect (function Error es -> es | Ok _ -> [])
        |> Error

let private resolveShadowObject
    (path: string)
    (file: TokenFile)
    (s: ShadowObject)
    : Result<ResolvedShadowObject, ValidationError list> =
    let colorR = resolveValueOrRef extractColor "color" (path + ".color") file s.Color
    let oxR = resolveValueOrRef extractDimension "dimension" (path + ".offsetX") file s.OffsetX
    let oyR = resolveValueOrRef extractDimension "dimension" (path + ".offsetY") file s.OffsetY
    let blR = resolveValueOrRef extractDimension "dimension" (path + ".blur") file s.Blur
    let spR = resolveValueOrRef extractDimension "dimension" (path + ".spread") file s.Spread
    match colorR, oxR, oyR, blR, spR with
    | Ok c, Ok ox, Ok oy, Ok bl, Ok sp ->
        Ok { Color = c; OffsetX = ox; OffsetY = oy; Blur = bl; Spread = sp; Inset = s.Inset }
    | _ ->
        [ colorR |> Result.map ignore; oxR |> Result.map ignore; oyR |> Result.map ignore
          blR |> Result.map ignore; spR |> Result.map ignore ]
        |> List.collect (function Error es -> es | Ok _ -> [])
        |> Error

let private resolveShadow
    (path: string)
    (file: TokenFile)
    (s: ShadowValue)
    : Result<ResolvedShadowValue, ValidationError list> =
    match s with
    | ShadowSingle obj ->
        resolveShadowObject path file obj |> Result.map ResolvedShadowSingle
    | ShadowMultiple xs ->
        xs
        |> List.mapi (fun i item ->
            let p = sprintf "%s[%d]" path i
            match item with
            | ShadowLiteral obj -> resolveShadowObject p file obj
            | ShadowReference r ->
                match tryResolveAliasIn r file with
                | None ->
                    let refStr =
                        match r with
                        | CurlyBrace p2 -> "{" + String.concat "." p2 + "}"
                        | JsonPointer p2 -> p2
                    Error [UnresolvedReference (p, refStr)]
                | Some t ->
                    match t.Value with
                    | TokenValue.Shadow (ShadowSingle obj) -> resolveShadowObject p file obj
                    | _ -> Error [TypeMismatch (p, "shadow", "different value type")])
        |> collect
        |> Result.map ResolvedShadowMultiple

let private resolveTransition
    (path: string)
    (file: TokenFile)
    (t: TransitionValue)
    : Result<ResolvedTransitionValue, ValidationError list> =
    let durR = resolveValueOrRef extractDuration "duration" (path + ".duration") file t.Duration
    let delR = resolveValueOrRef extractDuration "duration" (path + ".delay") file t.Delay
    let tfR  = resolveValueOrRef extractCubicBezier "cubicBezier" (path + ".timingFunction") file t.TimingFunction
    match durR, delR, tfR with
    | Ok d, Ok dl, Ok tf -> Ok { Duration = d; Delay = dl; TimingFunction = tf }
    | _ ->
        [ durR |> Result.map ignore; delR |> Result.map ignore; tfR |> Result.map ignore ]
        |> List.collect (function Error es -> es | Ok _ -> [])
        |> Error

let private resolveGradient
    (path: string)
    (file: TokenFile)
    (g: GradientValue)
    : Result<ResolvedGradientValue, ValidationError list> =
    g
    |> List.mapi (fun i stop ->
        let p = sprintf "%s[%d]" path i
        let colorR = resolveValueOrRef extractColor "color" (p + ".color") file stop.Color
        let posR =
            match stop.Position with
            | Literal f -> Ok f
            | Reference r ->
                match tryResolveAliasIn r file with
                | None ->
                    let refStr =
                        match r with
                        | CurlyBrace p2 -> "{" + String.concat "." p2 + "}"
                        | JsonPointer p2 -> p2
                    Error [UnresolvedReference (p + ".position", refStr)]
                | Some t ->
                    match t.Value with
                    | TokenValue.Number n -> Ok n
                    | _ -> Error [TypeMismatch (p + ".position", "number", "different value type")]
        match colorR, posR with
        | Ok c, Ok pos -> Ok ({ Color = c; Position = pos } : ResolvedGradientStop)
        | _ ->
            [ colorR |> Result.map ignore; posR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error)
    |> collect

let private resolveTypography
    (path: string)
    (file: TokenFile)
    (t: TypographyValue)
    : Result<ResolvedTypographyValue, ValidationError list> =
    let famR = resolveValueOrRef extractFontFamily "fontFamily" (path + ".fontFamily") file t.FontFamily
    let sizeR = resolveValueOrRef extractDimension "dimension" (path + ".fontSize") file t.FontSize
    let wtR = resolveValueOrRef extractFontWeight "fontWeight" (path + ".fontWeight") file t.FontWeight
    let lsR = resolveValueOrRef extractDimension "dimension" (path + ".letterSpacing") file t.LetterSpacing
    let lhR =
        match t.LineHeight with
        | Literal f -> Ok f
        | Reference r ->
            match tryResolveAliasIn r file with
            | None ->
                let refStr =
                    match r with
                    | CurlyBrace p -> "{" + String.concat "." p + "}"
                    | JsonPointer p -> p
                Error [UnresolvedReference (path + ".lineHeight", refStr)]
            | Some tk ->
                match tk.Value with
                | TokenValue.Number n -> Ok n
                | _ -> Error [TypeMismatch (path + ".lineHeight", "number", "different value type")]
    match famR, sizeR, wtR, lsR, lhR with
    | Ok f, Ok s, Ok w, Ok l, Ok h ->
        Ok { FontFamily = f; FontSize = s; FontWeight = w; LetterSpacing = l; LineHeight = h }
    | _ ->
        [ famR |> Result.map ignore; sizeR |> Result.map ignore; wtR |> Result.map ignore
          lsR |> Result.map ignore; lhR |> Result.map ignore ]
        |> List.collect (function Error es -> es | Ok _ -> [])
        |> Error

let private toResolvedValue
    (path: string)
    (file: TokenFile)
    (v: TokenValue)
    : Result<ResolvedTokenValue, ValidationError list> =
    match v with
    | TokenValue.Color c       -> Ok (ResolvedColor c)
    | TokenValue.Dimension d   -> Ok (ResolvedDimension d)
    | TokenValue.FontFamily f  -> Ok (ResolvedFontFamily f)
    | TokenValue.FontWeight f  -> Ok (ResolvedFontWeight f)
    | TokenValue.Duration d    -> Ok (ResolvedDuration d)
    | TokenValue.CubicBezier c -> Ok (ResolvedCubicBezier c)
    | TokenValue.Number n      -> Ok (ResolvedNumber n)
    | TokenValue.StrokeStyle s -> resolveStrokeStyle path file s |> Result.map ResolvedStrokeStyle
    | TokenValue.Border b      -> resolveBorder path file b |> Result.map ResolvedBorder
    | TokenValue.Shadow s      -> resolveShadow path file s |> Result.map ResolvedShadow
    | TokenValue.Transition t  -> resolveTransition path file t |> Result.map ResolvedTransition
    | TokenValue.Gradient g    -> resolveGradient path file g |> Result.map ResolvedGradient
    | TokenValue.Typography t  -> resolveTypography path file t |> Result.map ResolvedTypography
    | TokenValue.Alias _       ->
        Error [ConstraintViolation (path, "alias not resolved before flattening")]

let private flattenResolvedFile
    (file: TokenFile)
    : Result<(string list * ResolvedToken) seq, ValidationError list> =
    let raw = flattenFile file
    let resolved =
        raw
        |> Seq.map (fun (path, t) ->
            let pathStr = String.concat "." path
            // Follow Alias chain first if needed
            let finalToken =
                match t.Value with
                | TokenValue.Alias r ->
                    match tryResolveAliasIn r file with
                    | Some target -> Ok { target with Type = target.Type |> Option.orElse t.Type }
                    | None ->
                        let refStr =
                            match r with
                            | CurlyBrace p -> "{" + String.concat "." p + "}"
                            | JsonPointer p -> p
                        Error [UnresolvedReference (pathStr, refStr)]
                | _ -> Ok t

            match finalToken with
            | Error es -> Error es
            | Ok ft ->
                let inferred = inferType ft.Value
                match ft.Type |> Option.orElse inferred with
                | None ->
                    Error [ConstraintViolation (pathStr, "cannot determine $type")]
                | Some tt ->
                    toResolvedValue pathStr file ft.Value
                    |> Result.map (fun rv ->
                        path, { Value = rv; Type = tt; Metadata = ft.Metadata }))
        |> List.ofSeq

    collect resolved |> Result.map (fun xs -> upcast xs)


// ─── Public API ──────────────────────────────────────────────────────────────

let import (jsonText: string) : Result<(string list * ResolvedToken) seq, ImportError list> =
    match Format.parse jsonText with
    | Error es -> Error [ParseFailed es]
    | Ok file ->
        match Validation.validate file with
        | Error es -> Error [ValidationFailed es]
        | Ok () ->
            match flattenResolvedFile file with
            | Error es -> Error [ValidationFailed es]
            | Ok seq -> Ok seq

let importWithResolver
    (loadFile: string -> Result<string, string>)
    (context: Map<string, string>)
    (jsonText: string)
    : Result<(string list * ResolvedToken) seq, ImportError list> =
    match Resolver.parseResolver jsonText with
    | Error es -> Error [ParseFailed es]
    | Ok doc ->
        match Validation.validateResolver doc with
        | Error es -> Error [ValidationFailed es]
        | Ok () ->
            match Resolver.resolve loadFile context doc with
            | Error es -> Error [ResolveFailed es]
            | Ok merged ->
                match Validation.validate merged with
                | Error es -> Error [ValidationFailed es]
                | Ok () ->
                    match flattenResolvedFile merged with
                    | Error es -> Error [ValidationFailed es]
                    | Ok seq -> Ok seq

/// Like flattenResolvedFile but partial-success: returns resolved tokens alongside any
/// unresolved-alias or type-inference errors rather than failing the whole batch.
let private partialFlattenResolvedFile
    (file: TokenFile)
    : (string list * ResolvedToken) list * (string * string) list =
    let oks  = ResizeArray<string list * ResolvedToken>()
    let errs = ResizeArray<string * string>()
    flattenFile file
    |> Seq.iter (fun (path, t) ->
        let pathStr = String.concat "." path
        let finalToken =
            match t.Value with
            | TokenValue.Alias r ->
                match tryResolveAliasIn r file with
                | Some target ->
                    // Alias token's own $type takes precedence over the target's type.
                    // This preserves intended semantics when an alias token has an explicit
                    // $type annotation that differs from its target — e.g. a spacing.sm
                    // (dimension) that aliases to scale.sm (number). Without this, the
                    // dimension annotation is silently discarded and the unit is lost.
                    Ok { target with Type = t.Type |> Option.orElse target.Type }
                | None ->
                    let refStr =
                        match r with
                        | CurlyBrace p -> "{" + String.concat "." p + "}"
                        | JsonPointer p -> p
                    Error (pathStr, refStr)
            | _ -> Ok t
        match finalToken with
        | Error e -> errs.Add e
        | Ok ft ->
            let inferred = inferType ft.Value
            match ft.Type |> Option.orElse inferred with
            | None -> errs.Add (pathStr, "cannot determine $type")
            | Some tt ->
                // Coerce bare numbers to the annotated type when the annotation and value
                // type differ. Handles spacing/radius alias chains where the resolved
                // value is a number but the token carries an explicit dimension annotation.
                let coercedValue =
                    match tt, ft.Value with
                    | DimensionType, TokenValue.Number n ->
                        TokenValue.Dimension { Value = n; Unit = Px }
                    | DurationType, TokenValue.Number n ->
                        TokenValue.Duration { Value = n; Unit = Milliseconds }
                    | _ -> ft.Value
                match toResolvedValue pathStr file coercedValue with
                | Error es ->
                    for e in es do
                        match e with
                        | UnresolvedReference (p, r) -> errs.Add (p, r)
                        | other -> errs.Add (pathStr, ValidationError.format other)
                | Ok rv -> oks.Add (path, { Value = rv; Type = tt; Metadata = ft.Metadata }))
    List.ofSeq oks, List.ofSeq errs


/// Result of <see cref="importTokensStudioRaw"/>: resolved tokens plus the raw shim
/// artifacts needed to export back to Tokens Studio format or build a resolver document.
type TokensStudioRawImport = {
    /// Resolved tokens and import warnings — same content as <see cref="importTokensStudio"/>.
    Import     : TokensStudioImportResult
    /// Raw shim output — pass directly to <see cref="exportTokensStudio"/> or <see cref="toResolverDocument"/>.
    ShimResult : ShimResult
    /// Parsed token files by set name — pass directly to <see cref="exportTokensStudio"/> or <see cref="toResolverDocument"/>.
    ParsedSets : Map<string, TokenFile>
}

/// Shim a Tokens Studio multi-set JSON export, resolve all sets, and return both the
/// resolved tokens and the raw shim artifacts needed to export back to Tokens Studio format.
///
/// <remarks>
/// Use this in place of <see cref="importTokensStudio"/> when you need the full round-trip
/// path: after resolving and optionally modifying tokens, pass <c>result.ShimResult</c> and
/// <c>result.ParsedSets</c> directly to <see cref="exportTokensStudio"/> or
/// <see cref="toResolverDocument"/> — no <c>Primitives</c> calls needed.
/// </remarks>
let importTokensStudioRaw
    (config   : ShimConfig)
    (jsonText : string)
    : Result<TokensStudioRawImport, ImportError list> =
    match TokensStudio.shimSingleFile config jsonText with
    | Error msg -> Error [ParseFailed [InvalidJson msg]]
    | Ok shimResult ->
        let warnings = ResizeArray<TokensStudioImportWarning>()

        // Parse each shimmed set; record a warning for any that fail
        let parsedSets =
            shimResult.Sets
            |> Map.toList
            |> List.choose (fun (name, setJson) ->
                match Format.parse setJson with
                | Ok file -> Some (name, file)
                | Error _ ->
                    warnings.Add (SetSkipped name)
                    None)
            |> Map.ofList

        // Respect tokenSetOrder: first = lowest priority, last = highest
        let orderedSets =
            shimResult.Metadata.TokenSetOrder
            |> List.choose (fun name ->
                parsedSets |> Map.tryFind name |> Option.map (fun f -> name, f))

        if orderedSets.IsEmpty then
            Ok {
                Import     = { Tokens = []; Warnings = List.ofSeq warnings }
                ShimResult = shimResult
                ParsedSets = parsedSets
            }
        else
            let setDefs =
                orderedSets
                |> List.map (fun (name, file) ->
                    name, { Sources = [Inline file]; Description = None; Extensions = [] })
            let resolutionOrder =
                orderedSets |> List.map (fun (name, _) -> SetRef name)
            let doc = {
                Name            = None
                Version         = V2025_10
                Description     = None
                Sets            = setDefs
                Modifiers       = []
                ResolutionOrder = resolutionOrder
            }
            match Resolver.resolve (fun _ -> Error "inline-only") Map.empty doc with
            | Error es -> Error [ResolveFailed es]
            | Ok merged ->
                let tokens, unresolved = partialFlattenResolvedFile merged
                for (path, ref) in unresolved do
                    warnings.Add (TokenUnresolved (path, ref))
                Ok {
                    Import     = { Tokens = tokens; Warnings = List.ofSeq warnings }
                    ShimResult = shimResult
                    ParsedSets = parsedSets
                }


/// Shim a Tokens Studio multi-set JSON export, merge all sets in tokenSetOrder, and resolve
/// cross-set aliases. Returns a partial-success result: resolved tokens plus warnings for
/// sets that could not parse (e.g. math expressions with PreserveMath) and tokens whose
/// alias references could not be resolved.
///
/// <remarks>
/// When you also need to export back to Tokens Studio format, use
/// <see cref="importTokensStudioRaw"/> instead — it returns the same resolved tokens plus
/// the <c>ShimResult</c> and <c>ParsedSets</c> required by <see cref="exportTokensStudio"/>
/// and <see cref="toResolverDocument"/>.
/// </remarks>
let importTokensStudio
    (config: ShimConfig)
    (jsonText: string)
    : Result<TokensStudioImportResult, ImportError list> =
    importTokensStudioRaw config jsonText |> Result.map (fun r -> r.Import)


/// Shim a Tokens Studio multi-set JSON export, activate a named subset of themes, and emit
/// base tokens (for <c>:root</c>) plus per-theme resolved tokens (for override blocks).
///
/// <remarks>
/// Base tokens come from sets that are not listed in any of the requested themes'
/// <c>selectedTokenSets</c>. Each theme's resolved tokens include both the base sets and the
/// theme's own enabled/source sets, so a CSS emitter can compute per-theme overrides as
/// the diff between each theme's full resolution and the base.
///
/// If <c>themeNames</c> is empty the entire <c>tokenSetOrder</c> is treated as the base and
/// <c>Themes</c> is empty (equivalent to a flat <c>importTokensStudio</c> call).
///
/// Unknown theme names produce a <see cref="ThemeNotFound"/> warning and are skipped.
/// </remarks>
let importTokensStudioThemed
    (config     : ShimConfig)
    (themeNames : string list)
    (jsonText   : string)
    : Result<ThemeAwareImportResult, ImportError list> =
    // Phase 1: global shim — used only to extract $themes and $metadata.
    // Token values from this pass are NOT used for resolution; the global index
    // is subject to the math-evaluator theme-bleed bug (last set wins for every
    // alias path). Per-set-list re-shims below fix this for each resolution.
    match TokensStudio.shimSingleFile config jsonText with
    | Error msg -> Error [ParseFailed [InvalidJson msg]]
    | Ok shimResult ->
        let warnings = ResizeArray<TokensStudioImportWarning>()

        let allSetOrder = shimResult.Metadata.TokenSetOrder

        let activeThemes =
            themeNames
            |> List.choose (fun name ->
                match shimResult.Themes |> List.tryFind (fun t -> t.Name = name) with
                | Some t -> Some t
                | None ->
                    warnings.Add (ThemeNotFound name)
                    None)

        // Sets explicitly listed in any active theme's selectedTokenSets
        let allThemeSets =
            activeThemes
            |> List.collect (fun t -> t.SelectedTokenSets |> Map.toList |> List.map fst)
            |> Set.ofList

        // Base: sets in tokenSetOrder that are NOT listed in any active theme
        let baseSets = allSetOrder |> List.filter (fun s -> not (allThemeSets.Contains s))

        // Per-theme set list: base sets PLUS the theme's own enabled/source sets, in order
        let setsForTheme (theme: TokensStudioTheme) : string list =
            let themeOwn =
                theme.SelectedTokenSets
                |> Map.toList
                |> List.choose (fun (name, status) ->
                    match status with
                    | "enabled" | "source" -> Some name
                    | _ -> None)
                |> Set.ofList
            allSetOrder |> List.filter (fun s ->
                not (allThemeSets.Contains s) || themeOwn.Contains s)

        // Phase 2: re-shim with a per-set-list math index, parse, and resolve.
        // Using only the active sets for the math index prevents mutually-exclusive
        // sets (e.g. Text zoom/100% vs Text zoom/200%) from bleeding into each other
        // when their alias-referenced values differ (ADR-020 theme-bleed fix).
        let resolveNamesWithIndex
            (mathIndexSets : string list)
            (setNames      : string list)
            : Result<(string list * ResolvedToken) list, ImportError list> =

            let parsedSets =
                match TokensStudio.shimSingleFileWithMathIndex config mathIndexSets jsonText with
                | Error msg -> Map.empty  // propagate as empty; resolution will return []
                | Ok perShim ->
                    perShim.Sets
                    |> Map.toList
                    |> List.choose (fun (name, setJson) ->
                        match Format.parse setJson with
                        | Ok file -> Some (name, file)
                        | Error _ ->
                            warnings.Add (SetSkipped name)
                            None)
                    |> Map.ofList

            let ordered =
                setNames |> List.choose (fun n ->
                    parsedSets |> Map.tryFind n |> Option.map (fun f -> n, f))
            if ordered.IsEmpty then Ok []
            else
                let setDefs =
                    ordered
                    |> List.map (fun (n, f) ->
                        n, { Sources = [Inline f]; Description = None; Extensions = [] })
                let resOrder = ordered |> List.map (fun (n, _) -> SetRef n)
                let doc = {
                    Name            = None
                    Version         = V2025_10
                    Description     = None
                    Sets            = setDefs
                    Modifiers       = []
                    ResolutionOrder = resOrder
                }
                match Resolver.resolve (fun _ -> Error "inline-only") Map.empty doc with
                | Error es -> Error [ResolveFailed es]
                | Ok merged ->
                    let tokens, unresolved = partialFlattenResolvedFile merged
                    for (p, r) in unresolved do
                        warnings.Add (TokenUnresolved (p, r))
                    Ok tokens

        match resolveNamesWithIndex baseSets baseSets with
        | Error es -> Error es
        | Ok baseTokens ->
            let themeResults =
                activeThemes
                |> List.map (fun theme ->
                    let themeSets = setsForTheme theme
                    match resolveNamesWithIndex themeSets themeSets with
                    | Error es -> Error es
                    | Ok tokens -> Ok { ThemeName = theme.Name; Tokens = tokens })
            let errors = themeResults |> List.collect (function Error es -> es | Ok _ -> [])
            if errors.IsEmpty then
                let themes = themeResults |> List.choose (function Ok t -> Some t | Error _ -> None)
                Ok { BaseTokens = baseTokens; Themes = themes; Warnings = List.ofSeq warnings }
            else
                Error errors


/// Shim a Tokens Studio multi-set JSON export, combine the sets from multiple named themes
/// into a single resolution context, and return a flat token list.
///
/// <remarks>
/// Unlike <see cref="importTokensStudioThemed"/>, which resolves each theme independently,
/// this function merges the sets from all requested themes into one math index and one
/// resolution pass. The key difference is that <c>allThemeSets</c> is computed from
/// <em>all</em> themes in the file (not just the active ones), so sets belonging to
/// non-requested themes are never mistaken for base sets and never bleed into the math index.
///
/// This is the correct call when themes from different modifier groups must be combined —
/// for example, a Color-mode theme ("Light") and a Breakpoint theme ("Desktop") — because
/// the sets of each group's non-selected variant (e.g. "Dark" or "Mobile") are excluded
/// from the math index rather than treated as base.
///
/// Returns a flat <see cref="TokensStudioImportResult"/> suitable for emitting a single
/// <c>:root</c> CSS block or a single token snapshot.
///
/// Unknown theme names produce a <see cref="ThemeNotFound"/> warning and are skipped.
/// </remarks>
let importTokensStudioCombined
    (config     : ShimConfig)
    (themeNames : string list)
    (jsonText   : string)
    : Result<TokensStudioImportResult, ImportError list> =
    match TokensStudio.shimSingleFile config jsonText with
    | Error msg -> Error [ParseFailed [InvalidJson msg]]
    | Ok shimResult ->
        let warnings = ResizeArray<TokensStudioImportWarning>()

        let allSetOrder = shimResult.Metadata.TokenSetOrder

        let activeThemes =
            themeNames
            |> List.choose (fun name ->
                match shimResult.Themes |> List.tryFind (fun t -> t.Name = name) with
                | Some t -> Some t
                | None ->
                    warnings.Add (ThemeNotFound name)
                    None)

        // Key difference from importTokensStudioThemed:
        // Use ALL themes in the file to identify which sets are theme-owned, so sets
        // belonging to non-requested themes are never included as implicit base sets
        // and never bleed into the math index (cross-group bleed fix, ADR-025).
        let allThemeSets =
            shimResult.Themes
            |> List.collect (fun t -> t.SelectedTokenSets |> Map.toList |> List.map fst)
            |> Set.ofList

        // Union of all active themes' enabled/source sets
        let combinedOwn =
            activeThemes
            |> List.collect (fun t ->
                t.SelectedTokenSets
                |> Map.toList
                |> List.choose (fun (name, status) ->
                    match status with
                    | "enabled" | "source" -> Some name
                    | _ -> None))
            |> Set.ofList

        // Combined set list in tokenSetOrder: base sets (not in any theme) plus all
        // active themes' own sets. Non-requested theme sets are excluded entirely.
        let combinedSets =
            allSetOrder |> List.filter (fun s ->
                not (allThemeSets.Contains s) || combinedOwn.Contains s)

        let parsedSets =
            match TokensStudio.shimSingleFileWithMathIndex config combinedSets jsonText with
            | Error _ -> Map.empty
            | Ok perShim ->
                perShim.Sets
                |> Map.toList
                |> List.choose (fun (name, setJson) ->
                    match Format.parse setJson with
                    | Ok file -> Some (name, file)
                    | Error _ ->
                        warnings.Add (SetSkipped name)
                        None)
                |> Map.ofList

        let ordered =
            combinedSets
            |> List.choose (fun n ->
                parsedSets |> Map.tryFind n |> Option.map (fun f -> n, f))

        if ordered.IsEmpty then
            Ok { Tokens = []; Warnings = List.ofSeq warnings }
        else
            let setDefs =
                ordered
                |> List.map (fun (n, f) ->
                    n, { Sources = [Inline f]; Description = None; Extensions = [] })
            let resOrder = ordered |> List.map (fun (n, _) -> SetRef n)
            let doc = {
                Name            = None
                Version         = V2025_10
                Description     = None
                Sets            = setDefs
                Modifiers       = []
                ResolutionOrder = resOrder
            }
            match Resolver.resolve (fun _ -> Error "inline-only") Map.empty doc with
            | Error es -> Error [ResolveFailed es]
            | Ok merged ->
                let tokens, unresolved = partialFlattenResolvedFile merged
                for (p, r) in unresolved do
                    warnings.Add (TokenUnresolved (p, r))
                Ok { Tokens = tokens; Warnings = List.ofSeq warnings }


/// Identical to <see cref="importTokensStudioCombined"/> but also includes one or more
/// native DTCG 2025.10 token files in the resolution as lowest-priority base sets.
///
/// <paramref name="sets"/> — a list of (setName, dtcgJson) pairs. Each is parsed with
/// <see cref="Format.parse"/>; a parse failure emits a <see cref="DtcgSetSkipped"/> warning
/// and excludes that set from the merge (other sets continue).
///
/// The <c>AsBasePrimitives</c> argument is required at every call site to confirm that the
/// extra sets are theme-agnostic base layers: they are always included regardless of the
/// <paramref name="themeNames"/> filter, and TS theme sets always override them. If you need
/// theme-conditional DTCG sets, assemble a <see cref="ResolverDocument"/> manually.
///
/// Resolution order: DTCG base sets (in list order) → TS base sets → TS active-theme sets.
let importTokensStudioCombinedWith
    (config     : ShimConfig)
    (themeNames : string list)
    (tsJson     : string)
    (sets       : (string * string) list)
    (_          : DtcgSetRole)
    : Result<TokensStudioImportResult, ImportError list> =
    match TokensStudio.shimSingleFile config tsJson with
    | Error msg -> Error [ParseFailed [InvalidJson msg]]
    | Ok shimResult ->
        let warnings = ResizeArray<TokensStudioImportWarning>()

        // Parse extra DTCG sets first; warn and skip on failure.
        let parsedExtra =
            sets |> List.choose (fun (name, json) ->
                match Format.parse json with
                | Ok file -> Some (name, file)
                | Error _ ->
                    warnings.Add (DtcgSetSkipped name)
                    None)

        let allSetOrder = shimResult.Metadata.TokenSetOrder

        let activeThemes =
            themeNames
            |> List.choose (fun name ->
                match shimResult.Themes |> List.tryFind (fun t -> t.Name = name) with
                | Some t -> Some t
                | None ->
                    warnings.Add (ThemeNotFound name)
                    None)

        let allThemeSets =
            shimResult.Themes
            |> List.collect (fun t -> t.SelectedTokenSets |> Map.toList |> List.map fst)
            |> Set.ofList

        let combinedOwn =
            activeThemes
            |> List.collect (fun t ->
                t.SelectedTokenSets
                |> Map.toList
                |> List.choose (fun (name, status) ->
                    match status with
                    | "enabled" | "source" -> Some name
                    | _ -> None))
            |> Set.ofList

        let combinedSets =
            allSetOrder |> List.filter (fun s ->
                not (allThemeSets.Contains s) || combinedOwn.Contains s)

        let parsedTs =
            match TokensStudio.shimSingleFileWithMathIndex config combinedSets tsJson with
            | Error _ -> Map.empty
            | Ok perShim ->
                perShim.Sets
                |> Map.toList
                |> List.choose (fun (name, setJson) ->
                    match Format.parse setJson with
                    | Ok file -> Some (name, file)
                    | Error _ ->
                        warnings.Add (SetSkipped name)
                        None)
                |> Map.ofList

        let orderedTs =
            combinedSets
            |> List.choose (fun n ->
                parsedTs |> Map.tryFind n |> Option.map (fun f -> n, f))

        // DTCG base sets first (lowest priority), then TS sets (they override).
        let allOrdered = parsedExtra @ orderedTs

        if allOrdered.IsEmpty then
            Ok { Tokens = []; Warnings = List.ofSeq warnings }
        else
            let setDefs =
                allOrdered
                |> List.map (fun (n, f) ->
                    n, { Sources = [Inline f]; Description = None; Extensions = [] })
            let resOrder = allOrdered |> List.map (fun (n, _) -> SetRef n)
            let doc = {
                Name            = None
                Version         = V2025_10
                Description     = None
                Sets            = setDefs
                Modifiers       = []
                ResolutionOrder = resOrder
            }
            match Resolver.resolve (fun _ -> Error "inline-only") Map.empty doc with
            | Error es -> Error [ResolveFailed es]
            | Ok merged ->
                let tokens, unresolved = partialFlattenResolvedFile merged
                for (p, r) in unresolved do
                    warnings.Add (TokenUnresolved (p, r))
                Ok { Tokens = tokens; Warnings = List.ofSeq warnings }


/// Round-trip a resolved-token sequence back to JSON.
/// Builds a flat TokenFile (no groups beyond what dot-paths require) and serializes.
let export (tokens: (string list * ResolvedToken) seq) : string =
    // Convert ResolvedToken back to Token (no aliases, all literals)
    let rec back (rv: ResolvedTokenValue) : TokenValue =
        match rv with
        | ResolvedColor c       -> TokenValue.Color c
        | ResolvedDimension d   -> TokenValue.Dimension d
        | ResolvedFontFamily f  -> TokenValue.FontFamily f
        | ResolvedFontWeight f  -> TokenValue.FontWeight f
        | ResolvedDuration d    -> TokenValue.Duration d
        | ResolvedCubicBezier c -> TokenValue.CubicBezier c
        | ResolvedNumber n      -> TokenValue.Number n
        | ResolvedStrokeStyle s ->
            let v =
                match s with
                | ResolvedStrokeKeyword k -> StrokeKeyword k
                | ResolvedStrokeCustom obj ->
                    StrokeCustom {
                        DashArray = obj.DashArray |> List.map Literal
                        LineCap = obj.LineCap
                    }
            TokenValue.StrokeStyle v
        | ResolvedBorder b ->
            TokenValue.Border {
                Color = Literal b.Color
                Width = Literal b.Width
                Style = Literal (
                    match b.Style with
                    | ResolvedStrokeKeyword k -> StrokeKeyword k
                    | ResolvedStrokeCustom obj ->
                        StrokeCustom {
                            DashArray = obj.DashArray |> List.map Literal
                            LineCap = obj.LineCap
                        })
            }
        | ResolvedShadow s ->
            let v =
                match s with
                | ResolvedShadowSingle obj ->
                    ShadowSingle {
                        Color = Literal obj.Color
                        OffsetX = Literal obj.OffsetX
                        OffsetY = Literal obj.OffsetY
                        Blur = Literal obj.Blur
                        Spread = Literal obj.Spread
                        Inset = obj.Inset
                    }
                | ResolvedShadowMultiple xs ->
                    ShadowMultiple (
                        xs |> List.map (fun obj ->
                            ShadowLiteral {
                                Color = Literal obj.Color
                                OffsetX = Literal obj.OffsetX
                                OffsetY = Literal obj.OffsetY
                                Blur = Literal obj.Blur
                                Spread = Literal obj.Spread
                                Inset = obj.Inset
                            }))
            TokenValue.Shadow v
        | ResolvedTransition t ->
            TokenValue.Transition {
                Duration = Literal t.Duration
                Delay = Literal t.Delay
                TimingFunction = Literal t.TimingFunction
            }
        | ResolvedGradient stops ->
            TokenValue.Gradient (
                stops |> List.map (fun s ->
                    { Color = Literal s.Color; Position = Literal s.Position }))
        | ResolvedTypography t ->
            TokenValue.Typography {
                FontFamily = Literal t.FontFamily
                FontSize = Literal t.FontSize
                FontWeight = Literal t.FontWeight
                LetterSpacing = Literal t.LetterSpacing
                LineHeight = Literal t.LineHeight
            }

    // Build a tree: insert each token at its dot-path
    let mutable rootChildren : (TokenName * TokenNode) list = []

    let rec insertChild
        (children: (TokenName * TokenNode) list)
        (path: string list)
        (token: Token)
        : (TokenName * TokenNode) list =
        match path with
        | [] -> children
        | [last] ->
            match TokenName.tryCreate last with
            | Error _ -> children
            | Ok name ->
                let leaf = TokenLeaf token
                children @ [(name, leaf)]
        | head :: tail ->
            match TokenName.tryCreate head with
            | Error _ -> children
            | Ok hname ->
                let existing = children |> List.tryFind (fun (n, _) -> TokenName.value n = head)
                match existing with
                | Some (_, Group g) ->
                    let updated =
                        Group { g with Children = insertChild g.Children tail token }
                    children
                    |> List.map (fun (n, x) -> if TokenName.value n = head then (n, updated) else (n, x))
                | _ ->
                    let newGroup =
                        Group {
                            Type = None
                            Metadata = { Description = None; Deprecated = None; Extensions = [] }
                            Root = None
                            Extends = None
                            Children = insertChild [] tail token
                        }
                    children @ [(hname, newGroup)]

    for (path, rt) in tokens do
        let token : Token = {
            Value = back rt.Value
            Type = Some rt.Type
            Metadata = rt.Metadata
        }
        rootChildren <- insertChild rootChildren path token

    let file = { Version = V2025_10; Schema = None; Children = rootChildren }
    Format.serialize file


let formatImportError (e: ImportError) : string = ImportError.format e

/// Map Tokens Studio $themes/$metadata to a DTCG ResolverDocument.
/// Wraps <see cref="TokensStudio.toResolverDocument"/>.
let toResolverDocument
    (shimResult : ShimResult)
    (parsedSets : Map<string, TokenFile>)
    : ResolverDocument =
    TokensStudio.toResolverDocument shimResult parsedSets

/// Export DTCG token sets back to Tokens Studio JSON format (preserve-aliases path).
/// Returns the JSON string and any lossy-color-conversion warnings.
/// Wraps <see cref="TokensStudio.exportToTokensStudio"/>.
let exportTokensStudio
    (shimResult : ShimResult)
    (parsedSets : Map<string, TokenFile>)
    : string * ExportWarning list =
    TokensStudio.exportToTokensStudio shimResult parsedSets

/// Serialize a <see cref="ResolverDocument"/> to JSON.
///
/// Produces output that <c>parseResolver</c> / <c>importWithResolver</c> can read back.
/// Round-trip guarantee: <c>parseResolver (serializeResolver doc)</c> returns a
/// structurally equivalent document. <c>$ref</c> pointers are never emitted — all
/// sources are written as concrete <c>inline</c> or <c>path</c> objects.
let serializeResolver (doc: ResolverDocument) : string =
    Resolver.serializeResolver doc

/// Strict DTCG 2025.10 compliance check. Returns errors for any feature that
/// is valid in this library's domain but not in the published spec — today,
/// only <see cref="DimensionUnit.Em"/> (ADR-028).
///
/// Use this when you need to guarantee that a <see cref="TokenFile"/> contains
/// no library extensions before exporting to a strict downstream consumer.
/// References are not followed; only literal positions are checked.
let validateStrictDtcg (file: TokenFile) : Result<unit, ValidationError list> =
    Validation.validateStrictDtcg file


// ─── Extension-aware resolve (ADR-034) ───────────────────────────────────────

/// Result of <see cref="evaluateMathExtensions"/>: the (possibly updated)
/// tokens, plus any non-fatal warnings collected during math evaluation.
type ResolveWithExtensionsResult = {
    Tokens   : (string list * ResolvedToken) list
    Warnings : ExtensionEvaluationWarning list
}

// The vendor namespace and field name are duplicated literals across the
// codebase (TokensStudio.fs and CssEmitter.fs each define their own private
// copies). Kept private here too rather than promoted to Foundation — the
// constants belong to the extension contract, not the domain.
let private extensionEvaluationVendorKey   = "com.fntools.designtokens"
let private extensionEvaluationMathExprKey = "tsMathExpression"

/// Read the <c>tsMathExpression</c> string from a token's vendor extensions.
let private tryReadMathExpressionAnnotation
    (extensions: Extensions)
    : string option =
    extensions
    |> List.tryPick (fun (k, node) ->
        if k <> extensionEvaluationVendorKey then None
        else
            match node with
            | :? System.Text.Json.Nodes.JsonObject as vendor
              when vendor.ContainsKey extensionEvaluationMathExprKey ->
                vendor.[extensionEvaluationMathExprKey]
                |> Option.ofObj
                |> Option.bind (fun v ->
                    try Some (v.GetValue<string>())
                    with _ -> None)
            | _ -> None)

/// Build a Map<string, float> of resolved numeric values keyed by full dot-path.
/// Only numeric token types (Number, Dimension, Duration) are included.
let private buildResolvedNumberIndex
    (tokens: (string list * ResolvedToken) list)
    : Map<string, float> =
    tokens
    |> List.choose (fun (path, token) ->
        match token.Value with
        | ResolvedNumber n    -> Some (String.concat "." path, n)
        | ResolvedDimension d -> Some (String.concat "." path, d.Value)
        | ResolvedDuration d  -> Some (String.concat "." path, d.Value)
        | _                   -> None)
    |> Map.ofList

/// Replace a numeric resolved value's payload with a new number, preserving
/// the token type (Number / Dimension / Duration). Non-numeric tokens are
/// returned unchanged — math evaluation should not be applied to them.
let private updateNumericValue (newValue: float) (token: ResolvedToken) : ResolvedToken =
    match token.Value with
    | ResolvedNumber _    -> { token with Value = ResolvedNumber    newValue }
    | ResolvedDimension d -> { token with Value = ResolvedDimension { d with Value = newValue } }
    | ResolvedDuration d  -> { token with Value = ResolvedDuration  { d with Value = newValue } }
    | _                   -> token

/// Walk resolved tokens; for any token carrying a <c>tsMathExpression</c>
/// extension (ADR-031), evaluate the expression against the resolved numeric
/// context and replace the token's value with the result. Tokens without the
/// extension pass through unchanged. Evaluation failures are reported as
/// <see cref="ExtensionEvaluationWarning"/>; the stale <c>$value</c> is kept.
///
/// Use after <see cref="resolveAll"/> or <see cref="importWithResolver"/> when
/// the resolver document combines axes that affect a math expression's
/// variables — e.g. a Breakpoint set overrides <c>multiplier</c>, and scale
/// tokens defined as <c>round({base} * pow({multiplier}, N))</c> should
/// re-evaluate against the new context rather than read the snapshot
/// <c>$value</c> written at the previous resolve.
let evaluateMathExtensions
    (tokens: (string list * ResolvedToken) seq)
    : ResolveWithExtensionsResult =
    let tokenList = List.ofSeq tokens
    let index     = buildResolvedNumberIndex tokenList
    let warnings  = ResizeArray<ExtensionEvaluationWarning>()

    let updated =
        tokenList |> List.map (fun (path, token) ->
            match tryReadMathExpressionAnnotation token.Metadata.Extensions with
            | None -> path, token
            | Some expr ->
                match TokensStudio.tryEvaluateMathExpression index expr with
                | Some n ->
                    path, updateNumericValue n token
                | None ->
                    let reason =
                        "evaluation failed (parse error, missing variable, or non-numeric result)"
                    warnings.Add (MathExpressionFailed (String.concat "." path, expr, reason))
                    path, token)

    { Tokens = updated; Warnings = List.ofSeq warnings }

/// Convenience: <see cref="importWithResolver"/> followed by
/// <see cref="evaluateMathExtensions"/>. Math-expression extensions are
/// evaluated against the fully-resolved per-axis context, so axis-dependent
/// formulas (e.g. <c>multiplier</c> overridden by a Breakpoint set) produce
/// correct values without per-axis pre-computation in the source file.
let importWithResolverEvaluatingExtensions
    (loadFile : string -> Result<string, string>)
    (context  : Map<string, string>)
    (jsonText : string)
    : Result<ResolveWithExtensionsResult, ImportError list> =
    importWithResolver loadFile context jsonText
    |> Result.map evaluateMathExtensions


// ─── Primitives module ───────────────────────────────────────────────────────

module Primitives =

    let parse        = Format.parse
    let parseAs      = Format.parseAs
    let parseAuto    = Format.parseAuto
    let serialize        = Format.serialize
    let serializeAs      = Format.serializeAs
    let serializePenpot  = Format.serializePenpot
    let validate            = Validation.validate
    let validateStrictDtcg  = Validation.validateStrictDtcg
    let evaluateMathExtensions                = evaluateMathExtensions
    let importWithResolverEvaluatingExtensions = importWithResolverEvaluatingExtensions
    let formatExtensionEvaluationWarning      = ExtensionEvaluationWarning.format
    let flatten      = flattenFile
    let tryFind      = tryFindIn
    let tryResolveAlias = tryResolveAliasIn
    let parseResolver    = Resolver.parseResolver
    let serializeResolver = Resolver.serializeResolver
    let resolve          = Resolver.resolve
    let resolveAll       = Resolver.resolveAll
    let flattenResolved = flattenResolvedFile

    let shimTokensStudio            = TokensStudio.shim
    let shimWithConfig              = TokensStudio.shimSingleFile
    let formatShimWarning           = TokensStudio.formatWarning
    let formatImportWarning         = TokensStudio.formatImportWarning
    let importTokensStudioRaw            = importTokensStudioRaw
    let importTokensStudioThemed         = importTokensStudioThemed
    let importTokensStudioCombined       = importTokensStudioCombined
    let importTokensStudioCombinedWith   = importTokensStudioCombinedWith
    let toResolverDocument          = TokensStudio.toResolverDocument
    let exportTokensStudio          = TokensStudio.exportToTokensStudio
    let formatExportWarning         = TokensStudio.formatExportWarning

    let load (jsonText: string) : Result<TokenFile, LoadError list> =
        match Format.parse jsonText with
        | Error es -> Error (es |> List.map LoadError.ParseError)
        | Ok file ->
            match Validation.validate file with
            | Error es -> Error (es |> List.map LoadError.ValidationError)
            | Ok () -> Ok file

    let formatParseError      = ParseError.format
    let formatValidationError = ValidationError.format
    let formatResolveError    = ResolveError.format
    let formatLoadError       = LoadError.format
    let formatSerializeError  = SerializeError.format
