namespace FnTools.DesignTokens

open System
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions


// ─── Public types ─────────────────────────────────────────────────────────────

/// Policy for Tokens Studio math expressions (e.g. round({base} * pow({multiplier}, 1))).
/// DTCG has no math syntax — choose how the shim handles them.
type MathPolicy =
    /// Evaluate the expression using a recursive-descent math evaluator.
    /// Alias references are resolved via the flat token index. If evaluation
    /// fails (unresolvable alias, divide-by-zero, etc.) the token is omitted
    /// and a MathEvalFailed warning is recorded.
    | EvaluateMath
    /// Keep the expression as a string $value with type "number".
    /// Note: Format.parse rejects string values for number tokens, so the
    /// containing set will be recorded as SetSkipped in importTokensStudio.
    | PreserveMath
    /// Omit the token and record a ShimWarning.
    | SkipMath

type ShimConfig = {
    MathPolicy : MathPolicy
}

module ShimConfig =
    let defaults = { MathPolicy = EvaluateMath }

type ShimWarning =
    | SkippedMathExpression of path: string * expr: string
    | MathEvalFailed        of path: string * expr: string
    | UnresolvedHslAlias    of path: string * alias: string

type TokensStudioTheme = {
    Id                : string
    Name              : string
    Group             : string
    SelectedTokenSets : Map<string, string>   // setName -> "enabled"|"disabled"|"source"
}

type TokensStudioMetadata = {
    TokenSetOrder : string list
    ActiveThemes  : string list   // UI state at export time — most reliable active-state record
    ActiveSets    : string list
}

/// Result of shimming a single-file Tokens Studio JSON export.
type ShimResult = {
    /// One DTCG-compatible JSON string per token set, keyed by set name.
    /// Each value is ready to pass directly to Format.parse / Api.import.
    Sets     : Map<string, string>
    Themes   : TokensStudioTheme list
    Metadata : TokensStudioMetadata
    Warnings : ShimWarning list
}

/// Warning produced during multi-set resolution of a Tokens Studio export.
type TokensStudioImportWarning =
    /// A set could not be parsed after shimming (typically: math expressions left as strings
    /// with PreserveMath, or other unsupported syntax) and was excluded from the merge.
    | SetSkipped      of setName: string
    /// A token's alias could not be resolved in the merged file — usually because the set
    /// that contained the referenced token was excluded via SetSkipped.
    | TokenUnresolved of path: string * ref: string
    /// A theme name passed to importTokensStudioThemed was not found in the $themes array.
    | ThemeNotFound   of name: string

/// Result of a Tokens Studio multi-set import. Partial-success: tokens that resolved are
/// returned alongside warnings for sets that could not parse and tokens that could not resolve.
type TokensStudioImportResult = {
    Tokens   : (string list * ResolvedToken) list
    Warnings : TokensStudioImportWarning list
}

/// One theme's fully-resolved tokens (base sets + the theme's own sets merged).
type ThemeSet = {
    ThemeName : string
    Tokens    : (string list * ResolvedToken) list
}

/// Result of a theme-aware Tokens Studio import.
///
/// <c>BaseTokens</c> contains tokens from sets that are not specific to any of the requested
/// themes (i.e., not listed in any theme's <c>selectedTokenSets</c>). These are the global
/// tokens that belong in <c>:root</c>.
///
/// Each <c>ThemeSet</c> contains the full resolution for that theme (base sets plus the
/// theme's own enabled/source sets). A CSS emitter computes per-theme overrides as the
/// diff between each theme's full resolution and the base.
type ThemeAwareImportResult = {
    BaseTokens : (string list * ResolvedToken) list
    Themes     : ThemeSet list
    Warnings   : TokensStudioImportWarning list
}


// ─── Implementation ──────────────────────────────────────────────────────────

module TokensStudio =

    // ── Constant maps ─────────────────────────────────────────────────────────

    /// Tokens Studio type names that are not valid DTCG types → DTCG equivalent.
    let private typeRenames =
        Map.ofList [
            "fontFamilies", "fontFamily"
            "spacing",      "dimension"
            "borderRadius", "dimension"
            "fontSizes",    "dimension"
            "borderWidth",  "dimension"
        ]

    /// Tokens Studio types whose bare-number values need a "px" unit suffix.
    let private unitlessTypes =
        Set.ofList [ "spacing"; "borderRadius"; "fontSizes"; "borderWidth"; "dimension" ]

    /// Field renames inside a typography composite $value.
    let private typographyFieldRenames =
        Map.ofList [
            "fontFamilies", "fontFamily"
            "fontSizes",    "fontSize"
            "fontWeights",  "fontWeight"
            "lineHeights",  "lineHeight"
        ]


    // ── Helpers ───────────────────────────────────────────────────────────────

    let private tryGetNode (key: string) (obj: JsonObject) : JsonNode option =
        let mutable node : JsonNode | null = null
        if obj.TryGetPropertyValue(key, &node) && not (isNull node) then Some node
        else None

    let private tryGetString (key: string) (obj: JsonObject) : string option =
        tryGetNode key obj |> Option.map (fun n -> n.ToString().Trim('"'))

    let private tryGetArray (key: string) (obj: JsonObject) : JsonArray option =
        tryGetNode key obj |> Option.bind (function :? JsonArray as arr -> Some arr | _ -> None)

    let private isAlias (s: string) =
        let t = s.Trim()
        // A pure alias is exactly "{path}" — one brace pair, nothing else.
        // "{a} * {b}" starts with { and ends with } but has a } before the last position.
        t.Length > 2 &&
        t.[0] = '{' &&
        t.[t.Length-1] = '}' &&
        t.IndexOf('{', 1) = -1 &&           // no second opening brace
        t.IndexOf('}', 1) = t.Length - 1    // the only closing brace is the last char

    let private isBareNumber (s: string) =
        let t = s.Trim()
        not (isAlias t) &&
        not (t.Contains("px") || t.Contains("rem") || t.Contains("em")) &&
        Double.TryParse(t, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) |> fst

    // ── Math expression detection ─────────────────────────────────────────────
    // Catches function-call forms (round/pow/ceil/etc.) and binary * or / with
    // numeric or alias operands (e.g. "16 * {zoom}", "{base} / {divisor}").

    let private mathRx =
        Regex(
            @"round\s*\(|pow\s*\(|ceil\s*\(|floor\s*\(|abs\s*\(|sqrt\s*\(|min\s*\(|max\s*\(|\*|/",
            RegexOptions.Compiled)

    let private isMathExpression (s: string) =
        not (isAlias s) && mathRx.IsMatch(s)

    // ── Math expression evaluator ─────────────────────────────────────────────
    // Recursive-descent evaluator for Tokens Studio number math expressions.
    // Grammar: expr = add; add = mul ((+|-) mul)*; mul = unary ((*|/|%) unary)*;
    //          unary = -unary | primary; primary = num | alias | (expr) | fn(args)

    module private MathEval =

        type Tok =
            | TNum of float | TAlias of string | TIdent of string
            | TLParen | TRParen | TComma
            | TPlus | TMinus | TStar | TSlash | TPercent | TEOF

        let private tokenize (s: string) : Tok array =
            let result = ResizeArray<Tok>()
            let mutable i = 0
            let len = s.Length
            while i < len do
                match s.[i] with
                | ' ' | '\t' | '\r' | '\n' -> i <- i + 1
                | '{' ->
                    let j = s.IndexOf('}', i + 1)
                    if j > i then result.Add(TAlias s.[i+1..j-1]); i <- j + 1
                    else i <- i + 1
                | '(' -> result.Add TLParen;  i <- i + 1
                | ')' -> result.Add TRParen;  i <- i + 1
                | ',' -> result.Add TComma;   i <- i + 1
                | '+' -> result.Add TPlus;    i <- i + 1
                | '-' -> result.Add TMinus;   i <- i + 1
                | '*' -> result.Add TStar;    i <- i + 1
                | '/' -> result.Add TSlash;   i <- i + 1
                | '%' -> result.Add TPercent; i <- i + 1
                | c when Char.IsDigit c || (c = '.' && i + 1 < len && Char.IsDigit s.[i+1]) ->
                    let mutable j = i
                    while j < len && (Char.IsDigit s.[j] || s.[j] = '.') do j <- j + 1
                    match Double.TryParse(s.[i..j-1], Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                    | true, f -> result.Add(TNum f)
                    | _ -> ()
                    i <- j
                | c when Char.IsLetter c || c = '_' ->
                    let mutable j = i
                    while j < len && (Char.IsLetterOrDigit s.[j] || s.[j] = '_') do j <- j + 1
                    result.Add(TIdent s.[i..j-1])
                    i <- j
                | _ -> i <- i + 1
            result.Add TEOF
            result.ToArray()

        /// Evaluate a math expression string, resolving {alias} references via index.
        /// seen: alias paths currently on the call stack (cycle detection).
        let rec tryEval (index: Map<string, string>) (seen: Set<string>) (expr: string) : float option =
            let tokens = tokenize (expr.Trim())
            let pos = ref 0
            let peek () = tokens.[!pos]
            let consume () = pos := !pos + 1

            let resolveAlias (path: string) : float option =
                if seen.Contains path then None
                else
                    match Map.tryFind path index with
                    | None -> None
                    | Some raw ->
                        tryEval index (seen.Add path) (raw.Trim().TrimEnd('%').Trim())

            let applyFn (name: string) (args: float list) : float option =
                match name.ToLowerInvariant(), args with
                | "round", [x]    -> Some (Math.Round(x, MidpointRounding.AwayFromZero))
                | "ceil",  [x]    -> Some (Math.Ceiling x)
                | "floor", [x]    -> Some (Math.Floor x)
                | "abs",   [x]    -> Some (Math.Abs x)
                | "sqrt",  [x]    -> Some (Math.Sqrt x)
                | "pow",   [x; y] -> Some (Math.Pow(x, y))
                | "min",   [x; y] -> Some (Math.Min(x, y))
                | "max",   [x; y] -> Some (Math.Max(x, y))
                | _ -> None

            let rec evalExpr () = evalAdd ()

            and evalAdd () =
                let mutable result : float option = evalMul ()
                let mutable cont = result.IsSome
                while cont do
                    match peek () with
                    | TPlus  ->
                        consume ()
                        match evalMul () with
                        | None   -> result <- None; cont <- false
                        | Some r -> result <- result |> Option.map (fun lhs -> lhs + r)
                    | TMinus ->
                        consume ()
                        match evalMul () with
                        | None   -> result <- None; cont <- false
                        | Some r -> result <- result |> Option.map (fun lhs -> lhs - r)
                    | _ -> cont <- false
                result

            and evalMul () =
                let mutable result : float option = evalUnary ()
                let mutable cont = result.IsSome
                while cont do
                    match peek () with
                    | TStar ->
                        consume ()
                        match evalUnary () with
                        | None   -> result <- None; cont <- false
                        | Some r -> result <- result |> Option.map (fun lhs -> lhs * r)
                    | TSlash ->
                        consume ()
                        match evalUnary () with
                        | None   -> result <- None; cont <- false
                        | Some r ->
                            if r = 0.0 then result <- None; cont <- false
                            else result <- result |> Option.map (fun lhs -> lhs / r)
                    | TPercent ->
                        consume ()
                        match evalUnary () with
                        | None   -> result <- None; cont <- false
                        | Some r -> result <- result |> Option.map (fun lhs -> lhs % r)
                    | _ -> cont <- false
                result

            and evalUnary () =
                match peek () with
                | TMinus -> consume (); evalUnary () |> Option.map (~-)
                | TPlus  -> consume (); evalUnary ()
                | _      -> evalPrimary ()

            and evalPrimary () =
                match peek () with
                | TNum f ->
                    consume (); Some f
                | TAlias path ->
                    consume (); resolveAlias path
                | TLParen ->
                    consume ()
                    let v = evalExpr ()
                    if peek () = TRParen then consume ()
                    v
                | TIdent name ->
                    consume ()
                    if peek () = TLParen then
                        consume ()
                        match evalArgList () with
                        | None -> None
                        | Some args ->
                            if peek () = TRParen then consume ()
                            applyFn name args
                    else None
                | _ -> None

            and evalArgList () : float list option =
                if peek () = TRParen then Some []
                else
                    match evalExpr () with
                    | None -> None
                    | Some first ->
                        let mutable args = [first]
                        let mutable cont = true
                        let mutable ok = true
                        while cont do
                            match peek () with
                            | TComma ->
                                consume ()
                                match evalExpr () with
                                | None   -> ok <- false; cont <- false
                                | Some v -> args <- args @ [v]
                            | _ -> cont <- false
                        if ok then Some args else None

            evalExpr ()

    // ── HSL pattern and evaluation ────────────────────────────────────────────

    // Matches: hsla({alias},{alias},{alias},N) or hsla(N,N,N,N)
    // Also matches hsl(...) without alpha.
    let private hslRx =
        Regex(
            @"^hsla?\(\s*(\{[^}]+\}|[\d.]+)\s*,\s*(\{[^}]+\}|[\d.]+)\s*,\s*(\{[^}]+\}|[\d.]+)\s*(?:,\s*([\d.]+)\s*)?\)$",
            RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private hslToHex (h: float) (s: float) (l: float) (alpha: float) : string =
        let s' = s / 100.0
        let l' = l / 100.0
        let c  = (1.0 - abs (2.0 * l' - 1.0)) * s'
        let x  = c * (1.0 - abs (h / 60.0 % 2.0 - 1.0))
        let m  = l' - c / 2.0
        let r', g', b' =
            match int (h / 60.0) with
            | 0 -> c, x, 0.0
            | 1 -> x, c, 0.0
            | 2 -> 0.0, c, x
            | 3 -> 0.0, x, c
            | 4 -> x, 0.0, c
            | _ -> c, 0.0, x
        let toByte v = int (Math.Round((v + m) * 255.0)) |> max 0 |> min 255
        if alpha >= 1.0 then sprintf "#%02x%02x%02x" (toByte r') (toByte g') (toByte b')
        else sprintf "#%02x%02x%02x%02x" (toByte r') (toByte g') (toByte b') (int (Math.Round(alpha * 255.0)))


    // ── Flat token-value index (for HSL alias resolution) ────────────────────
    // Maps dot-path → raw $value string across all sets.

    let private buildFlatIndex (allSets: (string * JsonObject) seq) : Map<string, string> =
        let acc = ResizeArray<string * string>()
        let rec walk (prefix: string) (node: JsonNode) =
            match node with
            | :? JsonObject as obj ->
                if obj.ContainsKey("$type") && obj.ContainsKey("$value") then
                    match obj["$value"] with
                    | null -> ()
                    | v    -> acc.Add(prefix, v.ToString().Trim('"'))
                else
                    for kvp in obj do
                        if not (kvp.Key.StartsWith("$")) && kvp.Value <> null then
                            let childPrefix = if prefix = "" then kvp.Key else prefix + "." + kvp.Key
                            walk childPrefix kvp.Value
            | _ -> ()
        for (_, setObj) in allSets do
            walk "" setObj
        Map.ofSeq acc

    let private resolveToFloat (index: Map<string, string>) (raw: string) : float option =
        let rec follow (seen: Set<string>) (s: string) =
            let s = s.Trim()
            if isAlias s then
                let key = s.[1..s.Length-2]
                if seen.Contains key then None
                else
                    match Map.tryFind key index with
                    | None -> None
                    | Some v -> follow (seen.Add key) v
            else
                // strip trailing % (saturation/lightness stored as bare numbers, no %)
                let t = s.TrimEnd('%').Trim()
                match Double.TryParse(t, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                | true, f -> Some f
                | _ -> None
        follow Set.empty raw


    // ── Typography value transform ────────────────────────────────────────────

    // ── Dimension object builder (DTCG 2025.10 format) ───────────────────────
    // DTCG 2025.10 dimension $value is {value: float, unit: string}, not "16px".
    // upgradeStringValues is NOT called for V2025_10 so we must emit the object form.

    let private dimensionObj (n: float) (unit: string) : JsonNode =
        let o = JsonObject()
        o.Add("value", JsonValue.Create(n))
        o.Add("unit",  JsonValue.Create(unit))
        o :> JsonNode

    let private toDimensionNode (raw: string) : JsonNode option =
        if isAlias raw then None   // caller keeps as alias string
        else
            match Double.TryParse(raw.Trim(), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
            | true, f -> Some (dimensionObj f "px")
            | _ ->
                // already has a unit suffix like "16px" or "1.5rem"
                let m = Regex.Match(raw.Trim(), @"^(-?\d+(?:\.\d+)?)(px|rem)$")
                if m.Success then
                    let f2 = Double.Parse(m.Groups.[1].Value, Globalization.CultureInfo.InvariantCulture)
                    Some (dimensionObj f2 m.Groups.[2].Value)
                else None


    let private transformTypographyValue (v: JsonNode) : JsonNode =
        match v with
        | :? JsonObject as obj ->
            let result = JsonObject()
            for kvp in obj do
                let outKey =
                    match Map.tryFind kvp.Key typographyFieldRenames with
                    | Some k -> k
                    | None   -> kvp.Key
                let rawStr =
                    if kvp.Value <> null then kvp.Value.ToString().Trim('"') else ""
                let outVal =
                    match kvp.Key with
                    | "fontFamilies" ->
                        // Unwrap single-element array: ["Figtree"] → "Figtree"
                        match kvp.Value with
                        | :? JsonArray as arr when arr.Count = 1 && arr.[0] <> null ->
                            arr.[0].DeepClone()
                        | _ -> if kvp.Value <> null then kvp.Value.DeepClone()
                               else JsonValue.Create(null: string) :> JsonNode
                    | "fontSizes" ->
                        // Dimension: alias ref stays as string; literals → {value, unit}
                        if isAlias rawStr then
                            if kvp.Value <> null then kvp.Value.DeepClone()
                            else JsonValue.Create(null: string) :> JsonNode
                        else
                            toDimensionNode rawStr
                            |> Option.defaultWith (fun () ->
                                if kvp.Value <> null then kvp.Value.DeepClone()
                                else JsonValue.Create(null: string) :> JsonNode)
                    | "lineHeights" ->
                        // DTCG lineHeight in typography is a float (unitless ratio or percentage)
                        if isAlias rawStr then
                            if kvp.Value <> null then kvp.Value.DeepClone()
                            else JsonValue.Create(null: string) :> JsonNode
                        else
                            match Double.TryParse(rawStr.TrimEnd('%').Trim(), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                            | true, f -> JsonValue.Create(f) :> JsonNode
                            | _ ->
                                if kvp.Value <> null then kvp.Value.DeepClone()
                                else JsonValue.Create(null: string) :> JsonNode
                    | "fontWeights" ->
                        // DTCG fontWeight: numeric → JSON int, keyword → JSON string.
                        // Tokens Studio may store combined values like "400 Italic" — extract
                        // the leading integer and discard the style suffix (italic is a separate
                        // DTCG field; Tokens Studio doesn't have a separate fontStyle token type).
                        if isAlias rawStr then
                            if kvp.Value <> null then kvp.Value.DeepClone()
                            else JsonValue.Create(null: string) :> JsonNode
                        else
                            let numericPart = rawStr.Split(' ').[0].Trim()
                            match Double.TryParse(numericPart, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                            | true, f when f = Math.Floor(f) ->
                                JsonValue.Create(int f) :> JsonNode
                            | _ ->
                                if kvp.Value <> null then kvp.Value.DeepClone()
                                else JsonValue.Create(null: string) :> JsonNode
                    | _ ->
                        if kvp.Value <> null then kvp.Value.DeepClone()
                        else JsonValue.Create(null: string) :> JsonNode
                result.Add(outKey, outVal)
            result :> JsonNode
        | _ -> if v <> null then v.DeepClone() else JsonValue.Create(null: string) :> JsonNode


    // ── Single token transform ────────────────────────────────────────────────

    let private transformToken
        (config: ShimConfig)
        (index: Map<string, string>)
        (warnings: ResizeArray<ShimWarning>)
        (path: string)
        (tsType: string)
        (value: JsonNode)
        : (string * JsonNode) option =   // Some (dtcgType, newValue) or None to skip

        let dtcgType =
            match Map.tryFind tsType typeRenames with
            | Some t -> t
            | None   -> tsType

        let rawValue =
            match value with
            | null -> ""
            | _    -> value.ToString().Trim('"')

        match tsType with

        // ── number: check for math expressions; convert string literals to JSON numbers
        | "number" ->
            if isMathExpression rawValue then
                match config.MathPolicy with
                | SkipMath ->
                    warnings.Add(SkippedMathExpression (path, rawValue))
                    None
                | PreserveMath ->
                    Some (dtcgType, value)   // keep as string — resolver evaluates later
                | EvaluateMath ->
                    match MathEval.tryEval index Set.empty rawValue with
                    | Some f -> Some (dtcgType, JsonValue.Create(f) :> JsonNode)
                    | None   ->
                        warnings.Add(MathEvalFailed (path, rawValue))
                        None
            elif not (isAlias rawValue) then
                // Tokens Studio stores numbers as JSON strings — convert to JSON number
                match Double.TryParse(rawValue, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                | true, f -> Some (dtcgType, JsonValue.Create(f) :> JsonNode)
                | _ -> Some (dtcgType, value)
            else
                Some (dtcgType, value)

        // ── color: HSL expressions and transparent keyword ───────────────────
        | "color" ->
            if rawValue = "transparent" then
                // Emit DTCG structured color: sRGB black at alpha=0.
                // Using the object form avoids 8-digit hex validation issues.
                let obj = JsonObject()
                obj.Add("colorSpace", JsonValue.Create("srgb"))
                let comps = JsonArray()
                comps.Add(JsonValue.Create(0.0))
                comps.Add(JsonValue.Create(0.0))
                comps.Add(JsonValue.Create(0.0))
                obj.Add("components", comps)
                obj.Add("alpha", JsonValue.Create(0.0))
                Some (dtcgType, obj :> JsonNode)
            else
                let m = hslRx.Match(rawValue)
                if m.Success then
                    let resolve (token: string) =
                        let t = token.Trim()
                        match resolveToFloat index t with
                        | Some f -> Ok f
                        | None ->
                            let alias = if isAlias t then t.[1..t.Length-2] else t
                            warnings.Add(UnresolvedHslAlias (path, alias))
                            Error alias
                    let hR = resolve m.Groups.[1].Value
                    let sR = resolve m.Groups.[2].Value
                    let lR = resolve m.Groups.[3].Value
                    let alpha =
                        if m.Groups.[4].Success then
                            match Double.TryParse(m.Groups.[4].Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                            | true, a -> a
                            | _ -> 1.0
                        else 1.0
                    match hR, sR, lR with
                    | Ok h, Ok s, Ok l ->
                        Some (dtcgType, JsonValue.Create(hslToHex h s l alpha) :> JsonNode)
                    | _ ->
                        Some (dtcgType, value)   // leave as-is; warnings already recorded
                else
                    Some (dtcgType, value)

        // ── dimension-family types: emit DTCG 2025.10 {value, unit} object ────────
        // upgradeStringValues is NOT called for V2025_10, so we must emit the object
        // form directly. Alias refs stay as strings (isRefShape handles them upstream).
        | t when Set.contains t unitlessTypes ->
            if isAlias rawValue then
                Some (dtcgType, value)   // alias ref — parser handles via isRefShape
            else
                match toDimensionNode rawValue with
                | Some node -> Some (dtcgType, node)
                | None      -> Some (dtcgType, value)   // unrecognised form — pass through

        // ── fontFamilies: unwrap single-element array at token level ──────────
        | "fontFamilies" ->
            let newVal =
                match value with
                | :? JsonArray as arr when arr.Count = 1 && arr.[0] <> null ->
                    arr.[0].DeepClone()
                | _ -> if value <> null then value.DeepClone()
                       else JsonValue.Create(null: string) :> JsonNode
            Some ("fontFamily", newVal)

        // ── typography: rename composite field names ──────────────────────────
        | "typography" ->
            Some (dtcgType, transformTypographyValue value)

        // ── everything else: pass through with type rename ────────────────────
        | _ ->
            Some (dtcgType, value)


    // ── Recursive set-tree walker ─────────────────────────────────────────────

    let rec private walkObj
        (config: ShimConfig)
        (index: Map<string, string>)
        (warnings: ResizeArray<ShimWarning>)
        (path: string)
        (obj: JsonObject)
        : JsonObject =

        let result = JsonObject()

        // Preserve group-level $type if present without $value (inherited type hint)
        if obj.ContainsKey("$type") && not (obj.ContainsKey("$value")) then
            let groupType = obj["$type"].ToString().Trim('"')
            let dtcgType  = match Map.tryFind groupType typeRenames with Some t -> t | None -> groupType
            result.Add("$type", JsonValue.Create(dtcgType))

        for kvp in obj do
            if not (kvp.Key.StartsWith("$")) then
                match kvp.Value with
                | null -> ()
                | :? JsonObject as child ->
                    let childPath = if path = "" then kvp.Key else path + "." + kvp.Key
                    if child.ContainsKey("$type") && child.ContainsKey("$value") then
                        // Token leaf
                        let tsType = child["$type"].ToString().Trim('"')
                        match transformToken config index warnings childPath tsType child["$value"] with
                        | None -> ()
                        | Some (dtcgType, newValue) ->
                            let leaf = JsonObject()
                            leaf.Add("$type", JsonValue.Create(dtcgType))
                            leaf.Add("$value", newValue.DeepClone())
                            match tryGetString "$description" child with
                            | Some d when d.Length > 0 -> leaf.Add("$description", JsonValue.Create(d))
                            | _ -> ()
                            result.Add(kvp.Key, leaf)
                    else
                        // Group — recurse
                        let childResult = walkObj config index warnings childPath child
                        if childResult.Count > 0 then
                            result.Add(kvp.Key, childResult)
                | _ -> ()

        result


    // ── $themes parser ────────────────────────────────────────────────────────

    let private parseThemes (node: JsonNode) : TokensStudioTheme list =
        match node with
        | :? JsonArray as arr ->
            arr
            |> Seq.choose (function
                | :? JsonObject as obj ->
                    let sets =
                        match tryGetNode "selectedTokenSets" obj with
                        | Some (:? JsonObject as setsObj) ->
                            setsObj
                            |> Seq.map (fun kvp -> kvp.Key, kvp.Value.ToString().Trim('"'))
                            |> Map.ofSeq
                        | _ -> Map.empty
                    Some {
                        Id                = tryGetString "id"    obj |> Option.defaultValue ""
                        Name              = tryGetString "name"  obj |> Option.defaultValue ""
                        Group             = tryGetString "group" obj |> Option.defaultValue ""
                        SelectedTokenSets = sets
                    }
                | _ -> None)
            |> List.ofSeq
        | _ -> []


    // ── $metadata parser ──────────────────────────────────────────────────────

    let private parseMetadata (node: JsonNode) : TokensStudioMetadata =
        match node with
        | :? JsonObject as obj ->
            let strList key =
                match tryGetArray key obj with
                | Some arr -> arr |> Seq.map (fun n -> n.ToString().Trim('"')) |> List.ofSeq
                | None     -> []
            { TokenSetOrder = strList "tokenSetOrder"
              ActiveThemes  = strList "activeThemes"
              ActiveSets    = strList "activeSets" }
        | _ -> { TokenSetOrder = []; ActiveThemes = []; ActiveSets = [] }


    // ── Public API ────────────────────────────────────────────────────────────

    /// Shim a Tokens Studio single-file export to DTCG 2025.10 per-set JSON.
    ///
    /// Input: the full JSON text from Penpot's Tokens panel → Tools → Export.
    /// Output: ShimResult with one DTCG JSON string per set, plus extracted
    ///         themes and metadata.
    let shimSingleFile (config: ShimConfig) (jsonText: string) : Result<ShimResult, string> =
        try
            match JsonNode.Parse(jsonText) with
            | :? JsonObject as root ->
                let themes   = tryGetNode "$themes"   root |> Option.map parseThemes   |> Option.defaultValue []
                let metadata = tryGetNode "$metadata" root |> Option.map parseMetadata |> Option.defaultValue { TokenSetOrder = []; ActiveThemes = []; ActiveSets = [] }

                let sets =
                    root
                    |> Seq.choose (fun kvp ->
                        if kvp.Key.StartsWith("$") then None
                        else
                            match kvp.Value with
                            | :? JsonObject as obj -> Some (kvp.Key, obj)
                            | _ -> None)
                    |> Array.ofSeq

                let index    = buildFlatIndex sets
                let warnings = ResizeArray<ShimWarning>()
                let opts     = JsonSerializerOptions(WriteIndented = true)

                let transformedSets =
                    sets
                    |> Array.map (fun (setName, setObj) ->
                        let transformed = walkObj config index warnings "" setObj
                        setName, transformed.ToJsonString(opts))
                    |> Map.ofArray

                Ok {
                    Sets     = transformedSets
                    Themes   = themes
                    Metadata = metadata
                    Warnings = List.ofSeq warnings
                }
            | _ -> Error "root is not a JSON object"
        with ex ->
            Error ex.Message

    /// Shim with default config (math expressions preserved).
    let shim (jsonText: string) : Result<ShimResult, string> =
        shimSingleFile ShimConfig.defaults jsonText

    /// Format a ShimWarning as a human-readable string.
    let formatWarning (w: ShimWarning) : string =
        match w with
        | SkippedMathExpression (path, expr) ->
            sprintf "SKIP  %s — math expression: %s" path expr
        | MathEvalFailed (path, expr) ->
            sprintf "EVAL  %s — could not evaluate: %s" path expr
        | UnresolvedHslAlias (path, alias) ->
            sprintf "WARN  %s — unresolved HSL alias: %s" path alias

    /// Format a TokensStudioImportWarning as a human-readable string.
    let formatImportWarning (w: TokensStudioImportWarning) : string =
        match w with
        | SetSkipped name ->
            sprintf "SKIP  set '%s' — parse failed (contains math expressions or unsupported syntax)" name
        | TokenUnresolved (path, ref) ->
            sprintf "UNRESOLVED  %s → %s" path ref
        | ThemeNotFound name ->
            sprintf "THEME  '%s' — not found in $themes" name
