class CleanCommonNumbers
{
    const int MaxRetries = 5;
// clean — not flagged by magic number
    static int CleanCommonValues(int x) => x * 1;
}

class CleanNegativeNumbers
{
// clean — not flagged by magic number
    static int CleanNegativeValue(bool flag) => flag ? -1 : 1;
}

class FlaggedNumbers
{
// flagged — magic number
    static double FlaggedMagicNumbers(double price)
    {
        double total = price * 1.08;
        return total > 50 ? total + 15.75 : total;
    }
}
