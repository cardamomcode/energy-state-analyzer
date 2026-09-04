// decision: a stateless generic class of pure combinators over ONE domain type (Option<T>). It has
// many methods — more than the method-count bar — yet every method transforms that single domain
// type, so its type-diversity ratio stays low and it must NOT be flagged as a god class.
class Option<T> private constructor(private val value: T?) {

    companion object {
        fun <T> some(value: T): Option<T> = Option(value)
        fun <T> nothing(): Option<T> = Option(null)
    }

    fun defaultValue(value: T): T = value

    fun map<U>(f: (T) -> U): Option<U> = null ?: Option(null)

    fun bind<U>(f: (T) -> Option<U>): Option<U> = null ?: Option(null)

    fun filter(pred: (T) -> Boolean): Option<T> = this

    fun orElse(other: Option<T>): Option<T> = other

    fun isSome(): Boolean = true

    fun isNone(): Boolean = false

    fun toList(): List<T> = emptyList()

    fun inspect(f: (T) -> Any): Option<T> = this

    fun unwrapOr(defaultValue: T): T = defaultValue

    fun orThrow(): T = null ?: T

    fun mapTo<U>(_value: U): Option<U> = null ?: Option(null)

    fun chain(other: Option<T>): Option<T> = other

    fun peek(f: (T) -> Unit) {
        // noop
    }

    fun toNullable(): T? = value
}
