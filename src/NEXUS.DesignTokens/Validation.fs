module NEXUS.DesignTokens.Validation

open System
open NEXUS.DesignTokens


// ─── Path helpers ────────────────────────────────────────────────────────────

let private joinPath (segments: string list) : string =
    String.concat "." segments


// ─── Generic helpers ─────────────────────────────────────────────────────────

let private requireFinite (path: string) (label: string) (n: float) : ValidationError option =
    if Double.IsFinite n then None
    else Some (ConstraintViolation (path, sprintf "%s must be finite (got %f)" label n))

let private inRangeF (path: string) (label: string) (lo: float) (hi: float) (n: float) : ValidationError option =
    if n >= lo && n <= hi then None
    else Some (ConstraintViolation (path, sprintf "%s must be in [%g, %g] (got %g)" label lo hi n))

let private inRangeI (path: string) (label: string) (lo: int) (hi: int) (n: int) : ValidationError option =
    if n >= lo && n <= hi then None
    else Some (ConstraintViolation (path, sprintf "%s must be in [%d, %d] (got %d)" label lo hi n))

let private hexRegex =
    System.Text.RegularExpressions.Regex(@"^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled)


// ─── Primitive value validators ─────────────────────────────────────────────

let private validateColor (path: string) (c: ColorValue) : ValidationError list =
    let errors = ResizeArray()

    let (a, b, ch) = c.Components
    let validateChannel label cc =
        match cc with
        | Channel n ->
            requireFinite path label n
            |> Option.iter errors.Add
        | Missing -> ()

    validateChannel "components[0]" a
    validateChannel "components[1]" b
    validateChannel "components[2]" ch

    match c.Alpha with
    | Some alpha ->
        requireFinite path "alpha" alpha |> Option.iter errors.Add
        inRangeF path "alpha" 0.0 1.0 alpha |> Option.iter errors.Add
    | None -> ()

    match c.Hex with
    | Some hex when not (hexRegex.IsMatch hex) ->
        errors.Add (ConstraintViolation (path, sprintf "hex must match #RRGGBB (got '%s')" hex))
    | _ -> ()

    // sRGB hex/components consistency check
    match c.ColorSpace, c.Hex, c.Components with
    | SRGB, Some hex, (Channel r, Channel g, Channel b) when hexRegex.IsMatch hex ->
        let parseHex (i: int) = Convert.ToInt32(hex.Substring(i, 2), 16)
        let hr = float (parseHex 1) / 255.0
        let hg = float (parseHex 3) / 255.0
        let hb = float (parseHex 5) / 255.0
        let tolerance = 1.0 / 255.0
        let close x y = abs (x - y) <= tolerance
        if not (close r hr && close g hg && close b hb) then
            errors.Add (ConstraintViolation (
                path,
                sprintf "sRGB hex %s does not match components (%g, %g, %g)" hex r g b))
    | _ -> ()

    List.ofSeq errors

let private validateDimension (path: string) (d: DimensionValue) : ValidationError list =
    requireFinite path "value" d.Value
    |> Option.toList

let private validateDuration (path: string) (d: DurationValue) : ValidationError list =
    requireFinite path "value" d.Value
    |> Option.toList

let private validateCubicBezier (path: string) (cb: CubicBezierValue) : ValidationError list = [
    yield! requireFinite path "P1x" cb.P1x |> Option.toList
    yield! requireFinite path "P1y" cb.P1y |> Option.toList
    yield! requireFinite path "P2x" cb.P2x |> Option.toList
    yield! requireFinite path "P2y" cb.P2y |> Option.toList
    yield! inRangeF path "P1x" 0.0 1.0 cb.P1x |> Option.toList
    yield! inRangeF path "P2x" 0.0 1.0 cb.P2x |> Option.toList
]

let private validateFontWeight (path: string) (fw: FontWeightValue) : ValidationError list =
    match fw with
    | Numeric n -> inRangeI path "fontWeight" 1 1000 n |> Option.toList
    | Keyword _ -> []

let private validateNumber (path: string) (n: float) : ValidationError list =
    requireFinite path "value" n |> Option.toList

let private validateValueOrRefDimension (path: string) (vor: ValueOrRef<DimensionValue>) : ValidationError list =
    match vor with
    | Literal d -> validateDimension path d
    | Reference _ -> []

let private validateValueOrRefDuration (path: string) (vor: ValueOrRef<DurationValue>) : ValidationError list =
    match vor with
    | Literal d -> validateDuration path d
    | Reference _ -> []

let private validateValueOrRefColor (path: string) (vor: ValueOrRef<ColorValue>) : ValidationError list =
    match vor with
    | Literal c -> validateColor path c
    | Reference _ -> []

let private validateValueOrRefCubicBezier (path: string) (vor: ValueOrRef<CubicBezierValue>) : ValidationError list =
    match vor with
    | Literal cb -> validateCubicBezier path cb
    | Reference _ -> []

let private validateValueOrRefFloat (path: string) (label: string) (vor: ValueOrRef<float>) : ValidationError list =
    match vor with
    | Literal n ->
        let finite = requireFinite path label n |> Option.toList
        let inRange = inRangeF path label 0.0 1.0 n |> Option.toList
        finite @ inRange
    | Reference _ -> []

let private validateStrokeStyle (path: string) (s: StrokeStyleValue) : ValidationError list =
    match s with
    | StrokeKeyword _ -> []
    | StrokeCustom obj ->
        obj.DashArray
        |> List.mapi (fun i v -> validateValueOrRefDimension (sprintf "%s.dashArray[%d]" path i) v)
        |> List.concat


// ─── Composite validators ────────────────────────────────────────────────────

let private validateBorder (path: string) (b: BorderValue) : ValidationError list =
    [ validateValueOrRefColor (path + ".color") b.Color
      validateValueOrRefDimension (path + ".width") b.Width
      match b.Style with
      | Literal s -> validateStrokeStyle (path + ".style") s
      | Reference _ -> [] ]
    |> List.concat

let private validateShadowObject (path: string) (s: ShadowObject) : ValidationError list =
    [ validateValueOrRefColor (path + ".color") s.Color
      validateValueOrRefDimension (path + ".offsetX") s.OffsetX
      validateValueOrRefDimension (path + ".offsetY") s.OffsetY
      validateValueOrRefDimension (path + ".blur") s.Blur
      validateValueOrRefDimension (path + ".spread") s.Spread ]
    |> List.concat

let private validateShadow (path: string) (s: ShadowValue) : ValidationError list =
    match s with
    | ShadowSingle obj -> validateShadowObject path obj
    | ShadowMultiple xs ->
        xs
        |> List.mapi (fun i x ->
            match x with
            | ShadowLiteral obj -> validateShadowObject (sprintf "%s[%d]" path i) obj
            | ShadowReference _ -> [])
        |> List.concat

let private validateTransition (path: string) (t: TransitionValue) : ValidationError list =
    [ validateValueOrRefDuration (path + ".duration") t.Duration
      validateValueOrRefDuration (path + ".delay") t.Delay
      validateValueOrRefCubicBezier (path + ".timingFunction") t.TimingFunction ]
    |> List.concat

let private validateGradient (path: string) (g: GradientValue) : ValidationError list =
    let lengthCheck =
        if List.length g < 2 then
            [ ConstraintViolation (path, sprintf "gradient must have at least 2 stops (got %d)" (List.length g)) ]
        else []
    let stopChecks =
        g
        |> List.mapi (fun i stop ->
            let stopPath = sprintf "%s[%d]" path i
            [ validateValueOrRefColor (stopPath + ".color") stop.Color
              validateValueOrRefFloat (stopPath + ".position") "position" stop.Position ]
            |> List.concat)
        |> List.concat
    lengthCheck @ stopChecks

let private validateTypography (path: string) (t: TypographyValue) : ValidationError list =
    let fontWeightCheck =
        match t.FontWeight with
        | Literal fw -> validateFontWeight (path + ".fontWeight") fw
        | Reference _ -> []
    let lineHeightCheck =
        match t.LineHeight with
        | Literal n -> requireFinite (path + ".lineHeight") "lineHeight" n |> Option.toList
        | Reference _ -> []
    [ validateValueOrRefDimension (path + ".fontSize") t.FontSize
      validateValueOrRefDimension (path + ".letterSpacing") t.LetterSpacing
      fontWeightCheck
      lineHeightCheck ]
    |> List.concat


// ─── TokenValue dispatch ─────────────────────────────────────────────────────

let private validateTokenValue (path: string) (v: TokenValue) : ValidationError list =
    match v with
    | TokenValue.Color c       -> validateColor path c
    | TokenValue.Dimension d   -> validateDimension path d
    | TokenValue.FontFamily _  -> []
    | TokenValue.FontWeight fw -> validateFontWeight path fw
    | TokenValue.Duration d    -> validateDuration path d
    | TokenValue.CubicBezier c -> validateCubicBezier path c
    | TokenValue.Number n      -> validateNumber path n
    | TokenValue.StrokeStyle s -> validateStrokeStyle path s
    | TokenValue.Border b      -> validateBorder path b
    | TokenValue.Shadow s      -> validateShadow path s
    | TokenValue.Transition t  -> validateTransition path t
    | TokenValue.Gradient g    -> validateGradient path g
    | TokenValue.Typography t  -> validateTypography path t
    | TokenValue.Alias _       -> []  // alias targets validated separately


// ─── Tree walk ───────────────────────────────────────────────────────────────

let rec private walkNode (path: string list) (node: TokenNode) : ValidationError list =
    match node with
    | TokenLeaf t ->
        validateTokenValue (joinPath path) t.Value
    | Group g ->
        let rootErrs =
            match g.Root with
            | Some t -> validateTokenValue (joinPath path) t.Value
            | None -> []
        let childErrs =
            g.Children
            |> List.collect (fun (name, child) ->
                walkNode (path @ [TokenName.value name]) child)
        rootErrs @ childErrs


// ─── Circular alias detection ────────────────────────────────────────────────
// DFS through alias chains; cycle = path back to a name already on the stack.

let private collectAliasEdges (file: TokenFile) : Map<string, string list> =
    let edges = ResizeArray<string * string>()

    let rec walk (path: string list) (node: TokenNode) =
        match node with
        | TokenLeaf t ->
            match t.Value with
            | TokenValue.Alias (CurlyBrace target) ->
                edges.Add (joinPath path, joinPath target)
            | _ -> ()
        | Group g ->
            g.Children |> List.iter (fun (name, child) ->
                walk (path @ [TokenName.value name]) child)

    file.Children |> List.iter (fun (name, node) ->
        walk [TokenName.value name] node)

    edges
    |> Seq.groupBy fst
    |> Seq.map (fun (k, vs) -> k, vs |> Seq.map snd |> List.ofSeq)
    |> Map.ofSeq

let private findCycles (graph: Map<string, string list>) : string list list =
    let cycles = ResizeArray<string list>()
    let visiting = System.Collections.Generic.HashSet<string>()
    let visited  = System.Collections.Generic.HashSet<string>()

    let rec dfs (stack: string list) (node: string) =
        if visiting.Contains node then
            // cycle: trim stack to start from node's first occurrence
            let cycleStart = List.findIndex ((=) node) stack
            let cycle = stack |> List.skip cycleStart
            cycles.Add (cycle @ [node])
        elif not (visited.Contains node) then
            visiting.Add node |> ignore
            match Map.tryFind node graph with
            | Some neighbors -> neighbors |> List.iter (fun n -> dfs (stack @ [node]) n)
            | None -> ()
            visiting.Remove node |> ignore
            visited.Add node |> ignore

    graph |> Map.iter (fun k _ -> dfs [] k)
    List.ofSeq cycles


// ─── Public API ──────────────────────────────────────────────────────────────

let validate (file: TokenFile) : Result<unit, ValidationError list> =
    let valueErrs =
        file.Children
        |> List.collect (fun (name, node) ->
            walkNode [TokenName.value name] node)

    let cycleErrs =
        collectAliasEdges file
        |> findCycles
        |> List.map CircularReference

    match valueErrs @ cycleErrs with
    | [] -> Ok ()
    | xs -> Error xs

let validateResolver (doc: ResolverDocument) : Result<unit, ValidationError list> =
    let errors = ResizeArray<ValidationError>()

    // Modifiers cannot reference other modifiers (spec rule, validated structurally below).
    // For now: check every modifier has at least 2 contexts.
    doc.Modifiers
    |> List.iter (fun (name, m) ->
        if List.length m.Contexts < 2 then
            errors.Add (ConstraintViolation (
                sprintf "modifiers.%s" name,
                sprintf "modifier must have at least 2 contexts (got %d)" (List.length m.Contexts))))

    // Resolution order references must point to known sets/modifiers
    doc.ResolutionOrder
    |> List.iteri (fun i item ->
        let path = sprintf "resolutionOrder[%d]" i
        match item with
        | SetRef name ->
            if not (doc.Sets |> List.exists (fun (k, _) -> k = name)) then
                errors.Add (ConstraintViolation (path, sprintf "unknown set '%s'" name))
        | ModifierRef (name, ctx) ->
            match doc.Modifiers |> List.tryFind (fun (k, _) -> k = name) with
            | None ->
                errors.Add (ConstraintViolation (path, sprintf "unknown modifier '%s'" name))
            | Some (_, m) when not (m.Contexts |> List.exists (fun (k, _) -> k = ctx)) ->
                errors.Add (ConstraintViolation (path, sprintf "modifier '%s' has no context '%s'" name ctx))
            | _ -> ())

    if errors.Count = 0 then Ok ()
    else Error (List.ofSeq errors)
