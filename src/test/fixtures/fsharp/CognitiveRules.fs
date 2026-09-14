module CognitiveRules

// straightLine: cognitive score 0.
let straightLine (ready: bool) (enabled: bool) (urgent: bool) = ready

// booleanRun: cognitive score 1.
let booleanRun (ready: bool) (enabled: bool) (urgent: bool) = ready && enabled && urgent

// mixedRuns: cognitive score 3.
let mixedRuns (ready: bool) (enabled: bool) (urgent: bool) = ready && enabled || urgent && ready

// groupedRun: cognitive score 1.
let groupedRun (ready: bool) (enabled: bool) (urgent: bool) = ready && (enabled && urgent)

// negatedGroup: cognitive score 2.
let negatedGroup (ready: bool) (enabled: bool) (urgent: bool) = ready && not (enabled && urgent)

// plainElse: cognitive score 2.
let plainElse (ready: bool) (enabled: bool) (urgent: bool) = if ready then deliver () else queue ()

// flatChain: cognitive score 3.
let flatChain (ready: bool) (enabled: bool) (urgent: bool) =
    if ready then deliver ()
    elif urgent then expedite ()
    else queue ()

// nestedChain: cognitive score 4.
let nestedChain (ready: bool) (enabled: bool) (urgent: bool) =
    if enabled then
        if ready then
            deliver ()
        elif urgent then
            expedite ()

// nestedChecks: cognitive score 3.
let nestedChecks (ready: bool) (enabled: bool) (urgent: bool) =
    if enabled then
        if ready then
            deliver ()

// dispatch: cognitive score 3.
let dispatch (ready: bool) (enabled: bool) (urgent: bool) =
    match ready with
    | true ->
        if enabled then
            deliver ()
    | false -> queue ()

// loopBody: cognitive score 3.
let loopBody (ready: bool) (enabled: bool) (urgent: bool) =
    while ready do
        if enabled then
            deliver ()

// lambdaBody: cognitive score 2.
let lambdaBody (ready: bool) (enabled: bool) (urgent: bool) =
    let action =
        fun () ->
            if ready then
                deliver ()

    action ()

// emptyLambda: cognitive score 0.
let emptyLambda (ready: bool) (enabled: bool) (urgent: bool) =
    let action = fun () -> deliver ()
    action ()

// unitConditional: cognitive score 1.
let unitConditional (ready: bool) (enabled: bool) (urgent: bool) =
    if not ready then
        ()

    deliver ()

// catches: cognitive score 4.
let catches (ready: bool) (enabled: bool) (urgent: bool) =
    try
        deliver ()
    with
    | :? System.TimeoutException ->
        if ready then
            queue ()
    | :? System.ArgumentException -> reject ()

// cleanup: cognitive score 0.
let cleanup (ready: bool) (enabled: bool) (urgent: bool) =
    try
        deliver ()
    finally
        cleanup ()

// nestedFunction: cognitive score 2.
let nestedFunction (ready: bool) (enabled: bool) (urgent: bool) =
    audit ()

    let action () =
        if ready then
            deliver ()

    action ()

// emptyFunction: cognitive score 0.
let emptyFunction (ready: bool) (enabled: bool) (urgent: bool) =
    audit ()
    let action () = deliver ()
    action ()

// bracedElseCheck: cognitive score 4.
let bracedElseCheck (ready: bool) (enabled: bool) (urgent: bool) =
    if enabled then
        deliver ()
    else
        (if ready then
             deliver ())
