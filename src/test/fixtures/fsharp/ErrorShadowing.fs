module ErrorShadowing

let private compute () = 1

let private transform value = value + 1

let private finalize value = value * 2

// decision: most of this function's named nodes live inside the try/except region, so error handling
// shadows the (tiny) unguarded business logic — the error-shadowing detector should flag it High.
let shadowedByError () =
    let result =
        try
            let value = compute ()
            let processed = transform value
            finalize processed
        with
        | :? System.ValueError as err -> handleValueError err
        | :? System.KeyError as err -> handleKeyError err

    result

let private handleValueError _err = -1

let private handleKeyError _err = -2

// control: no error handling at all, so nothing should be flagged.
let cleanPath () =
    let a = compute ()
    let b = transform a
    finalize b
