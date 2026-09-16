let encode value = value

let moduleBound = fun value -> value

let outer items =
    let localBound = fun value -> value
    items |> List.map (fun item -> item)

type Operations() =
    member _.ClassBound = fun value -> value

    member _.OuterMethod items =
        let localBound = fun value -> value
        items |> List.map (fun item -> item)
