from dataclasses import dataclass

@dataclass(frozen=True)
class Positive:
    amount: int

def flaggedPositive(amount: int, limit: int):
    if amount <= 0:
        raise ValueError("positive")
    return amount

def flaggedUpperBound(amount: int, limit: int):
    if amount > 100:
        raise ValueError("positive")
    return amount


def flaggedDocstring(amount: int, limit: int):
    """Reject non-positive amounts."""
    if amount <= 0:
        raise ValueError("positive")
    return amount

def cleanConstructed(amount: int, limit: int):
    if amount <= 0:
        raise ValueError("positive")
    return Positive(amount)

def cleanTransformed(amount: int, limit: int):
    if amount <= 0:
        raise ValueError("positive")
    return amount + 1

def cleanIdentity(amount: int, limit: int):
    return amount

def cleanOtherInput(amount: int, limit: int):
    if limit <= 0:
        raise ValueError("positive")
    return amount

def cleanNestedThrow(amount: int, limit: int):
    if amount <= 0:
        if limit < 0:
            raise ValueError("positive")
    return amount

def cleanInterveningWork(amount: int, limit: int):
    if amount <= 0:
        raise ValueError("positive")
    amount = abs(amount)
    return amount


def flaggedNonEmpty(items: list[int]):
    if len(items) == 0:
        raise ValueError("empty")
    return items

def cleanNull(value: str | None):
    if value is None:
        raise ValueError("missing")
    return value


def flaggedCheckOnly(amount: int) -> None:
    if amount <= 0:
        raise ValueError("bad")

def flaggedExplicitEmpty(amount: int) -> None:
    if amount <= 0:
        raise ValueError("bad")
    return None

def flaggedBareReturn(amount: int) -> None:
    if amount <= 0:
        raise ValueError("bad")
    return

def cleanGuardThenWork(amount: int) -> None:
    if amount <= 0:
        raise ValueError("bad")
    print(amount)

def cleanNoCheck(amount: int) -> None:
    return None

def cleanConditionalThrow(amount: int) -> None:
    if amount <= 0:
        if amount < -1:
            raise ValueError("bad")

def cleanUnconditionalThrow(amount: int) -> None:
    raise ValueError("bad")
