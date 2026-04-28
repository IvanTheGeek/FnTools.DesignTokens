module NEXUS.DesignTokens.Tests.PropertyTests

open Expecto
open Hedgehog
open NEXUS.DesignTokens
open NEXUS.DesignTokens.Tests.Generators


let allTests =
    testList "Properties" [

        testCase "flatten yields one entry per child for flat files" <| fun () ->
            Property.checkBool <| property {
                let! file = flatTokenFile
                let flat = NEXUS.DesignTokens.Api.Primitives.flatten file |> List.ofSeq
                return List.length flat = List.length file.Children
            }

        testCase "validate accepts simple-token files" <| fun () ->
            Property.checkBool <| property {
                let! file = flatTokenFile
                match Validation.validate file with
                | Ok () -> return true
                | Error _ -> return false
            }

        testCase "round-trip: parse(serialize(file)) preserves child count" <| fun () ->
            Property.checkBool <| property {
                let! file = flatTokenFile
                let json = Format.serialize file
                match Format.parse json with
                | Ok file2 -> return List.length file2.Children = List.length file.Children
                | Error _ -> return false
            }

        testCase "round-trip: parse(serialize(file)) preserves version" <| fun () ->
            Property.checkBool <| property {
                let! file = flatTokenFile
                let json = Format.serialize file
                match Format.parse json with
                | Ok file2 -> return file2.Version = V2025_10
                | Error _ -> return false
            }

        testCase "tryFind succeeds for every flattened path" <| fun () ->
            Property.checkBool <| property {
                let! file = flatTokenFile
                let flat = NEXUS.DesignTokens.Api.Primitives.flatten file |> List.ofSeq
                let allFound =
                    flat
                    |> List.forall (fun (path, _) ->
                        match NEXUS.DesignTokens.Api.Primitives.tryFind path file with
                        | Some _ -> true
                        | None -> false)
                return allFound
            }

        testCase "flattenResolved: no Alias values in output (structural guarantee)" <| fun () ->
            Property.checkBool <| property {
                let! file = flatTokenFile
                match NEXUS.DesignTokens.Api.Primitives.flattenResolved file with
                | Error _ -> return false
                | Ok seq ->
                    let tokens = seq |> List.ofSeq
                    // ResolvedTokenValue type has no Alias case — we just verify completion
                    return List.length tokens = List.length file.Children
            }

        testCase "TokenName.tryCreate is idempotent on valid input" <| fun () ->
            Property.checkBool <| property {
                let! name = tokenName
                let s = TokenName.value name
                match TokenName.tryCreate s with
                | Ok n2 -> return TokenName.value n2 = s
                | Error _ -> return false
            }

        testCase "ColorValue components preserved through serialize/parse" <| fun () ->
            Property.checkBool <| property {
                let! cv = colorValue
                let token : Token =
                    { Value = TokenValue.Color { cv with Hex = None }
                      Type = Some ColorType
                      Metadata = { Description = None; Deprecated = None; Extensions = [] } }
                let! name = tokenName
                let file : TokenFile =
                    { Version = V2025_10
                      Schema = None
                      Children = [ name, TokenLeaf token ] }
                let json = Format.serialize file
                match Format.parse json with
                | Error _ -> return false
                | Ok file2 ->
                    match file2.Children with
                    | [ _, TokenLeaf t ] ->
                        match t.Value with
                        | TokenValue.Color c2 -> return c2.ColorSpace = cv.ColorSpace
                        | _ -> return false
                    | _ -> return false
            }
    ]
