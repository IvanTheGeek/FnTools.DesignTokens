module FnTools.DesignTokens.Format

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open FnTools.DesignTokens
open FnTools.DesignTokens.Json


// ─── TokenType ↔ string ──────────────────────────────────────────────────────

let private tokenTypeToString (t: TokenType) : string =
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

let private parseTokenType (path: string) (raw: string) : Result<TokenType, ParseError> =
    match raw with
    | "color"       -> Ok ColorType
    | "dimension"   -> Ok DimensionType
    | "fontFamily"  -> Ok FontFamilyType
    | "fontWeight"  -> Ok FontWeightType
    | "duration"    -> Ok DurationType
    | "cubicBezier" -> Ok CubicBezierType
    | "number"      -> Ok NumberType
    | "strokeStyle" -> Ok StrokeStyleType
    | "border"      -> Ok BorderType
    | "transition"  -> Ok TransitionType
    | "shadow"      -> Ok ShadowType
    | "gradient"    -> Ok GradientType
    | "typography"  -> Ok TypographyType
    | _             -> Error (UnknownTokenType (path, raw))


// ─── ColorSpace ↔ string ─────────────────────────────────────────────────────

let private colorSpaceToString (cs: ColorSpace) : string =
    match cs with
    | SRGB        -> "srgb"
    | SRGBLinear  -> "srgb-linear"
    | DisplayP3   -> "display-p3"
    | A98RGB      -> "a98-rgb"
    | ProPhotoRGB -> "prophoto-rgb"
    | Rec2020     -> "rec2020"
    | HSL         -> "hsl"
    | HWB         -> "hwb"
    | Lab         -> "lab"
    | LCH         -> "lch"
    | OKLab       -> "oklab"
    | OKLCH       -> "oklch"
    | XYZD65      -> "xyz-d65"
    | XYZD50      -> "xyz-d50"

let private parseColorSpace (path: string) (raw: string) : Result<ColorSpace, ParseError> =
    match raw with
    | "srgb"         -> Ok SRGB
    | "srgb-linear"  -> Ok SRGBLinear
    | "display-p3"   -> Ok DisplayP3
    | "a98-rgb"      -> Ok A98RGB
    | "prophoto-rgb" -> Ok ProPhotoRGB
    | "rec2020"      -> Ok Rec2020
    | "hsl"          -> Ok HSL
    | "hwb"          -> Ok HWB
    | "lab"          -> Ok Lab
    | "lch"          -> Ok LCH
    | "oklab"        -> Ok OKLab
    | "oklch"        -> Ok OKLCH
    | "xyz-d65"      -> Ok XYZD65
    | "xyz-d50"      -> Ok XYZD50
    | _              -> Error (InvalidValue (path, sprintf "unknown color space '%s'" raw))


// ─── Misc keyword tables ─────────────────────────────────────────────────────

let private parseDimensionUnit (path: string) (raw: string) : Result<DimensionUnit, ParseError> =
    match raw with
    | "px"  -> Ok Px
    | "rem" -> Ok Rem
    | _     -> Error (InvalidValue (path, sprintf "unknown dimension unit '%s'" raw))

let private dimensionUnitToString (u: DimensionUnit) : string =
    match u with Px -> "px" | Rem -> "rem"

let private parseDurationUnit (path: string) (raw: string) : Result<DurationUnit, ParseError> =
    match raw with
    | "ms" -> Ok Milliseconds
    | "s"  -> Ok Seconds
    | _    -> Error (InvalidValue (path, sprintf "unknown duration unit '%s'" raw))

let private durationUnitToString (u: DurationUnit) : string =
    match u with Milliseconds -> "ms" | Seconds -> "s"

let private parseLineCap (path: string) (raw: string) : Result<LineCap, ParseError> =
    match raw with
    | "round"  -> Ok Round
    | "butt"   -> Ok Butt
    | "square" -> Ok Square
    | _        -> Error (InvalidValue (path, sprintf "unknown lineCap '%s'" raw))

let private lineCapToString (lc: LineCap) : string =
    match lc with Round -> "round" | Butt -> "butt" | Square -> "square"

let private parseStrokeKeyword (path: string) (raw: string) : Result<StrokeStyleKeyword, ParseError> =
    match raw with
    | "solid"  -> Ok Solid
    | "dashed" -> Ok Dashed
    | "dotted" -> Ok Dotted
    | "double" -> Ok Double
    | "groove" -> Ok Groove
    | "ridge"  -> Ok Ridge
    | "outset" -> Ok Outset
    | "inset"  -> Ok Inset
    | _        -> Error (InvalidValue (path, sprintf "unknown strokeStyle keyword '%s'" raw))

let private strokeKeywordToString (k: StrokeStyleKeyword) : string =
    match k with
    | Solid -> "solid" | Dashed -> "dashed" | Dotted -> "dotted" | Double -> "double"
    | Groove -> "groove" | Ridge -> "ridge" | Outset -> "outset" | Inset -> "inset"

let private parseFontWeightKeyword (path: string) (raw: string) : Result<FontWeightKeyword, ParseError> =
    match raw with
    | "thin"        -> Ok Thin
    | "hairline"    -> Ok Hairline
    | "extra-light" -> Ok ExtraLight
    | "ultra-light" -> Ok UltraLight
    | "light"       -> Ok Light
    | "normal"      -> Ok Normal
    | "regular"     -> Ok Regular
    | "book"        -> Ok Book
    | "medium"      -> Ok Medium
    | "semi-bold"   -> Ok SemiBold
    | "demi-bold"   -> Ok DemiBold
    | "bold"        -> Ok Bold
    | "extra-bold"  -> Ok ExtraBold
    | "ultra-bold"  -> Ok UltraBold
    | "black"       -> Ok Black
    | "heavy"       -> Ok Heavy
    | "extra-black" -> Ok ExtraBlack
    | "ultra-black" -> Ok UltraBlack
    | _             -> Error (InvalidValue (path, sprintf "unknown fontWeight keyword '%s'" raw))

let private fontWeightKeywordToString (k: FontWeightKeyword) : string =
    match k with
    | Thin -> "thin" | Hairline -> "hairline"
    | ExtraLight -> "extra-light" | UltraLight -> "ultra-light"
    | Light -> "light"
    | Normal -> "normal" | Regular -> "regular" | Book -> "book"
    | Medium -> "medium"
    | SemiBold -> "semi-bold" | DemiBold -> "demi-bold"
    | Bold -> "bold"
    | ExtraBold -> "extra-bold" | UltraBold -> "ultra-bold"
    | Black -> "black" | Heavy -> "heavy"
    | ExtraBlack -> "extra-black" | UltraBlack -> "ultra-black"


// ─── Result list helpers ────────────────────────────────────────────────────

let private collectResults (xs: Result<'T, ParseError> list) : Result<'T list, ParseError list> =
    let oks = ResizeArray()
    let errs = ResizeArray()
    for x in xs do
        match x with
        | Ok v -> oks.Add v
        | Error e -> errs.Add e
    if errs.Count > 0 then Error (List.ofSeq errs)
    else Ok (List.ofSeq oks)

let private (>>=) (r: Result<'a, 'e list>) (f: 'a -> Result<'b, 'e list>) : Result<'b, 'e list> =
    match r with
    | Ok v -> f v
    | Error es -> Error es

let private liftSingle (r: Result<'a, ParseError>) : Result<'a, ParseError list> =
    match r with
    | Ok v -> Ok v
    | Error e -> Error [e]


// ─── Value parsers (V2025_10 form) ──────────────────────────────────────────

let private parseColorValue (path: string) (n: JsonNode) : Result<ColorValue, ParseError list> =
    requireObject path "color" n |> liftSingle >>= fun o ->
        let csR =
            match tryGetProperty "colorSpace" o with
            | Some node -> requireString path "colorSpace" node |> Result.bind (parseColorSpace path) |> liftSingle
            | None      -> Error [MissingRequiredField (path, "colorSpace")]

        let parseComponent (i: int) (item: JsonNode) : Result<ColorComponent, ParseError> =
            match tryGetString item with
            | Some "none" -> Ok Missing
            | Some s -> Error (InvalidValue (sprintf "%s.components[%d]" path i, sprintf "unexpected string '%s'" s))
            | None ->
                match tryGetFloat item with
                | Some f -> Ok (Channel f)
                | None -> Error (InvalidValue (sprintf "%s.components[%d]" path i, "expected number or 'none'"))

        let componentsR =
            match tryGetProperty "components" o with
            | Some node ->
                match asArray node with
                | Some arr when arr.Count = 3 ->
                    let parsed =
                        [ for i in 0 .. 2 -> parseComponent i arr.[i] ]
                    collectResults parsed
                    |> Result.map (fun xs -> (xs.[0], xs.[1], xs.[2]))
                | Some arr ->
                    Error [InvalidValue (path, sprintf "components must have exactly 3 elements (got %d)" arr.Count)]
                | None ->
                    Error [InvalidValue (path, "components must be an array")]
            | None -> Error [MissingRequiredField (path, "components")]

        let alphaR =
            match tryGetProperty "alpha" o with
            | Some node -> requireFloat path "alpha" node |> liftSingle |> Result.map Some
            | None      -> Ok None

        let hexR =
            match tryGetProperty "hex" o with
            | Some node -> requireString path "hex" node |> liftSingle |> Result.map Some
            | None      -> Ok None

        match csR, componentsR, alphaR, hexR with
        | Ok cs, Ok comps, Ok alpha, Ok hex ->
            Ok { ColorSpace = cs; Components = comps; Alpha = alpha; Hex = hex }
        | _ ->
            let errs =
                [ csR |> Result.map ignore; componentsR |> Result.map ignore
                  alphaR |> Result.map ignore; hexR |> Result.map ignore ]
                |> List.collect (function Error es -> es | Ok _ -> [])
            Error errs

let private parseDimensionValue (path: string) (n: JsonNode) : Result<DimensionValue, ParseError list> =
    requireObject path "dimension" n |> liftSingle >>= fun o ->
        let valueR =
            requireProperty path "value" o |> liftSingle
            >>= fun node -> requireFloat path "value" node |> liftSingle
        let unitR =
            requireProperty path "unit" o |> liftSingle
            >>= fun node -> requireString path "unit" node |> liftSingle
            >>= fun raw -> parseDimensionUnit path raw |> liftSingle
        match valueR, unitR with
        | Ok v, Ok u -> Ok { Value = v; Unit = u }
        | _ ->
            [ valueR |> Result.map ignore; unitR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error

let private parseDurationValue (path: string) (n: JsonNode) : Result<DurationValue, ParseError list> =
    requireObject path "duration" n |> liftSingle >>= fun o ->
        let valueR =
            requireProperty path "value" o |> liftSingle
            >>= fun node -> requireFloat path "value" node |> liftSingle
        let unitR =
            requireProperty path "unit" o |> liftSingle
            >>= fun node -> requireString path "unit" node |> liftSingle
            >>= fun raw -> parseDurationUnit path raw |> liftSingle
        match valueR, unitR with
        | Ok v, Ok u -> Ok { Value = v; Unit = u }
        | _ ->
            [ valueR |> Result.map ignore; unitR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error

let private parseFontFamilyValue (path: string) (n: JsonNode) : Result<FontFamilyValue, ParseError list> =
    match tryGetString n with
    | Some s -> Ok (Single s)
    | None ->
        match asArray n with
        | Some arr ->
            let strings =
                [ for i in 0 .. arr.Count - 1 ->
                    match tryGetString arr.[i] with
                    | Some s -> Ok s
                    | None -> Error (InvalidValue (sprintf "%s[%d]" path i, "expected string in fontFamily stack")) ]
            collectResults strings |> Result.map Stack
        | None -> Error [InvalidValue (path, "fontFamily must be a string or array of strings")]

let private parseFontWeightValue (path: string) (n: JsonNode) : Result<FontWeightValue, ParseError list> =
    match tryGetInt n with
    | Some i -> Ok (Numeric i)
    | None ->
        match tryGetFloat n with
        | Some f when f = floor f && Double.IsFinite f -> Ok (Numeric (int f))
        | _ ->
            match tryGetString n with
            | Some s -> parseFontWeightKeyword path s |> liftSingle |> Result.map Keyword
            | None   -> Error [InvalidValue (path, "fontWeight must be an integer or keyword")]

let private parseCubicBezierValue (path: string) (n: JsonNode) : Result<CubicBezierValue, ParseError list> =
    match asArray n with
    | Some arr when arr.Count = 4 ->
        let parseFloat i =
            requireFloat (sprintf "%s[%d]" path i) "cubicBezier component" arr.[i]
        match parseFloat 0, parseFloat 1, parseFloat 2, parseFloat 3 with
        | Ok a, Ok b, Ok c, Ok d ->
            Ok { P1x = a; P1y = b; P2x = c; P2y = d }
        | a, b, c, d ->
            [ a; b; c; d ]
            |> List.collect (function Error e -> [e] | Ok _ -> [])
            |> Error
    | Some arr ->
        Error [InvalidValue (path, sprintf "cubicBezier must have 4 elements (got %d)" arr.Count)]
    | None -> Error [InvalidValue (path, "cubicBezier must be an array")]


// ─── ValueOrRef helpers ──────────────────────────────────────────────────────

let private parseValueOrRef
    (parseLit: string -> JsonNode -> Result<'T, ParseError list>)
    (path: string)
    (n: JsonNode)
    : Result<ValueOrRef<'T>, ParseError list> =
    if isRefShape n then
        readRef path n |> liftSingle |> Result.map Reference
    else
        parseLit path n |> Result.map Literal


let private parseStrokeStyleValue (path: string) (n: JsonNode) : Result<StrokeStyleValue, ParseError list> =
    match tryGetString n with
    | Some s -> parseStrokeKeyword path s |> liftSingle |> Result.map StrokeKeyword
    | None ->
        requireObject path "strokeStyle" n |> liftSingle >>= fun o ->
            let dashArrayR =
                match tryGetProperty "dashArray" o with
                | Some node ->
                    match asArray node with
                    | Some arr ->
                        let items =
                            [ for i in 0 .. arr.Count - 1 ->
                                parseValueOrRef parseDimensionValue (sprintf "%s.dashArray[%d]" path i) arr.[i] ]
                        let oks = ResizeArray()
                        let errs = ResizeArray()
                        for x in items do
                            match x with Ok v -> oks.Add v | Error es -> errs.AddRange es
                        if errs.Count > 0 then Error (List.ofSeq errs)
                        else Ok (List.ofSeq oks)
                    | None -> Error [InvalidValue (path, "dashArray must be an array")]
                | None -> Error [MissingRequiredField (path, "dashArray")]
            let lineCapR =
                requireProperty path "lineCap" o |> liftSingle
                >>= fun node -> requireString path "lineCap" node |> liftSingle
                >>= fun s -> parseLineCap path s |> liftSingle
            match dashArrayR, lineCapR with
            | Ok da, Ok lc -> Ok (StrokeCustom { DashArray = da; LineCap = lc })
            | _ ->
                [ dashArrayR |> Result.map ignore; lineCapR |> Result.map ignore ]
                |> List.collect (function Error es -> es | Ok _ -> [])
                |> Error

let private parseBorderValue (path: string) (n: JsonNode) : Result<BorderValue, ParseError list> =
    requireObject path "border" n |> liftSingle >>= fun o ->
        let colorR =
            requireProperty path "color" o |> liftSingle
            >>= parseValueOrRef parseColorValue (path + ".color")
        let widthR =
            requireProperty path "width" o |> liftSingle
            >>= parseValueOrRef parseDimensionValue (path + ".width")
        let styleR =
            requireProperty path "style" o |> liftSingle
            >>= parseValueOrRef parseStrokeStyleValue (path + ".style")
        match colorR, widthR, styleR with
        | Ok c, Ok w, Ok s -> Ok { Color = c; Width = w; Style = s }
        | _ ->
            [ colorR |> Result.map ignore; widthR |> Result.map ignore; styleR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error

let private parseShadowObject (path: string) (n: JsonNode) : Result<ShadowObject, ParseError list> =
    requireObject path "shadow" n |> liftSingle >>= fun o ->
        let colorR =
            requireProperty path "color" o |> liftSingle
            >>= parseValueOrRef parseColorValue (path + ".color")
        let offXR =
            requireProperty path "offsetX" o |> liftSingle
            >>= parseValueOrRef parseDimensionValue (path + ".offsetX")
        let offYR =
            requireProperty path "offsetY" o |> liftSingle
            >>= parseValueOrRef parseDimensionValue (path + ".offsetY")
        let blurR =
            requireProperty path "blur" o |> liftSingle
            >>= parseValueOrRef parseDimensionValue (path + ".blur")
        let spreadR =
            requireProperty path "spread" o |> liftSingle
            >>= parseValueOrRef parseDimensionValue (path + ".spread")
        let insetR =
            match tryGetProperty "inset" o with
            | Some node -> requireBool path "inset" node |> liftSingle |> Result.map Some
            | None -> Ok None
        match colorR, offXR, offYR, blurR, spreadR, insetR with
        | Ok c, Ok ox, Ok oy, Ok bl, Ok sp, Ok ins ->
            Ok { Color = c; OffsetX = ox; OffsetY = oy; Blur = bl; Spread = sp; Inset = ins }
        | _ ->
            [ colorR |> Result.map ignore; offXR |> Result.map ignore; offYR |> Result.map ignore
              blurR |> Result.map ignore; spreadR |> Result.map ignore; insetR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error

let private parseShadowValue (path: string) (n: JsonNode) : Result<ShadowValue, ParseError list> =
    match asArray n with
    | Some arr ->
        let items =
            [ for i in 0 .. arr.Count - 1 ->
                let itemPath = sprintf "%s[%d]" path i
                if isRefShape arr.[i] then
                    readRef itemPath arr.[i] |> liftSingle |> Result.map ShadowReference
                else
                    parseShadowObject itemPath arr.[i] |> Result.map ShadowLiteral ]
        let oks = ResizeArray()
        let errs = ResizeArray()
        for x in items do
            match x with Ok v -> oks.Add v | Error es -> errs.AddRange es
        if errs.Count > 0 then Error (List.ofSeq errs)
        else Ok (ShadowMultiple (List.ofSeq oks))
    | None ->
        parseShadowObject path n |> Result.map ShadowSingle

let private parseTransitionValue (path: string) (n: JsonNode) : Result<TransitionValue, ParseError list> =
    requireObject path "transition" n |> liftSingle >>= fun o ->
        let durR =
            requireProperty path "duration" o |> liftSingle
            >>= parseValueOrRef parseDurationValue (path + ".duration")
        let delR =
            match tryGetProperty "delay" o with
            | Some node -> parseValueOrRef parseDurationValue (path + ".delay") node
            | None      -> Ok (Literal { Value = 0.0; Unit = Milliseconds })
        let tfR =
            requireProperty path "timingFunction" o |> liftSingle
            >>= parseValueOrRef parseCubicBezierValue (path + ".timingFunction")
        match durR, delR, tfR with
        | Ok d, Ok dl, Ok tf -> Ok { Duration = d; Delay = dl; TimingFunction = tf }
        | _ ->
            [ durR |> Result.map ignore; delR |> Result.map ignore; tfR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error

let private parseGradientStop (path: string) (n: JsonNode) : Result<GradientStop, ParseError list> =
    requireObject path "gradient stop" n |> liftSingle >>= fun o ->
        let colorR =
            requireProperty path "color" o |> liftSingle
            >>= parseValueOrRef parseColorValue (path + ".color")
        let posR =
            requireProperty path "position" o |> liftSingle
            >>= fun node ->
                if isRefShape node then
                    readRef (path + ".position") node |> liftSingle |> Result.map Reference
                else
                    requireFloat (path + ".position") "position" node |> liftSingle |> Result.map Literal
        match colorR, posR with
        | Ok c, Ok p -> Ok { Color = c; Position = p }
        | _ ->
            [ colorR |> Result.map ignore; posR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error

let private parseGradientValue (path: string) (n: JsonNode) : Result<GradientValue, ParseError list> =
    match asArray n with
    | Some arr ->
        let items =
            [ for i in 0 .. arr.Count - 1 ->
                parseGradientStop (sprintf "%s[%d]" path i) arr.[i] ]
        let oks = ResizeArray()
        let errs = ResizeArray()
        for x in items do
            match x with Ok v -> oks.Add v | Error es -> errs.AddRange es
        if errs.Count > 0 then Error (List.ofSeq errs)
        else Ok (List.ofSeq oks)
    | None -> Error [InvalidValue (path, "gradient must be an array of stops")]

let private parseTypographyValue (path: string) (n: JsonNode) : Result<TypographyValue, ParseError list> =
    requireObject path "typography" n |> liftSingle >>= fun o ->
        let famR =
            requireProperty path "fontFamily" o |> liftSingle
            >>= parseValueOrRef parseFontFamilyValue (path + ".fontFamily")
        let sizeR =
            requireProperty path "fontSize" o |> liftSingle
            >>= parseValueOrRef parseDimensionValue (path + ".fontSize")
        let weightR =
            requireProperty path "fontWeight" o |> liftSingle
            >>= parseValueOrRef parseFontWeightValue (path + ".fontWeight")
        let letterR =
            match tryGetProperty "letterSpacing" o with
            | Some node -> parseValueOrRef parseDimensionValue (path + ".letterSpacing") node
            | None -> Ok (Literal { Value = 0.0; Unit = Px })
        let lhR =
            requireProperty path "lineHeight" o |> liftSingle
            >>= fun node ->
                if isRefShape node then
                    readRef (path + ".lineHeight") node |> liftSingle |> Result.map Reference
                else
                    requireFloat (path + ".lineHeight") "lineHeight" node |> liftSingle |> Result.map Literal
        match famR, sizeR, weightR, letterR, lhR with
        | Ok f, Ok s, Ok w, Ok l, Ok h ->
            Ok { FontFamily = f; FontSize = s; FontWeight = w; LetterSpacing = l; LineHeight = h }
        | _ ->
            [ famR |> Result.map ignore; sizeR |> Result.map ignore; weightR |> Result.map ignore
              letterR |> Result.map ignore; lhR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error


// ─── TokenValue dispatch (using inherited $type) ─────────────────────────────

let private parseTokenValueOfType
    (path: string)
    (tokenType: TokenType)
    (n: JsonNode)
    : Result<TokenValue, ParseError list> =
    if isRefShape n then
        readRef path n |> liftSingle |> Result.map TokenValue.Alias
    else
        match tokenType with
        | ColorType        -> parseColorValue path n        |> Result.map TokenValue.Color
        | DimensionType    -> parseDimensionValue path n    |> Result.map TokenValue.Dimension
        | FontFamilyType   -> parseFontFamilyValue path n   |> Result.map TokenValue.FontFamily
        | FontWeightType   -> parseFontWeightValue path n   |> Result.map TokenValue.FontWeight
        | DurationType     -> parseDurationValue path n     |> Result.map TokenValue.Duration
        | CubicBezierType  -> parseCubicBezierValue path n  |> Result.map TokenValue.CubicBezier
        | NumberType ->
            requireFloat path "number" n |> liftSingle |> Result.map TokenValue.Number
        | StrokeStyleType  -> parseStrokeStyleValue path n  |> Result.map TokenValue.StrokeStyle
        | BorderType       -> parseBorderValue path n       |> Result.map TokenValue.Border
        | TransitionType   -> parseTransitionValue path n   |> Result.map TokenValue.Transition
        | ShadowType       -> parseShadowValue path n       |> Result.map TokenValue.Shadow
        | GradientType     -> parseGradientValue path n     |> Result.map TokenValue.Gradient
        | TypographyType   -> parseTypographyValue path n   |> Result.map TokenValue.Typography


// ─── Metadata parsing ────────────────────────────────────────────────────────

let private parseDeprecated (path: string) (n: JsonNode) : Result<Deprecated, ParseError> =
    match tryGetBool n with
    | Some true  -> Ok Deprecated.Deprecated
    | Some false -> Error (InvalidValue (path, "$deprecated cannot be false"))
    | None ->
        match tryGetString n with
        | Some s -> Ok (DeprecatedWithMessage s)
        | None   -> Error (InvalidValue (path, "$deprecated must be true or a string"))

let private parseMetadata (path: string) (o: JsonObject) : Result<Metadata, ParseError list> =
    let descR =
        match tryGetProperty "$description" o with
        | Some node -> requireString path "$description" node |> liftSingle |> Result.map Some
        | None -> Ok None
    let depR =
        match tryGetProperty "$deprecated" o with
        | Some node -> parseDeprecated path node |> liftSingle |> Result.map Some
        | None -> Ok None
    let extR =
        match tryGetProperty "$extensions" o with
        | Some node ->
            match asObject node with
            | Some eo -> Ok (readExtensions eo)
            | None    -> Error [InvalidValue (path, "$extensions must be an object")]
        | None -> Ok []
    match descR, depR, extR with
    | Ok d, Ok dep, Ok ex -> Ok { Description = d; Deprecated = dep; Extensions = ex }
    | _ ->
        [ descR |> Result.map ignore; depR |> Result.map ignore; extR |> Result.map ignore ]
        |> List.collect (function Error es -> es | Ok _ -> [])
        |> Error


// ─── Tree walk ───────────────────────────────────────────────────────────────

let private isMetadataKey (k: string) : bool = k.StartsWith "$"

let private parseTypeAt (path: string) (o: JsonObject) : Result<TokenType option, ParseError list> =
    match tryGetProperty "$type" o with
    | Some node -> requireString path "$type" node |> Result.bind (parseTokenType path) |> liftSingle |> Result.map Some
    | None -> Ok None

let private parseExtends (path: string) (o: JsonObject) : Result<TokenRef option, ParseError list> =
    match tryGetProperty "$extends" o with
    | Some node -> readRef path node |> liftSingle |> Result.map Some
    | None -> Ok None


let rec private parseNode
    (path: string)
    (inheritedType: TokenType option)
    (name: string)
    (n: JsonNode)
    : Result<TokenNode, ParseError list> =
    requireObject path (sprintf "node '%s'" name) n |> liftSingle >>= fun o ->
        let nodeTypeR = parseTypeAt path o
        nodeTypeR >>= fun nodeType ->
            let effectiveType = nodeType |> Option.orElse inheritedType
            let metaR = parseMetadata path o

            match tryGetProperty "$value" o with
            | Some valueNode ->
                metaR >>= fun meta ->
                    let valueR =
                        match effectiveType with
                        | Some t -> parseTokenValueOfType path t valueNode
                        | None ->
                            // Type unknown: only Alias is parseable without type
                            if isRefShape valueNode then
                                readRef path valueNode |> liftSingle |> Result.map TokenValue.Alias
                            else
                                Error [MissingRequiredField (path, "$type")]
                    valueR |> Result.map (fun v ->
                        TokenLeaf { Value = v; Type = nodeType; Metadata = meta })
            | None ->
                // It's a group
                let extendsR = parseExtends path o
                let rootR =
                    match tryGetProperty "$root" o with
                    | Some rnode ->
                        match effectiveType with
                        | Some t ->
                            requireObject path "$root" rnode |> liftSingle >>= fun ro ->
                                let rmetaR = parseMetadata path ro
                                let rvalueR =
                                    match tryGetProperty "$value" ro with
                                    | Some rv -> parseTokenValueOfType path t rv
                                    | None    -> Error [MissingRequiredField (path + ".$root", "$value")]
                                match rvalueR, rmetaR with
                                | Ok v, Ok m ->
                                    Ok (Some ({ Value = v; Type = Some t; Metadata = m } : Token))
                                | _ ->
                                    [ rvalueR |> Result.map ignore; rmetaR |> Result.map ignore ]
                                    |> List.collect (function Error es -> es | Ok _ -> [])
                                    |> Error
                        | None -> Error [MissingRequiredField (path, "$type (required for $root)")]
                    | None -> Ok None

                let childrenR =
                    let childResults =
                        properties o
                        |> Seq.filter (fun (k, _) -> not (isMetadataKey k))
                        |> Seq.map (fun (k, v) ->
                            let childPath = if path = "" then k else path + "." + k
                            match TokenName.tryCreate k with
                            | Error msg -> Error [InvalidValue (childPath, msg)]
                            | Ok tn ->
                                parseNode childPath effectiveType k v
                                |> Result.map (fun node -> tn, node))
                        |> List.ofSeq
                    let oks = ResizeArray()
                    let errs = ResizeArray()
                    for r in childResults do
                        match r with Ok v -> oks.Add v | Error es -> errs.AddRange es
                    if errs.Count > 0 then Error (List.ofSeq errs)
                    else Ok (List.ofSeq oks)

                match metaR, extendsR, rootR, childrenR with
                | Ok m, Ok ex, Ok r, Ok ch ->
                    Ok (Group { Type = nodeType; Metadata = m; Root = r; Extends = ex; Children = ch })
                | _ ->
                    [ metaR |> Result.map ignore; extendsR |> Result.map ignore
                      rootR |> Result.map ignore; childrenR |> Result.map ignore ]
                    |> List.collect (function Error es -> es | Ok _ -> [])
                    |> Error


// ─── Version detection ──────────────────────────────────────────────────────

let private detectVersionFromSchema (url: string) : SpecVersion option =
    if url.Contains "/2025.10/" || url.Contains "/format/2025-10/" then Some V2025_10
    elif url.Contains "/third-editors-draft/" then Some ThirdEditorsDraft
    elif url.Contains "/second-editors-draft/" then Some SecondEditorsDraft
    elif url.Contains "/drafts/format/" then Some FirstEditorsDraft
    else None

/// Heuristic: First ED uses unprefixed `type`/`value`; Second/Third ED & 2025.10 use `$type`/`$value`.
/// First/Second ED store color as a hex string; Third ED+ uses an object.
/// Resolver-related shapes only exist in 2025.10.
let private detectVersionStructurally (root: JsonObject) : SpecVersion =
    // Walk only token-tree nodes (groups + leaves). Stop recursing into the
    // *contents* of $value, since the inner value shape may legitimately have
    // keys named "value" or "type" that are unrelated to First-ED markers.
    let rec scan (o: JsonObject) (foundUnprefixed: bool ref) (foundColorObj: bool ref) (foundColorHex: bool ref) =
        let hasDollarValue = tryGetProperty "$value" o |> Option.isSome
        let hasDollarType  = tryGetProperty "$type"  o |> Option.isSome
        for kv in o do
            match kv.Key with
            | "type" | "value" when not hasDollarValue && not hasDollarType ->
                foundUnprefixed.Value <- true
            | "$value" ->
                match kv.Value with
                | :? JsonObject as inner when (tryGetProperty "colorSpace" inner |> Option.isSome) ->
                    foundColorObj.Value <- true
                | :? JsonValue ->
                    match tryGetString kv.Value with
                    | Some s when s.StartsWith "#" -> foundColorHex.Value <- true
                    | _ -> ()
                | _ -> ()
            | _ -> ()
        // Recurse only into siblings that are not $value/$extensions/$deprecated content.
        for kv in o do
            match kv.Key, kv.Value with
            | ("$value" | "$extensions" | "$deprecated"), _ -> ()
            | _, (:? JsonObject as child) -> scan child foundUnprefixed foundColorObj foundColorHex
            | _ -> ()

    let unprefixed = ref false
    let colorObj = ref false
    let colorHex = ref false
    scan root unprefixed colorObj colorHex

    if unprefixed.Value then FirstEditorsDraft
    elif colorObj.Value then V2025_10
    elif colorHex.Value then SecondEditorsDraft
    else V2025_10


// ─── Upgrade pass ────────────────────────────────────────────────────────────
// Rewrites JSON in place to the V2025_10 shape.

let rec private upgradeFirstED (node: JsonNode) : unit =
    match node with
    | :? JsonObject as o ->
        // Rename `type` → `$type`, `value` → `$value`
        let renames = ResizeArray<string * string>()
        for kv in o do
            match kv.Key with
            | "type"  -> renames.Add ("type", "$type")
            | "value" -> renames.Add ("value", "$value")
            | _ -> ()
        for (oldK, newK) in renames do
            let v = o.[oldK]
            o.Remove oldK |> ignore
            if not (isNull v) then
                let detached = v.DeepClone()
                o.[newK] <- detached
        // Recurse
        for kv in o do
            if not (isNull kv.Value) then upgradeFirstED kv.Value
    | :? JsonArray as a ->
        for i in 0 .. a.Count - 1 do
            if not (isNull a.[i]) then upgradeFirstED a.[i]
    | _ -> ()

/// Second/Third ED upgrades: if a $value is a string in dimension/duration/color form, rewrite to object.
/// Walks the tree carrying inherited $type so we know how to interpret string values.
let rec private upgradeStringValues (inheritedType: string option) (node: JsonNode) : unit =
    match node with
    | :? JsonObject as o ->
        let nodeType =
            match tryGetProperty "$type" o with
            | Some t -> tryGetString t |> Option.orElse inheritedType
            | None -> inheritedType

        match tryGetProperty "$value" o, nodeType with
        | Some v, Some "dimension" ->
            match tryGetString v with
            | Some s ->
                // Parse "16px" or "1.5rem"
                let mDigits = System.Text.RegularExpressions.Regex.Match(s, @"^(-?\d+(?:\.\d+)?)(px|rem)$")
                if mDigits.Success then
                    let value = Double.Parse(mDigits.Groups.[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                    let unit = mDigits.Groups.[2].Value
                    let obj = JsonObject()
                    obj.["value"] <- JsonValue.Create(value)
                    obj.["unit"]  <- JsonValue.Create(unit)
                    o.["$value"] <- obj
            | None -> ()
        | Some v, Some "duration" ->
            match tryGetString v with
            | Some s ->
                let mDigits = System.Text.RegularExpressions.Regex.Match(s, @"^(-?\d+(?:\.\d+)?)(ms|s)$")
                if mDigits.Success then
                    let value = Double.Parse(mDigits.Groups.[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                    let unit = mDigits.Groups.[2].Value
                    let obj = JsonObject()
                    obj.["value"] <- JsonValue.Create(value)
                    obj.["unit"]  <- JsonValue.Create(unit)
                    o.["$value"] <- obj
            | None -> ()
        | Some v, Some "color" ->
            match tryGetString v with
            | Some s when s.StartsWith "#" && (s.Length = 7 || s.Length = 9) ->
                let parseHex (i: int) =
                    Convert.ToInt32(s.Substring(i, 2), 16) |> float
                    |> (fun x -> x / 255.0)
                let r = parseHex 1
                let g = parseHex 3
                let b = parseHex 5
                let obj = JsonObject()
                obj.["colorSpace"] <- JsonValue.Create("srgb")
                let comps = JsonArray()
                comps.Add(JsonValue.Create(r))
                comps.Add(JsonValue.Create(g))
                comps.Add(JsonValue.Create(b))
                obj.["components"] <- comps
                if s.Length = 9 then
                    obj.["alpha"] <- JsonValue.Create(parseHex 7)
                obj.["hex"] <- JsonValue.Create(s)
                o.["$value"] <- obj
            | _ -> ()
        | _ -> ()

        for kv in o do
            if not (isNull kv.Value) then upgradeStringValues nodeType kv.Value
    | :? JsonArray as a ->
        for i in 0 .. a.Count - 1 do
            if not (isNull a.[i]) then upgradeStringValues inheritedType a.[i]
    | _ -> ()


// ─── Top-level parse ─────────────────────────────────────────────────────────

let private parseRoot (json: string) : Result<JsonNode, ParseError list> =
    Json.parseRoot json |> liftSingle

let parse (jsonText: string) : Result<TokenFile, ParseError list> =
    parseRoot jsonText >>= fun rootNode ->
        match asObject rootNode with
        | None -> Error [InvalidJson "root must be an object"]
        | Some root ->
            // Schema URL (raw, preserved)
            let schemaUrl =
                match tryGetProperty "$schema" root with
                | Some node -> tryGetString node
                | None -> None

            // Detect version
            let schemaVersion = schemaUrl |> Option.bind detectVersionFromSchema
            let structuralVersion = detectVersionStructurally root

            let versionResult : Result<SpecVersion, ParseError list> =
                match schemaVersion with
                | Some v when v <> structuralVersion ->
                    // Allow $schema to override only if structural says V2025_10 (default fallback) and schema points to older;
                    // otherwise contradiction.
                    if structuralVersion = V2025_10 then Ok v
                    else Error [SchemaVersionContradicts (Option.defaultValue "" schemaUrl, structuralVersion)]
                | Some v -> Ok v
                | None   -> Ok structuralVersion

            versionResult >>= fun version ->
                // Upgrade pass
                match version with
                | FirstEditorsDraft ->
                    upgradeFirstED root
                    upgradeStringValues None root
                | SecondEditorsDraft | ThirdEditorsDraft ->
                    upgradeStringValues None root
                | V2025_10 -> ()

                // Walk children
                let childResults =
                    properties root
                    |> Seq.filter (fun (k, _) -> not (isMetadataKey k))
                    |> Seq.map (fun (k, v) ->
                        match TokenName.tryCreate k with
                        | Error msg -> Error [InvalidValue (k, msg)]
                        | Ok tn ->
                            parseNode k None k v
                            |> Result.map (fun node -> tn, node))
                    |> List.ofSeq

                let oks = ResizeArray()
                let errs = ResizeArray()
                for r in childResults do
                    match r with Ok v -> oks.Add v | Error es -> errs.AddRange es

                if errs.Count > 0 then Error (List.ofSeq errs)
                else Ok { Version = version; Schema = schemaUrl; Children = List.ofSeq oks }

let parseAs (expected: SpecVersion) (jsonText: string) : Result<TokenFile, ParseError list> =
    parse jsonText
    |> Result.bind (fun file ->
        if file.Version = expected then Ok file
        else Error [InvalidValue ("$schema",
                                  sprintf "expected %s, got %s"
                                      (SpecVersion.display expected)
                                      (SpecVersion.display file.Version))])

let parseAuto (jsonText: string) : Result<AutoParseResult, ParseError list> =
    parseRoot jsonText >>= fun root ->
        match asObject root with
        | None -> Error [InvalidJson "root must be an object"]
        | Some o ->
            let hasResolutionOrder = tryGetProperty "resolutionOrder" o |> Option.isSome
            if hasResolutionOrder then
                // Resolver document — parsing handled elsewhere; signal to caller.
                Error [InvalidValue ("", "resolver document detected — use Resolver.parseResolver")]
            else
                parse jsonText |> Result.map TokenFileResult


// ─── Serialization (always V2025_10) ─────────────────────────────────────────

let private writeColorValue (w: Utf8JsonWriter) (c: ColorValue) =
    w.WriteStartObject()
    w.WriteString("colorSpace", colorSpaceToString c.ColorSpace)
    w.WritePropertyName("components")
    w.WriteStartArray()
    let writeComp cc =
        match cc with
        | Channel f -> w.WriteNumberValue f
        | Missing   -> w.WriteStringValue "none"
    let (a, b, ch) = c.Components
    writeComp a; writeComp b; writeComp ch
    w.WriteEndArray()
    match c.Alpha with Some a -> w.WriteNumber("alpha", a) | None -> ()
    match c.Hex with Some h -> w.WriteString("hex", h) | None -> ()
    w.WriteEndObject()

let private writeDimensionValue (w: Utf8JsonWriter) (d: DimensionValue) =
    w.WriteStartObject()
    w.WriteNumber("value", d.Value)
    w.WriteString("unit", dimensionUnitToString d.Unit)
    w.WriteEndObject()

let private writeDurationValue (w: Utf8JsonWriter) (d: DurationValue) =
    w.WriteStartObject()
    w.WriteNumber("value", d.Value)
    w.WriteString("unit", durationUnitToString d.Unit)
    w.WriteEndObject()

let private writeFontFamilyValue (w: Utf8JsonWriter) (f: FontFamilyValue) =
    match f with
    | Single s -> w.WriteStringValue s
    | Stack xs ->
        w.WriteStartArray()
        for x in xs do w.WriteStringValue x
        w.WriteEndArray()

let private writeFontWeightValue (w: Utf8JsonWriter) (fw: FontWeightValue) =
    match fw with
    | Numeric n -> w.WriteNumberValue n
    | Keyword k -> w.WriteStringValue (fontWeightKeywordToString k)

let private writeCubicBezier (w: Utf8JsonWriter) (cb: CubicBezierValue) =
    w.WriteStartArray()
    w.WriteNumberValue cb.P1x
    w.WriteNumberValue cb.P1y
    w.WriteNumberValue cb.P2x
    w.WriteNumberValue cb.P2y
    w.WriteEndArray()

let private writeRef (w: Utf8JsonWriter) (r: TokenRef) =
    match r with
    | CurlyBrace path -> w.WriteStringValue ("{" + String.concat "." path + "}")
    | JsonPointer p ->
        w.WriteStartObject()
        w.WriteString("$ref", p)
        w.WriteEndObject()

let private writeValueOrRef (writeLit: Utf8JsonWriter -> 'T -> unit) (w: Utf8JsonWriter) (vor: ValueOrRef<'T>) =
    match vor with
    | Literal v -> writeLit w v
    | Reference r -> writeRef w r

let private writeStrokeStyleValue (w: Utf8JsonWriter) (s: StrokeStyleValue) =
    match s with
    | StrokeKeyword k -> w.WriteStringValue (strokeKeywordToString k)
    | StrokeCustom obj ->
        w.WriteStartObject()
        w.WritePropertyName("dashArray")
        w.WriteStartArray()
        for d in obj.DashArray do writeValueOrRef writeDimensionValue w d
        w.WriteEndArray()
        w.WriteString("lineCap", lineCapToString obj.LineCap)
        w.WriteEndObject()

let private writeBorderValue (w: Utf8JsonWriter) (b: BorderValue) =
    w.WriteStartObject()
    w.WritePropertyName("color");  writeValueOrRef writeColorValue w b.Color
    w.WritePropertyName("width");  writeValueOrRef writeDimensionValue w b.Width
    w.WritePropertyName("style");  writeValueOrRef writeStrokeStyleValue w b.Style
    w.WriteEndObject()

let private writeShadowObject (w: Utf8JsonWriter) (s: ShadowObject) =
    w.WriteStartObject()
    w.WritePropertyName("color");   writeValueOrRef writeColorValue w s.Color
    w.WritePropertyName("offsetX"); writeValueOrRef writeDimensionValue w s.OffsetX
    w.WritePropertyName("offsetY"); writeValueOrRef writeDimensionValue w s.OffsetY
    w.WritePropertyName("blur");    writeValueOrRef writeDimensionValue w s.Blur
    w.WritePropertyName("spread");  writeValueOrRef writeDimensionValue w s.Spread
    match s.Inset with Some b -> w.WriteBoolean("inset", b) | None -> ()
    w.WriteEndObject()

let private writeShadowValue (w: Utf8JsonWriter) (s: ShadowValue) =
    match s with
    | ShadowSingle obj -> writeShadowObject w obj
    | ShadowMultiple xs ->
        w.WriteStartArray()
        for x in xs do
            match x with
            | ShadowLiteral obj -> writeShadowObject w obj
            | ShadowReference r -> writeRef w r
        w.WriteEndArray()

let private writeTransitionValue (w: Utf8JsonWriter) (t: TransitionValue) =
    w.WriteStartObject()
    w.WritePropertyName("duration");       writeValueOrRef writeDurationValue w t.Duration
    w.WritePropertyName("delay");          writeValueOrRef writeDurationValue w t.Delay
    w.WritePropertyName("timingFunction"); writeValueOrRef writeCubicBezier w t.TimingFunction
    w.WriteEndObject()

let private writeGradientValue (w: Utf8JsonWriter) (g: GradientValue) =
    w.WriteStartArray()
    for stop in g do
        w.WriteStartObject()
        w.WritePropertyName("color"); writeValueOrRef writeColorValue w stop.Color
        w.WritePropertyName("position")
        match stop.Position with
        | Literal f -> w.WriteNumberValue f
        | Reference r -> writeRef w r
        w.WriteEndObject()
    w.WriteEndArray()

let private writeTypographyValue (w: Utf8JsonWriter) (t: TypographyValue) =
    w.WriteStartObject()
    w.WritePropertyName("fontFamily");    writeValueOrRef writeFontFamilyValue w t.FontFamily
    w.WritePropertyName("fontSize");      writeValueOrRef writeDimensionValue w t.FontSize
    w.WritePropertyName("fontWeight");    writeValueOrRef writeFontWeightValue w t.FontWeight
    w.WritePropertyName("letterSpacing"); writeValueOrRef writeDimensionValue w t.LetterSpacing
    w.WritePropertyName("lineHeight")
    match t.LineHeight with
    | Literal f -> w.WriteNumberValue f
    | Reference r -> writeRef w r
    w.WriteEndObject()

let private writeTokenValue (w: Utf8JsonWriter) (v: TokenValue) =
    match v with
    | TokenValue.Color c       -> writeColorValue w c
    | TokenValue.Dimension d   -> writeDimensionValue w d
    | TokenValue.FontFamily f  -> writeFontFamilyValue w f
    | TokenValue.FontWeight fw -> writeFontWeightValue w fw
    | TokenValue.Duration d    -> writeDurationValue w d
    | TokenValue.CubicBezier c -> writeCubicBezier w c
    | TokenValue.Number n      -> w.WriteNumberValue n
    | TokenValue.StrokeStyle s -> writeStrokeStyleValue w s
    | TokenValue.Border b      -> writeBorderValue w b
    | TokenValue.Shadow s      -> writeShadowValue w s
    | TokenValue.Transition t  -> writeTransitionValue w t
    | TokenValue.Gradient g    -> writeGradientValue w g
    | TokenValue.Typography ty -> writeTypographyValue w ty
    | TokenValue.Alias r       -> writeRef w r

let private writeMetadata (w: Utf8JsonWriter) (m: Metadata) =
    match m.Description with Some d -> w.WriteString("$description", d) | None -> ()
    match m.Deprecated with
    | Some Deprecated.Deprecated            -> w.WriteBoolean("$deprecated", true)
    | Some (DeprecatedWithMessage s) -> w.WriteString("$deprecated", s)
    | None -> ()
    match m.Extensions with
    | [] -> ()
    | xs ->
        w.WritePropertyName("$extensions")
        w.WriteStartObject()
        for (k, v) in xs do
            w.WritePropertyName k
            if isNull v then w.WriteNullValue()
            else v.WriteTo w
        w.WriteEndObject()

let private writeTokenType (w: Utf8JsonWriter) (t: TokenType) =
    w.WriteString("$type", tokenTypeToString t)

let rec private writeNode (w: Utf8JsonWriter) (node: TokenNode) =
    match node with
    | TokenLeaf t ->
        w.WriteStartObject()
        match t.Type with Some tt -> writeTokenType w tt | None -> ()
        writeMetadata w t.Metadata
        w.WritePropertyName("$value")
        writeTokenValue w t.Value
        w.WriteEndObject()
    | Group g ->
        w.WriteStartObject()
        match g.Type with Some tt -> writeTokenType w tt | None -> ()
        match g.Extends with
        | Some r ->
            w.WritePropertyName("$extends")
            writeRef w r
        | None -> ()
        writeMetadata w g.Metadata
        match g.Root with
        | Some t ->
            w.WritePropertyName("$root")
            w.WriteStartObject()
            writeMetadata w t.Metadata
            w.WritePropertyName("$value")
            writeTokenValue w t.Value
            w.WriteEndObject()
        | None -> ()
        for (name, child) in g.Children do
            w.WritePropertyName(TokenName.value name)
            writeNode w child
        w.WriteEndObject()

let serialize (file: TokenFile) : string =
    use stream = new MemoryStream()
    let opts = JsonWriterOptions(Indented = true, SkipValidation = false)
    use w = new Utf8JsonWriter(stream, opts)
    w.WriteStartObject()
    match file.Schema with Some s -> w.WriteString("$schema", s) | None -> ()
    for (name, node) in file.Children do
        w.WritePropertyName(TokenName.value name)
        writeNode w node
    w.WriteEndObject()
    w.Flush()
    Encoding.UTF8.GetString(stream.ToArray())

let serializeAs (target: SpecVersion) (file: TokenFile) : Result<string, SerializeError list> =
    match target with
    | V2025_10 -> Ok (serialize file)
    | older ->
        // Older versions: collect unsupported features. For now we mark all composite types
        // as unsupported in First/Second ED if encountered.
        let errs = ResizeArray<SerializeError>()
        let rec walk (path: string) (node: TokenNode) =
            match node with
            | TokenLeaf t ->
                match t.Value, older with
                | TokenValue.Gradient _, FirstEditorsDraft
                | TokenValue.Gradient _, SecondEditorsDraft ->
                    errs.Add (UnsupportedInTargetVersion (path, "gradient", older))
                | TokenValue.Border _, FirstEditorsDraft
                | TokenValue.Border _, SecondEditorsDraft ->
                    errs.Add (UnsupportedInTargetVersion (path, "border", older))
                | TokenValue.Shadow _, FirstEditorsDraft ->
                    errs.Add (UnsupportedInTargetVersion (path, "shadow", older))
                | TokenValue.Transition _, FirstEditorsDraft ->
                    errs.Add (UnsupportedInTargetVersion (path, "transition", older))
                | TokenValue.Typography _, FirstEditorsDraft ->
                    errs.Add (UnsupportedInTargetVersion (path, "typography", older))
                | TokenValue.StrokeStyle _, FirstEditorsDraft
                | TokenValue.StrokeStyle _, SecondEditorsDraft ->
                    errs.Add (UnsupportedInTargetVersion (path, "strokeStyle", older))
                | _ -> ()
            | Group g ->
                for (name, child) in g.Children do
                    let child_path = if path = "" then TokenName.value name else path + "." + TokenName.value name
                    walk child_path child
        for (name, node) in file.Children do
            walk (TokenName.value name) node
        if errs.Count > 0 then Error (List.ofSeq errs)
        else Ok (serialize file)
