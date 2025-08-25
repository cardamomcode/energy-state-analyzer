from typing import TypedDict


class Detail(TypedDict):
    important: bool


class SubItem(TypedDict):
    active: bool
    details: list[Detail]


class Item(TypedDict):
    valid: bool
    children: list[SubItem]


def process(detail: Detail) -> None:
    """Example process function"""
    pass


def complex_function(data: list[Item]) -> None:
    if data:
        for item in data:
            if item["valid"]:
                for subitem in item["children"]:
                    if subitem["active"]:
                        for detail in subitem["details"]:
                            if detail["important"]:
                                # Deep nesting!
                                process(detail)


def many_params(
    a: int,
    b: str,
    c: bool,
    d: float,
    e: list[str],
    f: dict[str, int],
    g: int,
    h: str,
    i: bool,
    j: float,
    k: list[int],
) -> str | None:
    if a and b:
        if c or d:
            if e and f:
                return "complex"
    return None


def simple_function() -> str:
    return "hello world"
