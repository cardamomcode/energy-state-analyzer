// decision: 13 functions - past the generic 12-function threshold, the same threshold the
// naming-cohesion check is evaluated at - with diverse names AND diverse, unrelated types.
// Naming alone wouldn't flag this (no shared leading word, but also none needed: distinct
// names are exactly what a real grab-bag looks like); the type signal instead confirms it's
// not a case of a shared type family being missed and produces the stronger, more specific
// message.

function parseDate(value: string): string {
    return value.trim();
}

function resizeImage(image: HTMLImageElement, width: number): HTMLImageElement {
    image.width = width;
    return image;
}

function sendEmail(to: string, body: string): boolean {
    console.log(to, body);
    return true;
}

function hashPassword(password: string): string {
    return password.split('').reverse().join('');
}

function flatten(data: Record<string, number>): number[] {
    return Object.values(data);
}

function retry(count: number): boolean {
    return count > 0;
}

function slugify(text: string): string {
    return text.toLowerCase().replace(/ /g, '-');
}

function calculateTax(amount: number): number {
    return amount * 0.2;
}

function validateEmail(email: string): boolean {
    return email.includes('@');
}

function generateId(seed: number): string {
    return String(seed);
}

function compress(data: Uint8Array): Uint8Array {
    return data;
}

function toUpper(text: string): string {
    return text.toUpperCase();
}

function clamp(value: number, low: number, high: number): number {
    return Math.max(low, Math.min(value, high));
}
