using BrimDeck.Core;
using BrimDeck.Mac.Platform;
using BrimDeck.Mac.Theme;

namespace BrimDeck.Mac.Hosting;

public sealed class Session : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Playback _playback = new();
    private readonly SystemProbe _probe = new();
    private readonly List<PlayerLine> _players = [];
    private int _usageBusy, _mediaBusy, _monitorBusy;
    private DateTimeOffset _usageDue = DateTimeOffset.Now;
    private string? _pinned;

    public Session(LaunchOptions options)
    {
        Options = options;
        Directory.CreateDirectory(AppPaths.DataDirectory);
        Store = new SettingsStore(AppPaths.DataDirectory);
        Settings = Store.Load();
        if (!string.IsNullOrWhiteSpace(options.Language)) Settings.Language = Loc.Normalize(options.Language);
        AppPaths.UseLanguage(Settings.Language);
        if (Store.IsFirstStart) Store.Save(Settings);
        try { LoginItems.Apply(Settings.LaunchAtStartup); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        Secrets = new SecretStore(AppPaths.DataDirectory);
        Prices = new Pricing(AppPaths.DataDirectory);
        Usage = new UsageService(new UnixDesktopSources(), AppPaths.Locations(), secrets: Secrets,
            quotaCache: Path.Combine(AppPaths.DataDirectory, ClaudeQuotaCache.FileName),
            kimiQuotaCache: Path.Combine(AppPaths.DataDirectory, KimiQuotaCache.FileName));
        Palette = Ink.Load(Settings, AppPaths.ThemeDirectory);
        Page = DeckPages.Resolve(Settings, DeckPages.Opening(Settings.DefaultPage) ?? Settings.LastPage);
        if (options.Preview)
        {
            foreach (var id in new[] { ProviderId.Claude, ProviderId.Codex, ProviderId.Cursor, ProviderId.Antigravity }) Settings.SetEnabled(id, true);
            Settings.Width = 1040;
            Settings.Height = 268;
            Settings.MusicWidth = 640;
            Settings.MusicHeight = 220;
            Settings.MonitorWidth = 760;
            Settings.MonitorHeight = 240;
            var now = DateTimeOffset.Now;
            Snapshots = PreviewData.Snapshots(now);
            Track = PreviewData.Track(now);
            Monitor = PreviewData.Monitor(now);
            _pinned = "preview";
        }
        InstanceLock.Signaled += _ => Avalonia.Threading.Dispatcher.UIThread.Post(Reveal);
    }

    public LaunchOptions Options { get; }
    public SettingsStore Store { get; }
    public DeckSettings Settings { get; private set; }
    public UsageService Usage { get; }
    public Pricing Prices { get; }
    public IProviderSecrets Secrets { get; }
    public PanelPalette Palette { get; private set; }
    public IReadOnlyList<ProviderSnapshot> Snapshots { get; private set; } = [];
    public MediaTrack? Track { get; private set; }
    public string? PlayerId => _pinned;
    public IReadOnlyList<PlayerLine> Players => _players;
    public MonitorSnapshot? Monitor { get; private set; }
    public DeckPage Page { get; private set; }
    public bool Hidden { get; private set; }
    public ScreenContext Foreground { get; private set; } = ScreenContext.Desktop;
    public string ForegroundApp { get; private set; } = "";
    public bool Preview => Options.Preview;
    public DateTimeOffset UsageStarted { get; private set; } = DateTimeOffset.Now;
    public bool UsageBusy => _usageBusy > 0;

    public event Action? Changed;
    public event Action? RevealRequested;

    public void Save()
    {
        Settings.LastPage = Page;
        Settings.Normalize();
        AppPaths.UseLanguage(Settings.Language);
        Palette = Ink.Load(Settings, AppPaths.ThemeDirectory);
        Store.Save(Settings);
        LoginItems.Apply(Settings.LaunchAtStartup);
        Changed?.Invoke();
    }

    public void Select(DeckPage page)
    {
        Page = DeckPages.Resolve(Settings, page);
        Settings.LastPage = Page;
        Changed?.Invoke();
    }

    public void Hide(bool hidden)
    {
        Hidden = hidden;
        Changed?.Invoke();
    }

    public void Reveal()
    {
        Hidden = false;
        RevealRequested?.Invoke();
        Changed?.Invoke();
    }

    public void Pin(string? id)
    {
        _pinned = id;
        Track = _players.FirstOrDefault(player => player.Id == id)?.Track ?? _players.FirstOrDefault(player => player.Track?.State == MediaState.Playing)?.Track ?? _players.FirstOrDefault()?.Track;
        Changed?.Invoke();
    }

    public async Task RefreshUsageAsync()
    {
        if (Preview || Interlocked.Exchange(ref _usageBusy, 1) == 1) return;
        try
        {
            UsageStarted = DateTimeOffset.Now;
            Changed?.Invoke();
            var snapshots = await Usage.RefreshAsync(Settings, _lifetime.Token);
            Snapshots = snapshots;
            _usageDue = DateTimeOffset.Now.AddSeconds(60);
            try { await Prices.RefreshAsync(cancellation: _lifetime.Token); } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { AppPaths.LogCrash(ex); }
        finally { Interlocked.Exchange(ref _usageBusy, 0); Changed?.Invoke(); }
    }

    public async Task TickAsync()
    {
        if (_lifetime.IsCancellationRequested) return;
        if (!Preview && Settings.UsageAutoSync && Settings.QuotaWanted && DateTimeOffset.Now >= _usageDue) await RefreshUsageAsync();
        if (!Preview && Settings.MediaWanted) await PollMediaAsync();
        if (!Preview && (Page == DeckPage.Monitor && !Hidden || Settings.ShowsMonitor(Settings.Style))) await PollMonitorAsync();
        if (!Preview) await PollForegroundAsync();
    }

    public Task CommandAsync(MediaCommand command)
    {
        if (Preview)
        {
            if (command == MediaCommand.PlayPause && Track is not null)
                Track = Track with { State = Track.State == MediaState.Playing ? MediaState.Paused : MediaState.Playing, ReportedPosition = Track.Position(DateTimeOffset.Now), PositionAt = DateTimeOffset.Now };
            Changed?.Invoke();
            return Task.CompletedTask;
        }
        return _pinned is { } id ? _playback.CommandAsync(id, command, _lifetime.Token) : Task.CompletedTask;
    }

    public Task SeekAsync(double seconds)
    {
        if (Preview && Track is not null)
        {
            Track = Track with { ReportedPosition = TimeSpan.FromSeconds(seconds), PositionAt = DateTimeOffset.Now };
            Changed?.Invoke();
            return Task.CompletedTask;
        }
        return _pinned is { } id ? _playback.SeekAsync(id, seconds, _lifetime.Token) : Task.CompletedTask;
    }

    private async Task PollMediaAsync()
    {
        if (Interlocked.Exchange(ref _mediaBusy, 1) == 1) return;
        try
        {
            var players = await _playback.PollAsync(_lifetime.Token);
            _players.Clear();
            _players.AddRange(players);
            var chosen = _pinned is not null ? players.FirstOrDefault(player => player.Id == _pinned) : null;
            chosen ??= players.FirstOrDefault(player => player.Track?.State == MediaState.Playing) ?? players.FirstOrDefault();
            _pinned = chosen?.Id;
            var next = chosen?.Track;
            if (next?.SongKey != Track?.SongKey || next?.State != Track?.State) { Track = next; Changed?.Invoke(); }
            else Track = next;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { AppPaths.LogCrash(ex); }
        finally { Interlocked.Exchange(ref _mediaBusy, 0); }
    }

    private async Task PollMonitorAsync()
    {
        if (Interlocked.Exchange(ref _monitorBusy, 1) == 1) return;
        try
        {
            var snapshot = await Task.Run(_probe.Read, _lifetime.Token);
            Monitor = snapshot;
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { AppPaths.LogCrash(ex); }
        finally { Interlocked.Exchange(ref _monitorBusy, 0); }
    }

    private async Task PollForegroundAsync()
    {
        try
        {
            var (name, context) = await Frontmost.ReadAsync(_lifetime.Token);
            if (name == ForegroundApp && context == Foreground) return;
            ForegroundApp = name;
            Foreground = context;
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }

    public WindowBehavior Behavior
    {
        get
        {
            if (Foreground == ScreenContext.Desktop) return WindowBehavior.Normal;
            return Settings.Behavior(Foreground, ForegroundApp, null);
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        Usage.Dispose();
        Prices.Dispose();
        _lifetime.Dispose();
    }
}
