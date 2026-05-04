// resolve-laura-tokens.fsx
// Resolves Laura's System Library tokens for Light + Desktop + Core + 100% themes
// and emits a Penpot-ready token file for the Dashboard's semantic token paths.
//
// Usage: dotnet fsi scripts/resolve-laura-tokens.fsx

#r "nuget: System.Text.Json"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Foundation/bin/Debug/net10.0/FnTools.DesignTokens.Foundation.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Format/bin/Debug/net10.0/FnTools.DesignTokens.Format.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Validation/bin/Debug/net10.0/FnTools.DesignTokens.Validation.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.Resolver/bin/Debug/net10.0/FnTools.DesignTokens.Resolver.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens.TokensStudio/bin/Debug/net10.0/FnTools.DesignTokens.TokensStudio.dll"
#r "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/src/FnTools.DesignTokens/bin/Debug/net10.0/FnTools.DesignTokens.dll"

open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open FnTools.DesignTokens

let lauraJson = File.ReadAllText "/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens/samples/laura-system-library.tokens.json"

// Pass all mutually-exclusive theme variants so they don't bleed into the base.
// We want: Always-on base + Light + Core + Desktop + 100%.
// We also pass Dark/NeonBooks/EcoTools/Mobile/Tablet/150%/200% so they stay OUT of the base
// and are isolated into their own theme buckets (never mixed into Light's resolution).
let allThemes = [
    "Light"; "Dark"
    "Core"; "NeonBooks"; "Eco Tools"
    "Mobile"; "Tablet"; "Desktop"
    "100%"; "150%"; "200%"
]

let result =
    match Api.importTokensStudioThemed ShimConfig.defaults allThemes lauraJson with
    | Error es -> failwithf "Import failed: %A" es
    | Ok r -> r

printfn "Base tokens: %d" (List.length result.BaseTokens)
printfn "Themes resolved: %A" (result.Themes |> List.map (fun t -> t.ThemeName, List.length t.Tokens))
printfn "Warnings: %d" (List.length result.Warnings)

// The base now has only Always-on sets (no Dark or breakpoint bleed).
// The "Light" theme resolution = base + Light Core/Accent/Component.
// The "Desktop" theme resolution = base + Breakpoints/Desktop.
// The "Core" theme resolution = base + Brand/Core.
//
// To get Light + Core + Desktop + 100% combined:
// Start from the "Light" theme map; overlay Desktop breakpoint tokens (breakpoint, spacing, radius, size);
// overlay Core brand (color palette hue is in the base already for brand/Core sets);
// overlay 100% zoom.
//
// In practice: take Light theme tokens as the canonical resolved set for color + typography,
// and replace spacing/radius/size/breakpoint with Desktop theme values.

let themeMap name =
    result.Themes
    |> List.tryFind (fun t -> t.ThemeName = name)
    |> Option.map (fun t -> t.Tokens |> List.map (fun (path, token) -> String.concat "." path, token) |> Map.ofList)
    |> Option.defaultValue Map.empty

let lightTokens   = themeMap "Light"
let coreTokens    = themeMap "Core"
let desktopTokens = themeMap "Desktop"
let zoom100Tokens = themeMap "100%"

// Merge: base < core < light < desktop < 100%
// Each successive map overrides the previous for the same key.
let merge maps =
    List.fold (fun acc m -> Map.fold (fun a k v -> Map.add k v a) acc m) Map.empty maps

let allTokens = merge [ coreTokens; lightTokens; desktopTokens; zoom100Tokens ]

// Dashboard token paths (from phase1-findings)
let dashboardPaths = [
    "color.background.accent"
    "color.background.alt"
    "color.background.body"
    "color.background.default"
    "color.button.primary.background.default"
    "color.button.primary.icon"
    "color.button.primary.text"
    "color.button.primary.border"
    "color.icon.accent"
    "color.icon.default"
    "color.text.bright"
    "color.text.main"
    "color.text.muted"
    "color.border.accent"
    "color.border.default"
    "stroke.hairline"
    "typography.button"
    "typography.default"
    "typography.heading.level-2"
    "typography.meta"
    "typography.quote"
    "typography.strong"
    "spacing.3xs"
    "spacing.2xs"
    "spacing.xs"
    "spacing.sm"
    "spacing.md"
    "spacing.xl"
    "spacing.micro"
    "radius.3xs"
    "radius.xs"
    "radius.sm"
    "radius.full"
    "breakpoint"
    "size.3xl"
]

printfn "\nDashboard token resolution (Light + Core + Desktop + 100%%):"
for path in dashboardPaths do
    match Map.tryFind path allTokens with
    | Some token -> printfn "  %s: %A = %A" path token.Type token.Value
    | None       -> printfn "  %s: NOT FOUND" path
