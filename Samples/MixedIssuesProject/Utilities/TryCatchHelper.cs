using System;

namespace MixedIssuesProject.Utilities;

public static class TryCatchHelper
{
    public static void RunSafely(Action action)
    {
        try
        {
            action();
        }
        // MNA0002: empty catch block swallows exceptions entirely.
        // Not OperationCanceledException, so it is not ignored.
        catch (Exception)
        {
        }
    }
}
