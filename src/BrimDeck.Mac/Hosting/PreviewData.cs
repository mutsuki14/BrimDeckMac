using BrimDeck.Core;

namespace BrimDeck.Mac.Hosting;

public static class PreviewData
{
    public static List<ProviderSnapshot> Snapshots(DateTimeOffset now)
    {
        ProviderSnapshot Column(ProviderId id, string plan, (string Label, double Used, int Hours)[] quotas, long todayTokens, decimal todayCost, long weekTokens, decimal weekCost)
        {
            var snapshot = new ProviderSnapshot(id)
            {
                Plan = plan,
                Status = Loc.T("已连接", "Connected"),
                StatusLabel = Loc.T("已连接", "Connected"),
                LiveQuota = true,
                UsageAvailable = true,
                UsageComplete = true,
                QuotaTime = now,
                Quotas = quotas.Select(quota => new Quota(quota.Label, quota.Used, now.AddHours(quota.Hours))).ToList(),
                Entries =
                [
                    new TokenEntry("today", now, id + "-preview", todayTokens / 2, 0, 0, 0, todayTokens / 2, todayCost),
                    // The seven-day total includes today, so the earlier records are the remainder.
                    new TokenEntry("week", now.AddDays(-3), id + "-preview", (weekTokens - todayTokens) / 2, 0, 0, 0, (weekTokens - todayTokens) / 2, weekCost - todayCost)
                ]
            };
            return snapshot;
        }
        return
        [
            Column(ProviderId.Claude, "Max", [("5 小时额度", 34, 2), ("每周额度", 58, 77)], 26_400_000, 17.84m, 163_890_000, 110.75m),
            Column(ProviderId.Codex, "Plus", [("5 小时额度", 22, 4), ("每周额度", 41, 98)], 11_200_000, 6.37m, 67_680_000, 38.50m),
            Column(ProviderId.Cursor, "Pro", [("Cursor 模型", 76, 294), ("其他模型", 18, 294)], 3_900_000, 2.41m, 24_130_000, 14.91m),
            Column(ProviderId.Antigravity, "Pro", [("Gemini · 5 小时额度", 12, 5), ("Gemini · 每周额度", 29, 121)], 5_600_000, 3.12m, 38_260_000, 21.31m)
        ];
    }

    public static MediaTrack Track(DateTimeOffset now) => new()
    {
        Id = "preview",
        Source = "Music",
        Title = Loc.T("夜航", "Night Flight"),
        Artist = Loc.T("海岸线", "Coastline"),
        Album = Loc.T("预览", "Preview"),
        State = MediaState.Playing,
        CanToggle = true,
        CanNext = true,
        CanPrevious = true,
        CanSeek = true,
        Start = TimeSpan.Zero,
        End = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(42),
        ReportedPosition = TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(16),
        PositionAt = now,
        PositionKnown = true,
        Rate = 1,
        CurrentLyric = Loc.T("灯火沿着海岸慢慢亮起来", "Lights come on along the coast")
    };

    public static MonitorSnapshot Monitor(DateTimeOffset now)
    {
        var cores = Enumerable.Range(0, 8).Select(i =>
        {
            double usage = 0.18 + 0.55 * Math.Abs(Math.Sin(i * 0.9));
            return new CoreSample(i, 0, i < 4 ? CoreTier.Performance : CoreTier.Efficiency, (i % 4) + 1, Math.Clamp(usage, 0, 1), Reading<double>.Of(2400 + i * 80), 3500);
        }).ToList();
        var cpu = new CpuSample(cores, Reading<double>.Of(3200));
        var memory = new MemorySample(32L * 1024 * 1024 * 1024, 18L * 1024 * 1024 * 1024, 20L * 1024 * 1024 * 1024, 40L * 1024 * 1024 * 1024, 6L * 1024 * 1024 * 1024);
        var processes = new List<ProcessSample>
        {
            new(101, "Cursor", 86, 2_400_000_000, "Cursor"),
            new(102, "Music", 12, 420_000_000, "Music"),
            new(103, "Safari", 9, 1_100_000_000, "Safari"),
            new(104, "BrimDeck", 2, 180_000_000, "BrimDeck")
        };
        return new MonitorSnapshot(now, cores.Count, cpu, memory, new NetworkSample(1_250_000, 86_000), Reading<GpuSample>.Unavailable(Loc.T("预览不读取显卡", "Preview does not read the GPU")),
            new ProcessList(ProcessSample.Folded(processes, ProcessSort.Cpu), ProcessSample.Folded(processes, ProcessSort.Memory)))
        {
            CpuHistory = [0.22, 0.28, 0.31, 0.27, 0.41, 0.36],
            DownHistory = [400_000, 800_000, 1_250_000],
            UpHistory = [40_000, 70_000, 86_000]
        };
    }
}
