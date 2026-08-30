module Energy.Cli.Main

open Energy.Cli

[<EntryPoint>]
let main _ =
    runCli () |> ignore

    0
