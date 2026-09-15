def named(value):
    return value


module_bound = lambda left, right: left + right


class Operations:
    class_bound = lambda value: value + 1


def outer(items):
    local_bound = lambda value: value * 2
    return list(map(lambda item: item + 1, items))
