using Avalonia;
using BrimDeck.Mac.Hosting;
using BrimDeck.Mac.Platform;

namespace BrimDeck.Mac;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);
        if (!InstanceLock.Acquire(options)) return;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppPaths.LogCrash(ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
