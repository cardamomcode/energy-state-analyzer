# Reading the source

Energy State Analyzer turns source syntax into maintainability findings in a VS Code
extension and a Node CLI. Both hosts use the same registered detectors and project
thresholds. Grammar adapters describe syntax; they do not run a compiler, resolve
symbols, or prove a suggested refactoring correct.

Read the file introductions, definition documentation, and decision comments together.
Narrative describes current behavior; ADCs preserve deliberate choices, invariants,
external assumptions, and accepted costs. The [architecture](architecture.md) describes
build boundaries, and [ADC guidance](../AGENT_DECISION_COMMENTS.md) defines the annotation
syntax. Source comments use Markdown within native F# comments. The two optional
Lean contracts below have an explicit extraction and checking command.

## Follow a document

1. [CLI](../src/Cli.fs) parses arguments, validates paths, resolves configuration, and
   dispatches a scan or revision comparison. [CliRuntime](../src/CliRuntime.fs) reads
   source and caches parsers. [CliModes](../src/CliModes.fs) chooses reports and exit codes.
2. [Extension](../src/Extension/Extension.fs) owns editor events, commands, caches, and
   presentation resources. [Analysis](../src/Extension/Analysis.fs) reads current document
   text and folder policy. Pending grammar loads are shared, and stale document or
   activation identities are checked before results are displayed.
3. [ParsedAnalysis](../src/Core/ParsedAnalysis.fs) parses source, calls the synchronous
   analyzer, and releases the tree. Its result contains either detached findings or a
   typed error, never partial findings from a failed analysis.
4. [Analyze](../src/Core/Analyze.fs) builds the document context and runs
   [DetectorPipeline](../src/Core/DetectorPipeline.fs). Stages accumulate findings in
   reverse order; Analyze restores order and applies
   [Suppressions](../src/Core/Suppressions.fs) once to the complete result.
5. [Violation](../src/Core/Violation.fs) defines the detached result and stable public
   rule identities. [Report](../src/Core/Report.fs) computes additive scores, while
   [ReportHuman](../src/Core/ReportHuman.fs) provides a separate prioritization view.
   Editor [decorations](../src/Extension/Decorations.fs) and
   [diagnostics](../src/Extension/Diagnostics.fs) convert pure presentation specifications
   to host objects.

## Read the boundaries

| Boundary | Responsibility and limits |
| --- | --- |
| [Config](../src/Core/Config.fs) | Defaults and project-file overlays; CLI flags and editor toggles are applied by their hosts. |
| [Registry](../src/Languages/Registry.fs) | Editor language IDs and longest matching filename suffixes. |
| [LanguageAdapter](../src/Core/LanguageAdapter.fs) | Callable roles, branches, imports, recovery regions, and literal exemptions; views borrow a live tree. |
| [TreeSitter](../src/Core/TreeSitter.fs) | Native runtime initialization, parsing, node access, and tree release. |
| [Scan](../src/Core/Scan.fs) | Supported files, ignore patterns, limited wildcards, and deterministic path ordering. |
| [NodeInterop](../src/Core/NodeInterop.fs) | Selected throwing Node operations converted to typed results; no rollback guarantee. |
| [SARIF export](../src/Extension/SarifExport.fs) | Disk-based workspace scan followed by report writing; writes are not atomic. |
| [TestUtils](../tests/TestUtils.fs) | Real grammar/fixture integration harness; cached fixture trees live for the test process. |
| [Fixture matrix](../tests/DetectorFixtureMatrixTests.fs) | Named positive, negative, and language-limitation scenarios through the complete pipeline. |

## Build metadata and excluded inputs

[package.json](../package.json) owns npm scripts, extension contributions, and published
entry paths. [webpack.config.js](../webpack.config.js) packages separate Fable output
roots into CommonJS extension and CLI bundles, including the CLI shebang and runtime
WASM. [EnergyState.fsproj](../src/EnergyState.fsproj) lists F# files in dependency order;
the [CLI project](../cli/EnergyState.Cli.fsproj) and
[test project](../tests/EnergyState.Tests.fsproj) select their executable entry points.
These files and the [Justfile](../Justfile) are the source of truth for generated output.
JSON manifests and settings remain data; their ownership is explained here instead of
adding invalid JSON comments.

This annotation pass covers authored F# product modules, CLI entry, test suites and
helpers, and the webpack packaging boundary. It excludes generated Fable output,
vendored grammars, dependency code, and analyzer input fixtures under
`src/test/fixtures/` and `tests/scan-fixtures/`. Fixture text is behaviorally significant:
adding comments can change line ranges and deliberately measured source shapes.
The retained `src/test/extension.test.js` is a generated legacy test artifact, not the
current Scriptorium source. PythonTypeInfo is retained extraction scaffolding and does
not participate in the registered detector pipeline.

## Checking the Lean contracts

Compact `lean:` predicates beside `Context.addViolations` and `Cyclomatic.addDecision`
specify finding order and the change in `E - N + 2`. Nearby prose maps parameters to
F# values and states the preconditions. They are contracts, not implementation proofs.

Run `just lean-check` with Python 3.10+, Lean via elan, and the
[ADC extractor](https://github.com/dbrattli/adc/tree/cf571c36bceeeb816203b0074f67f0c221d9177d/tools)
at `../adc/tools/leanify.py`, or pass its path: `just lean-check /path/to/leanify.py`.
[lean-toolchain](../lean-toolchain) pins Lean 4.24.0. The command checks formation with
warnings as errors and writes ignored views under `.lean-out/`; edit the F# comments.
Run it when contracts change. Normal builds and CI do not require the external ADC checkout.

## Source inventory

The links below include private implementation files as well as entry points. Local
function documentation explains contracts; nearby inline comments explain significant
closures and grammar-hook decisions. Named test cases specify the scenario and expected
outcome, with helper contracts beside their definitions.

### cli

- [Main.fs](../cli/Main.fs)

### src

- [Cli.fs](../src/Cli.fs)
- [CliModes.fs](../src/CliModes.fs)
- [CliNode.fs](../src/CliNode.fs)
- [CliRuntime.fs](../src/CliRuntime.fs)

### src/Core

- [Analyze.fs](../src/Core/Analyze.fs)
- [BodyLines.fs](../src/Core/BodyLines.fs)
- [CoherenceCollection.fs](../src/Core/CoherenceCollection.fs)
- [Config.fs](../src/Core/Config.fs)
- [Context.fs](../src/Core/Context.fs)
- [DetectorPipeline.fs](../src/Core/DetectorPipeline.fs)
- [Esaignore.fs](../src/Core/Esaignore.fs)
- [FsPath.fs](../src/Core/FsPath.fs)
- [LanguageAdapter.fs](../src/Core/LanguageAdapter.fs)
- [NamingCohesion.fs](../src/Core/NamingCohesion.fs)
- [NodeInterop.fs](../src/Core/NodeInterop.fs)
- [ParsedAnalysis.fs](../src/Core/ParsedAnalysis.fs)
- [Paths.fs](../src/Core/Paths.fs)
- [Position.fs](../src/Core/Position.fs)
- [Report.fs](../src/Core/Report.fs)
- [ReportDiff.fs](../src/Core/ReportDiff.fs)
- [ReportHuman.fs](../src/Core/ReportHuman.fs)
- [ReportSarif.fs](../src/Core/ReportSarif.fs)
- [Scan.fs](../src/Core/Scan.fs)
- [Suppressions.fs](../src/Core/Suppressions.fs)
- [TreeSitter.fs](../src/Core/TreeSitter.fs)
- [TypeCohesion.fs](../src/Core/TypeCohesion.fs)
- [Violation.fs](../src/Core/Violation.fs)

### src/Core/Detectors

- [ClassRelatedness.fs](../src/Core/Detectors/ClassRelatedness.fs)
- [Cognitive.fs](../src/Core/Detectors/Cognitive.fs)
- [Coherence.fs](../src/Core/Detectors/Coherence.fs)
- [Cyclomatic.fs](../src/Core/Detectors/Cyclomatic.fs)
- [ErrorShadowing.fs](../src/Core/Detectors/ErrorShadowing.fs)
- [ImportCoherence.fs](../src/Core/Detectors/ImportCoherence.fs)
- [Inversion.fs](../src/Core/Detectors/Inversion.fs)
- [LogicalControlFlow.fs](../src/Core/Detectors/LogicalControlFlow.fs)
- [MagicNumber.fs](../src/Core/Detectors/MagicNumber.fs)
- [MagicString.fs](../src/Core/Detectors/MagicString.fs)
- [MatchOpportunity.fs](../src/Core/Detectors/MatchOpportunity.fs)
- [MatchOpportunitySupport.fs](../src/Core/Detectors/MatchOpportunitySupport.fs)
- [Nesting.fs](../src/Core/Detectors/Nesting.fs)
- [OpaqueBoolean.fs](../src/Core/Detectors/OpaqueBoolean.fs)
- [ParameterCount.fs](../src/Core/Detectors/ParameterCount.fs)
- [ParseDontValidate.fs](../src/Core/Detectors/ParseDontValidate.fs)
- [PrimitiveObsession.fs](../src/Core/Detectors/PrimitiveObsession.fs)
- [TestFile.fs](../src/Core/Detectors/TestFile.fs)

### src/Core/PythonTypeInfo

- [Classes.fs](../src/Core/PythonTypeInfo/Classes.fs)
- [Extraction.fs](../src/Core/PythonTypeInfo/Extraction.fs)
- [Functions.fs](../src/Core/PythonTypeInfo/Functions.fs)
- [Imports.fs](../src/Core/PythonTypeInfo/Imports.fs)
- [PythonTypeInfo.fs](../src/Core/PythonTypeInfo/PythonTypeInfo.fs)

### src/Extension

- [Analysis.fs](../src/Extension/Analysis.fs)
- [Configuration.fs](../src/Extension/Configuration.fs)
- [ConfigurationValues.fs](../src/Extension/ConfigurationValues.fs)
- [DecorationModel.fs](../src/Extension/DecorationModel.fs)
- [Decorations.fs](../src/Extension/Decorations.fs)
- [DiagnosticModel.fs](../src/Extension/DiagnosticModel.fs)
- [Diagnostics.fs](../src/Extension/Diagnostics.fs)
- [Extension.fs](../src/Extension/Extension.fs)
- [Grammar.fs](../src/Extension/Grammar.fs)
- [LoadedLanguage.fs](../src/Extension/LoadedLanguage.fs)
- [SarifExport.fs](../src/Extension/SarifExport.fs)

### src/Extension/Vscode

- [DiagnosticValues.fs](../src/Extension/Vscode/DiagnosticValues.fs)
- [Diagnostics.fs](../src/Extension/Vscode/Diagnostics.fs)
- [Document.fs](../src/Extension/Vscode/Document.fs)
- [Host.fs](../src/Extension/Vscode/Host.fs)
- [Identity.fs](../src/Extension/Vscode/Identity.fs)
- [Presentation.fs](../src/Extension/Vscode/Presentation.fs)
- [Workspace.fs](../src/Extension/Vscode/Workspace.fs)

### src/Languages

- [CPlusPlus.fs](../src/Languages/CPlusPlus.fs)
- [CSharp.fs](../src/Languages/CSharp.fs)
- [CognitiveSyntax.fs](../src/Languages/CognitiveSyntax.fs)
- [Erlang.fs](../src/Languages/Erlang.fs)
- [ErlangValidation.fs](../src/Languages/ErlangValidation.fs)
- [FSharp.fs](../src/Languages/FSharp.fs)
- [Kotlin.fs](../src/Languages/Kotlin.fs)
- [Python.fs](../src/Languages/Python.fs)
- [PythonAdapterSyntax.fs](../src/Languages/PythonAdapterSyntax.fs)
- [Registry.fs](../src/Languages/Registry.fs)
- [Rust.fs](../src/Languages/Rust.fs)
- [RustImports.fs](../src/Languages/RustImports.fs)
- [RustPatterns.fs](../src/Languages/RustPatterns.fs)
- [RustValidation.fs](../src/Languages/RustValidation.fs)
- [TypeScript.fs](../src/Languages/TypeScript.fs)
- [TypeScriptReExports.fs](../src/Languages/TypeScriptReExports.fs)
- [ValidationSyntax.fs](../src/Languages/ValidationSyntax.fs)

### tests

- [AnonymousCallableTests.fs](../tests/AnonymousCallableTests.fs)
- [CPlusPlusTests.fs](../tests/CPlusPlusTests.fs)
- [CSharpTests.fs](../tests/CSharpTests.fs)
- [CallableTestUtils.fs](../tests/CallableTestUtils.fs)
- [CognitiveRuleCases.fs](../tests/CognitiveRuleCases.fs)
- [CognitiveRuleTests.fs](../tests/CognitiveRuleTests.fs)
- [CognitiveTests.fs](../tests/CognitiveTests.fs)
- [CoherenceTests.fs](../tests/CoherenceTests.fs)
- [ConfigTests.fs](../tests/ConfigTests.fs)
- [CyclomaticTests.fs](../tests/CyclomaticTests.fs)
- [DetectorFixtureMatrixTests.fs](../tests/DetectorFixtureMatrixTests.fs)
- [ErlangTests.fs](../tests/ErlangTests.fs)
- [ErrorShadowingTests.fs](../tests/ErrorShadowingTests.fs)
- [ExtensionPresentationTests.fs](../tests/ExtensionPresentationTests.fs)
- [GrammarTests.fs](../tests/GrammarTests.fs)
- [Helpers.fs](../tests/Helpers.fs)
- [InversionTests.fs](../tests/InversionTests.fs)
- [LogicalControlFlowTests.fs](../tests/LogicalControlFlowTests.fs)
- [MagicNumberTests.fs](../tests/MagicNumberTests.fs)
- [MagicStringTests.fs](../tests/MagicStringTests.fs)
- [Main.fs](../tests/Main.fs)
- [MatchOpportunityTests.fs](../tests/MatchOpportunityTests.fs)
- [NestingTests.fs](../tests/NestingTests.fs)
- [OpaqueBooleanTests.fs](../tests/OpaqueBooleanTests.fs)
- [ParameterCountTests.fs](../tests/ParameterCountTests.fs)
- [ParseDontValidateTests.fs](../tests/ParseDontValidateTests.fs)
- [ParsedAnalysisTests.fs](../tests/ParsedAnalysisTests.fs)
- [PrimitiveObsessionTests.fs](../tests/PrimitiveObsessionTests.fs)
- [RecoveryRuleTests.fs](../tests/RecoveryRuleTests.fs)
- [ReportTests.fs](../tests/ReportTests.fs)
- [RustTests.fs](../tests/RustTests.fs)
- [SpikeTests.fs](../tests/SpikeTests.fs)
- [SuppressionsTests.fs](../tests/SuppressionsTests.fs)
- [TestUtils.fs](../tests/TestUtils.fs)
- [TsxTests.fs](../tests/TsxTests.fs)
