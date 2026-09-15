let named value = value

let rec mutualFirst value = value
and mutualSecond left right = left + right

let moduleBound = fun left right -> left + right

type Operations() =
    static member ClassBound = fun value -> value + 1

    member _.NamedMethod value = value

let outer items =
    let localBound = fun value -> value * 2
    items |> List.map (fun item -> item + 1)
