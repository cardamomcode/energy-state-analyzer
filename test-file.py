def complex_function(data):
    if data:
        for item in data:
            if item.valid:
                for subitem in item.children:
                    if subitem.active:
                        for detail in subitem.details:
                            if detail.important:
                                # Deep nesting!
                                process(detail)

def many_params(a, b, c, d, e, f, g, h, i, j, k):
    if a and b:
        if c or d:
            if e and f:
                return "complex"

def simple_function():
    return "hello world"