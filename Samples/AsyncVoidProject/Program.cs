using System.Threading.Tasks;

// Entry point is this top-level statement file
await Task.Delay(1);

// Code under test
class C
{
    async void M()
    {
        await Task.Delay(1);
    }
}