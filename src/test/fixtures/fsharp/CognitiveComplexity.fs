module CognitiveComplexity

// clean — not flagged by cognitive complexity
let cleanSimpleFunction (x: int) = if x > 0 then 1 else 0

// flagged — cognitive complexity (medium)
let flaggedComplexFunction (x: int) =
    if x > 0 then
        if x > 1 then
            if x > 2 then
                if x > 3 then
                    if x > 4 then x else 0
                else
                    0
            else
                0
        else
            0
    else
        0

// flagged — cognitive complexity (high)
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
