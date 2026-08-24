module Coherence.Clean

open System.IO

let readConfig (path: string) =
    File.Exists path

let writeConfig (path: string) (data: string) =
    data.Length > 0
