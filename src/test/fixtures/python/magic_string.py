def cleanValues(name, config):
    message = f"user {name} not found"
    print("something went wrong")
    return message, config["timeout"]


def cleanInterpolatedKey(config, key):
    return config[f"{key}_value"]


def flaggedMagicString(status):
    if status == "pending":
        return 1
    if status == "pending":
        return 2
    return 0


def flaggedMembership(status):
    if status in ("queued", "completed"):
        return True
    if status in ("queued", "failed"):
        return True
    return False


def flaggedDictKey(config, other):
    return config["retries"] + other["retries"]
