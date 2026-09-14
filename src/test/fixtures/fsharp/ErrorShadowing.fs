module ErrorShadowing

// clean — not flagged by error shadowing
let private compute () = 1

// clean — not flagged by error shadowing
let private transform value = value + 1

// clean — not flagged by error shadowing
let private finalize value = value * 2

// decision: the protected try body is happy-path work and the small with rules are recovery, so the
// error-shadowing detector should stay quiet.
// clean — not flagged by error shadowing
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

// clean — not flagged by error shadowing
let private handleValueError _err = -1

// clean — not flagged by error shadowing
let private handleKeyError _err = -2

// control: no error handling at all, so nothing should be flagged.
// clean — not flagged by error shadowing
let cleanPath () =
    let a = compute ()
    let b = transform a
    finalize b
