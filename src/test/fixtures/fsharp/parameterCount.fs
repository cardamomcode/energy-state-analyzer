module ParameterCount

let cleanFewParams (a: int) (b: int) =
    a + b

let flaggedManyParams (a: int) (b: int) (c: int) (d: int) (e: int) (f: int) =
    a + b + c + d + e + f

let flaggedTooManyParams (a: int) (b: int) (c: int) (d: int) (e: int) (f: int) (g: int) (h: int) (i: int) =
    a + b + c + d + e + f + g + h + i
