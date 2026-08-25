def cleanValues(name, config):
    message = f"user {name} not found"
    print("something went wrong")
    return message, config["timeout"]


def flaggedMagicString(status):
    if status == "pending":
        return 1
    if status == "pending":
        return 2
    return 0
