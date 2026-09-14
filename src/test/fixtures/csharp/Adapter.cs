using System.Collections.Generic;

namespace Sample;

class Derived : Base, IWorker
{
    private const int DefaultRetries = 3;

    public static string Process(string mode, int retries = 3, bool verbose = false)
    {
        try
        {
            foreach (var item in new[] { 1, 2, 3 })
            {
                if (verbose && item > 1)
                {
                    return $"processed {item}";
                }
                else if (mode == "dry-run")
                {
                    return "skipped";
                }
            }
        }
        catch (System.Exception)
        {
            return "failed";
        }
        finally
        {
            Cleanup();
        }

        Log(false);
        return "complete";
    }

    public Derived(int retries)
    {
    }

    private static void Cleanup()
    {
    }
}
