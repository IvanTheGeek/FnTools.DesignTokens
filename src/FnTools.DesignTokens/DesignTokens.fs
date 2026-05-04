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

let private inferType (v: TokenValue) : TokenType option =
    match v with
    | TokenValue.Color _       -> Some ColorType
    | TokenValue.Dimension _   -> Some DimensionType
    | TokenValue.FontFamily _  -> Some FontFamilyType
    | TokenValue.FontWeight _  -> Some FontWeightType
    | TokenValue.Duration _    -> Some DurationType
    | TokenValue.CubicBezier _ -> Some CubicBezierType
    | TokenValue.Number _      -> Some NumberType
    | TokenValue.StrokeStyle _ -> Some StrokeStyleType
    | TokenValue.Border _      -> Some BorderType
    | TokenValue.Shadow _      -> Some ShadowType
    | TokenValue.Transition _  -> Some TransitionType
    | TokenValue.Gradient _    -> Some GradientType
    | TokenValue.Typography _  -> Some TypographyType
    | TokenValue.Alias _       -> None

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
                | Some target -> Ok { target with Type = target.Type |> Option.orElse t.Type }
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
                match toResolvedValue pathStr file ft.Value with
                | Error es ->
                    for e in es do
                        match e with
                        | UnresolvedReference (p, r) -> errs.Add (p, r)
                        | other -> errs.Add (pathStr, ValidationError.format other)
                | Ok rv -> oks.Add (path, { Value = rv; Type = tt; Metadata = ft.Metadata }))
    List.ofSeq oks, List.ofSeq errs


/// Shim a Tokens Studio multi-set JSON export, merge all sets in tokenSetOrder, and resolve
/// cross-set aliases. Returns a partial-success result: resolved tokens plus warnings for
/// sets that could not parse (e.g. math expressions with PreserveMath) and tokens whose
/// alias references could not be resolved.
let importTokensStudio
    (config: ShimConfig)
    (jsonText: string)
    : Result<TokensStudioImportResult, ImportError list> =
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
            Ok { Tokens = []; Warnings = List.ofSeq warnings }
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
                Ok { Tokens = tokens; Warnings = List.ofSeq warnings }


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
    match TokensStudio.shimSingleFile config jsonText with
    | Error msg -> Error [ParseFailed [InvalidJson msg]]
    | Ok shimResult ->
        let warnings = ResizeArray<TokensStudioImportWarning>()

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

        // Resolve an ordered list of set names, accumulating unresolved-alias warnings
        let resolveNames (setNames: string list) : Result<(string list * ResolvedToken) list, ImportError list> =
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

        match resolveNames baseSets with
        | Error es -> Error es
        | Ok baseTokens ->
            let themeResults =
                activeThemes
                |> List.map (fun theme ->
                    match resolveNames (setsForTheme theme) with
                    | Error es -> Error es
                    | Ok tokens -> Ok { ThemeName = theme.Name; Tokens = tokens })
            let errors = themeResults |> List.collect (function Error es -> es | Ok _ -> [])
            if errors.IsEmpty then
                let themes = themeResults |> List.choose (function Ok t -> Some t | Error _ -> None)
                Ok { BaseTokens = baseTokens; Themes = themes; Warnings = List.ofSeq warnings }
            else
                Error errors


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


// ─── Primitives module ───────────────────────────────────────────────────────

module Primitives =

    let parse        = Format.parse
    let parseAs      = Format.parseAs
    let parseAuto    = Format.parseAuto
    let serialize        = Format.serialize
    let serializeAs      = Format.serializeAs
    let serializePenpot  = Format.serializePenpot
    let validate     = Validation.validate
    let flatten      = flattenFile
    let tryFind      = tryFindIn
    let tryResolveAlias = tryResolveAliasIn
    let parseResolver = Resolver.parseResolver
    let resolve      = Resolver.resolve
    let resolveAll   = Resolver.resolveAll
    let flattenResolved = flattenResolvedFile

    let shimTokensStudio            = TokensStudio.shim
    let shimWithConfig              = TokensStudio.shimSingleFile
    let formatShimWarning           = TokensStudio.formatWarning
    let formatImportWarning         = TokensStudio.formatImportWarning
    let importTokensStudioThemed    = importTokensStudioThemed

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
