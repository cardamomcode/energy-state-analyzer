module MagicString

// clean — not flagged by magic string
let cleanValues (name: string) = sprintf "user %s not found" name

// flagged — magic string
let flaggedMagicString (status: string) =
    if status = "pending" then 1
    elif status = "pending" then 2
    else 0
