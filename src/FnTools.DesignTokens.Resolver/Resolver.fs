module FnTools.DesignTokens.Resolver

open System
open System.Text.Json.Nodes
open FnTools.DesignTokens
open FnTools.DesignTokens.Json


// ─── Helpers ─────────────────────────────────────────────────────────────────

let private liftSingle (r: Result<'a, ParseError>) : Result<'a, ParseError list> =
    match r with Ok v -> Ok v | Error e -> Error [e]

let private (>>=) (r: Result<'a, 'e list>) (f: 'a -> Result<'b, 'e list>) : Result<'b, 'e list> =
    match r with Ok v -> f v | Error es -> Error es

let private collect (xs: Result<'a, 'e list> list) : Result<'a list, 'e list> =
    let oks = ResizeArray()
    let errs = ResizeArray()
    for x in xs do
        match x with Ok v -> oks.Add v | Error es -> errs.AddRange es
    if errs.Count > 0 then Error (List.ofSeq errs) else Ok (List.ofSeq oks)


// ─── JSON Pointer resolution (RFC 6901, same-document "#/..." form) ──────────

/// Resolve a same-document JSON Pointer against the root object of the resolver document.
/// Handles reference-token escaping (~0 → ~, ~1 → /).
let private resolveJsonPointer (root: JsonObject) (ref_: string) : Result<JsonNode, ParseError list> =
    if ref_ = "#" then Ok (root :> JsonNode)
    elif ref_.StartsWith "#/" then
        let segments =
            ref_.[2..].Split '/'
            |> Array.map (fun s -> s.Replace("~1", "/").Replace("~0", "~"))
        let mutable current : JsonNode = root
        let mutable err : ParseError list option = None
        for seg in segments do
            if err.IsNone then
                match current with
                | :? JsonObject as obj ->
                    match tryGetProperty seg obj with
                    | Some node -> current <- node
                    | None ->
                        err <- Some [InvalidValue ("$ref", sprintf "segment '%s' not found (in %s)" seg ref_)]
                | :? JsonArray as arr ->
                    match Int32.TryParse seg with
                    | true, i when i >= 0 && i < arr.Count ->
                        match Option.ofObj arr.[i] with
                        | Some n -> current <- n
                        | None ->
                            err <- Some [InvalidValue ("$ref", sprintf "segment '%s' resolved to null (in %s)" seg ref_)]
                    | _ ->
                        err <- Some [InvalidValue ("$ref", sprintf "segment '%s' is not a valid array index (in %s)" seg ref_)]
                | _ ->
                    err <- Some [InvalidValue ("$ref", sprintf "cannot descend into non-object at '%s' (in %s)" seg ref_)]
        match err with
        | Some e -> Error e
        | None   -> Ok current
    else
        Error [InvalidValue ("$ref", sprintf "only same-document '#/...' pointers are supported; got '%s'" ref_)]


// ─── Parse TokenSource ───────────────────────────────────────────────────────

let rec private parseTokenSource (root: JsonObject) (path: string) (n: JsonNode) : Result<TokenSource, ParseError list> =
    match asObject n with
    | Some o ->
        match tryGetProperty "$ref" o with
        | Some refNode ->
            requireString path "$ref" refNode |> liftSingle >>= fun ref_ ->
                resolveJsonPointer root ref_ >>= parseTokenSource root path
        | None ->
            match tryGetProperty "path" o with
            | Some pNode ->
                requireString path "path" pNode |> liftSingle |> Result.map FileRef
            | None ->
                match tryGetProperty "inline" o with
                | Some inline_ ->
                    let raw = inline_.ToJsonString()
                    Format.parse raw |> Result.map Inline
                | None ->
                    Error [InvalidValue (path, "source must have '$ref', 'path', or 'inline'")]
    | None ->
        match tryGetString n with
        | Some s -> Ok (FileRef s)
        | None   -> Error [InvalidValue (path, "source must be a string or object")]

let private parseSources (root: JsonObject) (path: string) (n: JsonNode) : Result<TokenSource list, ParseError list> =
    match asArray n with
    | Some arr ->
        [ for i, item in elements arr ->
            parseTokenSource root (sprintf "%s[%d]" path i) item ]
        |> collect
    | None ->
        parseTokenSource root path n |> Result.map List.singleton


// ─── Parse Set ───────────────────────────────────────────────────────────────

let private parseSetDefinition (root: JsonObject) (path: string) (n: JsonNode) : Result<SetDefinition, ParseError list> =
    requireObject path "set" n |> liftSingle >>= fun o ->
        let sourcesR =
            requireProperty path "sources" o |> liftSingle
            >>= parseSources root (path + ".sources")
        let descR =
            match tryGetProperty "description" o with
            | Some node -> requireString path "description" node |> liftSingle |> Result.map Some
            | None -> Ok None
        let extR =
            match tryGetProperty "$extensions" o with
            | Some node ->
                match asObject node with
                | Some eo -> Ok (readExtensions eo)
                | None    -> Error [InvalidValue (path, "$extensions must be an object")]
            | None -> Ok []
        match sourcesR, descR, extR with
        | Ok s, Ok d, Ok e -> Ok { Sources = s; Description = d; Extensions = e }
        | _ ->
            [ sourcesR |> Result.map ignore; descR |> Result.map ignore; extR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error


// ─── Parse Modifier ──────────────────────────────────────────────────────────

let private parseModifierContext (root: JsonObject) (path: string) (n: JsonNode) : Result<ModifierContext, ParseError list> =
    requireObject path "context" n |> liftSingle >>= fun o ->
        let sourcesR =
            requireProperty path "sources" o |> liftSingle
            >>= parseSources root (path + ".sources")
        sourcesR |> Result.map (fun s -> { Sources = s })

let private parseModifierDefinition (root: JsonObject) (path: string) (n: JsonNode) : Result<ModifierDefinition, ParseError list> =
    requireObject path "modifier" n |> liftSingle >>= fun o ->
        let contextsR =
            match tryGetProperty "contexts" o with
            | Some node ->
                requireObject path "contexts" node |> liftSingle >>= fun co ->
                    let parsed =
                        properties co
                        |> Seq.map (fun (k, v) ->
                            parseModifierContext root (sprintf "%s.contexts.%s" path k) v
                            |> Result.map (fun ctx -> k, ctx))
                        |> List.ofSeq
                    collect parsed
            | None -> Error [MissingRequiredField (path, "contexts")]
        let defaultR =
            match tryGetProperty "default" o with
            | Some node -> requireString path "default" node |> liftSingle |> Result.map Some
            | None -> Ok None
        let descR =
            match tryGetProperty "description" o with
            | Some node -> requireString path "description" node |> liftSingle |> Result.map Some
            | None -> Ok None
        let extR =
            match tryGetProperty "$extensions" o with
            | Some node ->
                match asObject node with
                | Some eo -> Ok (readExtensions eo)
                | None    -> Error [InvalidValue (path, "$extensions must be an object")]
            | None -> Ok []
        match contextsR, defaultR, descR, extR with
        | Ok c, Ok dflt, Ok d, Ok e ->
            Ok { Contexts = c; Default = dflt; Description = d; Extensions = e }
        | _ ->
            [ contextsR |> Result.map ignore; defaultR |> Result.map ignore
              descR |> Result.map ignore; extR |> Result.map ignore ]
            |> List.collect (function Error es -> es | Ok _ -> [])
            |> Error


// ─── Parse ResolutionItem ────────────────────────────────────────────────────

let private parseResolutionItem (path: string) (n: JsonNode) : Result<ResolutionItem, ParseError list> =
    match tryGetString n with
    | Some s -> Ok (SetRef s)
    | None ->
        requireObject path "resolutionItem" n |> liftSingle >>= fun o ->
            let modR =
                match tryGetProperty "modifier" o with
                | Some node -> requireString path "modifier" node |> liftSingle |> Result.map Some
                | None -> Ok None
            let setR =
                match tryGetProperty "set" o with
                | Some node -> requireString path "set" node |> liftSingle |> Result.map Some
                | None -> Ok None
            let ctxR =
                match tryGetProperty "context" o with
                | Some node -> requireString path "context" node |> liftSingle |> Result.map Some
                | None -> Ok None
            match modR, setR, ctxR with
            | Ok (Some m), _, Ok (Some c) -> Ok (ModifierRef (m, c))
            | Ok (Some m), _, Ok None     -> Ok (ModifierRef (m, ""))
            | _, Ok (Some s), _           -> Ok (SetRef s)
            | _ ->
                Error [InvalidValue (path, "resolutionItem must specify 'set' or 'modifier'+'context'")]


// ─── parseResolver ───────────────────────────────────────────────────────────

let parseResolver (jsonText: string) : Result<ResolverDocument, ParseError list> =
    Json.parseRoot jsonText |> liftSingle >>= fun root ->
        match asObject root with
        | None -> Error [InvalidJson "root must be an object"]
        | Some o ->
            let nameR =
                match tryGetProperty "name" o with
                | Some node -> requireString "name" "name" node |> liftSingle |> Result.map Some
                | None -> Ok None

            let versionR : Result<SpecVersion, ParseError list> =
                match tryGetProperty "version" o with
                | Some node ->
                    requireString "version" "version" node |> liftSingle
                    >>= fun s ->
                        match s with
                        | "2025.10" -> Ok V2025_10
                        | other -> Error [UnknownSpecVersion other]
                | None -> Ok V2025_10

            let descR =
                match tryGetProperty "description" o with
                | Some node -> requireString "description" "description" node |> liftSingle |> Result.map Some
                | None -> Ok None

            let setsR =
                match tryGetProperty "sets" o with
                | Some node ->
                    requireObject "sets" "sets" node |> liftSingle >>= fun so ->
                        properties so
                        |> Seq.map (fun (k, v) ->
                            parseSetDefinition o ("sets." + k) v
                            |> Result.map (fun d -> k, d))
                        |> List.ofSeq
                        |> collect
                | None -> Ok []

            let modifiersR =
                match tryGetProperty "modifiers" o with
                | Some node ->
                    requireObject "modifiers" "modifiers" node |> liftSingle >>= fun mo ->
                        properties mo
                        |> Seq.map (fun (k, v) ->
                            parseModifierDefinition o ("modifiers." + k) v
                            |> Result.map (fun d -> k, d))
                        |> List.ofSeq
                        |> collect
                | None -> Ok []

            let orderR =
                match tryGetProperty "resolutionOrder" o with
                | Some node ->
                    requireArray "resolutionOrder" "resolutionOrder" node |> liftSingle >>= fun arr ->
                        [ for i, item in elements arr ->
                            parseResolutionItem (sprintf "resolutionOrder[%d]" i) item ]
                        |> collect
                | None -> Error [MissingRequiredField ("", "resolutionOrder")]

            match nameR, versionR, descR, setsR, modifiersR, orderR with
            | Ok n, Ok v, Ok d, Ok s, Ok m, Ok o' ->
                Ok { Name = n; Version = v; Description = d; Sets = s; Modifiers = m; ResolutionOrder = o' }
            | _ ->
                [ nameR |> Result.map ignore; versionR |> Result.map ignore
                  descR |> Result.map ignore; setsR |> Result.map ignore
                  modifiersR |> Result.map ignore; orderR |> Result.map ignore ]
                |> List.collect (function Error es -> es | Ok _ -> [])
                |> Error


// ─── Resolution algorithm ───────────────────────────────────────────────────

/// Validate ResolverInput against the document's modifier set.
let private validateInput (input: ResolverInput) (doc: ResolverDocument) : Result<unit, ResolveError list> =
    let errors = ResizeArray<ResolveError>()

    // Every input key must name a known modifier
    for KeyValue(k, v) in input do
        match doc.Modifiers |> List.tryFind (fun (n, _) -> n = k) with
        | None -> errors.Add (UnknownModifier k)
        | Some (_, m) ->
            if not (m.Contexts |> List.exists (fun (cn, _) -> cn = v)) then
                errors.Add (UnknownContext (k, v))

    // Every required modifier (no default) must be present in input
    for (name, m) in doc.Modifiers do
        if m.Default.IsNone && not (input.ContainsKey name) then
            errors.Add (MissingRequiredModifier name)

    if errors.Count = 0 then Ok ()
    else Error (List.ofSeq errors)

/// Pick the active context for a modifier given input + defaults.
let private contextFor (input: ResolverInput) (m: ModifierDefinition) (name: string) : string option =
    match input.TryFind name with
    | Some v -> Some v
    | None -> m.Default

/// Resolve a TokenSource to a TokenFile.
let private loadSource
    (loadFile: string -> Result<string, string>)
    (src: TokenSource)
    : Result<TokenFile, ResolveError list> =
    match src with
    | Inline f -> Ok f
    | FileRef path ->
        match loadFile path with
        | Error msg ->
            Error [ResolveError.ParseError (InvalidJson (sprintf "load failed for '%s': %s" path msg))]
        | Ok content ->
            match Format.parse content with
            | Ok f -> Ok f
            | Error es -> Error (es |> List.map ResolveError.ParseError)

/// Walk resolutionOrder, collecting TokenSource list.
let private collectSources
    (input: ResolverInput)
    (doc: ResolverDocument)
    : Result<TokenSource list, ResolveError list> =
    let errors = ResizeArray<ResolveError>()
    let sources = ResizeArray<TokenSource>()
    for item in doc.ResolutionOrder do
        match item with
        | SetRef name ->
            match doc.Sets |> List.tryFind (fun (n, _) -> n = name) with
            | Some (_, s) -> sources.AddRange s.Sources
            | None -> errors.Add (UnknownModifier name)  // misnomer, but represents unknown reference
        | ModifierRef (modName, ctxName) ->
            match doc.Modifiers |> List.tryFind (fun (n, _) -> n = modName) with
            | None -> errors.Add (UnknownModifier modName)
            | Some (_, m) ->
                let chosenCtx =
                    if String.IsNullOrEmpty ctxName then contextFor input m modName
                    else Some ctxName
                match chosenCtx with
                | None -> errors.Add (MissingRequiredModifier modName)
                | Some cname ->
                    match m.Contexts |> List.tryFind (fun (n, _) -> n = cname) with
                    | Some (_, ctx) -> sources.AddRange ctx.Sources
                    | None -> errors.Add (UnknownContext (modName, cname))

    if errors.Count > 0 then Error (List.ofSeq errors)
    else Ok (List.ofSeq sources)

/// Deep-merge two TokenNode trees: later wins on leaf conflict; group/group merges children;
/// type mismatch (group vs leaf at the same path) → ValidationError.
let rec private mergeNode
    (path: string)
    (a: TokenNode)
    (b: TokenNode)
    : Result<TokenNode, ResolveError list> =
    match a, b with
    | TokenLeaf _, TokenLeaf _ -> Ok b
    | Group ga, Group gb ->
        // Merge children by name (preserve order from a, append new keys from b)
        let mergeChildren =
            let aMap =
                ga.Children
                |> List.map (fun (n, x) -> TokenName.value n, (n, x))
                |> Map.ofList
            let bMap =
                gb.Children
                |> List.map (fun (n, x) -> TokenName.value n, (n, x))
                |> Map.ofList
            let aKeys = ga.Children |> List.map (fun (n, _) -> TokenName.value n)
            let bKeys = gb.Children |> List.map (fun (n, _) -> TokenName.value n)
            let allKeys =
                aKeys @ (bKeys |> List.filter (fun k -> not (List.contains k aKeys)))
            let merged =
                allKeys
                |> List.map (fun k ->
                    let childPath = if path = "" then k else path + "." + k
                    match Map.tryFind k aMap, Map.tryFind k bMap with
                    | Some (n, av), Some (_, bv) ->
                        mergeNode childPath av bv |> Result.map (fun mn -> n, mn)
                    | Some (n, av), None -> Ok (n, av)
                    | None, Some (n, bv) -> Ok (n, bv)
                    | None, None -> failwith "unreachable")
            collect merged
        mergeChildren |> Result.map (fun children ->
            Group {
                Type = gb.Type |> Option.orElse ga.Type
                Metadata = gb.Metadata
                Root = gb.Root |> Option.orElse ga.Root
                Extends = gb.Extends |> Option.orElse ga.Extends
                Children = children
            })
    | _ ->
        Error [ResolveError.ValidationError
                  (TypeMismatch (path, "matching node kind", "group/leaf mismatch"))]

let private mergeFiles (files: TokenFile list) : Result<TokenFile, ResolveError list> =
    match files with
    | [] -> Ok { Version = V2025_10; Schema = None; Children = [] }
    | first :: rest ->
        let mergeOne (acc: Result<TokenFile, ResolveError list>) (f: TokenFile) =
            acc >>= fun cur ->
                let aMap =
                    cur.Children
                    |> List.map (fun (n, x) -> TokenName.value n, (n, x))
                    |> Map.ofList
                let bMap =
                    f.Children
                    |> List.map (fun (n, x) -> TokenName.value n, (n, x))
                    |> Map.ofList
                let aKeys = cur.Children |> List.map (fun (n, _) -> TokenName.value n)
                let bKeys = f.Children |> List.map (fun (n, _) -> TokenName.value n)
                let allKeys =
                    aKeys @ (bKeys |> List.filter (fun k -> not (List.contains k aKeys)))
                let merged =
                    allKeys
                    |> List.map (fun k ->
                        match Map.tryFind k aMap, Map.tryFind k bMap with
                        | Some (n, av), Some (_, bv) ->
                            mergeNode k av bv |> Result.map (fun mn -> n, mn)
                        | Some (n, av), None -> Ok (n, av)
                        | None, Some (n, bv) -> Ok (n, bv)
                        | None, None -> failwith "unreachable")
                collect merged
                |> Result.map (fun children ->
                    { Version = V2025_10
                      Schema = f.Schema |> Option.orElse cur.Schema
                      Children = children })
        rest |> List.fold mergeOne (Ok first)

let resolve
    (loadFile: string -> Result<string, string>)
    (input: ResolverInput)
    (doc: ResolverDocument)
    : Result<TokenFile, ResolveError list> =
    validateInput input doc >>= fun () ->
        collectSources input doc >>= fun sources ->
            sources
            |> List.map (loadSource loadFile)
            |> collect
            >>= mergeFiles


// ─── Alias resolution (TokenFile → fully-flat literal-only TokenFile) ───────

/// Walk the file, replacing every TokenValue.Alias with the value it points to.
/// Cycles → CircularReference. Unresolved targets → UnresolvedReference.
let private flattenAliases (file: TokenFile) : Result<TokenFile, ResolveError list> =
    // Build a lookup: full dot-path → Token
    let lookup = System.Collections.Generic.Dictionary<string, Token * string>()

    let rec index (path: string list) (node: TokenNode) =
        match node with
        | TokenLeaf t ->
            lookup.[String.concat "." path] <- (t, String.concat "." path)
        | Group g ->
            for (name, child) in g.Children do
                index (path @ [TokenName.value name]) child

    for (name, node) in file.Children do
        index [TokenName.value name] node

    let visiting = System.Collections.Generic.HashSet<string>()

    let rec resolveAt (origin: string) (path: string) : Result<TokenValue, ResolveError list> =
        if visiting.Contains path then
            Error [ResolveError.ValidationError (CircularReference [origin; path])]
        else
            match lookup.TryGetValue path with
            | false, _ -> Error [ResolveError.ValidationError (UnresolvedReference (origin, path))]
            | true, (token, _) ->
                match token.Value with
                | TokenValue.Alias (CurlyBrace target) ->
                    visiting.Add path |> ignore
                    let r = resolveAt origin (String.concat "." target)
                    visiting.Remove path |> ignore
                    r
                | TokenValue.Alias (JsonPointer ptr) ->
                    Error [ResolveError.ValidationError (UnresolvedReference (origin, ptr))]
                | v -> Ok v

    let rec walk (path: string list) (node: TokenNode) : Result<TokenNode, ResolveError list> =
        match node with
        | TokenLeaf t ->
            let pathStr = String.concat "." path
            match t.Value with
            | TokenValue.Alias (CurlyBrace target) ->
                visiting.Clear()
                visiting.Add pathStr |> ignore
                let r = resolveAt pathStr (String.concat "." target)
                visiting.Clear()
                r |> Result.map (fun v -> TokenLeaf { t with Value = v })
            | _ -> Ok (TokenLeaf t)
        | Group g ->
            let childResults =
                g.Children
                |> List.map (fun (name, child) ->
                    walk (path @ [TokenName.value name]) child
                    |> Result.map (fun mn -> name, mn))
            collect childResults |> Result.map (fun ch -> Group { g with Children = ch })

    let rootResults =
        file.Children
        |> List.map (fun (name, node) ->
            walk [TokenName.value name] node
            |> Result.map (fun mn -> name, mn))
    collect rootResults |> Result.map (fun ch -> { file with Children = ch })

let resolveAll
    (loadFile: string -> Result<string, string>)
    (input: ResolverInput)
    (doc: ResolverDocument)
    : Result<TokenFile, ResolveError list> =
    resolve loadFile input doc >>= flattenAliases
