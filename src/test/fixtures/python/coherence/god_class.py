# decision: a mutable service whose methods each solve a different problem (DB, email, PDF,
# imaging, auth, queueing...). It has well over the method-count bar AND its methods span many
# unrelated domain types, so it is flagged as a god class — the class-level counterpart to
# function-count sprawl. Each helper type below exists only to be "unrelated" to the others.


class Connection:  # fixture-only marker types
    pass


class Row:
    pass


class Image:
    pass


class Report:
    pass


class Token:
    pass


class GodService:
    """A class doing too many unrelated things on purpose."""

    def __init__(self) -> None:
        self.state: list[object] = []

    def fetch_rows(self, conn: Connection) -> list[Row]:
        return []

    def send_email(self, to: str, body: str) -> bool:
        return True

    def render_pdf(self, data: dict) -> bytes:
        return b""

    def compress(self, path: str) -> str:
        return path

    def validate_token(self, token: Token) -> bool:
        return True

    def notify(self, message: str) -> None:
        return None

    def export_csv(self, rows: list) -> str:
        return ""

    def resize(self, image: Image) -> Image:
        return image

    def parse_yaml(self, text: str) -> dict:
        return {}

    def hash_password(self, password: str) -> str:
        return ""

    def send_sms(self, number: str, message: str) -> bool:
        return True

    def build_report(self, data: dict) -> Report:
        return None  # type: ignore

    def encrypt(self, raw: bytes) -> bytes:
        return raw

    def schedule(self, job: object) -> str:
        return ""

    def cache_get(self, key: str) -> object:
        return None  # type: ignore

    def log_event(self, event: str) -> None:
        return None
