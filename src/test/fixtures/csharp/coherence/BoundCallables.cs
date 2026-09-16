using System;

class Operations
{
    Func<DateTime, DateTime> ClassBound = (DateTime value) => value;

    decimal NamedMethod(Guid value)
    {
        return value.GetHashCode();
    }

    void Outer(int[] items)
    {
        Func<int, int> localBound = value => value;
        Array.ConvertAll(items, item => item);
    }
}
