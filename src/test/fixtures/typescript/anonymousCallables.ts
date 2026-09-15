function named(value: number): number {
    return value;
}

export const moduleArrow = (left: number, right: number): number => left + right;
const moduleFunction = function (value: number): number {
    return value + 1;
};

class Operations {
    classBound = (value: number): number => value + 1;

    namedMethod(value: number): number {
        return value;
    }
}

function outer(items: number[]): number[] {
    const localBound = (value: number): number => value * 2;
    return items.map(item => item + 1);
}
