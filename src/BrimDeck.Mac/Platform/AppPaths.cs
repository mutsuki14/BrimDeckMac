using System.Globalization;
using BrimDeck.Core;

namespace BrimDeck.Mac.Platform;

public static class AppPaths
{
    public static string DataDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS()) return Path.Combine(home, "Library", "Application Support", "BrimDeck");
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local)) local = Path.Combine(home, ".local", "share");
            return Path.Combine(local, "BrimDeck");
        }
    }

    public static string ThemeDirectory => Path.Combine(DataDirectory, "themes");

    // Cursor on macOS keeps its state database under Library/Application Support. Linux already uses
    // ~/.config, which is what .NET reports as ApplicationData there.
    public static DataLocations Locations()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? claude = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        string? codex = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? zcode = Environment.GetEnvironmentVariable("ZCODE_HOME");
        string? dsh = Environment.GetEnvironmentVariable("DSH_HOME");
        string? kimi = Environment.GetEnvironmentVariable("KIMI_CODE_HOME");
        var kimiEnvironment = new KimiEnvironment(
            Environment.GetEnvironmentVariable("KIMI_CODE_BASE_URL"),
            Environment.GetEnvironmentVariable("KIMI_CODE_OAUTH_HOST") ?? Environment.GetEnvironmentVariable("KIMI_OAUTH_HOST"),
            Environment.GetEnvironmentVariable("KIMI_CODE_REGION_MARKER"));
        if (!OperatingSystem.IsMacOS()) return DataLocations.Detect();
        var support = Path.Combine(home, "Library", "Application Support");
        return DataLocations.Resolve(home, support, support, claude, codex, zcode, dsh, kimi) with { KimiEnvironment = kimiEnvironment };
    }

    public static void LogCrash(object error)
    {
        try
        {
            var log = Path.Combine(DataDirectory, "crash.log");
            Directory.CreateDirectory(DataDirectory);
            if (File.Exists(log) && new FileInfo(log).Length > 1_048_576) File.Delete(log);
            File.AppendAllText(log, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {ApplicationVersion.Current}\n{error}\n\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static void RecordCrashes()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, args) => { LogCrash(args.Exception); args.SetObserved(); };
    }

    public static void UseLanguage(string language)
    {
        Loc.Use(language);
        var culture = Loc.Culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
