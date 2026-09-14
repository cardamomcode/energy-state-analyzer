# clean — not flagged by cyclomatic complexity
def classify(value):
    match value:
        case "a":
            return 1
        case "b":
            return 2
        case _:
            return 0


# clean — not flagged by cyclomatic complexity
def classify_without_fallback(value):
    match value:
        case "a":
            return 1
        case "b":
            return 2
