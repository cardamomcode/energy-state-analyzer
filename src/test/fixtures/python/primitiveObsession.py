def cleanDistinctTypes(name: str, age: int):
    return f"{name}:{age}"


def flaggedSwapRisk(x: int, y: int):
    return x + y


def flaggedStringlyTyped(status: str):
    if status == "pending":
        return 1
    elif status == "active":
        return 2
    elif status == "closed":
        return 3
    return 0


def flaggedMembershipCheck(status: str):
    if status in ("pending", "active", "closed"):
        return 1
    return 0
