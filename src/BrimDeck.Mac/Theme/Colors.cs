using Avalonia.Media;
using BrimDeck.Core;
using BrimDeck.Mac.Platform;

namespace BrimDeck.Mac.Theme;

public static class Ink
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SolidColorBrush> Brushes = new(StringComparer.OrdinalIgnoreCase);

    public static SolidColorBrush Brush(string hex) => Brushes.GetOrAdd(string.IsNullOrWhiteSpace(hex) ? "#000000" : hex, static text =>
    {
        var color = global::Avalonia.Media.Color.Parse(text);
        return new SolidColorBrush(color);
    });

    public static SolidColorBrush Brush(ThemeColor color) => Brush(color.ToString());

    public static string Hex(PanelPalette palette, string key) => palette[key].ToString();

    public static string App(PanelPalette palette, AppEntry entry)
        => entry.ThemeColor.Length > 0 ? palette.Readable(ThemeColor.Parse(entry.ThemeColor)).ToString() : palette.AppColor(entry.QuotaSource).ToString();

    public static string Quota(PanelPalette palette, AppEntry entry, double? usedPercent)
    {
        if (usedPercent is not { } used) return App(palette, entry);
        if (used >= AppPresets.CriticalPercent) return Critical(palette, entry);
        if (used >= AppPresets.WarningPercent) return Warning(palette, entry);
        return App(palette, entry);
    }

    public static string Warning(PanelPalette palette, AppEntry entry)
        => entry.WarningColor.Length > 0 ? palette.Readable(ThemeColor.Parse(entry.WarningColor)).ToString() : palette.WarningColor.ToString();

    public static string Critical(PanelPalette palette, AppEntry entry)
    {
        if (entry.CriticalColor.Length > 0) return palette.Readable(ThemeColor.Parse(entry.CriticalColor)).ToString();
        var fallback = palette.Apps.TryGetValue("critical", out var color) ? color : ThemeColor.Parse(AppPresets.CriticalColor);
        return palette.Readable(fallback).ToString();
    }

    public static PanelPalette Load(DeckSettings settings, string themeFolder)
    {
        var id = settings.PanelTheme;
        if (id == PanelThemes.SystemId) id = Appearance.IsDark() ? PanelThemes.Dark : PanelThemes.Light;
        if (PanelThemes.BuiltIn(id) is { } builtIn) return PanelThemes.Resolve(builtIn);
        foreach (var theme in PanelThemes.LoadFolder(themeFolder))
            if (theme.Usable && string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase)) return PanelThemes.Resolve(theme);
        return PanelThemes.Resolve(PanelThemes.BuiltIn(PanelThemes.Dark)!);
    }
}
