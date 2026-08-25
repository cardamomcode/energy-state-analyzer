def flaggedPositionalBoolean():
    configure(True)
    return None


def flaggedPositionalBooleanAmongOthers():
    process(1, False)
    return None


def suppressedKeywordArgument():
    configure(retries=True)
    return None


def suppressedNonCallUsage():
    ok = True
    return ok
