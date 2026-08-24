def cleanShallowNesting(x):
    if x > 0:
        if x > 10:
            return x
    return 0


def flaggedDeepNesting(x):
    if x > 0:
        if x > 1:
            if x > 2:
                if x > 3:
                    if x > 4:
                        return x
    return 0


def flaggedSevereNesting(x):
    if x > 0:
        if x > 1:
            if x > 2:
                if x > 3:
                    if x > 4:
                        if x > 5:
                            if x > 6:
                                return x
    return 0
