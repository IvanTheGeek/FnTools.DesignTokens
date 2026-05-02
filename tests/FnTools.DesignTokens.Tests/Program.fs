module FnTools.DesignTokens.Tests.Program

open Expecto


[<EntryPoint>]
let main args =
    let all =
        testList "all" [
            ValidationTests.allTests
            ParseTests.allTests
            VersionTests.allTests
            FlattenTests.allTests
            ResolverTests.allTests
            SimpleApiTests.allTests
            PropertyTests.allTests
            SmokeTests.allTests
        ]
    runTestsWithCLIArgs [] args all
