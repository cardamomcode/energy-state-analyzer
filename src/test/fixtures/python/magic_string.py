# clean — not flagged by magic string
def cleanValues(name, config):
    message = f"user {name} not found"
    print("something went wrong")
    return message, config["timeout"]


# clean — not flagged by magic string
def cleanInterpolatedKey(config, key):
    return config[f"{key}_value"]


# flagged — magic string
def flaggedMagicString(status):
    if status == "pending":
        return 1
    if status == "pending":
        return 2
    return 0


# flagged — magic string
def flaggedMembership(status):
    if status in ("queued", "completed"):
        return True
    if status in ("queued", "failed"):
        return True
    return False


# flagged — magic string
def flaggedDictKey(config, other):
    return config["retries"] + other["retries"]


# flagged — magic string
def flaggedCasePattern(event):
    match event:
        case "started":
            return 1
        case ("started", _):
            return 2
    return 0


# clean — not flagged by magic string
def cleanCaseResult(event):
    match event:
        case "opened":
            return "result"
        case _:
            return "result"
