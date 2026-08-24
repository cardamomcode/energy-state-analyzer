MAX_RETRIES = 5


def cleanCommonValues(x):
    total = x * 100
    return total + 1


def flaggedMagicNumbers(weight):
    cost = 5.5
    if weight > 50:
        cost += 15.75
    return cost


def flaggedMagicString():
    return "invalid input value not found"
