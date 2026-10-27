using BrimDeck.Core;
using BrimDeck.Mac.Drawing;
using BrimDeck.Mac.Hosting;
using BrimDeck.Mac.Platform;

var passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

var notch = IslandOutline.Build(238, 32, 16, 0, 0, 12, 12);
Check("A notch starts on the screen edge", notch.Count > 4 && notch[0].X == 0 && notch[0].Y == 0);
Check("A notch waist is inset by the shoulder", notch.Any(point => Math.Abs(point.X - (238 - 16)) < 0.01 && point.Y > 0 && point.Y < 32));
Check("A notch reaches its bottom edge", notch.Any(point => Math.Abs(point.Y - 32) < 0.01));
var capsule = IslandOutline.Build(196, 32, 0, 16, 16, 16, 16);
Check("A capsule has no shoulder inset", capsule.All(point => point.X is >= -0.01 and <= 196.01));
Check("A capsule rounds the top", capsule.Any(point => Math.Abs(point.X - 196) < 0.01 && point.Y > 0));

var stat = """
    cpu  100 0 50 750 100 0 0 0 0 0
    cpu0 40 0 20 300 40 0 0 0 0 0
    cpu1 60 0 30 450 60 0 0 0 0 0
    """;
var parsed = Readings.ParseProcStat(stat);
Check("proc stat keeps the aggregate and each core", parsed.Cores.Count == 2 && parsed.Total.Total == 1000 && parsed.Total.Idle == 850);
var later = Readings.ParseProcStat("""
    cpu  200 0 200 1600 0 0 0 0 0 0
    cpu0 80 0 80 640 0 0 0 0 0 0
    cpu1 120 0 120 960 0 0 0 0 0 0
    """);
Check("CPU share ignores idle time", Math.Abs((CpuTimes.Share(new CpuTimes(800, 1000), later.Total) ?? -1) - 0.2) < 1e-9);
Check("A counter that went backwards is skipped", CpuTimes.Share(later.Total, parsed.Total) is null);

var memory = Readings.ParseMeminfo("MemTotal: 16000000 kB\nMemAvailable: 8000000 kB\nMemFree: 2000000 kB\nCached: 3000000 kB\nBuffers: 1000000 kB\nSwapTotal: 1000000 kB\nSwapFree: 1000000 kB\nCommitted_AS: 9000000 kB\nCommitLimit: 17000000 kB\n");
Check("meminfo is read in bytes", memory?.Total == 16_000_000UL * 1024 && memory?.Available == 8_000_000UL * 1024);
var net = Readings.ParseProcNet("Inter-| face\n lo: 10 0 0 0 0 0 0 0 10 0 0 0 0 0 0 0\n eth0: 1000 0 0 0 0 0 0 0 250 0 0 0 0 0 0 0\n");
Check("Loopback is left out of the network totals", net.Received == 1000 && net.Sent == 250);
var process = Readings.ParseProcStatLine("12 (my app) S 1 0 0 0 0 0 0 0 0 0 15 25 0 0 20 0 1 0 100 4096 300 0");
Check("A process name with a space survives /proc/stat", process?.Pid == 12 && process?.Name == "my app" && process?.Ticks == 40 && process?.RssPages == 300);

var vm = Readings.ParseVmStat("Mach Virtual Memory Statistics: (page size of 16384 bytes)\nPages free: 10.\nPages active: 20.\nPages inactive: 5.\nPages wired down: 7.\nPages occupied by compressor: 3.\nPages speculative: 1.\n");
Check("vm_stat uses the page size in its header", vm?.PageSize == 16384 && vm?.Active == 20 && vm?.Compressed == 3);
var link = Readings.ParseNetstat("Name Mtu Network Address Ipkts Ierrs Ibytes Opkts Oerrs Obytes\nlo0 16384 <Link#1> 1 0 5 1 0 5\nen0 1500 <Link#14> aa 10 0 4000 8 0 900\nen0 1500 192.168.0 fe 10 0 1 8 0 1\n");
Check("netstat counts each interface link once and skips lo0", link.Received == 4000 && link.Sent == 900);
var ps = Readings.ParsePs("  42  12.5  2048 Cursor Helper\n");
Check("ps rows keep pid, percent of one core and rss", ps.Count == 1 && ps[0].Pid == 42 && Math.Abs(ps[0].Cpu - 12.5) < 1e-9 && ps[0].RssBytes == 2048UL * 1024 && ps[0].Name == "Cursor Helper");

var players = Playback.Lines("music\u001fMusic\u001fplaying\u001f夜航\u001f海岸线\u001f专辑\u001f222\u001f30\u001fall\u001ffalse\n", DateTimeOffset.Parse("2026-10-27T12:00:00Z"));
Check("Music reports a playing track with a timeline", players.Count == 1 && players[0].Track is { Title: "夜航", Artist: "海岸线", State: MediaState.Playing } track && track.Duration.TotalSeconds == 222 && track.CanSeek);
Check("A stopped line is not a track", PlaybackText.Parse("music", "Music", "music\u001fMusic\u001fstopped", DateTimeOffset.UnixEpoch) is null);
var (app, context) = Frontmost.Parse("Final Cut Pro\ntrue\n");
Check("A full-screen front app hides the island", app == "Final Cut Pro" && context == ScreenContext.Exclusive);
Check("Anything else leaves the island on the desktop", Frontmost.Parse("Finder\nfalse").Context == ScreenContext.Desktop);

var antigravity = UnixDesktopSources.ParseAntigravity("  9 /Applications/Antigravity.app/language_server --csrf_token=abc-123 --port 1\n  10 /usr/bin/other\n");
Check("Antigravity is found from its language server and token", antigravity.Count == 1 && antigravity[0].Pid == 9 && antigravity[0].Token == "abc-123");
var listeners = UnixDesktopSources.ParseLsof("language 9 user 21u IPv4 0x1 0t0 TCP 127.0.0.1:4312 (LISTEN)\n");
Check("A listening port is paired with its process", listeners.TryGetValue(9, out var ports) && ports.Contains(4312));

var options = LaunchOptions.Parse(["--preview", "--language", "zh-CN", "--screenshot", "/tmp/brimdeck"]);
Check("Launch flags select preview, language and screenshots", options.Preview && options.Language == "zh-CN" && options.ScreenshotDirectory == "/tmp/brimdeck");
var plist = LoginItems.BuildPlist("/Applications/BrimDeck.app/Contents/MacOS/BrimDeck");
Check("The login item names the app", plist.Contains("app.brimdeck.mac", StringComparison.Ordinal) && plist.Contains("/Applications/BrimDeck.app", StringComparison.Ordinal));

var root = Path.Combine(Path.GetTempPath(), "brimdeck-mac-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var database = Path.Combine(root, "state.vscdb");
    await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={database}"))
    {
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE ItemTable (key TEXT, value TEXT); INSERT INTO ItemTable VALUES ('cursorAuth/accessToken', 'token-1');";
        await command.ExecuteNonQueryAsync();
    }
    var sources = new UnixDesktopSources();
    Check("Cursor's token is read from its state database", sources.ReadCursorToken(database) == "token-1");
    var secrets = new SecretStore(root, preferKeychain: false);
    var id = Guid.NewGuid();
    secrets.Write(id, "secret-value");
    Check("A saved API key round-trips without the keychain", secrets.Read(id) == "secret-value");
    secrets.Delete(id);
    Check("Deleting a key removes it", secrets.Read(id).Length == 0);

    Loc.Use(Loc.Chinese);
    var reset = Format.ShortReset(DateTimeOffset.Now.AddHours(2).AddMinutes(5));
    Check("Reset text counts hours in Chinese", reset.Contains('时', StringComparison.Ordinal));
    Loc.Use(Loc.English);
    Check("A plan name is shown in words", Format.DisplayPlan("max") == "Max");
    var probe = new SystemProbe().Read();
    Check("A system sample never throws", probe.LogicalProcessors >= 1);
}
finally
{
    try { Directory.Delete(root, true); } catch (IOException) { }
}

Console.WriteLine($"Mac tests passed: {passed}");
