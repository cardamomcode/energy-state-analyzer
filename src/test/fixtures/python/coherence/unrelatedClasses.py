# decision: two classes with no shared inheritance, no type cross-reference, and no shared
# naming affix (Logger/HttpClient) - a real grab-bag of unrelated domains that should be
# flagged even though each class is individually small and internally cohesive.


class Logger:
    def __init__(self, prefix: str) -> None:
        self.prefix = prefix

    def log(self, message: str) -> None:
        print(self.prefix, message)


class HttpClient:
    def __init__(self, base_url: str) -> None:
        self.base_url = base_url

    def get(self, path: str) -> bytes:
        return b""
