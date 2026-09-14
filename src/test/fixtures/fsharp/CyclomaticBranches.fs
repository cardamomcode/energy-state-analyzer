// clean — not flagged by cyclomatic complexity
let classify value =
    match value with
    | "a" -> 1
    | "b" -> 2
    | _ -> 0

// clean — not flagged by cyclomatic complexity
let classifyWithoutFallback value =
    match value with
    | "a" -> 1
    | "b" -> 2
