class StaticOperations {
    static parseDate = (value: Date): Date => value;
    static formatNumber = (value: number): string => value.toString();
    static testPattern = (value: RegExp): boolean => value.test("");
}
