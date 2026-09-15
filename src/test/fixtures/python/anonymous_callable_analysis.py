cleanAnonymousCallable = lambda first, second: first + second


flaggedAnonymousCyclomatic = lambda value: (
    0 if value == 0 else
    1 if value == 1 else
    2 if value == 2 else
    3 if value == 3 else
    4 if value == 4 else
    5 if value == 5 else
    6 if value == 6 else
    7 if value == 7 else
    8 if value == 8 else
    9 if value == 9 else
    10 if value == 10 else value
)


flaggedSevereAnonymousCyclomatic = lambda value: (
    0 if value == 0 else
    1 if value == 1 else
    2 if value == 2 else
    3 if value == 3 else
    4 if value == 4 else
    5 if value == 5 else
    6 if value == 6 else
    7 if value == 7 else
    8 if value == 8 else
    9 if value == 9 else
    10 if value == 10 else
    11 if value == 11 else
    12 if value == 12 else
    13 if value == 13 else
    14 if value == 14 else
    15 if value == 15 else value
)


flaggedAnonymousCognitive = lambda value: (
    value if value > 5 else
    (value if value > 4 else
     (value if value > 3 else
      (value if value > 2 else
       (value if value > 1 else
        (value if value > 0 else 0)))))
)


flaggedSevereAnonymousCognitive = lambda value: (
    value if value > 6 else
    (value if value > 5 else
     (value if value > 4 else
      (value if value > 3 else
       (value if value > 2 else
        (value if value > 1 else
         (value if value > 0 else 0))))))
)


flaggedAnonymousParameters = lambda a, b, c, d, e, f: a + b + c + d + e + f


flaggedSevereAnonymousParameters = lambda a, b, c, d, e, f, g, h, i: a + b + c + d + e + f + g + h + i


flaggedAnonymousParameterForms = lambda a, b=0, c=0, d=0, e=0, f=0: a + b + c + d + e + f


def nestedCallableBoundary(value):
    callback = lambda item: 1 if item > 0 else 0
    return callback(value)
