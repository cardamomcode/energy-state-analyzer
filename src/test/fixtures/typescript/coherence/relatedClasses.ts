// decision: regression fixture for a real false positive - two classes whose methods
// construct/return each other must NOT be flagged as unrelated just because there are 2 of
// them in one file.

class CancellationTokenSource {
    tokenValue: CancellationToken;

    constructor() {
        this.tokenValue = new CancellationToken(this);
    }

    cancel(): void {}

    token(): CancellationToken {
        return this.tokenValue;
    }
}

class CancellationToken {
    source: CancellationTokenSource;

    constructor(source: CancellationTokenSource) {
        this.source = source;
    }

    isCancellationRequested(): boolean {
        return false;
    }
}
