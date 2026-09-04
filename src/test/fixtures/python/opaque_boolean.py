def flaggedPositionalBoolean():
    configure(True)


def flaggedPositionalBooleanAmongOthers():
    process(1, False)


def suppressedKeywordArgument():
    configure(retries=True)


def suppressedNonCallUsage():
    return True
