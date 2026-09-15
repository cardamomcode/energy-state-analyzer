using System;

class CleanAnonymousCallable
{
    static Func<int, int, int> CleanAnonymousCallable = (first, second) => first + second;
}

class MediumAnonymousCyclomatic
{
    static Func<int, int> FlaggedAnonymousCyclomatic = value =>
    {
        if (value == 0) return 0;
        if (value == 1) return 1;
        if (value == 2) return 2;
        if (value == 3) return 3;
        if (value == 4) return 4;
        if (value == 5) return 5;
        if (value == 6) return 6;
        if (value == 7) return 7;
        if (value == 8) return 8;
        if (value == 9) return 9;
        if (value == 10) return 10;
        return value;
    };
}

class HighAnonymousCyclomatic
{
    static Func<int, int> FlaggedSevereAnonymousCyclomatic = value =>
    {
        if (value == 0) return 0;
        if (value == 1) return 1;
        if (value == 2) return 2;
        if (value == 3) return 3;
        if (value == 4) return 4;
        if (value == 5) return 5;
        if (value == 6) return 6;
        if (value == 7) return 7;
        if (value == 8) return 8;
        if (value == 9) return 9;
        if (value == 10) return 10;
        if (value == 11) return 11;
        if (value == 12) return 12;
        if (value == 13) return 13;
        if (value == 14) return 14;
        if (value == 15) return 15;
        return value;
    };
}

class MediumAnonymousCognitive
{
    static Func<int, int> FlaggedAnonymousCognitive = value =>
    {
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
}

class HighAnonymousCognitive
{
    static Func<int, int> FlaggedSevereAnonymousCognitive = value =>
    {
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
}

class MediumAnonymousParameters
{
    delegate int SixParameters(int a, int b, int c, int d, int e, int f);
    static SixParameters FlaggedAnonymousParameters = delegate(int a, int b, int c, int d, int e, int f)
    {
        return a + b + c + d + e + f;
    };
}

class HighAnonymousParameters
{
    delegate int NineParameters(int a, int b, int c, int d, int e, int f, int g, int h, int i);
    static NineParameters FlaggedSevereAnonymousParameters =
        (a, b, c, d, e, f, g, h, i) => a + b + c + d + e + f + g + h + i;
}

class AnonymousParameterForms
{
    delegate int ParameterForms(int a, int b, int c, int d, int e, int f);
    static ParameterForms FlaggedAnonymousParameterForms =
        (int a, int b = 0, int c = 0, int d = 0, int e = 0, int f = 0) =>
            a + b + c + d + e + f;
}

class NestedCallableExample
{
    static int NestedCallableBoundary(int value)
    {
        Func<int, int> callback = item => item > 0 ? 1 : 0;
        return callback(value);
    }
}
