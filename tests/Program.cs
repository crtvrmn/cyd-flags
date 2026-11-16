using System;

internal static class Program
{
    private static int Main()
    {
        int failures = CydFlagsTests.Run();
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
