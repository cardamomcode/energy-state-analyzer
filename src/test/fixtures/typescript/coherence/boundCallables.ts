function encode(value: number): number {
    return value;
}

const moduleBound = (value: string): string => value;

function outer(items: boolean[]): boolean[] {
    const localBound = (value: boolean): boolean => value;
    return items.map(item => item);
}

class Operations {
    classBound = (value: Date): Date => value;

    namedMethod(value: RegExp): Promise<void> {
        return Promise.resolve();
    }

    outerMethod(items: Set<number>): Map<number, number> {
        const localBound = (value: number): number => value;
        return new Map(Array.from(items, item => [item, item]));
    }
}
