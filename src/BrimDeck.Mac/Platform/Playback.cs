using System.Diagnostics;
using System.Globalization;
using BrimDeck.Core;

namespace BrimDeck.Mac.Platform;

public sealed record PlayerLine(string Id, string Name, MediaTrack? Track);

public static class PlaybackText
{
    public const char Separator = '\u001f';

    public static MediaTrack? Parse(string id, string name, string line, DateTimeOffset now)
    {
        var parts = line.Split(Separator);
        if (parts.Length < 3) return null;
        var state = parts[2].Trim().ToLowerInvariant() switch
        {
            "playing" or "play" => MediaState.Playing,
            "paused" or "pause" => MediaState.Paused,
            "stopped" or "stop" => MediaState.Stopped,
            _ => MediaState.Unknown
        };
        if (state is MediaState.Stopped or MediaState.Unknown || parts.Length < 8) return state == MediaState.Stopped ? null : null;
        string title = parts[3], artist = parts.Length > 4 ? parts[4] : "", album = parts.Length > 5 ? parts[5] : "";
        double duration = Number(parts[6]), position = Number(parts[7]);
        if (duration > 20_000) { duration /= 1_000_000; position /= 1_000_000; }
        var repeat = parts.Length > 8 ? parts[8].Trim().ToLowerInvariant() : "off";
        bool shuffle = parts.Length > 9 && parts[9].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        if (title.Length == 0 && artist.Length == 0) return null;
        return new MediaTrack
        {
            Id = id + "\n" + title + "\n" + artist,
            Source = name,
            Title = title,
            Artist = artist,
            Album = album,
            State = state,
            CanToggle = true,
            CanPrevious = true,
            CanNext = true,
            CanSeek = duration > 0,
            CanShuffle = parts.Length > 9,
            CanRepeat = parts.Length > 8 && repeat.Length > 0,
            Shuffle = shuffle,
            Repeat = repeat is "one" or "track" ? MediaRepeat.Track : repeat is "all" or "list" or "true" ? MediaRepeat.List : MediaRepeat.None,
            Start = TimeSpan.Zero,
            End = TimeSpan.FromSeconds(Math.Max(0, duration)),
            ReportedPosition = TimeSpan.FromSeconds(Math.Clamp(position, 0, duration > 0 ? duration : position)),
            PositionAt = now,
            PositionKnown = duration > 0,
            Rate = state == MediaState.Playing ? 1 : 0
        };
    }

    private static double Number(string text)
        => double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : 0;
}

public sealed class Playback
{
    public async Task<IReadOnlyList<PlayerLine>> PollAsync(CancellationToken cancellation)
    {
        var now = DateTimeOffset.Now;
        var players = new List<PlayerLine>();
        if (OperatingSystem.IsMacOS())
        {
            var output = await RunAsync("osascript", ["-e", AppleScript], cancellation);
            players.AddRange(Lines(output, now));
        }
        if (players.Count == 0)
        {
            var output = await RunAsync("playerctl", ["-a", "metadata", "--format", "{{playerName}}" + PlaybackText.Separator + "{{playerName}}" + PlaybackText.Separator + "{{status}}" + PlaybackText.Separator + "{{xesam:title}}" + PlaybackText.Separator + "{{xesam:artist}}" + PlaybackText.Separator + "{{xesam:album}}" + PlaybackText.Separator + "{{mpris:length}}" + PlaybackText.Separator + "{{position}}"], cancellation);
            foreach (var raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = raw.Split(PlaybackText.Separator);
                if (parts.Length < 3) continue;
                var track = PlaybackText.Parse("mpris:" + parts[0], parts[1], raw, now);
                if (track is not null) players.Add(new PlayerLine("mpris:" + parts[0], parts[1], track));
            }
        }
        return players;
    }

    public Task CommandAsync(string id, MediaCommand command, CancellationToken cancellation)
    {
        if (id is "music" or "spotify") return RunAsync("osascript", ["-e", AppleCommand(id == "music" ? "Music" : "Spotify", command, null)], cancellation);
        if (id.StartsWith("mpris:", StringComparison.Ordinal)) return RunAsync("playerctl", ["-p", id["mpris:".Length..], Mpris(command)], cancellation);
        return Task.CompletedTask;
    }

    public Task SeekAsync(string id, double seconds, CancellationToken cancellation)
    {
        string value = seconds.ToString("0.###", CultureInfo.InvariantCulture);
        if (id is "music" or "spotify")
        {
            string app = id == "music" ? "Music" : "Spotify";
            return RunAsync("osascript", ["-e", $"tell application \"{app}\" to set player position to {value}"], cancellation);
        }
        if (id.StartsWith("mpris:", StringComparison.Ordinal))
            return RunAsync("playerctl", ["-p", id["mpris:".Length..], "position", value], cancellation);
        return Task.CompletedTask;
    }

    public static IReadOnlyList<PlayerLine> Lines(string output, DateTimeOffset now)
    {
        var players = new List<PlayerLine>();
        foreach (var raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split(PlaybackText.Separator);
            if (parts.Length < 2) continue;
            players.Add(new PlayerLine(parts[0], parts[1], PlaybackText.Parse(parts[0], parts[1], raw, now)));
        }
        return players.Where(player => player.Track is not null).ToList();
    }

    private static string AppleCommand(string app, MediaCommand command, double? seconds) => command switch
    {
        MediaCommand.PlayPause => $"tell application \"{app}\" to playpause",
        MediaCommand.Next => $"tell application \"{app}\" to next track",
        MediaCommand.Previous => $"tell application \"{app}\" to previous track",
        _ => $"tell application \"{app}\" to playpause"
    };

    private static string Mpris(MediaCommand command) => command switch
    {
        MediaCommand.Next => "next",
        MediaCommand.Previous => "previous",
        _ => "play-pause"
    };

    private const string AppleScript = """
        set sep to ASCII character 31
        set nl to linefeed
        on clean(value)
          set t to ""
          try
            set t to value as string
          end try
          set AppleScript's text item delimiters to {nl, return, sep}
          set pieces to text items of t
          set AppleScript's text item delimiters to " "
          set t to pieces as string
          set AppleScript's text item delimiters to ""
          return t
        end clean
        set output to ""
        try
          if application "Music" is running then
            tell application "Music"
              set st to player state as string
              if st is not "stopped" then
                set t to current track
                set rep to "off"
                set sh to "false"
                try
                  set rep to song repeat as string
                end try
                try
                  set sh to shuffle enabled as string
                end try
                set output to output & "music" & sep & "Music" & sep & st & sep & my clean(name of t) & sep & my clean(artist of t) & sep & my clean(album of t) & sep & (duration of t as string) & sep & (player position as string) & sep & rep & sep & sh & nl
              end if
            end tell
          end if
        end try
        try
          if application "Spotify" is running then
            tell application "Spotify"
              set st to player state as string
              if st is not "stopped" then
                set rep to "off"
                set sh to "false"
                try
                  if repeating then set rep to "all"
                end try
                try
                  if shuffling then set sh to "true"
                end try
                set output to output & "spotify" & sep & "Spotify" & sep & st & sep & my clean(name of current track) & sep & my clean(artist of current track) & sep & my clean(album of current track) & sep & ((duration of current track) / 1000 as string) & sep & (player position as string) & sep & rep & sep & sh & nl
              end if
            end tell
          end if
        end try
        return output
        """;

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
