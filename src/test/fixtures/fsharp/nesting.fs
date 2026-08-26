module Nesting

let cleanShallowNesting (x: int) =
    if x > 0 then
        if x > 10 then x
        else 0
    else 0

let flaggedDeepNesting (x: int) =
    if x > 0 then
        if x > 1 then
            if x > 2 then
                if x > 3 then
                    if x > 4 then x
                    else 0
                else 0
            else 0
        else 0
    else 0

let flaggedSevereNesting (x: int) =
    if x > 0 then
        if x > 1 then
            if x > 2 then
                if x > 3 then
                    if x > 4 then
                        if x > 5 then
                            if x > 6 then x
                            else 0
                        else 0
                    else 0
                else 0
            else 0
        else 0
    else 0

let flaggedTryNesting (x: int) =
    try
        try
            try
                try
                    try
                        x
                    with _ -> 0
                with _ -> 0
            with _ -> 0
        with _ -> 0
    with _ -> 0
