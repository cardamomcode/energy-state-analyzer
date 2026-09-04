MAX_RETRIES = 5


def cleanCommonValues(x):
    total = x * 1
    return total + 0


def flaggedMagicNumbers(price):
    total = price * 1.08
    if total > 50:
        total += 15.75
    return total


def exemptIndexAndDefault(arr, weight=42):
    first = arr[0]
    return first + weight


def cleanNegativeValue(flag):
    if flag:
        return -1
    return 1
