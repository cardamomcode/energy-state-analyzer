import java.io.File

fun readConfig(path: String): Boolean {
    return File(path).exists()
}

fun writeConfig(path: String, data: String): Boolean {
    return data.length > 0
}
