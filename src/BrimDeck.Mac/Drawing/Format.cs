using System.Globalization;
using BrimDeck.Core;

namespace BrimDeck.Mac.Drawing;

public static class Format
{
    public static string Number(long number) => number switch
    {
        >= 1_000_000_000 => (number / 1_000_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "B",
        >= 1_000_000 => (number / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "M",
        >= 1000 => (number / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "K",
        _ => number.ToString("N0", CultureInfo.InvariantCulture)
    };

    public static string Amount(CostSummary cost) => "$" + cost.Amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string ShortReset(DateTimeOffset? date, DateTimeOffset? now = null)
    {
        if (date is null) return "";
        var clock = now ?? DateTimeOffset.Now;
        var remaining = date.Value - clock;
        if (remaining <= TimeSpan.Zero) return Loc.T("待重置", "Reset due");
        if (remaining.TotalDays >= 1) return Loc.T($"{(int)remaining.TotalDays} 天 {remaining.Hours} 时后", $"in {(int)remaining.TotalDays}d {remaining.Hours}h");
        if (remaining.TotalHours >= 1) return Loc.T($"{remaining.Hours} 时 {remaining.Minutes} 分后", $"in {remaining.Hours}h {remaining.Minutes}m");
        int minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return Loc.T($"{minutes} 分后", $"in {minutes}m");
    }

    public static string DisplayPlan(string plan) => plan.Trim().ToLowerInvariant() switch
    {
        "prolite" => "Pro Lite", "pro" or "google ai pro" => "Pro", "google ai ultra" => "Ultra",
        "plus" => "Plus", "free" => "Free", "max" => "Max", "team" => "Team", "business" => "Business",
        "enterprise" => "Enterprise", _ => plan.Trim()
    };

    public static string QuotaGroup(string label) => label.Contains('·') ? label.Split('·')[0].Trim() : "";

    public static string QuotaLabel(UsageMetric metric)
    {
        if (metric.Kind == MetricKind.Text) return metric.Label;
        var period = (metric.Label.Contains('·') ? metric.Label.Split('·')[^1] : metric.Label).Replace("额度", "", StringComparison.Ordinal).Trim();
        var group = QuotaGroup(metric.Label);
        if (group.Length == 0) return Loc.Label(period);
        return period == "每周" ? Loc.Label(group + " 每周") : group + " " + Loc.Label(period);
    }

    public static string Clock(DateTimeOffset time, bool hour24)
        => hour24 ? time.ToString("HH:mm", CultureInfo.InvariantCulture) : time.ToString("h:mm", CultureInfo.InvariantCulture);
}
