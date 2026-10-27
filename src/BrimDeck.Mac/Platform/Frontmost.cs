using System.Diagnostics;
using BrimDeck.Core;

namespace BrimDeck.Mac.Platform;

public static class Frontmost
{
    public static (string Name, ScreenContext Context) Parse(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return ("", ScreenContext.Desktop);
        bool full = lines.Length > 1 && lines[1].Equals("true", StringComparison.OrdinalIgnoreCase);
        return (lines[0], full ? ScreenContext.Exclusive : ScreenContext.Desktop);
    }

    public static async Task<(string Name, ScreenContext Context)> ReadAsync(CancellationToken cancellation)
    {
        if (!OperatingSystem.IsMacOS()) return ("", ScreenContext.Desktop);
        var output = await RunAsync(cancellation);
        return Parse(output);
    }

    private const string Script = """
        tell application "System Events"
          set front to first application process whose frontmost is true
          set appName to name of front
          set full to false
          try
            set full to value of attribute "AXFullScreen" of window 1 of front
          end try
          return appName & linefeed & full
        end tell
        """;

    private static async Task<string> RunAsync(CancellationToken cancellation)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "osascript";
            process.StartInfo.ArgumentList.Add("-e");
            process.StartInfo.ArgumentList.Add(Script);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            if (!process.Start()) return "";
            var output = await process.StandardOutput.ReadToEndAsync(cancellation);
            await process.WaitForExitAsync(cancellation);
            return output;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { return ""; }
    }
}
