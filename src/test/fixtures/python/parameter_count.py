# clean — not flagged by parameter count
def cleanFewParams(a, b):
    return a + b


# flagged — parameter count (medium)
def flaggedManyParams(a, b, c, d, e, f):
    return a + b + c + d + e + f


# flagged — parameter count (high)
def flaggedTooManyParams(a, b, c, d, e, f, g, h, i):
    return a + b + c + d + e + f + g + h + i
