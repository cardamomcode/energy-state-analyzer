# clean — not flagged by primitive obsession
def cleanDistinctTypes(name: str, age: int):
    return f"{name}:{age}"


# flagged — primitive obsession
def flaggedSwapRisk(x: int, y: int):
    return x + y


# flagged — primitive obsession (low)
def flaggedStringlyTyped(status: str):
    if status == "pending":
        return 1
    elif status == "active":
        return 2
    elif status == "closed":
        return 3
    return 0


# flagged — primitive obsession (low)
def flaggedMembershipCheck(status: str):
    if status in ("pending", "active", "closed"):
        return 1
    return 0


# clean — not flagged by primitive obsession
def suppressedKeywordOnly(*, lat: float, lon: float):
    return (lat, lon)


# clean — not flagged by primitive obsession
def suppressedAfterStarArgs(name: str, *args, lat: float, lon: float):
    return (name, args, lat, lon)


# flagged — primitive obsession
def flaggedPartiallyKeywordOnly(x: int, *, y: int):
    return x + y


# flagged — boolean blindness
def flaggedBooleanBlindness(compress: bool, notify: bool):
    return compress and notify
