def cleanEarlyReturn(a, b):
    if not a:
        return 0
    if not b:
        return 0
    return a + b


def flaggedDominantIf(x):
    if x > 0:
        a = 1
        b = 2
        c = 3
        d = 4
        e = 5
        f = a + b + c + d + e
        return f
    return 0


def flaggedValidationChain(a, b, c):
    if a:
        if b:
            if c:
                return 1
    return 0
