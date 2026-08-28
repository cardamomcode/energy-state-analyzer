# decision: mirrors the real-world F#-style Seq module pattern this fixture
# regression-tests against (expression/collections/seq.py) - one verb per operation, no
# shared name prefix, but nearly every function touches Iterable[T]/Iterator[T]. Must NOT
# be flagged as function-count sprawl despite exceeding the generic 12-function threshold
# with no naming cohesion at all.
from collections.abc import Callable, Iterable, Iterator
from typing import TypeVar

T = TypeVar("T")
U = TypeVar("U")


def map(source: Iterable[T], mapper: Callable[[T], U]) -> Iterable[U]:
    return (mapper(x) for x in source)


def filter(source: Iterable[T], predicate: Callable[[T], bool]) -> Iterable[T]:
    return (x for x in source if predicate(x))


def fold(source: Iterable[T], folder: Callable[[U, T], U], state: U) -> U:
    for x in source:
        state = folder(state, x)
    return state


def choose(source: Iterable[T], chooser: Callable[[T], Iterable[U]]) -> Iterable[U]:
    for x in source:
        yield from chooser(x)


def concat(sources: Iterable[Iterable[T]]) -> Iterable[T]:
    for source in sources:
        yield from source


def collect(source: Iterable[T], mapping: Callable[[T], Iterable[U]]) -> Iterable[U]:
    for x in source:
        yield from mapping(x)


def delay(generator: Callable[[], Iterable[T]]) -> Iterator[T]:
    return iter(generator())


def head(source: Iterable[T]) -> T:
    for x in source:
        return x
    raise ValueError("empty")


def length(source: Iterable[T]) -> int:
    return sum(1 for _ in source)


def mapi(source: Iterable[T], mapping: Callable[[int, T], U]) -> Iterable[U]:
    return (mapping(i, x) for i, x in enumerate(source))


def scan(source: Iterable[T], scanner: Callable[[U, T], U], state: U) -> Iterator[U]:
    for x in source:
        state = scanner(state, x)
        yield state


def skip(source: Iterable[T], count: int) -> Iterator[T]:
    it = iter(source)
    for _ in range(count):
        next(it, None)
    return it


def tail(source: Iterable[T]) -> Iterator[T]:
    it = iter(source)
    next(it, None)
    return it


def take(source: Iterable[T], count: int) -> Iterator[T]:
    it = iter(source)
    for _ in range(count):
        yield next(it)
