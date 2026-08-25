module MagicNumber

let cleanCommonValues (x: float) =
    let total = x * 1.0
    total + 0.0

let flaggedMagicNumbers (price: float) =
    let total = price * 1.08
    if total > 50.0 then
        total + 15.75
    else
        total

let cleanNegativeValue (flag: bool) =
    if flag then -1 else 1
