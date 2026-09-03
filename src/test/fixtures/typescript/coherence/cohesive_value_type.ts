// decision: a stateless generic class of pure combinators over ONE domain type (Option<T>). It has
// many methods — more than the method-count bar — yet every method transforms that single domain
// type, so its type-diversity ratio stays low and it must NOT be flagged as a god class.
export class Option<T> {
    private readonly _value: T | null;

    constructor(value: T | null) {
        this._value = value;
    }

    static some<T>(value: T): Option<T> {
        return new Option(value);
    }

    static nothing<T>(): Option<T> {
        return new Option(null);
    }

    defaultValue(value: T): T {
        return value;
    }

    map<U>(f: (a: T) => U): Option<U> {
        return null as unknown as Option<U>;
    }

    bind<U>(f: (a: T) => Option<U>): Option<U> {
        return null as unknown as Option<U>;
    }

    filter(pred: (a: T) => boolean): Option<T> {
        return null as unknown as Option<T>;
    }

    orElse(other: Option<T>): Option<T> {
        return other;
    }

    isSome(): boolean {
        return true;
    }

    isNone(): boolean {
        return false;
    }

    toArray(): Array<T> {
        return [];
    }

    toPrimitive(): T | null {
        return null;
    }

    inspect(f: (a: T) => unknown): Option<T> {
        return null as unknown as Option<T>;
    }

    unwrapOr(defaultValue: T): T {
        return defaultValue;
    }

    orThrow(): T {
        return null as unknown as T;
    }

    mapTo<U>(_value: U): Option<U> {
        return null as unknown as Option<U>;
    }

    chain(other: Option<T>): Option<T> {
        return other;
    }
}
