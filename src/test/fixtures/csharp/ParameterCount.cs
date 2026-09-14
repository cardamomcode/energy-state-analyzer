class FewParameters
{
    static int CleanFewParams(int a, int b) => a + b;
}

class ManyParameters
{
    static int FlaggedManyParams(int a, int b, int c, int d, int e, int f) => a + b + c + d + e + f;
}

class TooManyParameters
{
    static int FlaggedTooManyParams(int a, int b, int c, int d, int e, int f, int g, int h, int i) => a + b + c + d + e + f + g + h + i;
}
