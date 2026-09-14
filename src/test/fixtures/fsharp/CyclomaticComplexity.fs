module CyclomaticComplexity

// clean — not flagged by cyclomatic complexity
let cleanSimpleFunction (x: int) = if x > 0 then 1 else 0

// flagged — cyclomatic complexity (medium)
let flaggedComplexFunction (status: string) =
    if status = "a" then 1
    elif status = "b" then 2
    elif status = "c" then 3
    elif status = "d" then 4
    elif status = "e" then 5
    elif status = "f" then 6
    elif status = "g" then 7
    elif status = "h" then 8
    elif status = "i" then 9
    elif status = "j" then 10
    elif status = "k" then 11
    else 0

// flagged — cyclomatic complexity (high)
let flaggedSevereFunction (status: string) =
    if status = "a" then 1
    elif status = "b" then 2
    elif status = "c" then 3
    elif status = "d" then 4
    elif status = "e" then 5
    elif status = "f" then 6
    elif status = "g" then 7
    elif status = "h" then 8
    elif status = "i" then 9
    elif status = "j" then 10
    elif status = "k" then 11
    elif status = "l" then 12
    elif status = "m" then 13
    elif status = "n" then 14
    elif status = "o" then 15
    elif status = "p" then 16
    else 0
