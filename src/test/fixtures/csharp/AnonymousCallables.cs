using System;

class Operations
{
    static Func<int, int, int> ClassBound = (left, right) => left + right;

    static Predicate<int> ClassMethod = delegate(int value)
    {
        return value > 0;
    };

    int NamedMethod(int value)
    {
        return value;
    }

    void Outer(int[] items)
    {
        Func<int, int> localBound = value => value * 2;
        Array.ConvertAll(items, item => item + 1);
    }
}
