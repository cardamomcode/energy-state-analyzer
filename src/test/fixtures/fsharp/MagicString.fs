module MagicString

// clean — not flagged by magic string
let cleanValues (name: string) = sprintf "user %s not found" name

// flagged — magic string
let flaggedMagicString (status: string) =
    if status = "pending" then 1
    elif status = "pending" then 2
    else 0

// flagged — magic string
let flaggedCasePattern (event: string option) =
    match event with
    | Some "started" -> 1
    | Some("started" | "stopped") -> 2
    | _ -> 0

// clean — not flagged by magic string
let cleanCaseResult (event: string) =
    match event with
    | "opened" -> "result"
    | _ -> "result"
