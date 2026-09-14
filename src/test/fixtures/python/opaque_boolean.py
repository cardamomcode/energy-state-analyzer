# flagged — opaque boolean
def flaggedPositionalBoolean():
    configure(True)


# flagged — opaque boolean
def flaggedPositionalBooleanAmongOthers():
    process(1, False)


# clean — not flagged by opaque boolean
def suppressedKeywordArgument():
    configure(retries=True)


# clean — not flagged by opaque boolean
def suppressedNonCallUsage():
    return True
