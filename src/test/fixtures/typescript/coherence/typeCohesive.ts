// decision: mirrors the real-world F#-style Seq module pattern this fixture
// regression-tests against (expression/collections/seq.py) - one verb per operation, no
// shared name prefix, but nearly every function touches Iterable<T>. Must NOT be flagged
// as function-count sprawl despite exceeding the generic 12-function threshold with no
// naming cohesion at all.

function map<T, U>(source: Iterable<T>, mapper: (x: T) => U): Iterable<U> {
    return Array.from(source, mapper);
}

function filter<T>(source: Iterable<T>, predicate: (x: T) => boolean): Iterable<T> {
    return Array.from(source).filter(predicate);
}

function fold<T, U>(source: Iterable<T>, folder: (state: U, x: T) => U, seed: U): U {
    let state = seed;
    for (const x of source) {
        state = folder(state, x);
    }
    return state;
}

function head<T>(source: Iterable<T>): T {
    for (const x of source) {
        return x;
    }
    throw new Error('empty');
}

function length<T>(source: Iterable<T>): number {
    return Array.from(source).length;
}

function take<T>(source: Iterable<T>, count: number): Iterable<T> {
    return Array.from(source).slice(0, count);
}

function skip<T>(source: Iterable<T>, count: number): Iterable<T> {
    return Array.from(source).slice(count);
}

function tail<T>(source: Iterable<T>): Iterable<T> {
    return skip(source, 1);
}

function concat<T>(a: Iterable<T>, b: Iterable<T>): Iterable<T> {
    return [...Array.from(a), ...Array.from(b)];
}

function reverse<T>(source: Iterable<T>): Iterable<T> {
    return Array.from(source).reverse();
}

function distinct<T>(source: Iterable<T>): Iterable<T> {
    return Array.from(new Set(source));
}

function sum(source: Iterable<number>): number {
    let total = 0;
    for (const x of source) {
        total += x;
    }
    return total;
}

function max(source: Iterable<number>): number {
    return Math.max(...Array.from(source));
}

function min(source: Iterable<number>): number {
    return Math.min(...Array.from(source));
}
