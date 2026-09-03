# decision: a stateless tagged union of pure combinators over ONE domain type (Option[T]). It has
# many methods — more than the method-count bar — yet every method transforms that single domain
# type, so its type-diversity ratio stays low and it must NOT be flagged as a god class. This is a
# regression guard for the "module-like value type used for method chaining" case.
from __future__ import annotations

from typing import Callable, Generic, TypeVar

T = TypeVar("T")
U = TypeVar("U")


class Option(Generic[T]):
    """An Option value with a full combinator API — cohesive, not a god class."""

    none: object

    @staticmethod
    def some(value: T) -> Option[T]:
        return None  # type: ignore

    @staticmethod
    def nothing() -> Option[T]:
        return None  # type: ignore

    def default_value(self, value: T) -> T:
        return value

    def default_with(self, getter: Callable[[], T]) -> T:
        return getter()

    def map(self, f: Callable[[T], U]) -> Option[U]:
        return None  # type: ignore

    def bind(self, f: Callable[[T], Option[U]]) -> Option[U]:
        return None  # type: ignore

    def filter(self, pred: Callable[[T], bool]) -> Option[T]:
        return None  # type: ignore

    def or_else(self, other: Option[T]) -> Option[T]:
        return other

    def or_else_with(self, f: Callable[[], Option[T]]) -> Option[T]:
        return f()

    def is_some(self) -> bool:
        return True

    def is_none(self) -> bool:
        return False

    def to_list(self) -> list[T]:
        return []

    def to_optional(self) -> T | None:
        return None  # type: ignore

    def inspect(self, f: Callable[[T], object]) -> Option[T]:
        return None  # type: ignore

    def unwrap_or(self, default: T) -> T:
        return default
