using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MixedIssuesProject.Services;
using MixedIssuesProject.Utilities;

namespace MixedIssuesProject;

internal static class Program
{
    // Entry point kept conventional to avoid top-level statement quirks in tests.
    private static async Task Main(string[] args)
    {
        var worker = new BackgroundWorker();

        // MNA0001: async lambda converted to void-returning delegate (Action)
        // Analyzer reports because the delegate returns void and the lambda is async.
        // Expected: MNA0001 on the async lambda.
        Action onTick = async () =>
        {
            await Task.Delay(10);
        };

        onTick();

        // MNA0004: short local name "tmp" (weak token and length < min_length=4).
        // Expected: MNA0004 on 'tmp' with type-aware suggestions skipped here.
        int tmp = 42;

        // MNA0004: foreach variable "x" is short; check_foreach=true in .editorconfig.
        // Expected: MNA0004 on 'x' with collection naming hint.
        foreach (var x in MakeNumbers())
        {
            // Do something trivial to keep this used.
            if (x > 0) { }
        }

        // Call into a method with weak parameter names to trigger parameter diagnostics.
        var sum = Compute(1, 2);
        Console.WriteLine($"Sum: {sum}");

        // Demonstrate empty catch handling in helper.
        TryCatchHelper.RunSafely(() =>
        {
            throw new InvalidOperationException("boom");
        });

        // MNA0001: call async-void method, which will be flagged at its declaration site.
        worker.Start();

        await Task.Delay(10);
    }

    private static IEnumerable<int> MakeNumbers()
    {
        return new[] { 1, 2, 3 };
    }

    // MNA0004 x2: parameters 'a' and 'b1' violate min_length=4 and weak token rules.
    private static int Compute(int a, int b1) => a + b1;
}
