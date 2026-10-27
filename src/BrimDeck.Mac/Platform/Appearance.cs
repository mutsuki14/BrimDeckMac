namespace BrimDeck.Mac.Platform;

public static class Appearance
{
    public static bool IsDark()
    {
        if (OperatingSystem.IsMacOS())
        {
            var style = Run("defaults", "read", "-g", "AppleInterfaceStyle");
            return style.Contains("Dark", StringComparison.OrdinalIgnoreCase);
        }
        var theme = Environment.GetEnvironmentVariable("GTK_THEME") ?? "";
        return theme.Contains("dark", StringComparison.OrdinalIgnoreCase);
    }

    private static string Run(string file, params string[] args)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = file;
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            if (!process.Start()) return "";
            if (!process.WaitForExit(1500)) { TryKill(process); return ""; }
            return process.StandardOutput.ReadToEnd();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { return ""; }
    }

    private static void TryKill(System.Diagnostics.Process process)
    {
        try { process.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }
}
