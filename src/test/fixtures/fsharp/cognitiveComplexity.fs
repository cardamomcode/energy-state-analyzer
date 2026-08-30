module CognitiveComplexity

let cleanSimpleFunction (x: int) = if x > 0 then 1 else 0

let flaggedComplexFunction (x: int) =
    if x > 0 then
        if x > 1 then
            if x > 2 then
                if x > 3 then
                    if x > 4 then
                        if x > 5 then x else 0
                    else
                        0
                else
                    0
            else
                0
        else
            0
    else
        0

let flaggedSevereFunction (x: int) =
    if x > 0 then
        if x > 1 then
            if x > 2 then
                if x > 3 then
                    if x > 4 then
                        if x > 5 then
                            if x > 6 then x else 0
                        else
                            0
                    else
                        0
                else
                    0
            else
                0
        else
            0
    else
        0
