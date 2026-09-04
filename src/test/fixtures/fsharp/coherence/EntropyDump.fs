module EntropyDump

// decision: 13 functions - past the generic 12-function threshold, the same threshold the
// naming-cohesion check is evaluated at - with diverse names AND diverse, unrelated types.
// Naming alone wouldn't flag this (no shared leading word, but also none needed: distinct
// names are exactly what a real grab-bag looks like); the type signal instead confirms it's
// not a case of a shared type family being missed and produces the stronger, more specific
// message.

let parseDate (value: string) : string = value.Trim()

let resizeImage (image: byte[]) (width: int) : byte[] = image

let sendEmail (recipient: string) (body: string) : bool =
    printfn "%s %s" recipient body
    true

let hashPassword (password: string) : string =
    System.String(Array.rev (password.ToCharArray()))

let flatten (data: Map<string, int>) : int list = data |> Map.toList |> List.map snd

let retryCount (attempts: int) : bool = attempts > 0

let slugify (text: string) : string =
    text.ToLowerInvariant().Replace(" ", "-")

let calculateTax (amount: float) : float = amount * 0.2

let validateEmail (email: string) : bool = email.Contains("@")

let generateId (seed: int) : string = string seed

let compress (data: byte[]) : byte[] = data

let toUpper (text: string) : string = text.ToUpperInvariant()

let clamp (value: float) (low: float) (high: float) : float = max low (min value high)

// decision: tree-sitter-fsharp only parses a curried function's `: <type> =` return-type
// annotation into a clean function_declaration_left shape when something follows it in the
// file - the last such function in a module misparses as a plain value_declaration_left
// instead (silently dropping it from isFunctionDefinition entirely). This trailing binding
// exists purely so clamp above isn't the last declaration in the file.
let _sentinel = 0
