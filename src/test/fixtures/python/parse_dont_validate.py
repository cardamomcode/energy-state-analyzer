from dataclasses import dataclass

@dataclass(frozen=True)
class Positive:
    amount: int

# flagged — parse, don't validate (low)
def flaggedPositive(amount: int, limit: int):
    if amount <= 0:
        raise ValueError("positive")
    return amount

# flagged — parse, don't validate (low)
def flaggedUpperBound(amount: int, limit: int):
    if amount > 100:
        raise ValueError("positive")
    return amount


# flagged — parse, don't validate (low)
def flaggedDocstring(amount: int, limit: int):
    """Reject non-positive amounts."""
    if amount <= 0:
        raise ValueError("positive")
    return amount

# clean — not flagged by parse, don't validate
def cleanConstructed(amount: int, limit: int):
    if amount <= 0:
        raise ValueError("positive")
    return Positive(amount)

# clean — not flagged by parse, don't validate
def cleanTransformed(amount: int, limit: int):
    if amount <= 0:
        raise ValueError("positive")
    return amount + 1

# clean — not flagged by parse, don't validate
def cleanIdentity(amount: int, limit: int):
    return amount

# clean — not flagged by parse, don't validate
def cleanOtherInput(amount: int, limit: int):
    if limit <= 0:
        raise ValueError("positive")
    return amount

# clean — not flagged by parse, don't validate
def cleanNestedThrow(amount: int, limit: int):
    if amount <= 0:
        if limit < 0:
            raise ValueError("positive")
    return amount

# clean — not flagged by parse, don't validate
def cleanInterveningWork(amount: int, limit: int):
    if amount <= 0:
        raise ValueError("positive")
    amount = abs(amount)
    return amount


# flagged — parse, don't validate (low)
def flaggedNonEmpty(items: list[int]):
    if len(items) == 0:
        raise ValueError("empty")
    return items

# clean — not flagged by parse, don't validate
def cleanNull(value: str | None):
    if value is None:
        raise ValueError("missing")
    return value


# flagged — parse, don't validate (low)
def flaggedDispatch(command: str) -> str:
    if command == "quit":
        raise SystemExit("bye")
    return command


# flagged — parse, don't validate (low)
def flaggedCheckOnly(amount: int) -> None:
    if amount <= 0:
        raise ValueError("bad")

# flagged — parse, don't validate (low)
def flaggedExplicitEmpty(amount: int) -> None:
    if amount <= 0:
        raise ValueError("bad")
    return None

# flagged — parse, don't validate (low)
def flaggedBareReturn(amount: int) -> None:
    if amount <= 0:
        raise ValueError("bad")
    return

# clean — not flagged by parse, don't validate
def cleanGuardThenWork(amount: int) -> None:
    if amount <= 0:
        raise ValueError("bad")
    print(amount)

# clean — not flagged by parse, don't validate
def cleanNoCheck(amount: int) -> None:
    return None

# clean — not flagged by parse, don't validate
def cleanConditionalThrow(amount: int) -> None:
    if amount <= 0:
        if amount < -1:
            raise ValueError("bad")

# clean — not flagged by parse, don't validate
def cleanUnconditionalThrow(amount: int) -> None:
    raise ValueError("bad")
