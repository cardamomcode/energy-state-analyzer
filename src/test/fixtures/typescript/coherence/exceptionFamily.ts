// decision: several classes that never reference each other and share no naming affix at all
// (ValidationError/ParseError share a suffix, TimeoutFailure deliberately doesn't), but all
// extend the same base (Error, not itself defined in this file). Must NOT be flagged: shared
// inheritance from a common base is itself a real cohesion signal, independent of naming.

class ValidationError extends Error {
    constructor(message: string) {
        super(message);
    }
}

class ParseError extends Error {
    constructor(message: string) {
        super(message);
    }
}

class TimeoutFailure extends Error {
    constructor(message: string) {
        super(message);
    }
}
