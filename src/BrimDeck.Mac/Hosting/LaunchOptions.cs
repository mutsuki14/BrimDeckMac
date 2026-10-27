namespace BrimDeck.Mac.Hosting;

public sealed class LaunchOptions
{
    public bool Preview { get; init; }
    public string? Language { get; init; }
    public string? ScreenshotDirectory { get; init; }

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        bool preview = false;
        string? language = null, screenshot = null;
        for (int i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--preview") preview = true;
            else if (arg is "--language" && i + 1 < args.Count) language = args[++i];
            else if (arg.StartsWith("--language=", StringComparison.Ordinal)) language = arg["--language=".Length..];
            else if (arg is "--screenshot" && i + 1 < args.Count) screenshot = args[++i];
            else if (arg.StartsWith("--screenshot=", StringComparison.Ordinal)) screenshot = arg["--screenshot=".Length..];
        }
        return new LaunchOptions { Preview = preview, Language = language, ScreenshotDirectory = screenshot };
    }
}
