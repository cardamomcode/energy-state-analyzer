# flagged — opaque boolean
def flaggedPositionalBoolean():
    configure(True)


# flagged — opaque boolean
def flaggedPositionalBooleanAmongOthers():
    process(1, False)


# flagged in production; intentional expected values in test files
def flaggedAssertionBoolean():
    assert_equal(is_enabled(), True)
    assert_equal(is_disabled(), False)


# clean — not flagged by opaque boolean
def suppressedKeywordArgument():
    configure(retries=True)


# clean — not flagged by opaque boolean
def suppressedNonCallUsage():
    return True
