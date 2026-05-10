module FnTools.DesignTokens.Css

open System
open System.Text
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open FnTools.DesignTokens


// ─── Path → CSS variable name ─────────────────────────────────────────────────

/// <summary>Converts a token path to a CSS custom property name.</summary>
/// <example><c>["color"; "action"; "default"]</c> → <c>"--color-action-default"</c></example>
let cssVarName (path: string list) : string =
    "--" + String.concat "-" path


// ─── Color → CSS ──────────────────────────────────────────────────────────────

let private componentStr (c: ColorComponent) : string =
    match c with
    | Channel f -> sprintf "%g" f
    | Missing   -> "none"

let private alphaClause (a: float option) : string =
    match a with
    | Some v when v < 1.0 -> sprintf " / %g" v
    | _                   -> ""

/// <summary>Converts a <see cref="ColorValue"/> to its CSS Color Level 4 representation.</summary>
/// <remarks>
/// OKLCH, OKLab, LCH, Lab, HSL, HWB use their dedicated CSS functions.
/// All other colour spaces use the generic <c>color(space c1 c2 c3)</c> syntax.
/// L channels are emitted as-is (0–1 range); both forms are valid in CSS Color Level 4.
/// </remarks>
let colorToCss (c: ColorValue) : string =
    let (c1, c2, c3) = c.Components
    let a = alphaClause c.Alpha
    match c.ColorSpace with
    | OKLCH -> sprintf "oklch(%s %s %s%s)"  (componentStr c1) (componentStr c2) (componentStr c3) a
    | OKLab -> sprintf "oklab(%s %s %s%s)"  (componentStr c1) (componentStr c2) (componentStr c3) a
    | LCH   -> sprintf "lch(%s %s %s%s)"    (componentStr c1) (componentStr c2) (componentStr c3) a
    | Lab   -> sprintf "lab(%s %s %s%s)"    (componentStr c1) (componentStr c2) (componentStr c3) a
    | HSL   -> sprintf "hsl(%s %s %s%s)"    (componentStr c1) (componentStr c2) (componentStr c3) a
    | HWB   -> sprintf "hwb(%s %s %s%s)"    (componentStr c1) (componentStr c2) (componentStr c3) a
    | cs    ->
        let name =
            match cs with
            | SRGB        -> "srgb"
            | SRGBLinear  -> "srgb-linear"
            | DisplayP3   -> "display-p3"
            | A98RGB      -> "a98-rgb"
            | ProPhotoRGB -> "prophoto-rgb"
            | Rec2020     -> "rec2020"
            | XYZD65      -> "xyz-d65"
            | XYZD50      -> "xyz-d50"
            | _           -> "srgb"
        sprintf "color(%s %s %s %s%s)" name (componentStr c1) (componentStr c2) (componentStr c3) a


// ─── Scalar types → CSS ───────────────────────────────────────────────────────

/// Converts a <see cref="DimensionValue"/> to a CSS length string (e.g. <c>"4px"</c>, <c>"0.6875rem"</c>).
let dimensionToCss (d: DimensionValue) : string =
    let u = match d.Unit with Px -> "px" | Rem -> "rem" | Em -> "em"
    sprintf "%g%s" d.Value u

/// Converts a <see cref="DurationValue"/> to a CSS time string (e.g. <c>"200ms"</c>).
let durationToCss (d: DurationValue) : string =
    let u = match d.Unit with Milliseconds -> "ms" | Seconds -> "s"
    sprintf "%g%s" d.Value u

let private genericFamilies =
    Set.ofList
        [ "serif"; "sans-serif"; "monospace"; "cursive"; "fantasy"
          "system-ui"; "ui-serif"; "ui-sans-serif"; "ui-monospace" ]

let private quoteFamilyName (n: string) : string =
    if Set.contains (n.ToLowerInvariant()) genericFamilies then n
    else sprintf "\"%s\"" n

/// Converts a <see cref="FontFamilyValue"/> to a CSS font-family string.
/// Generic keywords are left unquoted; all other names are double-quoted.
let fontFamilyToCss (f: FontFamilyValue) : string =
    match f with
    | Single s  -> quoteFamilyName s
    | Stack  xs -> xs |> List.map quoteFamilyName |> String.concat ", "

/// Converts a <see cref="FontWeightValue"/> to a CSS font-weight string.
let fontWeightToCss (fw: FontWeightValue) : string =
    match fw with
    | Numeric n -> string n
    | Keyword k ->
        match k with
        | Thin | Hairline                 -> "100"
        | ExtraLight | UltraLight         -> "200"
        | Light                           -> "300"
        | Normal | Regular | Book         -> "400"
        | Medium                          -> "500"
        | SemiBold | DemiBold             -> "600"
        | Bold                            -> "700"
        | ExtraBold | UltraBold           -> "800"
        | Black | Heavy                   -> "900"
        | ExtraBlack | UltraBlack         -> "900"  // no CSS 950 equivalent

/// Converts a <see cref="CubicBezierValue"/> to a CSS <c>cubic-bezier()</c> string.
let cubicBezierToCss (cb: CubicBezierValue) : string =
    sprintf "cubic-bezier(%g, %g, %g, %g)" cb.P1x cb.P1y cb.P2x cb.P2y

/// Converts a <see cref="ResolvedStrokeStyleValue"/> to a CSS border-style keyword.
/// Custom stroke styles (dash array + line cap) are approximated as <c>"dashed"</c>.
let strokeStyleToCss (s: ResolvedStrokeStyleValue) : string =
    match s with
    | ResolvedStrokeKeyword k ->
        match k with
        | Solid  -> "solid"
        | Dashed -> "dashed"
        | Dotted -> "dotted"
        | Double -> "double"
        | Groove -> "groove"
        | Ridge  -> "ridge"
        | Outset -> "outset"
        | Inset  -> "inset"
    | ResolvedStrokeCustom _ -> "dashed"


// ─── Composite types → CSS ────────────────────────────────────────────────────

let private shadowObjectToCss (s: ResolvedShadowObject) : string =
    let inset = if s.Inset = Some true then "inset " else ""
    sprintf "%s%s %s %s %s %s"
        inset
        (dimensionToCss s.OffsetX)
        (dimensionToCss s.OffsetY)
        (dimensionToCss s.Blur)
        (dimensionToCss s.Spread)
        (colorToCss s.Color)

/// Converts a <see cref="ResolvedShadowValue"/> to a CSS <c>box-shadow</c> string.
let shadowToCss (s: ResolvedShadowValue) : string =
    match s with
    | ResolvedShadowSingle   obj -> shadowObjectToCss obj
    | ResolvedShadowMultiple xs  -> xs |> List.map shadowObjectToCss |> String.concat ", "

/// Converts a <see cref="ResolvedBorderValue"/> to a CSS border shorthand string.
let borderToCss (b: ResolvedBorderValue) : string =
    sprintf "%s %s %s"
        (dimensionToCss b.Width)
        (strokeStyleToCss b.Style)
        (colorToCss b.Color)

/// Converts a <see cref="ResolvedTransitionValue"/> to a CSS transition shorthand string.
let transitionToCss (t: ResolvedTransitionValue) : string =
    sprintf "%s %s %s"
        (durationToCss t.Duration)
        (cubicBezierToCss t.TimingFunction)
        (durationToCss t.Delay)

/// <summary>Converts a <see cref="ResolvedGradientValue"/> to a CSS <c>linear-gradient()</c> string.</summary>
/// <remarks>
/// DTCG gradient tokens contain colour stops only — no direction. This emitter
/// uses <c>to right</c> as the default direction. Override by wrapping the var in your own
/// <c>linear-gradient(angle, var(--your-token))</c> expression.
/// </remarks>
let gradientToCss (g: ResolvedGradientValue) : string =
    let stops =
        g
        |> List.map (fun s -> sprintf "%s %g%%" (colorToCss s.Color) (s.Position * 100.0))
        |> String.concat ", "
    sprintf "linear-gradient(to right, %s)" stops


// ─── Dimension unit policy ────────────────────────────────────────────────────

/// <summary>
/// Determines the CSS unit to emit for a dimension token.
/// Receives the token's path and its declared unit; returns the unit to emit.
/// </summary>
/// <remarks>
/// The default policy (<see cref="DimensionUnitPolicy.identity"/>) passes through the
/// token's declared unit unchanged. Override for accessibility — for example, emitting
/// <c>rem</c> for font-size tokens while keeping layout tokens as <c>px</c>.
/// For composite tokens (shadow, border, transition), the policy applies to each
/// dimension sub-property using the composite token's own path.
/// </remarks>
type DimensionUnitPolicy = string list -> DimensionUnit -> DimensionUnit

/// Predefined <see cref="DimensionUnitPolicy"/> values.
module DimensionUnitPolicy =
    /// Passes through the token's declared unit unchanged. This is the default
    /// for all existing emitter functions.
    let identity : DimensionUnitPolicy = fun _ u -> u

let private dimensionWithPolicy (policy: DimensionUnitPolicy) (path: string list) (d: DimensionValue) : string =
    let targetUnit = policy path d.Unit
    let value =
        match d.Unit, targetUnit with
        | Px,  Rem -> d.Value / 16.0
        | Rem, Px  -> d.Value * 16.0
        | _,   _   -> d.Value
    let u = match targetUnit with Px -> "px" | Rem -> "rem" | Em -> "em"
    sprintf "%g%s" value u

let private tokenToCssDeclsWith (policy: DimensionUnitPolicy) (path: string list) (token: ResolvedToken) : (string * string) list =
    let v   = cssVarName path
    let dim = dimensionWithPolicy policy path
    match token.Value with
    | ResolvedColor       c  -> [ v, colorToCss c ]
    | ResolvedDimension   d  -> [ v, dim d ]
    | ResolvedFontFamily  f  -> [ v, fontFamilyToCss f ]
    | ResolvedFontWeight  fw -> [ v, fontWeightToCss fw ]
    | ResolvedDuration    d  -> [ v, durationToCss d ]
    | ResolvedCubicBezier b  -> [ v, cubicBezierToCss b ]
    | ResolvedNumber      n  -> [ v, sprintf "%g" n ]
    | ResolvedStrokeStyle s  -> [ v, strokeStyleToCss s ]
    | ResolvedBorder      b  -> [ v, borderToCss b ]
    | ResolvedShadow      s  -> [ v, shadowToCss s ]
    | ResolvedTransition  t  -> [ v, transitionToCss t ]
    | ResolvedGradient    g  -> [ v, gradientToCss g ]
    | ResolvedTypography  ty ->
        [ v + "-font-family",    fontFamilyToCss ty.FontFamily
          v + "-font-size",      dim ty.FontSize
          v + "-font-weight",    fontWeightToCss ty.FontWeight
          v + "-letter-spacing", dim ty.LetterSpacing
          v + "-line-height",    sprintf "%g"    ty.LineHeight ]


// ─── Token → CSS declaration(s) ───────────────────────────────────────────────

/// <summary>
/// Converts a single resolved token to one or more CSS custom property declarations.
/// </summary>
/// <remarks>
/// Most token types produce exactly one declaration. <see cref="ResolvedTokenValue.ResolvedTypography"/>
/// expands to five sub-property declarations (<c>-font-family</c>, <c>-font-size</c>,
/// <c>-font-weight</c>, <c>-letter-spacing</c>, <c>-line-height</c>).
/// Dimension units are emitted as declared. To override units per path, use
/// <see cref="emitWith"/> or <see cref="emitThemedWith"/> with a
/// <see cref="DimensionUnitPolicy"/>.
/// </remarks>
let tokenToCssDecls (path: string list) (token: ResolvedToken) : (string * string) list =
    tokenToCssDeclsWith DimensionUnitPolicy.identity path token


// ─── Top-level emitter ────────────────────────────────────────────────────────

/// Top-level CSS emitter functions.
/// The module is auto-opened so <c>emit</c> is usable unqualified;
/// <c>emitBlock</c> and <c>emitMultiMode</c> can also be called as <c>CssEmitter.*</c>.
[<AutoOpen>]
module CssEmitter =

    /// <summary>
    /// Emits all resolved tokens as a CSS block under the given selector.
    /// </summary>
    /// <remarks>
    /// When <paramref name="selector"/> starts with <c>@</c> (an at-rule such as
    /// <c>@media (max-width: 600px)</c>), the custom property declarations are wrapped
    /// in an inner <c>:root { }</c> rule, producing valid CSS:
    /// <code>
    /// @media (max-width: 600px) {
    ///   :root {
    ///     --breakpoint: 600px;
    ///   }
    /// }
    /// </code>
    /// For ordinary selectors (<c>":root"</c>, <c>"[data-theme=\"dark\"]"</c>, etc.)
    /// declarations are emitted directly inside the selector block.
    /// </remarks>
    /// <param name="selector">CSS selector or at-rule for the block.</param>
    /// <param name="tokens">Flat sequence of <c>(path, token)</c> pairs.</param>
    let emitBlock (selector: string) (tokens: (string list * ResolvedToken) seq) : string =
        let sb = StringBuilder()
        let isAtRule = selector.TrimStart().StartsWith "@"
        if isAtRule then
            sb.AppendLine (selector + " {") |> ignore
            sb.AppendLine "  :root {" |> ignore
            for (path, token) in tokens do
                for (name, value) in tokenToCssDecls path token do
                    sb.AppendLine (sprintf "    %s: %s;" name value) |> ignore
            sb.AppendLine "  }" |> ignore
            sb.AppendLine "}" |> ignore
        else
            sb.AppendLine (selector + " {") |> ignore
            for (path, token) in tokens do
                for (name, value) in tokenToCssDecls path token do
                    sb.AppendLine (sprintf "  %s: %s;" name value) |> ignore
            sb.AppendLine "}" |> ignore
        sb.ToString()

    /// <summary>
    /// Emits all resolved tokens as a CSS <c>:root { }</c> block of custom properties.
    /// </summary>
    /// <param name="tokens">
    /// Flat sequence of <c>(path, token)</c> pairs — the output of
    /// <see cref="FnTools.DesignTokens.Api.importWithResolver"/> or equivalent.
    /// </param>
    /// <returns>CSS text ready to write to a <c>.css</c> file.</returns>
    let emit (tokens: (string list * ResolvedToken) seq) : string =
        emitBlock ":root" tokens

    /// <summary>
    /// Emits a multi-theme CSS file: a <c>:root</c> block with base tokens, followed by one
    /// override block per theme containing only the tokens that differ from the base.
    /// </summary>
    /// <param name="selectorForTheme">Maps a theme name to a CSS selector,
    ///   e.g. <c>fun n -&gt; sprintf "[data-theme=\"%s\"]" n</c>.</param>
    /// <param name="baseTokens">Tokens from globally-active sets (not specific to any theme).
    ///   These go in <c>:root</c>.</param>
    /// <param name="themes">Sequence of <c>(themeName, fullyResolvedTokens)</c> pairs.
    ///   Each pair produces one override selector block with only the tokens that differ
    ///   from <c>baseTokens</c>. Themes with no differences produce no output block.</param>
    let emitThemed
        (selectorForTheme : string -> string)
        (baseTokens       : (string list * ResolvedToken) seq)
        (themes           : (string * (string list * ResolvedToken) seq) seq)
        : string =
        let baseDecls =
            baseTokens
            |> Seq.map (fun (path, token) -> String.concat "." path, tokenToCssDecls path token)
            |> dict
        let sb = StringBuilder()
        sb.Append (emitBlock ":root" baseTokens) |> ignore
        for (themeName, themeTokens) in themes do
            let diffs =
                themeTokens
                |> Seq.filter (fun (path, token) ->
                    let key = String.concat "." path
                    match baseDecls.TryGetValue key with
                    | false, _        -> true
                    | true,  baseDecl -> tokenToCssDecls path token <> baseDecl)
                |> Array.ofSeq
            if diffs.Length > 0 then
                sb.AppendLine "" |> ignore
                sb.Append (emitBlock (selectorForTheme themeName) diffs) |> ignore
        sb.ToString()

    /// <summary>
    /// Emits a two-block CSS file: a <c>:root</c> block with all base tokens, followed by
    /// an override block containing only the tokens whose values differ between the two
    /// resolved sets.
    /// </summary>
    /// <param name="baseTokens">Tokens resolved for the default/light context.</param>
    /// <param name="overrideTokens">Tokens resolved for the override context (e.g. dark theme).</param>
    /// <param name="overrideSelector">CSS selector for the override block, e.g. <c>"[data-theme=\"dark\"]"</c>.</param>
    /// <returns>
    /// CSS text with <c>:root { }</c> followed by the override block.
    /// If no tokens differ, only the <c>:root</c> block is emitted.
    /// </returns>
    let emitMultiMode
        (baseTokens:       (string list * ResolvedToken) seq)
        (overrideTokens:   (string list * ResolvedToken) seq)
        (overrideSelector: string)
        : string =
        let baseDecls =
            baseTokens
            |> Seq.map (fun (path, token) -> String.concat "." path, tokenToCssDecls path token)
            |> dict
        let diffs =
            overrideTokens
            |> Seq.filter (fun (path, token) ->
                let key = String.concat "." path
                match baseDecls.TryGetValue key with
                | false, _          -> true   // new token not in base — include it
                | true,  baseDecl   -> tokenToCssDecls path token <> baseDecl)
            |> Array.ofSeq
        let sb = StringBuilder()
        sb.Append (emitBlock ":root" baseTokens) |> ignore
        if diffs.Length > 0 then
            sb.AppendLine "" |> ignore
            sb.Append (emitBlock overrideSelector diffs) |> ignore
        sb.ToString()


    // ─── Policy-aware emitters (Gap 2 — rem per token type) ──────────────────

    let private emitBlockWith (policy: DimensionUnitPolicy) (selector: string) (tokens: (string list * ResolvedToken) seq) : string =
        let sb = StringBuilder()
        let isAtRule = selector.TrimStart().StartsWith "@"
        if isAtRule then
            sb.AppendLine (selector + " {") |> ignore
            sb.AppendLine "  :root {" |> ignore
            for (path, token) in tokens do
                for (name, value) in tokenToCssDeclsWith policy path token do
                    sb.AppendLine (sprintf "    %s: %s;" name value) |> ignore
            sb.AppendLine "  }" |> ignore
            sb.AppendLine "}" |> ignore
        else
            sb.AppendLine (selector + " {") |> ignore
            for (path, token) in tokens do
                for (name, value) in tokenToCssDeclsWith policy path token do
                    sb.AppendLine (sprintf "  %s: %s;" name value) |> ignore
            sb.AppendLine "}" |> ignore
        sb.ToString()

    /// <summary>
    /// Emits all resolved tokens as a CSS <c>:root { }</c> block, applying the given
    /// <see cref="DimensionUnitPolicy"/> to choose the CSS unit for each dimension token.
    /// </summary>
    /// <param name="policy">Unit selection policy. Use <see cref="DimensionUnitPolicy.identity"/>
    /// for the same behaviour as <see cref="emit"/>.</param>
    /// <param name="tokens">Flat sequence of <c>(path, token)</c> pairs.</param>
    let emitWith (policy: DimensionUnitPolicy) (tokens: (string list * ResolvedToken) seq) : string =
        emitBlockWith policy ":root" tokens

    /// <summary>
    /// Emits a multi-theme CSS file applying the given <see cref="DimensionUnitPolicy"/>
    /// to all dimension tokens in every block.
    /// </summary>
    /// <remarks>
    /// Behaves identically to <see cref="emitThemed"/> when <paramref name="policy"/>
    /// is <see cref="DimensionUnitPolicy.identity"/>.
    /// </remarks>
    /// <param name="policy">Unit selection policy, e.g. emit <c>rem</c> for font-size paths.</param>
    /// <param name="selectorForTheme">Maps a theme name to a CSS selector or at-rule.</param>
    /// <param name="baseTokens">Tokens for the default context; goes in <c>:root</c>.</param>
    /// <param name="themes">Override themes; each emits a diff block.</param>
    let emitThemedWith
        (policy           : DimensionUnitPolicy)
        (selectorForTheme : string -> string)
        (baseTokens       : (string list * ResolvedToken) seq)
        (themes           : (string * (string list * ResolvedToken) seq) seq)
        : string =
        let baseDecls =
            baseTokens
            |> Seq.map (fun (path, token) -> String.concat "." path, tokenToCssDeclsWith policy path token)
            |> dict
        let sb = StringBuilder()
        sb.Append (emitBlockWith policy ":root" baseTokens) |> ignore
        for (themeName, themeTokens) in themes do
            let diffs =
                themeTokens
                |> Seq.filter (fun (path, token) ->
                    let key = String.concat "." path
                    match baseDecls.TryGetValue key with
                    | false, _        -> true
                    | true,  baseDecl -> tokenToCssDeclsWith policy path token <> baseDecl)
                |> Array.ofSeq
            if diffs.Length > 0 then
                sb.AppendLine "" |> ignore
                sb.Append (emitBlockWith policy (selectorForTheme themeName) diffs) |> ignore
        sb.ToString()


    /// <summary>
    /// Emits a two-block CSS file applying the given <see cref="DimensionUnitPolicy"/>
    /// to all dimension tokens in both blocks.
    /// </summary>
    /// <remarks>
    /// Behaves identically to <see cref="emitMultiMode"/> when <paramref name="policy"/>
    /// is <see cref="DimensionUnitPolicy.identity"/>.
    /// </remarks>
    /// <param name="policy">Unit selection policy, e.g. emit <c>rem</c> for font-size paths.</param>
    /// <param name="baseTokens">Tokens resolved for the default context; goes in <c>:root</c>.</param>
    /// <param name="overrideTokens">Tokens resolved for the override context.</param>
    /// <param name="overrideSelector">CSS selector for the override block.</param>
    let emitMultiModeWith
        (policy           : DimensionUnitPolicy)
        (baseTokens       : (string list * ResolvedToken) seq)
        (overrideTokens   : (string list * ResolvedToken) seq)
        (overrideSelector : string)
        : string =
        let baseDecls =
            baseTokens
            |> Seq.map (fun (path, token) -> String.concat "." path, tokenToCssDeclsWith policy path token)
            |> dict
        let diffs =
            overrideTokens
            |> Seq.filter (fun (path, token) ->
                let key = String.concat "." path
                match baseDecls.TryGetValue key with
                | false, _        -> true
                | true,  baseDecl -> tokenToCssDeclsWith policy path token <> baseDecl)
            |> Array.ofSeq
        let sb = StringBuilder()
        sb.Append (emitBlockWith policy ":root" baseTokens) |> ignore
        if diffs.Length > 0 then
            sb.AppendLine "" |> ignore
            sb.Append (emitBlockWith policy overrideSelector diffs) |> ignore
        sb.ToString()


    // ─── Calc-preserving emitter (Gap 1 — design-tool workbench) ─────────────

    // ─── Annotated-expression helpers (ADR-027 option a / ADR-031) ───────────────

    /// Vendor namespace written by the Tokens Studio shim (ADR-023).
    let private tsVendorKey   = "com.fntools.designtokens"
    let private tsMathExprKey = "tsMathExpression"

    /// Read the tsMathExpression string from a token's vendor extensions if present.
    let private tryReadMathExpr (ext: Extensions) : string option =
        ext |> List.tryPick (fun (k, node) ->
            if k = tsVendorKey then
                match node with
                | :? JsonObject as vendor when vendor.ContainsKey tsMathExprKey ->
                    vendor.[tsMathExprKey]
                    |> Option.ofObj
                    |> Option.bind (fun v -> try Some (v.GetValue<string>()) with _ -> None)
                | _ -> None
            else None)

    /// Strip an outer round(…) wrapper from a math expression string.
    /// "round({base} * pow({multiplier}, 3))" → "{base} * pow({multiplier}, 3)"
    let private stripRound (expr: string) : string =
        let m = Regex.Match(expr.Trim(), @"^round\((.+)\)$", RegexOptions.IgnoreCase)
        if m.Success then m.Groups.[1].Value.Trim() else expr.Trim()

    /// Try to parse the integer exponent N from a Tokens Studio power-of-multiplier
    /// expression. Handles both plain and round()-wrapped forms.
    ///
    /// Recognised patterns (after round() stripping):
    ///   {base}                           → n = 0
    ///   {base} * pow({multiplier}, N)    → n = N
    let private tryParsePowN (baseRef: string) (multRef: string) (expr: string) : int option =
        let inner = stripRound expr
        if inner = sprintf "{%s}" baseRef then Some 0
        else
            let pat =
                sprintf @"^\{%s\}\s*\*\s*pow\(\{%s\}\s*,\s*(-?\d+)\)$"
                    (Regex.Escape baseRef) (Regex.Escape multRef)
            let m = Regex.Match(inner, pat, RegexOptions.IgnoreCase)
            if m.Success then Some (int m.Groups.[1].Value) else None

    let private tryInferCalcN (baseVal: float) (multVal: float) (dimVal: float) : int option =
        if baseVal < 1e-9 || abs(multVal - 1.0) < 1e-9 || dimVal < 1e-9 then None
        else
            let n    = Math.Round(Math.Log(dimVal / baseVal) / Math.Log(multVal))
            let nInt = int n
            let reconstructed = baseVal * (pown multVal nInt)
            if abs(reconstructed - dimVal) / dimVal < 1e-6 then Some nInt
            else None

    /// Builds a CSS calc() expression for a dimension that equals base × mult^n.
    let private buildCalcExpr (baseVarName: string) (multVarName: string) (n: int) : string =
        let bv = sprintf "var(%s)" baseVarName
        let mv = sprintf "var(%s)" multVarName
        match n with
        | 0 ->
            sprintf "calc(%s * 1px)" bv
        | n when n > 0 ->
            let mults = List.replicate n mv |> String.concat " * "
            sprintf "calc(%s * %s * 1px)" bv mults
        | n ->
            let divs = List.replicate (abs n) mv |> String.concat " / "
            sprintf "calc(%s / %s * 1px)" bv divs

    /// <summary>
    /// Emits a CSS <c>:root</c> block for the design-tool workbench.
    /// </summary>
    /// <remarks>
    /// Base and multiplier tokens are emitted as plain numbers — live CSS variables that
    /// a slider can change at runtime. All <c>dimension</c> tokens whose resolved value
    /// satisfies <c>value = base × multiplier^n</c> for an integer n are emitted as
    /// <c>calc()</c> expressions referencing those variables, so the entire scale
    /// recomputes in the browser without a rebuild.
    /// Dimension tokens that do not fit the pattern (fixed radii, border widths,
    /// breakpoints) fall back to their concrete resolved values.
    /// </remarks>
    /// <param name="baseVarName">CSS variable name for the base size, e.g. <c>"--base"</c>.</param>
    /// <param name="multiplierVarName">CSS variable name for the scale step, e.g. <c>"--multiplier"</c>.</param>
    /// <param name="tokens">Flat sequence of <c>(path, token)</c> pairs.</param>
    let emitCalcPreserving
        (baseVarName       : string)
        (multiplierVarName : string)
        (tokens            : (string list * ResolvedToken) seq)
        : string =
        let tokenList = tokens |> List.ofSeq
        let varToPath (v: string) = v.TrimStart('-').Split('-') |> Array.toList
        let basePath  = varToPath baseVarName
        let multPath  = varToPath multiplierVarName
        let findNumber path =
            tokenList |> List.tryPick (fun (p, t) ->
                if p = path then match t.Value with ResolvedNumber n -> Some n | _ -> None
                else None)
        let baseOpt = findNumber basePath
        let multOpt = findNumber multPath
        let sb = StringBuilder()
        sb.AppendLine ":root {" |> ignore
        for (path, token) in tokenList do
            if path = basePath || path = multPath then
                match token.Value with
                | ResolvedNumber n ->
                    sb.AppendLine (sprintf "  %s: %g;" (cssVarName path) n) |> ignore
                | _ -> ()
            else
                let decls =
                    match token.Value, baseOpt, multOpt with
                    | ResolvedDimension d, Some bv, Some mv ->
                        let baseRef = String.concat "." basePath
                        let multRef = String.concat "." multPath
                        let nOpt =
                            (tryReadMathExpr token.Metadata.Extensions
                             |> Option.bind (tryParsePowN baseRef multRef))
                            |> Option.orElseWith (fun () -> tryInferCalcN bv mv d.Value)
                        match nOpt with
                        | Some n -> [ cssVarName path, buildCalcExpr baseVarName multiplierVarName n ]
                        | None   -> tokenToCssDecls path token
                    | _ -> tokenToCssDecls path token
                for (name, value) in decls do
                    sb.AppendLine (sprintf "  %s: %s;" name value) |> ignore
        sb.AppendLine "}" |> ignore
        sb.ToString()
