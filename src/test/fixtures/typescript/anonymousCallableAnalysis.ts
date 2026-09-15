const cleanAnonymousCallable = (first: number, second: number): number => first + second;

const flaggedAnonymousCyclomatic = (value: number): number => {
    if (value === 0) return 0;
    if (value === 1) return 1;
    if (value === 2) return 2;
    if (value === 3) return 3;
    if (value === 4) return 4;
    if (value === 5) return 5;
    if (value === 6) return 6;
    if (value === 7) return 7;
    if (value === 8) return 8;
    if (value === 9) return 9;
    if (value === 10) return 10;
    return value;
};

const flaggedSevereAnonymousCyclomatic = (value: number): number => {
    if (value === 0) return 0;
    if (value === 1) return 1;
    if (value === 2) return 2;
    if (value === 3) return 3;
    if (value === 4) return 4;
    if (value === 5) return 5;
    if (value === 6) return 6;
    if (value === 7) return 7;
    if (value === 8) return 8;
    if (value === 9) return 9;
    if (value === 10) return 10;
    if (value === 11) return 11;
    if (value === 12) return 12;
    if (value === 13) return 13;
    if (value === 14) return 14;
    if (value === 15) return 15;
    return value;
};

const flaggedAnonymousCognitive = (value: number): number => {
    if (value > 0) {
        if (value > 1) {
            if (value > 2) {
                if (value > 3) {
                    if (value > 4) {
                        if (value > 5) return value;
                    }
                }
            }
        }
    }
    return 0;
};

const flaggedSevereAnonymousCognitive = (value: number): number => {
    if (value > 0) {
        if (value > 1) {
            if (value > 2) {
                if (value > 3) {
                    if (value > 4) {
                        if (value > 5) {
                            if (value > 6) return value;
                        }
                    }
                }
            }
        }
    }
    return 0;
};

const flaggedAnonymousParameters = function (a: number, b: number, c: number, d: number, e: number, f: number): number {
    return a + b + c + d + e + f;
};

const flaggedSevereAnonymousParameters = (a: number, b: number, c: number, d: number, e: number, f: number, g: number, h: number, i: number): number =>
    a + b + c + d + e + f + g + h + i;

const flaggedAnonymousParameterForms = function (
    a: number,
    b: number = 0,
    c: number = 0,
    d: number = 0,
    e?: number,
    ...rest: number[]
): number {
    return a + b + c + d + (e ?? 0) + rest.length;
};

function nestedCallableBoundary(value: number): number {
    const callback = (item: number): number => item > 0 ? 1 : 0;
    return callback(value);
}
