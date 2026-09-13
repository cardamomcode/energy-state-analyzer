def cleanRequiredFollowup(a, b, c, d):
    if a:
        if b:
            process()
    finish()


def cleanDominantFollowup(a, b, c, d):
    if a:
        recordAttempt()
        processRequest()
        recordResult()
    finish()


def cleanInterveningWork(a, b, c, d):
    prepare()
    if a:
        recordAttempt()
        if b:
            process()
        recordResult()
    return 0


def cleanAlternativeBranch(a, b, c, d):
    if a:
        if b:
            return 1
    else:
        return 2
    return 0


def cleanFlatAlternatives(a, b, c, d):
    if a:
        return 1
    elif b:
        return 2
    elif c:
        return 3
    elif d:
        return 4
    else:
        return 0


def cleanTwoLevels(a, b, c, d):
    prepare()
    if a:
        if b:
            process()
    return 0


def flaggedThreeLevels(a, b, c, d):
    prepare()
    if a:
        if b:
            if c:
                process()
    return 0


def flaggedFourLevels(a, b, c, d):
    prepare()
    if a:
        if b:
            if c:
                if d:
                    process()
    return 0


def flaggedFiveGuards(a, b, c, d):
    if a:
        if b:
            if c:
                if d:
                    if a:
                        return 1
    return 0


def flaggedNestedElse(a, b, c, d):
    if a:
        return 1
    else:
        if b:
            return 2
        else:
            if c:
                return 3
    return 0


def flaggedImplicitFallthrough(a, b, c, d):
    if a:
        if b:
            process()


def cleanCommentHeavyBlock(a, b, c, d):
    if a:
        # This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block.
        process()
        return 1
    return 0


def cleanNestedFunction(a, b, c, d):
    if a:
        if b:
            def inner():
                if c:
                    process()
            inner()
    finish()


def flaggedAlternativeBody(a, b, c, d):
    if a:
        return 1
    elif b:
        if c:
            if d:
                process()
    return 0
