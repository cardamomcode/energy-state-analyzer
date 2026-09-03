// decision: a class whose methods each solve a different problem (DB, email, PDF, imaging, auth,
// queueing...). Over the method-count bar AND spanning many unrelated domain types, so it is
// flagged as a god class. The marker types below exist only to be "unrelated" to each other.

class Connection {} // fixture-only marker types
class Row {}
class Image {}
class Report {}
class Token {}

export class GodService {
    private state: object = {};

    fetchRows(conn: Connection): Row {
        return null as unknown as Row;
    }

    sendEmail(to: string, body: string): boolean {
        return true;
    }

    renderPdf(data: Record<string, unknown>): Uint8Array {
        return new Uint8Array();
    }

    compress(path: string): string {
        return path;
    }

    validateToken(token: Token): boolean {
        return true;
    }

    notify(message: string): void {
        // noop
    }

    exportCsv(rows: Array<unknown>): string {
        return "";
    }

    resize(image: Image): Image {
        return image;
    }

    parseYaml(text: string): Record<string, unknown> {
        return {};
    }

    hashPassword(password: string): string {
        return "";
    }

    sendSms(number: string, message: string): boolean {
        return true;
    }

    buildReport(data: Record<string, unknown>): Report {
        return null as unknown as Report;
    }

    encrypt(raw: Uint8Array): Uint8Array {
        return raw;
    }

    schedule(job: object): string {
        return "";
    }

    cacheGet(key: string): object {
        return null as unknown as object;
    }

    logEvent(event: string): void {
        // noop
    }
}
