module ParseDontValidate

type Positive = Positive of int

// flagged — parse, don't validate
let flaggedPositive (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    amount

// flagged — parse, don't validate
let flaggedUpperBound (amount: int) (limit: int) =
    if amount > 100 then
        invalidArg "amount" "positive"

    amount


// flagged — parse, don't validate
let flaggedFailWithFormat (amount: int) (limit: int) =
    if amount <= 0 then
        failwithf "amount %d must be positive" amount

    amount


// flagged — parse, don't validate
let flaggedInvalidArgFormat (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArgf "amount" "%d must be positive" amount

    amount

// clean — not flagged by parse, don't validate
let cleanConstructed (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    Positive amount

// clean — not flagged by parse, don't validate
let cleanTransformed (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    amount + 1

// clean — not flagged by parse, don't validate
let cleanIdentity (amount: int) (limit: int) = amount

// clean — not flagged by parse, don't validate
let cleanOtherInput (amount: int) (limit: int) =
    if limit <= 0 then
        invalidArg "amount" "positive"

    amount

// clean — not flagged by parse, don't validate
let cleanNestedThrow (amount: int) (limit: int) =
    if amount <= 0 then
        if limit < 0 then
            invalidArg "amount" "positive"

    amount

// clean — not flagged by parse, don't validate
let cleanInterveningWork (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    printfn "%d" amount
    amount


// flagged — parse, don't validate
let flaggedNonEmpty (items: int list) =
    if List.isEmpty items then
        invalidArg "items" "empty"

    items

// clean — not flagged by parse, don't validate
let cleanNull (value: string) =
    if value = null then
        invalidArg "value" "missing"

    value


// flagged — parse, don't validate
let flaggedDispatch (command: string) =
    if command = "quit" then
        failwith "bye"

    command


// flagged — parse, don't validate
let flaggedCheckOnly (amount: int) =
    if amount <= 0 then
        invalidArg "amount" "bad"

// flagged — parse, don't validate
let flaggedExplicitEmpty (amount: int) =
    if amount <= 0 then
        invalidArg "amount" "bad"

    ()

// flagged — parse, don't validate
let flaggedBareReturn (amount: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    None


// clean — not flagged by parse, don't validate
let cleanGuardThenWork (amount: int) =
    if amount <= 0 then
        invalidArg "amount" "bad"

    printfn "%d" amount

// clean — not flagged by parse, don't validate
let cleanNoCheck (amount: int) = ()

// clean — not flagged by parse, don't validate
let cleanConditionalThrow (amount: int) =
    if amount <= 0 then
        if amount < -1 then
            invalidArg "amount" "bad"

// clean — not flagged by parse, don't validate
let cleanUnconditionalThrow (amount: int) = invalidArg "amount" "bad"


// flagged — parse, don't validate
let flaggedBooleanValidator (pw: string) = if pw = "" then false else true


// flagged — parse, don't validate
let flaggedThrowingBoolean (pw: string) =
    if pw = "" then
        invalidArg "pw" "empty"

    true


// flagged — parse, don't validate
let flaggedNullBooleanValidator (value: string) = if value = null then false else true


// clean — not flagged by parse, don't validate
let cleanBooleanQuery (role: string) =
    if role = null then false else role = "admin"


// flagged — F# has no narrowing-contract construct a signature can carry (no type predicates,
// no contracts), so this null-checking boolean validator still discards the checked value
// (limitation case for the shared explicit-narrowing row).
let cleanExplicitNarrowingValidator (value: string) = if value = null then false else true


// clean — not flagged by parse, don't validate (a truthy literal from the guard branch is not a rejection)
let cleanFlipped (value: string) = if value <> null then true else false
