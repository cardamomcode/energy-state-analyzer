class ErrorHandling
{
    static int CleanPath(int value) => value + 1;

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
