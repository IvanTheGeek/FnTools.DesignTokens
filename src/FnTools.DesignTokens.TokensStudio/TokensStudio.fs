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
    | SkippedMathExpression      of path: string * expr: string
    | MathEvalFailed             of path: string * expr: string
    /// Math evaluation failed and the expression likely references a theme-variant alias
    /// (a set that is enabled in some themes but not others). The flat index excludes
    /// variant sets to avoid theme-bleed, so variant aliases cannot resolve here.
    /// Use <c>Api.importTokensStudioThemed</c> for correct per-theme resolution.
    | MathEvalFailedVariantAlias of path: string * expr: string
    | UnresolvedHslAlias         of path: string * alias: string

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

/// Warning produced during Tokens Studio export.
type ExportWarning =
    /// A color value was converted from wide-gamut to an approximate sRGB hex string.
    /// The structured original is preserved in the token's <c>$extensions</c> under
    /// <c>com.fntools.designtokens.originalColor</c> for lossless DTCG round-trip
    /// (ADR-023). Penpot strips extensions on import — the warning still flags the
    /// loss for any pipeline stage that does not preserve <c>$extensions</c>.
    | LossyColorConversion of path: string * original: string

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

    /// Vendor namespace for our $extensions payload (ADR-023).
    let private extVendorKey = "com.fntools.designtokens"
    let private extOriginalColorKey = "originalColor"

    let private tryGetNode (key: string) (obj: JsonObject) : JsonNode option =
        let mutable node : JsonNode | null = null
        if obj.TryGetPropertyValue(key, &node) then
            match node with
            | null   -> None
            | nonNul -> Some nonNul
        else None

    /// Clone a possibly-null JsonNode: non-null clones via DeepClone; null stays null.
    /// In System.Text.Json.Nodes, a C# null inside a JsonObject/JsonArray represents
    /// the JSON `null` literal — preserving null on input is the intended semantics.
    let private cloneOrNull (n: JsonNode | null) : JsonNode | null =
        match Option.ofObj n with
        | Some v -> v.DeepClone()
        | None -> null

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
            @"round\s*\(|pow\s*\(|ceil\s*\(|floor\s*\(|abs\s*\(|sqrt\s*\(|min\s*\(|max\s*\(|sin\s*\(|cos\s*\(|tan\s*\(|log\s*\(|exp\s*\(|\*|/|\^",
            RegexOptions.Compiled)

    let private isMathExpression (s: string) =
        not (isAlias s) && mathRx.IsMatch(s)

    // ── Math expression evaluator ─────────────────────────────────────────────
    // Recursive-descent evaluator for Tokens Studio number math expressions.
    // Grammar: expr = add; add = mul ((+|-) mul)*; mul = pow ((*|/|%) pow)*;
    //          pow = unary ('^' pow)*; unary = -unary | primary;
    //          primary = num | alias | (expr) | fn(args)
    // Any unrecognised character produces TUnknown, which propagates to None.

    module private MathEval =

        type Tok =
            | TNum of float | TAlias of string | TIdent of string
            | TLParen | TRParen | TComma
            | TPlus | TMinus | TStar | TSlash | TPercent | TCaret
            | TUnknown of char | TEOF

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
                    else result.Add(TUnknown '{'); i <- i + 1   // unclosed alias — explicit failure
                | '(' -> result.Add TLParen;  i <- i + 1
                | ')' -> result.Add TRParen;  i <- i + 1
                | ',' -> result.Add TComma;   i <- i + 1
                | '+' -> result.Add TPlus;    i <- i + 1
                | '-' -> result.Add TMinus;   i <- i + 1
                | '*' -> result.Add TStar;    i <- i + 1
                | '/' -> result.Add TSlash;   i <- i + 1
                | '%' -> result.Add TPercent; i <- i + 1
                | '^' -> result.Add TCaret;   i <- i + 1
                | c when Char.IsDigit c || (c = '.' && i + 1 < len && Char.IsDigit s.[i+1]) ->
                    let mutable j = i
                    while j < len && (Char.IsDigit s.[j] || s.[j] = '.') do j <- j + 1
                    match Double.TryParse(s.[i..j-1], Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                    | true, f -> result.Add(TNum f)
                    | _       -> result.Add(TUnknown s.[i])   // malformed number literal — explicit failure
                    i <- j
                | c when Char.IsLetter c || c = '_' ->
                    let mutable j = i
                    while j < len && (Char.IsLetterOrDigit s.[j] || s.[j] = '_') do j <- j + 1
                    result.Add(TIdent s.[i..j-1])
                    i <- j
                | c -> result.Add(TUnknown c); i <- i + 1   // unrecognised character — explicit failure
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
                | "sin",   [x]    -> Some (Math.Sin x)
                | "cos",   [x]    -> Some (Math.Cos x)
                | "tan",   [x]    -> Some (Math.Tan x)
                | "asin",  [x]    -> Some (Math.Asin x)
                | "acos",  [x]    -> Some (Math.Acos x)
                | "atan",  [x]    -> Some (Math.Atan x)
                | "atan2", [x; y] -> Some (Math.Atan2(x, y))
                | "log",   [x]    -> Some (Math.Log x)
                | "log2",  [x]    -> Some (Math.Log2 x)
                | "log10", [x]    -> Some (Math.Log10 x)
                | "exp",   [x]    -> Some (Math.Exp x)
                | _ -> None   // unrecognised function or wrong arity → explicit failure

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
                let mutable result : float option = evalPow ()
                let mutable cont = result.IsSome
                while cont do
                    match peek () with
                    | TStar ->
                        consume ()
                        match evalPow () with
                        | None   -> result <- None; cont <- false
                        | Some r -> result <- result |> Option.map (fun lhs -> lhs * r)
                    | TSlash ->
                        consume ()
                        match evalPow () with
                        | None   -> result <- None; cont <- false
                        | Some r ->
                            if r = 0.0 then result <- None; cont <- false
                            else result <- result |> Option.map (fun lhs -> lhs / r)
                    | TPercent ->
                        consume ()
                        match evalPow () with
                        | None   -> result <- None; cont <- false
                        | Some r -> result <- result |> Option.map (fun lhs -> lhs % r)
                    | _ -> cont <- false
                result

            // Right-associative: a^b^c = a^(b^c). Higher precedence than *.
            and evalPow () =
                match evalUnary () with
                | None -> None
                | Some lhs ->
                    match peek () with
                    | TCaret ->
                        consume ()
                        match evalPow () with   // right-recursive for right-associativity
                        | None     -> None
                        | Some rhs -> Some (Math.Pow(lhs, rhs))
                    | _ -> Some lhs

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
                | TUnknown _ -> None   // unrecognised character — explicit failure, not silent skip
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
                    match Option.ofObj obj["$value"] with
                    | None   -> ()
                    | Some v -> acc.Add(prefix, v.ToString().Trim('"'))
                else
                    for kvp in obj do
                        if not (kvp.Key.StartsWith("$")) then
                            match Option.ofObj kvp.Value with
                            | Some child ->
                                let childPrefix = if prefix = "" then kvp.Key else prefix + "." + kvp.Key
                                walk childPrefix child
                            | None -> ()
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


    let private transformTypographyValue (index: Map<string, string>) (v: JsonNode) : JsonNode =
        match v with
        | :? JsonObject as obj ->
            let result = JsonObject()
            for kvp in obj do
                let outKey =
                    match Map.tryFind kvp.Key typographyFieldRenames with
                    | Some k -> k
                    | None   -> kvp.Key
                let rawStr =
                    match Option.ofObj kvp.Value with
                    | Some n -> n.ToString().Trim('"')
                    | None   -> ""
                let outVal : JsonNode | null =
                    match kvp.Key with
                    | "fontFamilies" ->
                        // Unwrap single-element array: ["Figtree"] → "Figtree"
                        match kvp.Value with
                        | :? JsonArray as arr when arr.Count = 1 ->
                            match Option.ofObj arr.[0] with
                            | Some item -> item.DeepClone()
                            | None      -> cloneOrNull kvp.Value
                        | _ -> cloneOrNull kvp.Value
                    | "fontSizes" ->
                        // Dimension: literals → {value, unit}; alias refs resolved eagerly
                        // via the flat index so cross-type number→dimension aliases resolve
                        // at shim time rather than failing in the type-strict resolver.
                        if isAlias rawStr then
                            match MathEval.tryEval index Set.empty rawStr with
                            | Some f -> dimensionObj f "px"
                            | None   -> cloneOrNull kvp.Value
                        else
                            toDimensionNode rawStr
                            |> Option.map (fun n -> (n: JsonNode | null))
                            |> Option.defaultWith (fun () -> cloneOrNull kvp.Value)
                    | "lineHeights" ->
                        // DTCG lineHeight in typography is a float (unitless ratio or percentage)
                        if isAlias rawStr then
                            cloneOrNull kvp.Value
                        else
                            match Double.TryParse(rawStr.TrimEnd('%').Trim(), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                            | true, f -> JsonValue.Create(f) :> JsonNode | null
                            | _       -> cloneOrNull kvp.Value
                    | "fontWeights" ->
                        // DTCG fontWeight: numeric → JSON int, keyword → JSON string.
                        // Tokens Studio may store combined values like "400 Italic" — extract
                        // the leading integer and discard the style suffix (italic is a separate
                        // DTCG field; Tokens Studio doesn't have a separate fontStyle token type).
                        if isAlias rawStr then
                            cloneOrNull kvp.Value
                        else
                            let numericPart = rawStr.Split(' ').[0].Trim()
                            match Double.TryParse(numericPart, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                            | true, f when f = Math.Floor(f) ->
                                JsonValue.Create(int f) :> JsonNode | null
                            | _ -> cloneOrNull kvp.Value
                    | _ -> cloneOrNull kvp.Value
                result.Add(outKey, outVal)
            result :> JsonNode
        | _ -> v.DeepClone()


    // ── Single token transform ────────────────────────────────────────────────

    let private transformToken
        (config           : ShimConfig)
        (index            : Map<string, string>)   // global flat index: HSL + typography alias resolution
        (mathIndex        : Map<string, string>)   // variant-filtered index: math expression evaluation only
        (warnings         : ResizeArray<ShimWarning>)
        (isVariantFiltered: bool)
        (path             : string)
        (tsType           : string)
        (value            : JsonNode)
        : (string * JsonNode) option =   // Some (dtcgType, newValue) or None to skip

        let dtcgType =
            match Map.tryFind tsType typeRenames with
            | Some t -> t
            | None   -> tsType

        let rawValue = value.ToString().Trim('"')

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
                    match MathEval.tryEval mathIndex Set.empty rawValue with
                    | Some f -> Some (dtcgType, JsonValue.Create(f) :> JsonNode)
                    | None   ->
                        let w =
                            if isVariantFiltered then MathEvalFailedVariantAlias (path, rawValue)
                            else MathEvalFailed (path, rawValue)
                        warnings.Add(w)
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
                        match Option.ofObj (JsonValue.Create(hslToHex h s l alpha)) with
                        | Some v -> Some (dtcgType, v :> JsonNode)
                        | None   -> Some (dtcgType, value)
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
                | :? JsonArray as arr when arr.Count = 1 ->
                    match Option.ofObj arr.[0] with
                    | Some item -> item.DeepClone()
                    | None      -> value.DeepClone()
                | _ -> value.DeepClone()
            Some ("fontFamily", newVal)

        // ── typography: rename composite field names ──────────────────────────
        | "typography" ->
            Some (dtcgType, transformTypographyValue index value)

        // ── everything else: pass through with type rename ────────────────────
        | _ ->
            Some (dtcgType, value)


    // ── Import: $extensions originalColor reconstruction (ADR-023) ──────────
    //
    // On import, when a color token's $extensions carries the structured
    // originalColor payload we wrote on export, replace the $value (which is
    // the lossy sRGB hex approximation) with a reconstructed DTCG color object.
    // The Tokens-Studio-side hex remains as the `hex` fallback inside the
    // structured value, so downstream tools that don't understand wide-gamut
    // colorSpaces still have a usable approximation.
    //
    // Extensions outside our vendor namespace are passed through verbatim so
    // unknown vendor extensions survive a round-trip per the DTCG spec.

    /// Read the originalColor payload from a token's $extensions, if present
    /// and structurally valid. Returns the inner JsonObject (colorSpace +
    /// components mandatory; alpha + hex optional) for use as a $value.
    let private tryReadOriginalColor (child: JsonObject) : JsonObject option =
        match tryGetNode "$extensions" child with
        | Some (:? JsonObject as ext) ->
            match tryGetNode extVendorKey ext with
            | Some (:? JsonObject as vendor) ->
                match tryGetNode extOriginalColorKey vendor with
                | Some (:? JsonObject as oc)
                    when oc.ContainsKey("colorSpace") && oc.ContainsKey("components") ->
                    Some oc
                | _ -> None
            | _ -> None
        | _ -> None

    /// Pattern that <c>lossyAnnotation</c> emits into <c>$description</c>.
    /// Reverse-engineered to recover the original wide-gamut color when
    /// <c>$extensions</c> have been stripped (e.g. by Penpot). Lower fidelity
    /// than the structured extension — components are parsed by string and
    /// "none" markers are honoured, but no <c>hex</c> field is recovered.
    let private lossyAnnotationRx =
        Regex(@"source:\s*color\(\s*([a-z0-9-]+)\s+([^)]+?)\s*\)\s*—\s*converted to sRGB hex for Tokens Studio compatibility",
              RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    /// Best-effort parser for a description annotation produced by our exporter.
    /// Returns a DTCG color JsonObject when the pattern matches and at least
    /// 3 components parse cleanly. Returns None otherwise; the caller should
    /// fall through to using the literal $value hex.
    let private tryParseColorFromDescription (desc: string) : JsonObject option =
        let m = lossyAnnotationRx.Match(desc)
        if not m.Success then None
        else
            let csStr = m.Groups.[1].Value
            let body  = m.Groups.[2].Value
            // Split on " / " to separate alpha (if any) from the components.
            let mainStr, alphaStr =
                let idx = body.IndexOf('/')
                if idx >= 0 then body.Substring(0, idx).Trim(), Some (body.Substring(idx + 1).Trim())
                else body.Trim(), None
            let parseComp (s: string) : (JsonNode | null) option =
                let t = s.Trim()
                if t = "none" then Some null
                else
                    match Double.TryParse(t, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                    | true, f ->
                        let v : JsonNode | null = JsonValue.Create(f)
                        Some v
                    | _       -> None
            let parts = mainStr.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
            if parts.Length < 3 then None
            else
                let comps = parts |> Array.take 3 |> Array.map parseComp
                if comps |> Array.exists Option.isNone then None
                else
                    let oc = JsonObject()
                    oc.Add("colorSpace", JsonValue.Create(csStr))
                    let arr = JsonArray()
                    for c in comps do arr.Add(c.Value)
                    oc.Add("components", arr)
                    match alphaStr |> Option.bind (fun s ->
                            match Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                            | true, a -> Some a
                            | _       -> None) with
                    | Some a -> oc.Add("alpha", JsonValue.Create(a))
                    | None   -> ()
                    Some oc

    let private tryRecoverWideGamutColor (child: JsonObject) : JsonObject option =
        match tryReadOriginalColor child with
        | Some _ as some -> some
        | None ->
            match tryGetString "$description" child with
            | Some d when d.Length > 0 -> tryParseColorFromDescription d
            | _ -> None

    /// Clone the source $extensions for emission, removing our reconstruction
    /// marker (which is a transport artifact, not part of canonical DTCG).
    /// If our vendor namespace becomes empty after removal, the namespace key
    /// itself is removed. Returns None when no extensions remain.
    let private cloneExtensionsForOutput (child: JsonObject) : JsonObject option =
        match tryGetNode "$extensions" child with
        | Some (:? JsonObject as ext) ->
            let cloned = ext.DeepClone() :?> JsonObject
            match tryGetNode extVendorKey cloned with
            | Some (:? JsonObject as vendor) ->
                if vendor.ContainsKey extOriginalColorKey then
                    vendor.Remove extOriginalColorKey |> ignore
                if vendor.Count = 0 then cloned.Remove extVendorKey |> ignore
            | _ -> ()
            if cloned.Count = 0 then None else Some cloned
        | _ -> None


    // ── Recursive set-tree walker ─────────────────────────────────────────────

    let rec private walkObj
        (config           : ShimConfig)
        (index            : Map<string, string>)   // global index: HSL + typography alias resolution
        (mathIndex        : Map<string, string>)   // variant-filtered index: math evaluation only
        (warnings         : ResizeArray<ShimWarning>)
        (isVariantFiltered: bool)
        (inheritedType    : string option)
        (path             : string)
        (obj              : JsonObject)
        : JsonObject =

        let result = JsonObject()

        // Group-level $type: preserve in output AND propagate to descendants
        // per DTCG §7.4 type inheritance (Tokens Studio uses this pattern heavily).
        let scopeType =
            if obj.ContainsKey("$type") && not (obj.ContainsKey("$value")) then
                let groupType =
                    match Option.ofObj obj["$type"] with
                    | Some n -> n.ToString().Trim('"')
                    | None   -> ""
                let dtcgType  = match Map.tryFind groupType typeRenames with Some t -> t | None -> groupType
                result.Add("$type", JsonValue.Create(dtcgType))
                Some groupType
            else
                inheritedType

        for kvp in obj do
            if not (kvp.Key.StartsWith("$")) then
                match kvp.Value with
                | null -> ()
                | :? JsonObject as child ->
                    let childPath = if path = "" then kvp.Key else path + "." + kvp.Key
                    let childHasOwnType = child.ContainsKey("$type")
                    let isLeaf =
                        child.ContainsKey("$value")
                        && (childHasOwnType || scopeType.IsSome)
                    if isLeaf then
                        // Token leaf — use own $type if present, else inherited.
                        let tsType =
                            if childHasOwnType then
                                match Option.ofObj child["$type"] with
                                | Some n -> n.ToString().Trim('"')
                                | None   -> scopeType |> Option.defaultValue ""
                            else scopeType.Value
                        match Option.ofObj child["$value"] with
                        | None -> ()
                        | Some valueNode ->
                        match transformToken config index mathIndex warnings isVariantFiltered childPath tsType valueNode with
                        | None -> ()
                        | Some (dtcgType, newValue) ->
                            let leaf = JsonObject()
                            leaf.Add("$type", JsonValue.Create(dtcgType))
                            // For colors, prefer the structured originalColor payload from
                            // $extensions over the lossy hex in $value (ADR-023). If absent,
                            // try parsing the lossy annotation out of $description as a
                            // graceful-degradation path for pipelines that strip extensions
                            // (e.g. Penpot). Fall through to the hex when neither matches.
                            let valueNode =
                                if dtcgType = "color" then
                                    match tryRecoverWideGamutColor child with
                                    | Some oc -> oc.DeepClone()
                                    | None    -> newValue.DeepClone()
                                else
                                    newValue.DeepClone()
                            leaf.Add("$value", valueNode)
                            match tryGetString "$description" child with
                            | Some d when d.Length > 0 -> leaf.Add("$description", JsonValue.Create(d))
                            | _ -> ()
                            // Pass through any non-marker $extensions so unknown vendor
                            // extensions survive the round-trip per the DTCG spec.
                            match cloneExtensionsForOutput child with
                            | Some ext -> leaf.Add("$extensions", ext)
                            | None     -> ()
                            result.Add(kvp.Key, leaf)
                    else
                        // Group — recurse, threading the inherited type scope.
                        let childResult = walkObj config index mathIndex warnings isVariantFiltered scopeType childPath child
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
                            |> Seq.map (fun kvp ->
                                let v =
                                    match Option.ofObj kvp.Value with
                                    | Some n -> n.ToString().Trim('"')
                                    | None   -> ""
                                kvp.Key, v)
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
                | Some arr ->
                    arr
                    |> Seq.choose (fun n ->
                        match Option.ofObj n with
                        | Some node -> Some (node.ToString().Trim('"'))
                        | None      -> None)
                    |> List.ofSeq
                | None     -> []
            { TokenSetOrder = strList "tokenSetOrder"
              ActiveThemes  = strList "activeThemes"
              ActiveSets    = strList "activeSets" }
        | _ -> { TokenSetOrder = []; ActiveThemes = []; ActiveSets = [] }


    // ── Public API ────────────────────────────────────────────────────────────

    /// Core shim implementation. <c>mathIndexFilter</c> restricts which sets contribute
    /// to the flat alias-resolution index used by the math evaluator:
    /// <c>None</c> uses all sets (global index); <c>Some names</c> uses only the named sets.
    /// All sets are still shimmed regardless of the filter — only math alias resolution
    /// is restricted to the filtered index.
    let private shimCore
        (config          : ShimConfig)
        (mathIndexFilter : Set<string> option)
        (jsonText        : string)
        : Result<ShimResult, string> =
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

                // Global index: all sets. Used for HSL alias resolution and typography
                // fontSizes alias resolution — these don't suffer from theme-bleed.
                let globalIndex = buildFlatIndex sets

                // Variant-set detection (only when no explicit filter provided).
                // A set is variant if, within any theme modifier group (non-empty group
                // field), it is "enabled" in some themes of that group but not in others.
                // Per-group detection avoids false positives from pseudo-themes that appear
                // in only one group (e.g. an "Always-on" group with a single theme).
                let variantSets =
                    match mathIndexFilter with
                    | Some _ -> Set.empty   // caller controls the math index — no auto-detection
                    | None ->
                        if themes.IsEmpty then Set.empty
                        else
                            themes
                            |> List.filter (fun t -> t.Group <> "")
                            |> List.groupBy (fun t -> t.Group)
                            |> List.collect (fun (_, groupThemes) ->
                                sets
                                |> Array.choose (fun (name, _) ->
                                    let statuses =
                                        groupThemes |> List.map (fun t ->
                                            t.SelectedTokenSets
                                            |> Map.tryFind name
                                            |> Option.defaultValue "disabled")
                                    if statuses |> List.exists (fun s -> s = "enabled") &&
                                       statuses |> List.exists (fun s -> s <> "enabled")
                                    then Some name
                                    else None)
                                |> Array.toList)
                            |> Set.ofList

                let isVariantFiltered = not (Set.isEmpty variantSets)

                // Math index: variant-filtered when auto-detected; caller-specified when
                // an explicit filter is provided (shimSingleFileWithMathIndex); global
                // when there are no variant sets.
                // HSL and typography alias resolution always use globalIndex.
                let mathIndex =
                    match mathIndexFilter with
                    | Some names ->
                        buildFlatIndex (sets |> Array.filter (fun (name, _) -> names.Contains name))
                    | None ->
                        if isVariantFiltered then
                            buildFlatIndex (sets |> Array.filter (fun (name, _) -> not (Set.contains name variantSets)))
                        else globalIndex   // no variants — reuse global index

                let warnings = ResizeArray<ShimWarning>()
                let opts     = JsonSerializerOptions(WriteIndented = true)

                let transformedSets =
                    sets
                    |> Array.map (fun (setName, setObj) ->
                        let transformed = walkObj config globalIndex mathIndex warnings isVariantFiltered None "" setObj
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

    /// Shim a Tokens Studio single-file export to DTCG 2025.10 per-set JSON.
    ///
    /// Input: the full JSON text from Penpot's Tokens panel → Tools → Export.
    /// Output: ShimResult with one DTCG JSON string per set, plus extracted
    ///         themes and metadata.
    ///
    /// Math expressions are evaluated using the full flat index (all sets combined,
    /// last-set-wins for duplicate paths). For per-theme math evaluation, use
    /// <see cref="shimSingleFileWithMathIndex"/>.
    let shimSingleFile (config: ShimConfig) (jsonText: string) : Result<ShimResult, string> =
        shimCore config None jsonText

    /// Like <see cref="shimSingleFile"/>, but evaluates math expressions using only the
    /// tokens from <c>mathIndexSets</c> to build the alias-resolution index.
    ///
    /// Use this in themed import pipelines to prevent mutually-exclusive sets (e.g.
    /// <c>Text zoom/100%</c> vs <c>Text zoom/200%</c>) from contaminating each other's
    /// math evaluation. Pass the set names active for the current theme as <c>mathIndexSets</c>;
    /// all sets are still shimmed — only math alias resolution is restricted.
    let shimSingleFileWithMathIndex
        (config        : ShimConfig)
        (mathIndexSets : string list)
        (jsonText      : string)
        : Result<ShimResult, string> =
        shimCore config (Some (Set.ofList mathIndexSets)) jsonText

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
        | MathEvalFailedVariantAlias (path, expr) ->
            sprintf "EVAL  %s — could not evaluate: %s (references a theme-variant alias; use Api.importTokensStudioThemed for correct per-theme resolution)" path expr
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

    /// Format an ExportWarning as a human-readable string.
    let formatExportWarning (w: ExportWarning) : string =
        match w with
        | LossyColorConversion (path, original) ->
            sprintf "LOSSY  %s — %s" path original


    // ── Export: type name ─────────────────────────────────────────────────────

    let private typeNameStr (t: TokenType) : string =
        match t with
        | ColorType      -> "color"       | DimensionType   -> "dimension"
        | FontFamilyType -> "fontFamily"  | FontWeightType  -> "fontWeight"
        | DurationType   -> "duration"    | CubicBezierType -> "cubicBezier"
        | NumberType     -> "number"      | StrokeStyleType -> "strokeStyle"
        | BorderType     -> "border"      | ShadowType      -> "shadow"
        | TransitionType -> "transition"  | GradientType    -> "gradient"
        | TypographyType -> "typography"


    // ── Export: color helpers ─────────────────────────────────────────────────

    let private colorSpaceStr (cs: ColorSpace) : string =
        match cs with
        | SRGB         -> "srgb"          | SRGBLinear   -> "srgb-linear"
        | DisplayP3    -> "display-p3"    | A98RGB       -> "a98-rgb"
        | ProPhotoRGB  -> "prophoto-rgb"  | Rec2020      -> "rec2020"
        | HSL          -> "hsl"           | HWB          -> "hwb"
        | Lab          -> "lab"           | LCH          -> "lch"
        | OKLab        -> "oklab"         | OKLCH        -> "oklch"
        | XYZD65       -> "xyz-d65"       | XYZD50       -> "xyz-d50"

    let private compF (c: ColorComponent) : float =
        match c with Channel f -> f | Missing -> 0.0

    let private compByte (c: ColorComponent) : int =
        int (Math.Round(compF c * 255.0)) |> max 0 |> min 255

    let private colorToHex (c: ColorValue) : string =
        let (c1, c2, c3) = c.Components
        match c.Alpha with
        | Some a when a < 1.0 ->
            let a8 = int (Math.Round(a * 255.0)) |> max 0 |> min 255
            sprintf "#%02x%02x%02x%02x" (compByte c1) (compByte c2) (compByte c3) a8
        | _ ->
            sprintf "#%02x%02x%02x" (compByte c1) (compByte c2) (compByte c3)

    let private colorDtcgStr (c: ColorValue) : string =
        let fStr cc = match cc with Channel f -> sprintf "%g" f | Missing -> "none"
        let (c1, c2, c3) = c.Components
        let alphaStr = match c.Alpha with Some a when a < 1.0 -> sprintf " / %g" a | _ -> ""
        sprintf "color(%s %s %s %s%s)"
            (colorSpaceStr c.ColorSpace) (fStr c1) (fStr c2) (fStr c3) alphaStr

    /// Wrap a primitive value in a JsonValue node. JsonValue.Create<T> is annotated
    /// as nullable in F# 9 even for non-nullable T; we surface the nullable result
    /// as JsonNode | null so call sites stay null-aware. Consumers (JsonObject.Add,
    /// JsonArray.Add, leaf.Add) all accept nullable JsonNode, so a literal null
    /// here yields a JSON `null` entry in the surrounding container.
    let inline private jv<'T> (x: 'T) : JsonNode | null =
        JsonValue.Create<'T>(x) :> JsonNode | null

    let private compToNode (cc: ColorComponent) : JsonNode | null =
        match cc with
        | Channel f -> jv f
        | Missing   -> null   // JSON null preserves the "missing component" semantics

    /// Build the structured originalColor payload for the $extensions vendor namespace.
    /// Shape: { colorSpace, components: [3], alpha?, hex? }. Alpha and hex are
    /// included only if present on the source ColorValue, so round-trip is exact.
    let private buildOriginalColorPayload (c: ColorValue) : JsonObject =
        let oc = JsonObject()
        oc.Add("colorSpace", JsonValue.Create(colorSpaceStr c.ColorSpace))
        let (c1, c2, c3) = c.Components
        let comps = JsonArray()
        comps.Add(compToNode c1)
        comps.Add(compToNode c2)
        comps.Add(compToNode c3)
        oc.Add("components", comps)
        match c.Alpha with
        | Some a -> oc.Add("alpha", JsonValue.Create(a))
        | None   -> ()
        match c.Hex with
        | Some h -> oc.Add("hex", JsonValue.Create(h))
        | None   -> ()
        oc

    /// Serialize a ColorValue to a hex string for the $value slot.
    /// Returns (hex, Some originalColorPayload) when conversion is lossy (wide-gamut
    /// without an explicit Hex fallback) so the caller can emit a $extensions
    /// reconstructor; (hex, None) when the conversion is lossless.
    let private exportColorHex
        (path    : string)
        (c       : ColorValue)
        (warnings: ResizeArray<ExportWarning>)
        : string * JsonObject option =
        match c.Hex with
        | Some hex -> hex, None
        | None ->
            match c.ColorSpace with
            | SRGB ->
                colorToHex c, None
            | _ ->
                let hex = colorToHex c
                warnings.Add(LossyColorConversion (path, colorDtcgStr c))
                hex, Some (buildOriginalColorPayload c)


    // ── Export: value helpers ─────────────────────────────────────────────────

    let private fwKeywordStr (k: FontWeightKeyword) : string =
        match k with
        | Thin | Hairline          -> "100"
        | ExtraLight | UltraLight  -> "200"
        | Light                    -> "300"
        | Normal | Regular | Book  -> "400"
        | Medium                   -> "500"
        | SemiBold | DemiBold      -> "600"
        | Bold                     -> "700"
        | ExtraBold | UltraBold    -> "800"
        | Black | Heavy            -> "900"
        | ExtraBlack | UltraBlack  -> "950"

    let private fwNode (fw: FontWeightValue) : JsonNode | null =
        match fw with
        | Numeric n -> jv n
        | Keyword k -> jv (fwKeywordStr k)

    let private dimStr (d: DimensionValue) : string =
        let u = match d.Unit with Px -> "px" | Rem -> "rem"
        sprintf "%g%s" d.Value u

    let private durStr (d: DurationValue) : string =
        let u = match d.Unit with Milliseconds -> "ms" | Seconds -> "s"
        sprintf "%g%s" d.Value u

    let private refStr (r: TokenRef) : string =
        match r with
        | CurlyBrace segs -> "{" + String.concat "." segs + "}"
        | JsonPointer p   -> p

    let private vorToNode (toLit: 'T -> JsonNode | null) (vor: ValueOrRef<'T>) : JsonNode | null =
        match vor with
        | Literal v   -> toLit v
        | Reference r -> jv (refStr r)

    let private strokeKwStr (k: StrokeStyleKeyword) : string =
        match k with
        | Solid   -> "solid"  | Dashed -> "dashed" | Dotted -> "dotted"
        | Double  -> "double" | Groove -> "groove" | Ridge  -> "ridge"
        | Outset  -> "outset" | Inset  -> "inset"

    let private lineCapStr (lc: LineCap) : string =
        match lc with Round -> "round" | Butt -> "butt" | Square -> "square"


    // ── Export: token value serializer ────────────────────────────────────────

    /// Human-readable annotation describing a lossy conversion. Appended to
    /// $description as a Penpot-survival fallback (ADR-023): Penpot strips
    /// $extensions, so the structured originalColor payload is lost there,
    /// but $description survives — the text gives a human (or a custom parser)
    /// a last-resort record of the source value.
    let private lossyAnnotation (c: ColorValue) : string =
        sprintf "source: %s — converted to sRGB hex for Tokens Studio compatibility"
            (colorDtcgStr c)

    /// Serialize a TokenValue to a JsonNode for Tokens Studio output.
    /// Returns Some (valueNode, optOriginalColorPayload, optDescriptionAnnotation)
    /// or None to skip this token. The originalColor payload + description
    /// annotation are emitted only for top-level Color tokens whose conversion
    /// is lossy (ADR-023). Composite token types that contain nested colors
    /// (Border, Shadow, Gradient) currently discard per-component payloads —
    /// see ADR-023 §Future work.
    let private exportValue
        (path    : string)
        (v       : TokenValue)
        (warnings: ResizeArray<ExportWarning>)
        : ((JsonNode | null) * JsonObject option * string option) option =
        let dimN (d: DimensionValue) : JsonNode | null = jv (dimStr d)
        let durN (d: DurationValue)  : JsonNode | null = jv (durStr d)
        let colorHexN (p: string) (c: ColorValue) : (JsonNode | null) * JsonObject option * string option =
            let hex, oc = exportColorHex p c warnings
            let ann = oc |> Option.map (fun _ -> lossyAnnotation c)
            jv hex, oc, ann
        let shadowObjN (p: string) (so: ShadowObject) : JsonNode | null =
            let o = JsonObject()
            let cHex =
                match so.Color with
                | Literal c   -> fst (exportColorHex p c warnings)
                | Reference r -> refStr r
            o.Add("color",   jv cHex)
            o.Add("offsetX", vorToNode dimN so.OffsetX)
            o.Add("offsetY", vorToNode dimN so.OffsetY)
            o.Add("blur",    vorToNode dimN so.Blur)
            o.Add("spread",  vorToNode dimN so.Spread)
            match so.Inset with
            | Some b -> o.Add("inset", jv b)
            | None   -> ()
            o :> JsonNode | null
        match v with
        | TokenValue.Alias r ->
            Some (jv (refStr r), None, None)
        | TokenValue.Color c ->
            let n, oc, ann = colorHexN path c
            Some (n, oc, ann)
        | TokenValue.Dimension d ->
            Some (dimN d, None, None)
        | TokenValue.FontFamily f ->
            match f with
            | Single s ->
                Some (jv s, None, None)
            | Stack ss ->
                let arr = JsonArray()
                for s in ss do arr.Add(jv s)
                Some (arr :> JsonNode | null, None, None)
        | TokenValue.FontWeight fw ->
            Some (fwNode fw, None, None)
        | TokenValue.Duration d ->
            Some (durN d, None, None)
        | TokenValue.CubicBezier cb ->
            let arr = JsonArray()
            arr.Add(jv cb.P1x)
            arr.Add(jv cb.P1y)
            arr.Add(jv cb.P2x)
            arr.Add(jv cb.P2y)
            Some (arr :> JsonNode | null, None, None)
        | TokenValue.Number n ->
            Some (jv n, None, None)
        | TokenValue.StrokeStyle s ->
            match s with
            | StrokeKeyword k ->
                Some (jv (strokeKwStr k), None, None)
            | StrokeCustom obj ->
                let o = JsonObject()
                let dashArr = JsonArray()
                for d in obj.DashArray do dashArr.Add(vorToNode dimN d)
                o.Add("dashArray", dashArr)
                o.Add("lineCap",   jv (lineCapStr obj.LineCap))
                Some (o :> JsonNode | null, None, None)
        | TokenValue.Shadow sv ->
            match sv with
            | ShadowSingle obj ->
                Some (shadowObjN path obj, None, None)
            | ShadowMultiple xs ->
                let arr = JsonArray()
                for x in xs do
                    match x with
                    | ShadowLiteral obj -> arr.Add(shadowObjN path obj)
                    | ShadowReference r -> arr.Add(jv (refStr r))
                Some (arr :> JsonNode | null, None, None)
        | TokenValue.Border b ->
            let o = JsonObject()
            let cHex =
                match b.Color with
                | Literal c   -> fst (exportColorHex (path + ".color") c warnings)
                | Reference r -> refStr r
            o.Add("color", jv cHex)
            o.Add("width", vorToNode dimN b.Width)
            let styleN : JsonNode | null =
                match b.Style with
                | Literal (StrokeKeyword k) -> jv (strokeKwStr k)
                | Literal (StrokeCustom _)  -> jv "solid"
                | Reference r               -> jv (refStr r)
            o.Add("style", styleN)
            Some (o :> JsonNode | null, None, None)
        | TokenValue.Transition t ->
            let o = JsonObject()
            o.Add("duration",       vorToNode durN t.Duration)
            o.Add("delay",          vorToNode durN t.Delay)
            let tfN : JsonNode | null =
                match t.TimingFunction with
                | Literal cb ->
                    let arr = JsonArray()
                    arr.Add(jv cb.P1x)
                    arr.Add(jv cb.P1y)
                    arr.Add(jv cb.P2x)
                    arr.Add(jv cb.P2y)
                    arr :> JsonNode | null
                | Reference r -> jv (refStr r)
            o.Add("timingFunction", tfN)
            Some (o :> JsonNode | null, None, None)
        | TokenValue.Gradient g ->
            let arr = JsonArray()
            for stop in g do
                let o = JsonObject()
                let cHex =
                    match stop.Color with
                    | Literal c   -> fst (exportColorHex path c warnings)
                    | Reference r -> refStr r
                o.Add("color", jv cHex)
                let posN : JsonNode | null =
                    match stop.Position with
                    | Literal f   -> jv f
                    | Reference r -> jv (refStr r)
                o.Add("position", posN)
                arr.Add(o :> JsonNode | null)
            Some (arr :> JsonNode | null, None, None)
        | TokenValue.Typography t ->
            let o = JsonObject()
            let ffN (f: FontFamilyValue) : JsonNode | null =
                match f with
                | Single s ->
                    jv s
                | Stack ss ->
                    let arr = JsonArray()
                    for s in ss do arr.Add(jv s)
                    arr :> JsonNode | null
            o.Add("fontFamily",    vorToNode ffN t.FontFamily)
            o.Add("fontSize",      vorToNode dimN t.FontSize)
            o.Add("fontWeight",    vorToNode fwNode t.FontWeight)
            o.Add("letterSpacing", vorToNode dimN t.LetterSpacing)
            let lhN : JsonNode | null =
                match t.LineHeight with
                | Literal f   -> jv f
                | Reference r -> jv (refStr r)
            o.Add("lineHeight", lhN)
            Some (o :> JsonNode | null, None, None)


    // ── Export: tree walker ───────────────────────────────────────────────────

    /// Build a token's $extensions object combining user-authored extensions
    /// (Token.Metadata.Extensions) with our optional originalColor payload (ADR-023).
    /// Returns None if neither is present.
    let private buildExtensionsObject
        (userExt   : Extensions)
        (origColor : JsonObject option)
        : JsonObject option =
        let outer = JsonObject()
        // 1. Pass through any user-authored extensions first (preserve insertion order).
        for (k, v) in userExt do
            outer.Add(k, v.DeepClone())
        // 2. Attach our originalColor payload under the vendor namespace.
        match origColor with
        | None -> ()
        | Some oc ->
            let vendor =
                match tryGetNode extVendorKey outer with
                | Some (:? JsonObject as existing) ->
                    // User had something under our namespace already — extend it.
                    existing
                | _ ->
                    let v = JsonObject()
                    // If user had a non-object value under our key, replace it.
                    if outer.ContainsKey extVendorKey then outer.Remove extVendorKey |> ignore
                    outer.Add(extVendorKey, v)
                    v
            // Only set originalColor if not already present (don't clobber user data).
            if not (vendor.ContainsKey extOriginalColorKey) then
                vendor.Add(extOriginalColorKey, oc)
        if outer.Count = 0 then None else Some outer

    let rec private addTokensToObj
        (warnings : ResizeArray<ExportWarning>)
        (path     : string)
        (children : (TokenName * TokenNode) list)
        (target   : JsonObject)
        : unit =
        for (name, node) in children do
            let key   = TokenName.value name
            let cPath = if path = "" then key else path + "." + key
            match node with
            | TokenLeaf t ->
                let leaf = JsonObject()
                match t.Type with
                | Some tt -> leaf.Add("$type", JsonValue.Create(typeNameStr tt))
                | None    -> ()
                match exportValue cPath t.Value warnings with
                | None -> ()
                | Some (vn, origColor, descAnn) ->
                    leaf.Add("$value", vn)
                    let desc =
                        match t.Metadata.Description, descAnn with
                        | Some d, Some a -> Some (d + "\n" + a)
                        | Some d, None   -> Some d
                        | None,   Some a -> Some a
                        | None,   None   -> None
                    match desc with
                    | Some d -> leaf.Add("$description", JsonValue.Create(d))
                    | None   -> ()
                    match buildExtensionsObject t.Metadata.Extensions origColor with
                    | Some ext -> leaf.Add("$extensions", ext)
                    | None     -> ()
                    target.Add(key, leaf)
            | Group g ->
                let grp = JsonObject()
                match g.Type with
                | Some tt -> grp.Add("$type", JsonValue.Create(typeNameStr tt))
                | None    -> ()
                addTokensToObj warnings cPath g.Children grp
                if grp.Count > 0 then target.Add(key, grp)


    // ── $themes / $metadata builders ─────────────────────────────────────────

    let private buildThemesArray (themes: TokensStudioTheme list) : JsonArray =
        let arr = JsonArray()
        for th in themes do
            let o = JsonObject()
            if th.Id    <> "" then o.Add("id",    JsonValue.Create(th.Id))
            if th.Name  <> "" then o.Add("name",  JsonValue.Create(th.Name))
            if th.Group <> "" then o.Add("group", JsonValue.Create(th.Group))
            let sets = JsonObject()
            for (k, v) in th.SelectedTokenSets |> Map.toList do sets.Add(k, JsonValue.Create(v))
            o.Add("selectedTokenSets", sets)
            arr.Add(o :> JsonNode)
        arr

    let private buildMetadataObj (metadata: TokensStudioMetadata) : JsonObject =
        let o = JsonObject()
        let orderArr = JsonArray()
        for s in metadata.TokenSetOrder do orderArr.Add(JsonValue.Create(s))
        o.Add("tokenSetOrder", orderArr)
        if not metadata.ActiveThemes.IsEmpty then
            let arr = JsonArray()
            for t in metadata.ActiveThemes do arr.Add(JsonValue.Create(t))
            o.Add("activeThemes", arr)
        if not metadata.ActiveSets.IsEmpty then
            let arr = JsonArray()
            for s in metadata.ActiveSets do arr.Add(JsonValue.Create(s))
            o.Add("activeSets", arr)
        o


    // ── Public: toResolverDocument ────────────────────────────────────────────

    /// Map Tokens Studio $themes/$metadata to a DTCG ResolverDocument (ADR-021).
    ///
    /// Themes with a non-empty <c>group</c> field become modifiers; sets whose status
    /// varies across a modifier group become per-context sources; the remainder become
    /// base <c>SetRef</c> entries in the resolution order.
    let toResolverDocument
        (shimResult : ShimResult)
        (parsedSets : Map<string, TokenFile>)
        : ResolverDocument =
        let allSets   = shimResult.Metadata.TokenSetOrder
        let allThemes = shimResult.Themes
        // Only themes with a non-empty group become modifiers
        let modGroups =
            allThemes
            |> List.filter (fun t -> t.Group <> "")
            |> List.groupBy (fun t -> t.Group)
        // Varying sets per modifier group: enabled in some themes but not all
        let varyingPerGroup =
            modGroups
            |> List.map (fun (gName, themes) ->
                let varying =
                    allSets
                    |> List.filter (fun setName ->
                        let statuses =
                            themes |> List.map (fun t ->
                                t.SelectedTokenSets
                                |> Map.tryFind setName
                                |> Option.defaultValue "disabled")
                        statuses |> List.exists (fun s -> s = "enabled") &&
                        statuses |> List.exists (fun s -> s <> "enabled"))
                    |> Set.ofList
                gName, varying)
            |> Map.ofList
        let allVarying =
            varyingPerGroup |> Map.toList |> List.map snd |> List.fold Set.union Set.empty
        // Base sets: not varying, and either:
        // - not mentioned in any theme's selectedTokenSets (global set), or
        // - present as "source" in any theme, or
        // - "enabled" in all themes
        let baseSets =
            allSets
            |> List.filter (fun setName ->
                not (Set.contains setName allVarying) &&
                let notMentioned =
                    allThemes |> List.forall (fun t -> not (t.SelectedTokenSets.ContainsKey setName))
                let isSource =
                    allThemes |> List.exists (fun t ->
                        t.SelectedTokenSets |> Map.tryFind setName = Some "source")
                let isEnabledAll =
                    not allThemes.IsEmpty &&
                    allThemes |> List.forall (fun t ->
                        t.SelectedTokenSets |> Map.tryFind setName = Some "enabled")
                notMentioned || isSource || isEnabledAll)
        // Build modifier definitions
        let modifiers =
            modGroups
            |> List.map (fun (gName, themes) ->
                let varying =
                    varyingPerGroup |> Map.tryFind gName |> Option.defaultValue Set.empty
                let contexts =
                    themes
                    |> List.map (fun theme ->
                        let ctxName =
                            let i = theme.Name.LastIndexOf('/')
                            if i >= 0 then theme.Name.[i+1..] else theme.Name
                        let sources =
                            allSets
                            |> List.choose (fun setName ->
                                if not (Set.contains setName varying) then None
                                else
                                    match theme.SelectedTokenSets
                                          |> Map.tryFind setName
                                          |> Option.defaultValue "disabled" with
                                    | "enabled" ->
                                        parsedSets |> Map.tryFind setName |> Option.map Inline
                                    | _ -> None)
                        ctxName, { Sources = sources })
                let defCtx = contexts |> List.tryHead |> Option.map fst
                gName, {
                    Contexts    = contexts
                    Default     = defCtx
                    Description = None
                    Extensions  = []
                })
        // Set definitions: every set in tokenSetOrder that has a parsed TokenFile
        let setDefs =
            allSets
            |> List.choose (fun n ->
                parsedSets |> Map.tryFind n |> Option.map (fun f ->
                    n, { Sources = [Inline f]; Description = None; Extensions = [] }))
        // Resolution order: base sets then one ModifierRef per modifier (empty = use default)
        let resOrder =
            (baseSets |> List.map SetRef) @
            (modifiers |> List.map (fun (n, _) -> ModifierRef (n, "")))
        {
            Name            = None
            Version         = V2025_10
            Description     = None
            Sets            = setDefs
            Modifiers       = modifiers
            ResolutionOrder = resOrder
        }


    // ── Public: exportToTokensStudio ─────────────────────────────────────────

    /// Export DTCG token sets back to Tokens Studio JSON format (ADR-022).
    ///
    /// Uses the preserve-aliases path: alias references remain as <c>{path.to.token}</c>
    /// strings. sRGB colors are serialized to hex without loss. Wide-gamut colors are
    /// approximated to sRGB hex and the original color value is appended to the token's
    /// <c>$description</c> field; each such conversion produces a
    /// <see cref="ExportWarning.LossyColorConversion"/> warning.
    ///
    /// The <c>$themes</c> and <c>$metadata</c> blocks are reconstructed from
    /// <c>shimResult</c>. Output is a Tokens Studio JSON file ready for Penpot import.
    let exportToTokensStudio
        (shimResult : ShimResult)
        (parsedSets : Map<string, TokenFile>)
        : string * ExportWarning list =
        let warnings = ResizeArray<ExportWarning>()
        let root     = JsonObject()
        let opts     = JsonSerializerOptions(WriteIndented = true)
        // Emit sets in tokenSetOrder
        for setName in shimResult.Metadata.TokenSetOrder do
            match parsedSets |> Map.tryFind setName with
            | None -> ()
            | Some file ->
                let setObj = JsonObject()
                addTokensToObj warnings "" file.Children setObj
                root.Add(setName, setObj)
        // Emit $themes and $metadata
        root.Add("$themes",   buildThemesArray shimResult.Themes   :> JsonNode)
        root.Add("$metadata", buildMetadataObj shimResult.Metadata :> JsonNode)
        root.ToJsonString(opts), List.ofSeq warnings
