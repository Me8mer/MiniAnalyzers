using System;
using System.Threading.Tasks;

class Button
{
    public event EventHandler? Click;
    public void Raise() => Click?.Invoke(this, EventArgs.Empty);
}

class Program
{
    static async Task Main()
    {
        var button = new Button();

        // This async lambda is compiled as async void when assigned to EventHandler.
        button.Click += async (sender, args) =>
        {
            await Task.Delay(1);
        };

        button.Raise();
    }
}
