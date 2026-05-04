module FnTools.DesignTokens.Json

open System
open System.Text.Json
open System.Text.Json.Nodes
open FnTools.DesignTokens


// ─── Type discrimination ─────────────────────────────────────────────────────

let isNull (n: JsonNode) : bool = isNull (box n)

let asObject (n: JsonNode) : JsonObject option =
    match n with
    | :? JsonObject as o -> Some o
    | _ -> None

let asArray (n: JsonNode) : JsonArray option =
    match n with
    | :? JsonArray as a -> Some a
    | _ -> None

let asValue (n: JsonNode) : JsonValue option =
    match n with
    | :? JsonValue as v -> Some v
    | _ -> None


// ─── Value extraction (try variants — return Option) ───────────────────────

let tryGetString (n: JsonNode) : string option =
    match asValue n with
    | Some v ->
        let mutable s = ""
        if v.TryGetValue<string>(&s) then Some s else None
    | None -> None

let tryGetFloat (n: JsonNode) : float option =
    match asValue n with
    | Some v ->
        let mutable d = 0.0
        if v.TryGetValue<float>(&d) then Some d else None
    | None -> None

let tryGetInt (n: JsonNode) : int option =
    match asValue n with
    | Some v ->
        let mutable i = 0
        if v.TryGetValue<int>(&i) then Some i else None
    | None -> None

let tryGetBool (n: JsonNode) : bool option =
    match asValue n with
    | Some v ->
        let mutable b = false
        if v.TryGetValue<bool>(&b) then Some b else None
    | None -> None


// ─── Property access (returns None when property missing OR null) ────────────

let tryGetProperty (name: string) (obj: JsonObject) : JsonNode option =
    let mutable node : JsonNode | null = null
    if obj.TryGetPropertyValue(name, &node) then
        match node with
        | null   -> None
        | nonNul -> Some nonNul
    else None


// ─── Required variants — return Result<_, ParseError> ───────────────────────

let requireString (path: string) (label: string) (n: JsonNode) : Result<string, ParseError> =
    match tryGetString n with
    | Some s -> Ok s
    | None   -> Error (InvalidValue (path, sprintf "expected string for '%s'" label))

let requireFloat (path: string) (label: string) (n: JsonNode) : Result<float, ParseError> =
    match tryGetFloat n with
    | Some f -> Ok f
    | None   -> Error (InvalidValue (path, sprintf "expected number for '%s'" label))

let requireBool (path: string) (label: string) (n: JsonNode) : Result<bool, ParseError> =
    match tryGetBool n with
    | Some b -> Ok b
    | None   -> Error (InvalidValue (path, sprintf "expected bool for '%s'" label))

let requireObject (path: string) (label: string) (n: JsonNode) : Result<JsonObject, ParseError> =
    match asObject n with
    | Some o -> Ok o
    | None   -> Error (InvalidValue (path, sprintf "expected object for '%s'" label))

let requireArray (path: string) (label: string) (n: JsonNode) : Result<JsonArray, ParseError> =
    match asArray n with
    | Some a -> Ok a
    | None   -> Error (InvalidValue (path, sprintf "expected array for '%s'" label))

let requireProperty (path: string) (name: string) (obj: JsonObject) : Result<JsonNode, ParseError> =
    match tryGetProperty name obj with
    | Some n -> Ok n
    | None   -> Error (MissingRequiredField (path, name))


// ─── Token name + reference parsing ──────────────────────────────────────────

let readTokenName (path: string) (raw: string) : Result<TokenName, ParseError> =
    match TokenName.tryCreate raw with
    | Ok name -> Ok name
    | Error msg -> Error (InvalidValue (path, msg))

/// Parse a curly-brace reference like "{foo.bar.baz}" → CurlyBrace ["foo"; "bar"; "baz"].
let private parseCurlyBrace (path: string) (raw: string) : Result<TokenRef, ParseError> =
    if raw.Length < 2 || not (raw.StartsWith "{") || not (raw.EndsWith "}") then
        Error (InvalidReference (path, raw))
    else
        let inner = raw.Substring(1, raw.Length - 2)
        if String.IsNullOrEmpty inner then
            Error (InvalidReference (path, raw))
        else
            Ok (CurlyBrace (inner.Split '.' |> List.ofArray))

/// Read a reference value. Accepts:
///   - string "{a.b.c}" → CurlyBrace
///   - object { "$ref": "/foo/bar" } → JsonPointer
let readRef (path: string) (n: JsonNode) : Result<TokenRef, ParseError> =
    match tryGetString n with
    | Some s -> parseCurlyBrace path s
    | None ->
        match asObject n with
        | Some o ->
            match tryGetProperty "$ref" o with
            | Some refNode ->
                match tryGetString refNode with
                | Some pointer -> Ok (JsonPointer pointer)
                | None -> Error (InvalidValue (path, "$ref must be a string"))
            | None -> Error (InvalidReference (path, n.ToJsonString()))
        | None ->
            Error (InvalidReference (path, n.ToJsonString()))

/// True if a JsonNode looks like a reference (string starting with '{' or object with $ref).
let isRefShape (n: JsonNode) : bool =
    match tryGetString n with
    | Some s -> s.StartsWith "{" && s.EndsWith "}"
    | None ->
        match asObject n with
        | Some o -> tryGetProperty "$ref" o |> Option.isSome
        | None -> false


// ─── Extensions ──────────────────────────────────────────────────────────────

/// Copy all properties of an `$extensions` object into an order-preserving list.
/// Each value is detached via DeepClone so it survives independently of the source document.
/// Properties whose JSON value is literal `null` are skipped — `Extensions` values are
/// non-nullable by construction, and a null payload carries no extension data anyway.
let readExtensions (obj: JsonObject) : Extensions =
    [ for kv in obj do
        match Option.ofObj kv.Value with
        | None -> ()
        | Some nonNul -> yield kv.Key, nonNul.DeepClone() ]


// ─── Object iteration helpers ────────────────────────────────────────────────

/// Iterate properties of a JsonObject as (name, node) pairs in insertion order.
/// JSON `null` values are skipped — callers expect a non-nullable JsonNode.
let properties (obj: JsonObject) : (string * JsonNode) seq =
    seq { for kv in obj do
            match Option.ofObj kv.Value with
            | None       -> ()
            | Some value -> yield kv.Key, value }

/// Iterate elements of a JsonArray with their indices.
/// JSON `null` elements are skipped — callers expect a non-nullable JsonNode.
let elements (arr: JsonArray) : (int * JsonNode) seq =
    seq { for i in 0 .. arr.Count - 1 do
            match Option.ofObj arr.[i] with
            | None       -> ()
            | Some value -> yield i, value }


// ─── Top-level parse ─────────────────────────────────────────────────────────

let parseRoot (json: string) : Result<JsonNode, ParseError> =
    try
        let opts = JsonNodeOptions(PropertyNameCaseInsensitive = false)
        let docOpts = JsonDocumentOptions(CommentHandling = JsonCommentHandling.Skip,
                                          AllowTrailingCommas = false)
        match JsonNode.Parse(json, opts, docOpts) with
        | null -> Error (InvalidJson "root is null")
        | node -> Ok node
    with
    | :? JsonException as ex -> Error (InvalidJson ex.Message)
