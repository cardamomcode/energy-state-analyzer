class LogicalControlFlow
{
    static void CleanExplicitIf(bool isLoggedIn)
    {
        if (isLoggedIn) Navigate();
    }

    static bool Navigate() => true;
}
