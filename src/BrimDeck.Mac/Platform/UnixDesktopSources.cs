using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using BrimDeck.Core;
using Microsoft.Data.Sqlite;

namespace BrimDeck.Mac.Platform;

public sealed partial class UnixDesktopSources : IDesktopSources
{
    public string? ReadCursorToken(string database)
    {
        var rows = QueryDatabase(database, "SELECT value FROM ItemTable WHERE key = 'cursorAuth/accessToken' LIMIT 1");
        return rows is [[{ } token], ..] && token.Length > 0 ? token : null;
    }

    public IReadOnlyList<string?[]>? QueryDatabase(string database, string sql)
    {
        if (string.IsNullOrWhiteSpace(database) || !File.Exists(database)) return null;
        try
        {
            var builder = new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new List<string?[]>();
            while (reader.Read())
            {
                var row = new string?[reader.FieldCount];
                for (int i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                rows.Add(row);
            }
            return rows;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }

    public async Task<IReadOnlyList<LocalEndpoint>> FindAntigravityAsync(CancellationToken cancellation)
    {
        var processes = ParseAntigravity(await RunAsync("ps", ["-axww", "-o", "pid=,command="], cancellation));
        if (processes.Count == 0) return [];
        var listeners = OperatingSystem.IsLinux() ? ListenersFromProc() : ParseLsof(await RunAsync("lsof", ["-nP", "-iTCP", "-sTCP:LISTEN"], cancellation));
        var endpoints = new List<LocalEndpoint>();
        foreach (var (pid, token) in processes)
        {
            if (!listeners.TryGetValue(pid, out var ports)) continue;
            foreach (var port in ports.Distinct())
            {
                endpoints.Add(new LocalEndpoint(port, "https", token));
                endpoints.Add(new LocalEndpoint(port, "http", token));
            }
        }
        return endpoints;
    }

    public static List<(int Pid, string Token)> ParseAntigravity(string text)
    {
        var found = new List<(int, string)>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            int space = line.IndexOf(' ');
            if (space <= 0 || !int.TryParse(line[..space], out var pid)) continue;
            var command = line[space..];
            if (!command.Contains("language_server", StringComparison.OrdinalIgnoreCase) || !command.Contains("antigravity", StringComparison.OrdinalIgnoreCase)) continue;
            var match = Regex.Match(command, "--csrf_token(?:=|\\s+)\"?([^\\s\"]+)");
            if (match.Success) found.Add((pid, match.Groups[1].Value));
        }
        return found;
    }

    public static Dictionary<int, List<int>> ParseLsof(string text)
    {
        var map = new Dictionary<int, List<int>>();
        foreach (var raw in text.Split('\n'))
        {
            if (!raw.Contains("(LISTEN)", StringComparison.Ordinal)) continue;
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var pid)) continue;
            var address = parts[^1] == "(LISTEN)" && parts.Length >= 2 ? parts[^2] : parts[^1];
            int colon = address.LastIndexOf(':');
            if (colon < 0 || !int.TryParse(address[(colon + 1)..], out var port)) continue;
            if (!map.TryGetValue(pid, out var ports)) map[pid] = ports = [];
            ports.Add(port);
        }
        return map;
    }

    private static Dictionary<int, List<int>> ListenersFromProc()
    {
        var inodes = new Dictionary<string, int>();
        void Read(string path, bool v6)
        {
            if (!File.Exists(path)) return;
            foreach (var raw in File.ReadLines(path).Skip(1))
            {
                var fields = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 10 || !string.Equals(fields[3], "0A", StringComparison.OrdinalIgnoreCase)) continue;
                var local = fields[1].Split(':');
                if (local.Length != 2 || !int.TryParse(local[1], NumberStyles.HexNumber, null, out var port)) continue;
                inodes[fields[9]] = port;
            }
        }
        Read("/proc/net/tcp", false);
        Read("/proc/net/tcp6", true);
        var map = new Dictionary<int, List<int>>();
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid)) continue;
            var fd = Path.Combine(dir, "fd");
            if (!Directory.Exists(fd)) continue;
            try
            {
                foreach (var link in Directory.EnumerateFiles(fd))
                {
                    string target;
                    try { target = new FileInfo(link).LinkTarget ?? ""; }
                    catch (IOException) { continue; }
                    const string prefix = "socket:[";
                    if (!target.StartsWith(prefix, StringComparison.Ordinal) || !target.EndsWith(']')) continue;
                    var inode = target[prefix.Length..^1];
                    if (!inodes.TryGetValue(inode, out var port)) continue;
                    if (!map.TryGetValue(pid, out var ports)) map[pid] = ports = [];
                    ports.Add(port);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return map;
    }

    private static async Task<string> RunAsync(string file, string[] args, CancellationToken cancellation)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = file;
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
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
