using System;
using System.Threading.Tasks;

// Top-level statement just to have a Main
await Task.CompletedTask;

static void WillBeFlagged()
{
    try
    {
        Console.WriteLine("work");
    }
    catch
    {
        // empty => should be flagged (MNA0002)
    }
}

static void CancellationCatchShouldBeIgnored()
{
    try
    {
        throw new TaskCanceledException();
    }
    catch (TaskCanceledException)
    {
        // empty but cancellation-derived => should NOT be flagged
    }
}
