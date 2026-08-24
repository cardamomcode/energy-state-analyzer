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
