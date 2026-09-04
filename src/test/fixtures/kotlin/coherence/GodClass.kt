// decision: a class whose methods each solve a different problem (DB, email, PDF, imaging, auth,
// queueing...). Over the method-count bar AND spanning many unrelated domain types, so it is
// flagged as a god class. The marker types below exist only to be "unrelated" to each other.

class Connection // fixture-only marker types
class Row
class Image
class Report
class Token

class GodService {
    val state: List<Any> = emptyList()

    fun fetchRows(conn: Connection): Row = Row()

    fun sendEmail(to: String, body: String): Boolean = true

    fun renderPdf(data: Map<String, Any>): ByteArray = ByteArray(0)

    fun compress(path: String): String = path

    fun validateToken(token: Token): Boolean = true

    fun notify(message: String) {
        // noop
    }

    fun exportCsv(rows: List<Any>): String = ""

    fun resize(image: Image): Image = image

    fun parseYaml(text: String): Map<String, Any> = emptyMap()

    fun hashPassword(password: String): String = ""

    fun sendSms(number: String, message: String): Boolean = true

    fun buildReport(data: Map<String, Any>): Report = Report()

    fun encrypt(raw: ByteArray): ByteArray = raw

    fun schedule(job: Any): String = ""

    fun cacheGet(key: String): Any = object {}

    fun logEvent(event: String) {
        // noop
    }
}
