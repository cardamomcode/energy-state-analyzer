let classify value =
    match value with
    | "a" -> 1
    | "b" -> 2
    | _ -> 0

let classifyWithoutFallback value =
    match value with
    | "a" -> 1
    | "b" -> 2
