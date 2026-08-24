module PrimitiveObsession

let cleanDistinctTypes (name: string) (age: int) =
    sprintf "%s:%d" name age

let flaggedSwapRisk (x: int) (y: int) =
    x + y

let flaggedStringlyTyped (status: string) =
    if status = "pending" then 1
    elif status = "active" then 2
    elif status = "closed" then 3
    else 0
