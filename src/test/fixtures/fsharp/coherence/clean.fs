module Coherence.Clean

open System.IO

let readConfig (path: string) =
    File.Exists path

type ConfigWrite = { Path: string; Data: string }

let writeConfig (config: ConfigWrite) =
    config.Data.Length > 0
