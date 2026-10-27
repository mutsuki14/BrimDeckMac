using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using BrimDeck.Mac.Hosting;
using BrimDeck.Mac.Platform;
using BrimDeck.Mac.Views;

namespace BrimDeck.Mac;

public sealed class App : Application
{
    public static LaunchOptions Options { get; private set; } = new();
    public Session? Session { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Options = LaunchOptions.Parse(desktop.Args ?? []);
            AppPaths.RecordCrashes();
            Session = new Session(Options);
            var island = new IslandWindow(Session);
            desktop.MainWindow = island;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Tray = BuildTray(Session, island, desktop);
            Tray.IsVisible = true;
            desktop.Exit += (_, _) => Session.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }

    public TrayIcon? Tray { get; private set; }

    private static TrayIcon BuildTray(Session session, IslandWindow island, IClassicDesktopStyleApplicationLifetime desktop)
    {
        NativeMenuItem item(string text, Action click)
        {
            var entry = new NativeMenuItem(text);
            entry.Click += (_, _) => click();
            return entry;
        }
        var menu = new NativeMenu();
        menu.Add(item(L("打开", "Open"), () => island.Reveal()));
        menu.Add(item(L("设置", "Settings"), () => island.OpenSettings()));
        menu.Add(item(session.Hidden ? L("显示", "Show") : L("隐藏", "Hide"), () => island.ToggleHidden()));
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(item(L("退出", "Quit"), () => desktop.Shutdown()));
        var tray = new TrayIcon
        {
            ToolTipText = "BrimDeck",
            Menu = menu,
            Icon = LoadIcon()
        };
        tray.Clicked += (_, _) => island.Reveal();
        session.Changed += () =>
        {
            var track = session.Track;
            tray.ToolTipText = track is { State: Core.MediaState.Playing, Title.Length: > 0 } ? "BrimDeck · " + track.Caption : "BrimDeck";
        };
        return tray;
    }

    private static WindowIcon? LoadIcon()
    {
        try
        {
            var uri = new Uri("avares://BrimDeck/Assets/icon.png");
            if (AssetLoader.Exists(uri)) return new WindowIcon(AssetLoader.Open(uri));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException) { }
        return null;
    }

    private static string L(string chinese, string english) => Core.Loc.T(chinese, english);
}
