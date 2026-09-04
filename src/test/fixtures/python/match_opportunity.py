def cleanMixedConditions(a, b, c):
    if a > 10:
        return 1
    elif b == "urgent":
        return 2
    elif c is None:
        return 3
    return 0


def flaggedThreeWayChain(status):
    if status == "open":
        return 1
    elif status == "closed":
        return 2
    elif status == "pending":
        return 3
    return 0
