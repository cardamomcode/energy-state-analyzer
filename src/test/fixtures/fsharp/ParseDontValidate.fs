module ParseDontValidate

type Positive = Positive of int

let flaggedPositive (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    amount

let flaggedUpperBound (amount: int) (limit: int) =
    if amount > 100 then
        invalidArg "amount" "positive"

    amount


let flaggedFailWithFormat (amount: int) (limit: int) =
    if amount <= 0 then
        failwithf "amount %d must be positive" amount

    amount


let flaggedInvalidArgFormat (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArgf "amount" "%d must be positive" amount

    amount

let cleanConstructed (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    Positive amount

let cleanTransformed (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    amount + 1

let cleanIdentity (amount: int) (limit: int) = amount

let cleanOtherInput (amount: int) (limit: int) =
    if limit <= 0 then
        invalidArg "amount" "positive"

    amount

let cleanNestedThrow (amount: int) (limit: int) =
    if amount <= 0 then
        if limit < 0 then
            invalidArg "amount" "positive"

    amount

let cleanInterveningWork (amount: int) (limit: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    printfn "%d" amount
    amount


let flaggedNonEmpty (items: int list) =
    if List.isEmpty items then
        invalidArg "items" "empty"

    items

let cleanNull (value: string) =
    if value = null then
        invalidArg "value" "missing"

    value


let flaggedDispatch (command: string) =
    if command = "quit" then
        failwith "bye"

    command


let flaggedCheckOnly (amount: int) =
    if amount <= 0 then
        invalidArg "amount" "bad"

let flaggedExplicitEmpty (amount: int) =
    if amount <= 0 then
        invalidArg "amount" "bad"

    ()

let flaggedBareReturn (amount: int) =
    if amount <= 0 then
        invalidArg "amount" "positive"

    None


let cleanGuardThenWork (amount: int) =
    if amount <= 0 then
        invalidArg "amount" "bad"

    printfn "%d" amount

let cleanNoCheck (amount: int) = ()

let cleanConditionalThrow (amount: int) =
    if amount <= 0 then
        if amount < -1 then
            invalidArg "amount" "bad"

let cleanUnconditionalThrow (amount: int) = invalidArg "amount" "bad"
