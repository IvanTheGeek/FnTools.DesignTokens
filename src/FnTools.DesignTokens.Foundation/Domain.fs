namespace FnTools.DesignTokens

open System.Text.Json.Nodes


// ─── Token references ────────────────────────────────────────────────────────

type TokenRef =
    | CurlyBrace  of path: string list
    | JsonPointer of pointer: string

type ValueOrRef<'T> =
    | Literal   of 'T
    | Reference of TokenRef


// ─── Color (Color module) ────────────────────────────────────────────────────

type ColorSpace =
    | SRGB | SRGBLinear | DisplayP3 | A98RGB | ProPhotoRGB | Rec2020
    | HSL | HWB
    | Lab | LCH | OKLab | OKLCH
    | XYZD65 | XYZD50

type ColorComponent =
    | Channel of float
    | Missing

type ColorValue = {
    ColorSpace : ColorSpace
    Components : ColorComponent * ColorComponent * ColorComponent
    Alpha      : float option
    Hex        : string option
}


// ─── Dimension, FontFamily, FontWeight, Duration, CubicBezier ───────────────

type DimensionUnit = Px | Rem | Em

type DimensionValue = {
    Value : float
    Unit  : DimensionUnit
}

type FontFamilyValue =
    | Single of string
    | Stack  of string list

type FontWeightKeyword =
    | Thin | Hairline
    | ExtraLight | UltraLight
    | Light
    | Normal | Regular | Book
    | Medium
    | SemiBold | DemiBold
    | Bold
    | ExtraBold | UltraBold
    | Black | Heavy
    | ExtraBlack | UltraBlack

type FontWeightValue =
    | Numeric of int
    | Keyword of FontWeightKeyword

type DurationUnit = Milliseconds | Seconds

type DurationValue = {
    Value : float
    Unit  : DurationUnit
}

type CubicBezierValue = {
    P1x : float
    P1y : float
    P2x : float
    P2y : float
}


// ─── StrokeStyle ─────────────────────────────────────────────────────────────

type LineCap = Round | Butt | Square

type StrokeStyleKeyword =
    | Solid | Dashed | Dotted | Double
    | Groove | Ridge | Outset | Inset

type StrokeStyleObject = {
    DashArray : ValueOrRef<DimensionValue> list
    LineCap   : LineCap
}

type StrokeStyleValue =
    | StrokeKeyword of StrokeStyleKeyword
    | StrokeCustom  of StrokeStyleObject


// ─── Composites ──────────────────────────────────────────────────────────────

type BorderValue = {
    Color : ValueOrRef<ColorValue>
    Width : ValueOrRef<DimensionValue>
    Style : ValueOrRef<StrokeStyleValue>
}

type ShadowObject = {
    Color   : ValueOrRef<ColorValue>
    OffsetX : ValueOrRef<DimensionValue>
    OffsetY : ValueOrRef<DimensionValue>
    Blur    : ValueOrRef<DimensionValue>
    Spread  : ValueOrRef<DimensionValue>
    Inset   : bool option
}

type ShadowOrRef =
    | ShadowLiteral   of ShadowObject
    | ShadowReference of TokenRef

type ShadowValue =
    | ShadowSingle   of ShadowObject
    | ShadowMultiple of ShadowOrRef list

type TransitionValue = {
    Duration       : ValueOrRef<DurationValue>
    Delay          : ValueOrRef<DurationValue>
    TimingFunction : ValueOrRef<CubicBezierValue>
}

type GradientStop = {
    Color    : ValueOrRef<ColorValue>
    Position : ValueOrRef<float>
}

type GradientValue = GradientStop list

type TypographyValue = {
    FontFamily    : ValueOrRef<FontFamilyValue>
    FontSize      : ValueOrRef<DimensionValue>
    FontWeight    : ValueOrRef<FontWeightValue>
    LetterSpacing : ValueOrRef<DimensionValue>
    LineHeight    : ValueOrRef<float>
}


// ─── TokenValue ──────────────────────────────────────────────────────────────

type TokenValue =
    | Color       of ColorValue
    | Dimension   of DimensionValue
    | FontFamily  of FontFamilyValue
    | FontWeight  of FontWeightValue
    | Duration    of DurationValue
    | CubicBezier of CubicBezierValue
    | Number      of float
    | StrokeStyle of StrokeStyleValue
    | Border      of BorderValue
    | Shadow      of ShadowValue
    | Transition  of TransitionValue
    | Gradient    of GradientValue
    | Typography  of TypographyValue
    | Alias       of TokenRef


// ─── TokenType ($type declarations) ──────────────────────────────────────────

type TokenType =
    | ColorType
    | DimensionType
    | FontFamilyType
    | FontWeightType
    | DurationType
    | CubicBezierType
    | NumberType
    | StrokeStyleType
    | BorderType
    | TransitionType
    | ShadowType
    | GradientType
    | TypographyType

module TokenType =
    /// DTCG spec type-string for this token type (e.g. <c>ColorType → "color"</c>).
    let displayName (t: TokenType) : string =
        match t with
        | ColorType        -> "color"
        | DimensionType    -> "dimension"
        | FontFamilyType   -> "fontFamily"
        | FontWeightType   -> "fontWeight"
        | DurationType     -> "duration"
        | CubicBezierType  -> "cubicBezier"
        | NumberType       -> "number"
        | StrokeStyleType  -> "strokeStyle"
        | BorderType       -> "border"
        | TransitionType   -> "transition"
        | ShadowType       -> "shadow"
        | GradientType     -> "gradient"
        | TypographyType   -> "typography"

module TokenValue =
    /// Map a literal <see cref="TokenValue"/> to its <see cref="TokenType"/>.
    /// Returns <c>None</c> for <see cref="TokenValue.Alias"/> — aliases have no
    /// intrinsic type until their target is followed.
    let inferType (v: TokenValue) : TokenType option =
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


// ─── Metadata ────────────────────────────────────────────────────────────────

type Deprecated =
    | Deprecated
    | DeprecatedWithMessage of string

type Extensions = (string * JsonNode) list

type Metadata = {
    Description : string option
    Deprecated  : Deprecated option
    Extensions  : Extensions
}


// ─── TokenName (private constructor) ─────────────────────────────────────────

type TokenName = private TokenName of string

module TokenName =

    /// Smart constructor — validates name pattern.
    /// Cannot start with $; cannot contain '.', '{', or '}'.
    let tryCreate (name: string) : Result<TokenName, string> =
        if System.String.IsNullOrEmpty name then
            Error "name cannot be empty"
        elif name.StartsWith "$" then
            Error (sprintf "name cannot start with '$' (got '%s')" name)
        elif name.Contains '.' || name.Contains '{' || name.Contains '}' then
            Error (sprintf "name cannot contain '.', '{', or '}' (got '%s')" name)
        else
            Ok (TokenName name)

    let value (TokenName name) : string = name


// ─── Token / TokenNode / Group (mutually recursive) ─────────────────────────

type Token = {
    Value    : TokenValue
    Type     : TokenType option
    Metadata : Metadata
}

type TokenNode =
    | TokenLeaf of Token
    | Group     of GroupData

and GroupData = {
    Type     : TokenType option
    Metadata : Metadata
    Root     : Token option
    Extends  : TokenRef option
    Children : (TokenName * TokenNode) list
}

type TokenFile = {
    Version  : SpecVersion
    Schema   : string option
    Children : (TokenName * TokenNode) list
}


// ─── Resolved tier (no Alias, Type non-optional) ─────────────────────────────

type ResolvedStrokeStyleObject = {
    DashArray : DimensionValue list
    LineCap   : LineCap
}

type ResolvedStrokeStyleValue =
    | ResolvedStrokeKeyword of StrokeStyleKeyword
    | ResolvedStrokeCustom  of ResolvedStrokeStyleObject

type ResolvedBorderValue = {
    Color : ColorValue
    Width : DimensionValue
    Style : ResolvedStrokeStyleValue
}

type ResolvedShadowObject = {
    Color   : ColorValue
    OffsetX : DimensionValue
    OffsetY : DimensionValue
    Blur    : DimensionValue
    Spread  : DimensionValue
    Inset   : bool option
}

type ResolvedShadowValue =
    | ResolvedShadowSingle   of ResolvedShadowObject
    | ResolvedShadowMultiple of ResolvedShadowObject list

type ResolvedTransitionValue = {
    Duration       : DurationValue
    Delay          : DurationValue
    TimingFunction : CubicBezierValue
}

type ResolvedGradientStop = {
    Color    : ColorValue
    Position : float
}

type ResolvedGradientValue = ResolvedGradientStop list

type ResolvedTypographyValue = {
    FontFamily    : FontFamilyValue
    FontSize      : DimensionValue
    FontWeight    : FontWeightValue
    LetterSpacing : DimensionValue
    LineHeight    : float
}

type ResolvedTokenValue =
    | ResolvedColor       of ColorValue
    | ResolvedDimension   of DimensionValue
    | ResolvedFontFamily  of FontFamilyValue
    | ResolvedFontWeight  of FontWeightValue
    | ResolvedDuration    of DurationValue
    | ResolvedCubicBezier of CubicBezierValue
    | ResolvedNumber      of float
    | ResolvedStrokeStyle of ResolvedStrokeStyleValue
    | ResolvedBorder      of ResolvedBorderValue
    | ResolvedShadow      of ResolvedShadowValue
    | ResolvedTransition  of ResolvedTransitionValue
    | ResolvedGradient    of ResolvedGradientValue
    | ResolvedTypography  of ResolvedTypographyValue

type ResolvedToken = {
    Value    : ResolvedTokenValue
    Type     : TokenType
    Metadata : Metadata
}


// ─── Resolver types ──────────────────────────────────────────────────────────

type TokenSource =
    | FileRef of path: string
    | Inline  of TokenFile

type SetDefinition = {
    Sources     : TokenSource list
    Description : string option
    Extensions  : Extensions
}

type ModifierContext = {
    Sources : TokenSource list
}

type ModifierDefinition = {
    Contexts    : (string * ModifierContext) list
    Default     : string option
    Description : string option
    Extensions  : Extensions
}

type ResolutionItem =
    | SetRef      of name: string
    | ModifierRef of name: string * context: string

type ResolverDocument = {
    Name            : string option
    Version         : SpecVersion
    Description     : string option
    Sets            : (string * SetDefinition) list
    Modifiers       : (string * ModifierDefinition) list
    ResolutionOrder : ResolutionItem list
}

type ResolverInput = Map<string, string>


// ─── Auto-parse result ───────────────────────────────────────────────────────

type AutoParseResult =
    | TokenFileResult of TokenFile
    | ResolverResult  of ResolverDocument
