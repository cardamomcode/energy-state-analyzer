module Energy.Extension.Extension

open System.Collections.Generic
open System.Threading.Tasks

open Fable.Core

open Energy.Extension.Analysis
open Energy.Extension.Configuration
open Energy.Extension.Decorations
open Energy.Extension.Diagnostics
open Energy.Extension.Grammar
open Energy.Extension.VscodeDiagnostics
open Energy.Extension.VscodeDocument
open Energy.Extension.VscodeHost
open Energy.Extension.VscodeIdentity
open Energy.Extension.VscodeWorkspace
open Energy.Languages.Registry

// Composition root: owns lifecycle state and event wiring only. Detection and presentation stay
// in their domain modules, and the grammar caches are reset for every activation.

type private ExtensionState =
    { Grammar: GrammarContext
      Diagnostics: obj
      mutable Decorations: DecorationSet }

let mutable private state: ExtensionState option = None

let console = JS.console

let private clearIgnored editor current =
    let document = editorDocument editor
    applyDecorations editor current.Decorations []
    deleteDiagnostics current.Diagnostics (documentUri document)

let private isCurrentDocument document =
    match activeTextEditor window with
    | null -> false
    | editor -> sameObject (editorDocument editor) document

// decision: re-reads the active editor after grammar loading because users can change tabs while
// the promise is pending; decorations must never be written onto the newly active document.
let private analyzeActiveEditor () : Task<unit> =
    task {
        console.log ("🔍 Analyzing active editor...")

        match state, activeTextEditor window with
        | _, null -> console.log ("❌ No active editor found")
        | None, _ -> ()
        | Some current, editor ->
            let document = editorDocument editor

            if isDocumentIgnored document then
                console.log ("🚫 Ignored by .esaignore: " + documentFileName document)
                clearIgnored editor current
            else
                let! loaded = getOrLoadLanguage (documentLanguageId document) current.Grammar

                match loaded with
                | None ->
                    console.log ("⚠️ Unsupported language: " + documentLanguageId document)
                    clearDiagnostics current.Diagnostics
                | Some _ when not (isCurrentDocument document) -> ()
                | Some loaded ->
                    console.log ("📄 Analyzing " + loaded.Adapter.Id + " file: " + documentFileName document)
                    let violations = analyzeDocument loaded document
                    console.log ("🔍 Found " + string violations.Length + " energy violations")
                    applyDecorations editor current.Decorations violations
                    updateProblemsPanel current.Diagnostics document violations
    }

let private requestAnalysis () = analyzeActiveEditor () |> ignore

let private subscribeEvents context =
    onDidChangeActiveTextEditor window (fun _ -> requestAnalysis ())
    |> addSubscription context

    onDidChangeTextDocument workspace (fun event ->
        match activeTextEditor window with
        | null -> ()
        | editor when sameObject (textDocumentFromEvent event) (editorDocument editor) -> requestAnalysis ()
        | _ -> ())
    |> addSubscription context

    onDidChangeConfiguration workspace (fun event ->
        match state with
        | Some current when affectsConfiguration event "energyStateAnalyzer.colors" ->
            disposeDecorations current.Decorations
            current.Decorations <- createDecorations (getEnergyColors ())
            requestAnalysis ()
        | Some _ when affectsConfiguration event "energyStateAnalyzer" -> requestAnalysis ()
        | _ -> ())
    |> addSubscription context

    onDidCloseTextDocument workspace (fun document ->
        if Map.containsKey (documentLanguageId document) Energy.Languages.Registry.languages then
            state
            |> Option.iter (fun current -> deleteDiagnostics current.Diagnostics (documentUri document)))
    |> addSubscription context

let activate (context: obj) : Task<unit> =
    task {
        console.log ("🚀 Activating Energy State Analyzer...")

        try
            console.log ("🔧 Initializing Parser...")
            do! initializeParser ()
            console.log ("✅ Parser initialized")

            let grammar =
                { ExtensionPath = extensionPath context
                  LoadedLanguages = Dictionary()
                  InFlightLoads = Dictionary() }

            let decorations = createDecorations (getEnergyColors ())
            let diagnostics = createDiagnosticCollection VscodeHost.languages "energyState"

            state <-
                Some
                    { Grammar = grammar
                      Diagnostics = diagnostics
                      Decorations = decorations }

            addSubscription context diagnostics
            console.log ("🎨 Decoration types created")
            console.log ("📋 Diagnostics collection created")

            registerCommand commands "energy-state-analyzer.analyze" (fun () ->
                showInformationMessage window "Energy State Analyzer: Manual analysis triggered!"
                requestAnalysis ())
            |> addSubscription context

            subscribeEvents context
            requestAnalysis ()
            console.log ("✅ Energy State Analyzer activated successfully!")
        with error ->
            console.error ("Failed to activate Energy State Analyzer:", box error)
            showErrorMessage window ("Energy State Analyzer failed to activate: " + string error)
    }

let deactivate () =
    state
    |> Option.iter (fun current ->
        disposeDecorations current.Decorations
        dispose current.Diagnostics)

    state <- None
