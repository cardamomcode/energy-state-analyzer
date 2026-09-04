module MagicString

let cleanValues (name: string) = sprintf "user %s not found" name

let flaggedMagicString (status: string) =
    if status = "pending" then 1
    elif status = "pending" then 2
    else 0
