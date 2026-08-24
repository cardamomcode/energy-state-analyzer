module MagicValues

let unflaggedMagicNumber (weight: float) =
    let mutable cost = 5.5
    if weight > 50.0 then
        cost <- cost + 15.75
    cost
