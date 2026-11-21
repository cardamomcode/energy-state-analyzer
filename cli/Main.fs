// # Main
//
// The Fable CLI entry starts Energy.Cli.runCli. The asynchronous workflow owns validation, reporting, and
// eventual process exit; this module only connects that workflow to the generated executable. Build
// metadata in EnergyState.Cli.fsproj and webpack.config.js selects and packages this entry.
//
// Reading path: [Source guide](../docs/source-guide.md).

module Energy.Cli.Main

open Energy.Cli

/// Start the asynchronous CLI workflow from the Fable entry point and return the startup code; the workflow
/// chooses the eventual process exit status.
[<EntryPoint>]
let main _ =
    runCli () |> ignore

    0
