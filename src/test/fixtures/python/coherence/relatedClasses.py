# decision: regression fixture for a real false positive - two classes whose methods
# construct/return each other (CancellationTokenSource.token() -> CancellationToken,
# CancellationToken's constructor takes a CancellationTokenSource) must NOT be flagged as
# unrelated classes just because there are 2 of them in one file.


class CancellationTokenSource:
    def __init__(self) -> None:
        self.token_value = CancellationToken(self)

    def cancel(self) -> None:
        pass

    def token(self) -> CancellationToken:
        return self.token_value


class CancellationToken:
    def __init__(self, source: CancellationTokenSource) -> None:
        self.source = source

    def is_cancellation_requested(self) -> bool:
        return False
