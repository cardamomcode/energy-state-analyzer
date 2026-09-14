MAX_RETRIES = 5


# clean — not flagged by magic number
def cleanCommonValues(x):
    total = x * 1
    return total + 0


# flagged — magic number
def flaggedMagicNumbers(price):
    total = price * 1.08
    if total > 50:
        total += 15.75
    return total


# clean — not flagged by magic number
def exemptIndexAndDefault(arr, weight=42):
    first = arr[0]
    return first + weight


# clean — not flagged by magic number
def cleanNegativeValue(flag):
    if flag:
        return -1
    return 1
