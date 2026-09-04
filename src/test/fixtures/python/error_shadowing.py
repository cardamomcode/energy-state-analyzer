def compute():
    return 1


def transform(value):
    return value + 1


def finalize(value):
    return value * 2


# decision: most of this function's named nodes live inside the try/catch region, so error handling
# shadows the (tiny) unguarded business logic — the error-shadowing detector should flag it High.
def shadowedByError():
    result = None
    try:
        value = compute()
        processed = transform(value)
        result = finalize(processed)
    except ValueError:
        result = handle_value_error()
    except KeyError:
        result = handle_key_error()
    return result


def handle_value_error():
    return -1


def handle_key_error():
    return -2


# control: no error handling at all, so nothing should be flagged.
def cleanPath():
    a = compute()
    b = transform(a)
    c = finalize(b)
    return c
