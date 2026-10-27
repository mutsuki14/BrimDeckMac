namespace BrimDeck.Mac.Platform;

public static class LoginItems
{
    public const string Label = "app.brimdeck.mac";

    public static string AgentPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", Label + ".plist");

    public static string LinuxDesktopPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", "brimdeck.desktop");

    public static void Apply(bool enabled)
    {
        try
        {
            if (OperatingSystem.IsMacOS()) ApplyFile(AgentPath, enabled, BuildPlist(Environment.ProcessPath ?? ""));
            else if (OperatingSystem.IsLinux()) ApplyFile(LinuxDesktopPath, enabled, BuildDesktop(Environment.ProcessPath ?? ""));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static string BuildPlist(string executable) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0"><dict>
          <key>Label</key><string>{Label}</string>
          <key>ProgramArguments</key><array><string>{Escape(executable)}</string></array>
          <key>RunAtLoad</key><true/>
          <key>LimitLoadToSessionType</key><string>Aqua</string>
        </dict></plist>
        """;

    public static string BuildDesktop(string executable) => $"""
        [Desktop Entry]
        Type=Application
        Name=BrimDeck
        Exec={executable}
        X-GNOME-Autostart-enabled=true
        """;

    private static void ApplyFile(string path, bool enabled, string contents)
    {
        if (!enabled)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal);
}
