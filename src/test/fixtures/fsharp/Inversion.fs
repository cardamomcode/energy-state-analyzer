module Inversion

let unflaggedValidationChain (a: bool) (b: bool) (c: bool) =
    if a then
        if b then
            if c then 1 else 0
        else
            0
    else
        0
