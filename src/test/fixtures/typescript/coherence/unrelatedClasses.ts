// decision: two classes with no shared inheritance, no type cross-reference, and no shared
// naming affix - a real grab-bag that should be flagged even though each class is small and
// internally cohesive.

class Logger {
    prefix: string;

    constructor(prefix: string) {
        this.prefix = prefix;
    }

    log(message: string): void {
        console.log(this.prefix, message);
    }
}

class HttpClient {
    baseUrl: string;

    constructor(baseUrl: string) {
        this.baseUrl = baseUrl;
    }

    get(path: string): string {
        return path;
    }
}
