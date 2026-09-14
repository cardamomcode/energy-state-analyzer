module ParameterCount

// clean — not flagged by parameter count
let cleanFewParams (a: int) (b: int) = a + b

// flagged — parameter count (medium)
let flaggedManyParams (a: int) (b: int) (c: int) (d: int) (e: int) (f: int) = a + b + c + d + e + f

// flagged — parameter count (high)
let flaggedTooManyParams (a: int) (b: int) (c: int) (d: int) (e: int) (f: int) (g: int) (h: int) (i: int) =
    a + b + c + d + e + f + g + h + i

// Bug 1 (and-binding): the second head's parameter count IS measured — a 6-parameter and-bound
// function is flagged the same as flaggedManyParams above, instead of inheriting the first head's
// (single) parameter count and escaping the check.
// clean — not flagged by parameter count
let rec paramCountAlpha (a: int) = a
and flaggedAndBindingManyParams (a: int) (b: int) (c: int) (d: int) (e: int) (f: int) = a + b + c + d + e + f
