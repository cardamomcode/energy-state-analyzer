module AnonymousCallableAnalysis

let cleanAnonymousCallable = fun first second -> first + second

let flaggedAnonymousCyclomatic =
    fun value ->
        if value = 0 then 0
        elif value = 1 then 1
        elif value = 2 then 2
        elif value = 3 then 3
        elif value = 4 then 4
        elif value = 5 then 5
        elif value = 6 then 6
        elif value = 7 then 7
        elif value = 8 then 8
        elif value = 9 then 9
        elif value = 10 then 10
        else value

let flaggedSevereAnonymousCyclomatic =
    fun value ->
        if value = 0 then 0
        elif value = 1 then 1
        elif value = 2 then 2
        elif value = 3 then 3
        elif value = 4 then 4
        elif value = 5 then 5
        elif value = 6 then 6
        elif value = 7 then 7
        elif value = 8 then 8
        elif value = 9 then 9
        elif value = 10 then 10
        elif value = 11 then 11
        elif value = 12 then 12
        elif value = 13 then 13
        elif value = 14 then 14
        elif value = 15 then 15
        else value

let flaggedAnonymousCognitive =
    fun value ->
        if value > 0 then
            if value > 1 then
                if value > 2 then
                    if value > 3 then
                        if value > 4 then value else 0
                    else
                        0
                else
                    0
            else
                0
        else
            0

let flaggedSevereAnonymousCognitive =
    fun value ->
        if value > 0 then
            if value > 1 then
                if value > 2 then
                    if value > 3 then
                        if value > 4 then
                            if value > 5 then
                                if value > 6 then value else 0
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

let flaggedAnonymousParameters = fun a b c d e f -> a + b + c + d + e + f

let flaggedSevereAnonymousParameters =
    fun a b c d e f g h i -> a + b + c + d + e + f + g + h + i

let nestedCallableBoundary value =
    let callback = fun item -> if item > 0 then 1 else 0
    callback value
