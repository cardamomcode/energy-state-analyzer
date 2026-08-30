module Energy.Tests.Main

open type Scriptorium.Quill.Runner

// JS/Node runner. On this target Quill cannot block, so `runTests` returns 0 immediately
// and chains `process.exit` onto the resolved promise itself — the value returned from here
// is ignored, and no wrapper script is needed. (Idiom from Fable.Giraffe test/js/Main.fs.)
[<EntryPoint>]
let main _ =
    runTests
        [ HelloTests.tests
          SpikeTests.tests
          NestingTests.tests
          CyclomaticTests.tests
          CognitiveTests.tests
          CoherenceTests.tests
          MagicNumberTests.tests
          MagicStringTests.tests
          InversionTests.tests
          MatchOpportunityTests.tests
          ParameterCountTests.tests ]
