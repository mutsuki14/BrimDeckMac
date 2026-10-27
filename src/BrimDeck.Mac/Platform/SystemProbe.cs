using System.Diagnostics;
using System.Globalization;
using BrimDeck.Core;

namespace BrimDeck.Mac.Platform;

public sealed class SystemProbe
{
    private readonly List<double> _cpu = [], _down = [], _up = [];
    private CpuTimes _linuxTotal;
    private List<(string Name, CpuTimes Times)> _linuxCores = [];
    private (ulong Rx, ulong Tx, DateTimeOffset At)? _net;
    private Dictionary<int, (long Ticks, DateTimeOffset At)> _tasks = [];
    private uint[] _macTicks = [];
    private bool _linuxReady, _macReady;

    public MonitorSnapshot Read()
    {
        try
        {
            if (OperatingSystem.IsLinux()) return ReadLinux();
            if (OperatingSystem.IsMacOS()) return ReadMac();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        return MonitorSnapshot.Empty(Math.Max(1, Environment.ProcessorCount), Loc.T("这台系统暂时读不到占用", "Usage cannot be read on this system yet"));
    }

    private MonitorSnapshot ReadLinux()
    {
        var stat = Readings.ParseProcStat(File.ReadAllText("/proc/stat"));
        var memory = Readings.ParseMeminfo(File.ReadAllText("/proc/meminfo"));
        var net = Readings.ParseProcNet(File.ReadAllText("/proc/net/dev"));
        var now = DateTimeOffset.Now;
        double? cpu = _linuxReady ? CpuTimes.Share(_linuxTotal, stat.Total) : null;
        var cores = new List<CoreSample>();
        for (int i = 0; i < stat.Cores.Count; i++)
        {
            double usage = 0;
            if (_linuxReady && i < _linuxCores.Count && _linuxCores[i].Name == stat.Cores[i].Name)
                usage = CpuTimes.Share(_linuxCores[i].Times, stat.Cores[i].Times) ?? 0;
            cores.Add(new CoreSample(i, 0, CoreTier.Unknown, i + 1, usage, Reading<double>.Unavailable(""), null));
        }
        _linuxTotal = stat.Total;
        _linuxCores = stat.Cores.ToList();
        _linuxReady = true;
        var processes = ReadLinuxProcesses(now);
        return Finish(now, cores, cpu, memory is { } mem ? MemoryOf(mem) : null, Rate(net.Received, net.Sent, now), processes,
            Loc.T("Linux 不报告独立显卡占用", "Linux does not report a discrete GPU here"));
    }

    private List<ProcessSample> ReadLinuxProcesses(DateTimeOffset now)
    {
        var list = new List<ProcessSample>();
        var next = new Dictionary<int, (long, DateTimeOffset)>();
        long page = Environment.SystemPageSize;
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            var name = Path.GetFileName(dir);
            if (!int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) continue;
            try
            {
                var parsed = Readings.ParseProcStatLine(File.ReadAllText(Path.Combine(dir, "stat")));
                if (parsed is not { } row) continue;
                next[pid] = (row.Ticks, now);
                double cpu = 0;
                if (_tasks.TryGetValue(pid, out var previous))
                {
                    double seconds = (now - previous.At).TotalSeconds;
                    long delta = row.Ticks - previous.Ticks;
                    if (seconds > 0 && delta >= 0) cpu = delta / 100d / seconds * 100;
                }
                if (cpu < 0.05 && row.RssPages < 1024) continue;
                list.Add(new ProcessSample(pid, row.Name, cpu, (ulong)Math.Max(0, row.RssPages) * (ulong)page, row.Name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        _tasks = next;
        return list;
    }

    private MonitorSnapshot ReadMac()
    {
        var now = DateTimeOffset.Now;
        var ticks = Readings.ParseCpuTicks(Command("sysctl", "-n", "kern.cp_time"));
        double? cpu = null;
        if (_macReady && ticks.Length >= 4 && _macTicks.Length >= 4)
        {
            long idle = ticks[3] - _macTicks[3];
            long elapsed = 0;
            for (int i = 0; i < Math.Min(4, ticks.Length); i++) elapsed += (long)ticks[i] - _macTicks[i];
            if (elapsed > 0 && idle >= 0 && idle <= elapsed) cpu = 1 - (double)idle / elapsed;
        }
        _macTicks = ticks;
        _macReady = ticks.Length >= 4;
        int count = Math.Max(1, Environment.ProcessorCount);
        var cores = Enumerable.Range(0, count).Select(i => new CoreSample(i, 0, CoreTier.Unknown, i + 1, cpu ?? 0, Reading<double>.Unavailable(""), null)).ToList();
        MemorySample? memory = null;
        if (ulong.TryParse(Command("sysctl", "-n", "hw.memsize").Trim(), out var total) && Readings.ParseVmStat(Command("vm_stat")) is { } vm)
        {
            ulong used = (vm.Active + vm.Wired + vm.Compressed) * vm.PageSize;
            ulong cached = (vm.Inactive + vm.Speculative) * vm.PageSize;
            if (used > total) used = total;
            memory = new MemorySample(total, used, used, total, Math.Min(cached, total - used));
        }
        var net = Readings.ParseNetstat(Command("netstat", "-ib"));
        var processes = Readings.ParsePs(Command("ps", "-axo", "pid=,pcpu=,rss=,comm="))
            .Where(row => row.Cpu >= 0.1 || row.RssBytes >= 50 * 1024 * 1024)
            .Select(row => new ProcessSample(row.Pid, Path.GetFileName(row.Name), row.Cpu, row.RssBytes, Path.GetFileName(row.Name)))
            .ToList();
        return Finish(now, cores, cpu, memory, Rate(net.Received, net.Sent, now), processes,
            Loc.T("macOS 不提供无需授权的 GPU 占用", "macOS does not report GPU usage without extra permission"));
    }

    private (double Down, double Up)? Rate(ulong rx, ulong tx, DateTimeOffset now)
    {
        (double, double)? rate = null;
        if (_net is { } previous)
        {
            double seconds = (now - previous.At).TotalSeconds;
            var down = MonitorMetrics.Rate(previous.Rx, rx, seconds);
            var up = MonitorMetrics.Rate(previous.Tx, tx, seconds);
            if (down is not null && up is not null) rate = (down.Value, up.Value);
        }
        _net = (rx, tx, now);
        return rate;
    }

    private MonitorSnapshot Finish(DateTimeOffset now, List<CoreSample> cores, double? cpu, MemorySample? memory, (double Down, double Up)? net, List<ProcessSample> processes, string gpuReason)
    {
        if (cpu is { } share) Push(_cpu, share);
        if (net is { } rate) { Push(_down, rate.Down); Push(_up, rate.Up); }
        var cpuReading = cpu is null || cores.Count == 0
            ? Reading<CpuSample>.Unavailable(Loc.T("还没有第二次采样", "Waiting for a second sample"))
            : Reading<CpuSample>.Of(new CpuSample(cores, Reading<double>.Unavailable("")));
        var memoryReading = memory is { } known ? Reading<MemorySample>.Of(known) : Reading<MemorySample>.Unavailable(Loc.T("读不到内存", "Memory was not read"));
        var netReading = net is { } speed ? Reading<NetworkSample>.Of(new NetworkSample(speed.Down, speed.Up)) : Reading<NetworkSample>.Unavailable(Loc.T("还没有第二次采样", "Waiting for a second sample"));
        var ranked = new ProcessList(ProcessSample.Folded(processes, ProcessSort.Cpu).Take(12).ToList(), ProcessSample.Folded(processes, ProcessSort.Memory).Take(12).ToList());
        return new MonitorSnapshot(now, Math.Max(1, cores.Count), cpuReading, memoryReading, netReading, Reading<GpuSample>.Unavailable(gpuReason), Reading<ProcessList>.Of(ranked))
        {
            CpuHistory = _cpu.ToArray(), DownHistory = _down.ToArray(), UpHistory = _up.ToArray()
        };
    }

    private static MemorySample MemoryOf((ulong Total, ulong Available, ulong Free, ulong Cached, ulong SwapTotal, ulong SwapFree, ulong Committed, ulong CommitLimit) mem)
    {
        ulong used = mem.Available > 0 && mem.Available <= mem.Total ? mem.Total - mem.Available : mem.Total - Math.Min(mem.Free, mem.Total);
        ulong limit = mem.CommitLimit > 0 ? mem.CommitLimit : mem.Total + mem.SwapTotal;
        return new MemorySample(mem.Total, Math.Min(used, mem.Total), mem.Committed, limit, Math.Min(mem.Cached, mem.Total));
    }

    private static void Push(List<double> values, double value)
    {
        values.Add(value);
        if (values.Count > 40) values.RemoveAt(0);
    }

    private static string Command(string file, params string[] args)
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
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(2000)) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } return ""; }
            return output;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { return ""; }
    }
}
