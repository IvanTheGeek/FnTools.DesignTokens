// phase3-detail.fsx — raw vs shimmed examples + overlap analysis
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Foundation/bin/Debug/net10.0/FnTools.DesignTokens.Foundation.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Format/bin/Debug/net10.0/FnTools.DesignTokens.Format.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.TokensStudio/bin/Debug/net10.0/FnTools.DesignTokens.TokensStudio.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens/bin/Debug/net10.0/FnTools.DesignTokens.dll"

open System.IO
open FnTools.DesignTokens

let lauraJson = File.ReadAllText "samples/laura-system-library.tokens.json"
let opts = System.Text.Json.JsonSerializerOptions(WriteIndented = true)
let raw = System.Text.Json.Nodes.JsonNode.Parse(lauraJson) :?> System.Text.Json.Nodes.JsonObject

printfn "=== RAW Foundations/Base ==="
printfn "%s" (raw.["Foundations/Base"].ToJsonString(opts))

let shimResult = TokensStudio.shimSingleFile ShimConfig.defaults lauraJson |> function Ok r -> r | Error e -> failwith e

printfn "\n=== SHIMMED Foundations/Base ==="
printfn "%s" shimResult.Sets.["Foundations/Base"]

printfn "\n=== RAW Typography (sample — fontSizes/fontFamilies) ==="
printfn "%s" (raw.["Typography"].ToJsonString(opts) |> fun s -> s.[..2000])

printfn "\n=== SHIMMED Typography ==="
printfn "%s" shimResult.Sets.["Typography"]

// Overlap analysis: which paths appear in >1 set?
printfn "\n=== PATH OVERLAP ANALYSIS ==="
let pathsPerSet =
    shimResult.Metadata.TokenSetOrder
    |> List.choose (fun name ->
        shimResult.Sets |> Map.tryFind name
        |> Option.bind (fun j ->
            match Api.Primitives.parse j with
            | Ok f ->
                let paths = Api.Primitives.flatten f |> Seq.map (fun (p, _) -> String.concat "." p) |> Set.ofSeq
                Some (name, paths)
            | Error _ -> None))

let allPaths =
    pathsPerSet |> List.collect (fun (_, ps) -> Set.toList ps)
    |> List.countBy id
    |> List.filter (fun (_, n) -> n > 1)
    |> List.sortByDescending snd

printfn "Paths defined in >1 set: %d" (List.length allPaths)
for (path, count) in allPaths |> List.truncate 10 do
    let setNames = pathsPerSet |> List.choose (fun (n, ps) -> if ps.Contains path then Some n else None)
    printfn "  %-40s  in %d sets: %s" path count (String.concat ", " setNames)
printfn "  ... (showing first 10)"

printfn "\n=== UNIQUE PATHS AFTER LAST-SET-WINS ==="
let uniquePaths =
    shimResult.Metadata.TokenSetOrder
    |> List.choose (fun name ->
        shimResult.Sets |> Map.tryFind name
        |> Option.bind (fun j ->
            match Api.Primitives.parse j with
            | Ok f -> Some (Api.Primitives.flatten f |> Seq.map (fun (p, _) -> String.concat "." p) |> Seq.toList)
            | Error _ -> None))
    |> List.concat
    |> List.distinct
printfn "Total per-set (with duplicates): 305"
printfn "After dedup (last-set-wins):     %d" (List.length uniquePaths)
printfn "Shadowed (removed by later set): %d" (305 - List.length uniquePaths)
