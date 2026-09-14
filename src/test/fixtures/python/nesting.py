# clean — not flagged by nesting
def cleanShallowNesting(x):
    if x > 0:
        if x > 10:
            return x
    return 0


# flagged — nesting (medium)
def flaggedDeepNesting(x):
    if x > 0:
        if x > 1:
            if x > 2:
                if x > 3:
                    if x > 4:
                        return x
    return 0


# flagged — nesting (high)
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


# flagged — nesting
def flaggedTryNesting(x):
    try:
        try:
            try:
                try:
                    try:
                        return x
                    except Exception:
                        return 0
                except Exception:
                    return 0
            except Exception:
                return 0
        except Exception:
            return 0
    except Exception:
        return 0
