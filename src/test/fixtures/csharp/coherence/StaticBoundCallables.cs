using System;

class StaticOperations
{
    static Func<DateTime, DateTime> ParseDate = (DateTime value) => value;
    static Func<decimal, string> FormatNumber = (decimal value) => value.ToString();
    static Predicate<Guid> TestIdentifier = (Guid value) => value != Guid.Empty;
}
