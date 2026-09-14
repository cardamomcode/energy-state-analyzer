module RecoveryRules

open System.Threading.Tasks

// flagged — error shadowing (high)
let broadScope () =
    try
        work0 ()
        work1 ()
        work2 ()
        work3 ()
        work4 ()
        work5 ()
        work6 ()
        work7 ()
    with Failure0 ->
        work10 ()

// flagged — recovery dominance (high)
let twoLineProtected () =
    try
        work0 ()
        work1 ()
    with
    | Failure0 ->
        work10 ()
        work11 ()
    | Failure1 ->
        work10 ()
        work11 ()
    | Failure2 ->
        work10 ()
        work11 ()
    | Failure3 ->
        work10 ()
        work11 ()
    | Failure4 ->
        work10 ()
        work11 ()

// clean — not flagged by recovery dominance
let oneLineProtected () =
    try
        work0 ()
    with
    | Failure0 ->
        work10 ()
        work11 ()
    | Failure1 ->
        work10 ()
        work11 ()
    | Failure2 ->
        work10 ()
        work11 ()
    | Failure3 ->
        work10 ()
        work11 ()
    | Failure4 ->
        work10 ()
        work11 ()

// flagged — recovery dominance (medium)
let workBefore () =
    work90 ()

    try
        work0 ()
        work1 ()
    with
    | Failure0 ->
        work10 ()
        work11 ()
    | Failure1 ->
        work10 ()
        work11 ()
    | Failure2 ->
        work10 ()
        work11 ()
    | Failure3 ->
        work10 ()
        work11 ()
    | Failure4 ->
        work10 ()
        work11 ()

// flagged — recovery dominance (medium)
let workAfter () =
    try
        work0 ()
        work1 ()
    with
    | Failure0 ->
        work10 ()
        work11 ()
    | Failure1 ->
        work10 ()
        work11 ()
    | Failure2 ->
        work10 ()
        work11 ()
    | Failure3 ->
        work10 ()
        work11 ()
    | Failure4 ->
        work10 ()
        work11 ()

    work90 ()

// flagged — recovery dominance (high)
let multilineLoop () =
    try
        for item in items do
            process item
    with
    | Failure0 ->
        work10 ()
        work11 ()
    | Failure1 ->
        work10 ()
        work11 ()
    | Failure2 ->
        work10 ()
        work11 ()
    | Failure3 ->
        work10 ()
        work11 ()
    | Failure4 ->
        work10 ()
        work11 ()

// clean — not flagged by recovery dominance
let commentedTrivial () =
    try
        work0 () // mixed

    // only comment
    with
    | Failure0 ->
        work10 ()

        // comment only
        (*
           multiline comment
        *)
        work11 ()
    | Failure1 ->
        work10 ()

        // comment only
        (*
           multiline comment
        *)
        work11 ()
    | Failure2 ->
        work10 ()

        // comment only
        (*
           multiline comment
        *)
        work11 ()
    | Failure3 ->
        work10 ()

        // comment only
        (*
           multiline comment
        *)
        work11 ()
    | Failure4 ->
        work10 ()

        // comment only
        (*
           multiline comment
        *)
        work11 ()

// clean — not flagged by oversized recovery block
let atLimit () =
    try
        work0 ()
    with Failure0 ->
        work10 ()
        work11 ()
        work12 ()
        work13 ()
        work14 ()
        work15 ()
        work16 ()
        work17 ()
        work18 ()
        work19 ()
        work20 ()
        work21 ()
        work22 ()
        work23 ()
        work24 ()
        work25 ()
        work26 ()
        work27 ()
        work28 ()
        work29 ()

// flagged — oversized recovery block (medium)
let overLimit () =
    try
        work0 ()
    with Failure0 ->
        work10 ()
        work11 ()
        work12 ()
        work13 ()
        work14 ()
        work15 ()
        work16 ()
        work17 ()
        work18 ()
        work19 ()
        work20 ()
        work21 ()
        work22 ()
        work23 ()
        work24 ()
        work25 ()
        work26 ()
        work27 ()
        work28 ()
        work29 ()
        work30 ()

// clean — not flagged by oversized recovery block
let commentedLimit () =
    try
        work0 () // mixed

    // only comment
    with Failure0 ->
        work10 ()

        // comment only
        (*
           multiline comment
        *)
        work11 ()
        work12 ()
        work13 ()
        work14 ()
        work15 ()
        work16 ()
        work17 ()
        work18 ()
        work19 ()
        work20 ()
        work21 ()
        work22 ()
        work23 ()
        work24 ()
        work25 ()
        work26 ()
        work27 ()
        work28 ()
        work29 ()

// clean — not flagged by oversized recovery block
let separateHandlers () =
    try
        work0 ()
    with
    | Failure0 ->
        work10 ()
        work11 ()
        work12 ()
        work13 ()
        work14 ()
        work15 ()
        work16 ()
        work17 ()
        work18 ()
        work19 ()
        work20 ()
    | Failure1 ->
        work10 ()
        work11 ()
        work12 ()
        work13 ()
        work14 ()
        work15 ()
        work16 ()
        work17 ()
        work18 ()
        work19 ()
        work20 ()

// flagged — oversized recovery block (medium)
let oversizedCleanup () =
    try
        work0 ()
    finally
        work10 ()
        work11 ()
        work12 ()
        work13 ()
        work14 ()
        work15 ()
        work16 ()
        work17 ()
        work18 ()
        work19 ()
        work20 ()
        work21 ()
        work22 ()
        work23 ()
        work24 ()
        work25 ()
        work26 ()
        work27 ()
        work28 ()
        work29 ()
        work30 ()

// flagged — recovery dominance (high)
let multilineBinding () =
    try
        let value = work0 ()
        value
    with
    | FirstFailure -> recover ()
    | SecondFailure -> recover ()
    | ThirdFailure -> recover ()
    | FourthFailure -> recover ()
    | FifthFailure -> recover ()

// flagged — error shadowing (high)
let taskBoundary context =
    task {
        do! prepare ()

        try
            work0 ()
            work1 ()
            work2 ()
            work3 ()
            work4 ()
            work5 ()
            work6 ()
            work7 ()
        with _ ->
            recover ()
    }
