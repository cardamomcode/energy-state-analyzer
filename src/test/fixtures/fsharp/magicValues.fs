module MagicValues

let cleanCommonValues (x: float) =
    let total = x * 100.0
    total + 1.0

let flaggedMagicNumbers (weight: float) =
    let mutable cost = 5.5
    if weight > 50.0 then
        cost <- cost + 15.75
    cost

let flaggedMagicString () =
    "invalid input value not found"
