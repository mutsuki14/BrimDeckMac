using System.Globalization;

namespace BrimDeck.Mac.Platform;

public readonly record struct CpuTimes(long Idle, long Total)
{
    public static double? Share(CpuTimes previous, CpuTimes current)
    {
        long idle = current.Idle - previous.Idle, total = current.Total - previous.Total;
        if (idle < 0 || total <= 0 || idle > total) return null;
        return Math.Clamp(1 - (double)idle / total, 0, 1);
    }
}

// Text parsers for /proc, vm_stat, netstat and ps. The sampler keeps the previous counters.
public static class Readings
{
    public static (CpuTimes Total, IReadOnlyList<(string Name, CpuTimes Times)> Cores) ParseProcStat(string text)
    {
        CpuTimes total = new(0, 0);
        var cores = new List<(string, CpuTimes)>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("cpu", StringComparison.Ordinal)) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;
            long Field(int index) => index < parts.Length && long.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
            long user = Field(1), nice = Field(2), system = Field(3), idle = Field(4), iowait = Field(5);
            long irq = Field(6), soft = Field(7), steal = Field(8);
            var times = new CpuTimes(idle + iowait, user + nice + system + idle + iowait + irq + soft + steal);
            if (parts[0] == "cpu") total = times;
            else cores.Add((parts[0], times));
        }
        return (total, cores);
    }

    public static (ulong Total, ulong Available, ulong Free, ulong Cached, ulong SwapTotal, ulong SwapFree, ulong Committed, ulong CommitLimit)? ParseMeminfo(string text)
    {
        var map = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            int colon = raw.IndexOf(':');
            if (colon <= 0) continue;
            var name = raw[..colon].Trim();
            var number = raw[(colon + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (number is not null && ulong.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) map[name] = value * 1024;
        }
        if (!map.TryGetValue("MemTotal", out var total) || total == 0) return null;
        map.TryGetValue("MemAvailable", out var available);
        map.TryGetValue("MemFree", out var free);
        map.TryGetValue("Cached", out var cached);
        map.TryGetValue("Buffers", out var buffers);
        map.TryGetValue("SwapTotal", out var swapTotal);
        map.TryGetValue("SwapFree", out var swapFree);
        map.TryGetValue("Committed_AS", out var committed);
        map.TryGetValue("CommitLimit", out var commitLimit);
        return (total, available, free, cached + buffers, swapTotal, swapFree, committed, commitLimit);
    }

    public static (ulong Received, ulong Sent) ParseProcNet(string text)
    {
        ulong rx = 0, tx = 0;
        foreach (var raw in text.Split('\n'))
        {
            int colon = raw.IndexOf(':');
            if (colon <= 0) continue;
            var name = raw[..colon].Trim();
            if (name is "lo" || name.StartsWith("lo", StringComparison.Ordinal)) continue;
            var fields = raw[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 9) continue;
            if (ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var received)) rx += received;
            if (ulong.TryParse(fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sent)) tx += sent;
        }
        return (rx, tx);
    }

    public static (int Pid, string Name, long Ticks, long RssPages)? ParseProcStatLine(string text)
    {
        int open = text.IndexOf('('), close = text.LastIndexOf(')');
        if (open <= 0 || close <= open) return null;
        if (!int.TryParse(text[..open].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) return null;
        var name = text[(open + 1)..close];
        var fields = text[(close + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 22) return null;
        if (!long.TryParse(fields[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var utime)) return null;
        if (!long.TryParse(fields[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stime)) return null;
        if (!long.TryParse(fields[21], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rss)) return null;
        return (pid, name, utime + stime, rss);
    }

    // `vm_stat` on macOS. Page size is read from the header; counts are in pages.
    public static (ulong PageSize, ulong Free, ulong Active, ulong Inactive, ulong Wired, ulong Compressed, ulong Speculative)? ParseVmStat(string text)
    {
        ulong page = 4096;
        var header = text.Split('\n').FirstOrDefault() ?? "";
        var size = System.Text.RegularExpressions.Regex.Match(header, @"(\d+)\s+bytes");
        if (size.Success && ulong.TryParse(size.Groups[1].Value, out var parsed) && parsed > 0) page = parsed;
        ulong Read(string label)
        {
            foreach (var raw in text.Split('\n'))
            {
                if (!raw.TrimStart().StartsWith(label, StringComparison.Ordinal)) continue;
                var digits = new string(raw.Where(ch => char.IsDigit(ch)).ToArray());
                return ulong.TryParse(digits, out var value) ? value : 0;
            }
            return 0;
        }
        if (!text.Contains("Pages free", StringComparison.Ordinal)) return null;
        return (page, Read("Pages free"), Read("Pages active"), Read("Pages inactive"), Read("Pages wired down"), Read("Pages occupied by compressor"), Read("Pages speculative"));
    }

    public static (ulong Received, ulong Sent) ParseNetstat(string text)
    {
        ulong rx = 0, tx = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var fields = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 10 || fields[0] is "Name") continue;
            if (fields[0] is "lo0" || !fields[2].StartsWith("<Link", StringComparison.Ordinal)) continue;
            if (!seen.Add(fields[0])) continue;
            // Name Mtu Network Address Ipkts Ierrs Ibytes Opkts Oerrs Obytes
            if (ulong.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var received)) rx += received;
            if (fields.Length > 9 && ulong.TryParse(fields[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sent)) tx += sent;
        }
        return (rx, tx);
    }

    public static List<(int Pid, double Cpu, ulong RssBytes, string Name)> ParsePs(string text)
    {
        var rows = new List<(int, double, ulong, string)>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var cpu)) continue;
            if (!ulong.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rssKb)) continue;
            rows.Add((pid, cpu, rssKb * 1024, parts[3].Trim()));
        }
        return rows;
    }

    public static uint[] ParseCpuTicks(string text)
        => text.Split([' ', '\n', '\t', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => uint.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : (uint?)null)
            .Where(value => value is not null).Select(value => value!.Value).ToArray();
}
