class ErrorHandling
{
// clean — not flagged by error shadowing
    static int CleanPath(int value) => value + 1;

// clean — not flagged by error shadowing
    static int ShadowedByError(int value)
    {
        try
        {
            int first = value + 1;
            int second = first * 2;
            return second - 1;
        }
        catch (System.Exception)
        {
            return 0;
        }
    }
}
