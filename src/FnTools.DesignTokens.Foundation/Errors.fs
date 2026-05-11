namespace FnTools.DesignTokens

// ─── SpecVersion ─────────────────────────────────────────────────────────────
// Defined here (not Domain.fs) because ParseError and SerializeError reference it
// and F# compile order is strict.

type SpecVersion =
    | FirstEditorsDraft
    | SecondEditorsDraft
    | ThirdEditorsDraft
    | V2025_10

module SpecVersion =

    let display (v: SpecVersion) : string =
        match v with
        | FirstEditorsDraft  -> "First Editors' Draft (2021-09-23)"
        | SecondEditorsDraft -> "Second Editors' Draft (2022-06-14)"
        | ThirdEditorsDraft  -> "Third Editors' Draft (2025-08-04)"
        | V2025_10           -> "2025.10"


// ─── Export loss acknowledgment ───────────────────────────────────────────────

/// Required argument on any lossy serialization path.
/// The caller must write the literal case name at the call site — it cannot
/// be stored in a variable, passed through a flag, or hidden behind a helper.
/// This makes loss explicit and traceable in code review.
type ExportLossAcknowledged = | IAcceptDataLoss


// ─── Error types ─────────────────────────────────────────────────────────────

type ParseError =
    | InvalidJson              of message: string
    | MissingRequiredField     of path: string * field: string
    | UnknownTokenType         of path: string * value: string
    | InvalidReference         of path: string * ref: string
    | InvalidValue             of path: string * message: string
    | UnknownSpecVersion       of version: string
    | SchemaVersionContradicts of schemaUrl: string * structuralVersion: SpecVersion

type ValidationError =
    | CircularReference   of cycle: string list
    | UnresolvedReference of path: string * ref: string
    | TypeMismatch        of path: string * expected: string * actual: string
    | ConstraintViolation of path: string * message: string

type ResolveError =
    | UnknownModifier         of name: string
    | UnknownContext          of modifier: string * context: string
    | MissingRequiredModifier of name: string
    | CircularSetReference    of name: string
    | ParseError              of ParseError
    | ValidationError         of ValidationError

type LoadError =
    | ParseError      of ParseError
    | ValidationError of ValidationError

type ImportError =
    | ParseFailed      of ParseError list
    | ValidationFailed of ValidationError list
    | ResolveFailed    of ResolveError list

type SerializeError =
    | UnsupportedInTargetVersion of path: string * tokenType: string * version: SpecVersion
    | UnsupportedColorSpace      of path: string * colorSpace: string * version: SpecVersion

/// Non-fatal warnings produced by post-resolve extension-evaluation passes
/// (see <c>Api.evaluateMathExtensions</c> — ADR-034). The associated token
/// keeps its stale <c>$value</c> when evaluation fails; the warning records
/// what happened so the author can fix the expression.
type ExtensionEvaluationWarning =
    | MathExpressionFailed of path: string * expression: string * reason: string


// ─── Formatters ──────────────────────────────────────────────────────────────
// One line per error: "path: message". Callers add prefix, indent, color.

module ParseError =

    let format (e: ParseError) : string =
        match e with
        | InvalidJson msg ->
            sprintf "invalid JSON: %s" msg
        | MissingRequiredField (path, field) ->
            sprintf "%s: missing required field '%s'" path field
        | UnknownTokenType (path, value) ->
            sprintf "%s: unknown token type '%s'" path value
        | InvalidReference (path, ref) ->
            sprintf "%s: invalid reference '%s'" path ref
        | InvalidValue (path, msg) ->
            sprintf "%s: %s" path msg
        | UnknownSpecVersion version ->
            sprintf "unknown spec version '%s'" version
        | SchemaVersionContradicts (url, structural) ->
            sprintf "$schema URL '%s' contradicts structural version %s"
                url (SpecVersion.display structural)

module ValidationError =

    let format (e: ValidationError) : string =
        match e with
        | CircularReference cycle ->
            sprintf "circular reference: %s" (String.concat " -> " cycle)
        | UnresolvedReference (path, ref) ->
            sprintf "%s: unresolved reference '%s'" path ref
        | TypeMismatch (path, expected, actual) ->
            sprintf "%s: type mismatch — expected %s, got %s" path expected actual
        | ConstraintViolation (path, msg) ->
            sprintf "%s: %s" path msg

module ResolveError =

    let rec format (e: ResolveError) : string =
        match e with
        | UnknownModifier name ->
            sprintf "unknown modifier '%s'" name
        | UnknownContext (modifier, context) ->
            sprintf "modifier '%s' has no context '%s'" modifier context
        | MissingRequiredModifier name ->
            sprintf "missing required modifier '%s' (no default)" name
        | CircularSetReference name ->
            sprintf "circular set reference at '%s'" name
        | ResolveError.ParseError pe ->
            ParseError.format pe
        | ResolveError.ValidationError ve ->
            ValidationError.format ve

module LoadError =

    let format (e: LoadError) : string =
        match e with
        | LoadError.ParseError pe      -> ParseError.format pe
        | LoadError.ValidationError ve -> ValidationError.format ve

module ImportError =

    let format (e: ImportError) : string =
        match e with
        | ParseFailed errs ->
            errs
            |> List.map (fun pe -> sprintf "[parse] %s" (ParseError.format pe))
            |> String.concat "\n"
        | ValidationFailed errs ->
            errs
            |> List.map (fun ve -> sprintf "[validation] %s" (ValidationError.format ve))
            |> String.concat "\n"
        | ResolveFailed errs ->
            errs
            |> List.map (fun re -> sprintf "[resolve] %s" (ResolveError.format re))
            |> String.concat "\n"

module SerializeError =

    let format (e: SerializeError) : string =
        match e with
        | UnsupportedInTargetVersion (path, tokenType, version) ->
            sprintf "%s: token type '%s' is not supported in %s"
                path tokenType (SpecVersion.display version)
        | UnsupportedColorSpace (path, colorSpace, version) ->
            sprintf "%s: color space '%s' is not supported in %s"
                path colorSpace (SpecVersion.display version)

module ExtensionEvaluationWarning =

    let format (w: ExtensionEvaluationWarning) : string =
        match w with
        | MathExpressionFailed (path, expr, reason) ->
            sprintf "%s: math expression '%s' failed — %s (kept stale $value)"
                path expr reason
