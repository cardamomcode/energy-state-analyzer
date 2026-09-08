module ErrorShadowing

let private compute () = 1

let private transform value = value + 1

let private finalize value = value * 2

// decision: the protected try body is happy-path work and the small with rules are recovery, so the
// error-shadowing detector should stay quiet.
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
