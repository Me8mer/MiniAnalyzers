using System;
using System.Threading.Tasks;

public class C
{
    // MNA0001: another async void (total now 2)
    public async void Bad() { await Task.Delay(1); }

    public void Use()
    {
        // MNA0004: weak name (total now 3)
        var tmp = 0;

        // MNA0003: general Console write (total now 2)
        Console.WriteLine("[APP] Library");
        _ = tmp;
    }
}
