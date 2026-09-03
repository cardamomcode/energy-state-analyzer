// decision: a stateless generic class of pure combinators over ONE domain type (Option<T>). It has
// many methods — more than the method-count bar — yet every method transforms that single domain
// type, so its type-diversity ratio stays low and it must NOT be flagged as a god class.
#include <optional>

template <typename T>
class Option {
public:
    static Option<T> some(T value) {
        return Option{};
    }

    static Option<T> nothing() {
        return Option{};
    }

    T defaultValue(T value) const {
        return value;
    }

    template <typename U>
    Option<U> map(U (*f)(T)) {
        return Option<U>{};
    }

    template <typename U>
    Option<U> bind(U (*f)(T)) {
        return Option<U>{};
    }

    bool isSome() const {
        return true;
    }

    bool isNone() const {
        return false;
    }

    T* data() const {
        return nullptr;
    }

    T unwrapOr(T defaultValue) const {
        return defaultValue;
    }

    template <typename U>
    Option<U> mapTo(U _value) {
        return Option<U>{};
    }

    Option<T> chain(const Option<T>& other) const {
        return other;
    }

    bool equals(const Option<T>& other) const {
        return true;
    }

    size_t size() const {
        return 1;
    }

    T* get() const {
        return nullptr;
    }

    void inspect(T _value) const {
        // noop
    }
};
