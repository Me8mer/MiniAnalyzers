using System.Threading.Tasks;

namespace MixedIssuesProject.Services;

public sealed class BackgroundWorker
{
    // MNA0004: weak field name 'val' (included in weak_names list).
    private int val;

    // MNA0001: async void ordinary method. Not an event handler signature.
    public async void Start()
    {
        await Task.Delay(5);
        val++;
    }
}
