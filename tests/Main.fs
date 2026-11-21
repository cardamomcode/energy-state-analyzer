// # Main
//
// This executable registers the complete Scriptorium suite for Node. Shared grammar and fixture tasks
// allow concurrent asynchronous cases to reuse immutable parse data; the runner controls the final process
// status. DetectorFixtureMatrixTests.fs is the cross-language scenario catalogue, and TestUtils.fs owns
// its common harness.
//
// Reading path: [Source guide](../docs/source-guide.md).

module Energy.Tests.Main

open type Scriptorium.Quill.Runner

// JS/Node runner. On this target Quill cannot block, so `runTests` returns 0 immediately
// and chains `process.exit` onto the resolved promise itself — the value returned from here
// is ignored, and no wrapper script is needed. (Idiom from Fable.Giraffe test/js/Main.fs.)
/// Register all Scriptorium suites with the Node runner. The runner schedules asynchronous completion and
/// chooses the eventual process exit code; its immediate return is only startup status.
[<EntryPoint>]
let main _ =
    runTests
        [
            SpikeTests.tests
            ConfigTests.tests
            CPlusPlusTests.tests
            CSharpTests.tests
            ErlangTests.tests
            RustTests.tests
            TsxTests.tests
            AnonymousCallableTests.tests
            NestingTests.tests
            NestingTests.gatingTests
            CyclomaticTests.tests
            CognitiveTests.tests
            CoherenceTests.tests
            MagicNumberTests.tests
            MagicStringTests.tests
            InversionTests.tests
            MatchOpportunityTests.tests
            LogicalControlFlowTests.tests
            OpaqueBooleanTests.tests
            ParseDontValidateTests.tests
            ParameterCountTests.tests
            ParameterCountTests.configOverrideTests
            PrimitiveObsessionTests.tests
            ErrorShadowingTests.tests
            RecoveryRuleTests.tests
            SuppressionsTests.tests
            DetectorFixtureMatrixTests.tests
            ReportTests.tests
            ExtensionPresentationTests.tests
            GrammarTests.tests
            ParsedAnalysisTests.tests
        ]
