using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BrimDeck.Core;
using BrimDeck.Mac.Drawing;
using BrimDeck.Mac.Hosting;
using BrimDeck.Mac.Theme;

namespace BrimDeck.Mac.Views;

public sealed class UsageBoard : Grid
{
    private readonly Dictionary<Guid, int> _page = [];

    public void Show(Session session, Action<ProviderSnapshot, int> details)
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();
        var palette = session.Palette;
        var columns = DashboardUsage.Columns(session.Settings, session.Snapshots);
        if (!session.Settings.UsagePage)
        {
            Children.Add(Note(Loc.T("用量页已关闭", "The usage page is off"), palette));
            return;
        }
        if (columns.Count == 0)
        {
            Children.Add(Note(Loc.T("在设置中打开一个工具", "Turn on a tool in Settings"), palette));
            return;
        }
        Margin = new Thickness(8, 0, 8, 8);
        for (int i = 0; i < columns.Count; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var column = Build(columns[i], palette, session.Prices, i > 0, details);
            Grid.SetColumn(column, i);
            Children.Add(column);
        }
    }

    private Control Build(DashboardColumn column, PanelPalette palette, Pricing prices, bool separator, Action<ProviderSnapshot, int> details)
    {
        var entry = column.Entry;
        var quota = column.Quota;
        var usage = column.Usage;
        var root = new Grid { Margin = new Thickness(separator ? 8 : 4, 0, 4, 0) };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (separator) root.Children.Add(new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-8, 8, 0, 8), Background = Ink.Brush(Ink.Hex(palette, "hairline")) });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2, 0, 8) };
        header.Children.Add(Face.Text(entry.Name, 16, Ink.Hex(palette, "textPrimary"), FontWeight.SemiBold));
        if (Format.DisplayPlan(quota.Plan) is { Length: > 0 } plan)
        {
            header.Children.Add(new Border
            {
                Child = Face.Text(plan, 11, Ink.Hex(palette, "textTertiary")),
                BorderBrush = Ink.Brush(Ink.Hex(palette, "outline")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var metrics = quota.Metrics.Where(metric => metric.Kind != MetricKind.Text || metric.Value.Length > 0).ToList();
        const int pageSize = 2;
        int pages = Math.Max(1, (int)Math.Ceiling(metrics.Count / (double)pageSize));
        int page = Math.Clamp(_page.GetValueOrDefault(entry.InstanceId), 0, pages - 1);
        var shown = metrics.Skip(page * pageSize).Take(pageSize).ToList();
        var body = new StackPanel { Spacing = 12, VerticalAlignment = shown.Count <= 1 ? VerticalAlignment.Center : VerticalAlignment.Top };
        if (shown.Count == 0)
        {
            body.Children.Add(Face.Text(quota.Status.Length > 0 ? quota.Status : Loc.T("等待更新", "Waiting for update"), 13, Ink.Hex(palette, "textSecondary")));
        }
        else
        {
            for (int i = 0; i < shown.Count; i++) body.Children.Add(QuotaBlock(entry, shown[i], palette));
        }
        if (pages > 1)
        {
            var chip = new Clickable { CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 2), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
            chip.Rest(Brushes.Transparent, Ink.Brush(Ink.Hex(palette, "hover")));
            chip.Child = Face.Text(Loc.T($"第 {page + 1}/{pages} 页", $"Page {page + 1}/{pages}"), 11, Ink.Hex(palette, "textTertiary"));
            chip.Pressed += (_, _) => { _page[entry.InstanceId] = (page + 1) % pages; ShowAgain?.Invoke(); };
            body.Children.Add(chip);
        }
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var footer = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 0) };
        footer.Children.Add(new Border { Height = 1, Background = Ink.Brush(Ink.Hex(palette, "hairline")), Margin = new Thickness(0, 0, 0, 6) });
        footer.Children.Add(Total(usage, prices, palette, 1, details));
        footer.Children.Add(Total(usage, prices, palette, 7, details));
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    public event Action? ShowAgain;

    private static Control QuotaBlock(AppEntry entry, UsageMetric metric, PanelPalette palette)
    {
        double used = metric.UsedPercent ?? 0;
        string color = Ink.Quota(palette, entry, metric.UsedPercent);
        var block = new StackPanel { Spacing = 3 };
        var title = new Grid();
        title.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        title.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var label = Face.Text(Format.QuotaLabel(metric), 12, Ink.Hex(palette, "textSecondary"));
        Grid.SetColumn(label, 0);
        title.Children.Add(label);
        var side = metric.Kind == MetricKind.Text ? metric.Note : "";
        if (side.Length > 0)
        {
            var note = Face.Text(side, 11, Ink.Hex(palette, "textTertiary"));
            note.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(note, 1);
            title.Children.Add(note);
        }
        block.Children.Add(title);
        var values = new Grid();
        values.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        values.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var number = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        number.Children.Add(Face.Text(metric.ValueText, 22, metric.UsedPercent is null || AppPresets.Level(used) == 0 ? Ink.Hex(palette, "textPrimary") : color, FontWeight.SemiBold));
        if (metric.ValueSuffix.Length > 0) number.Children.Add(Face.Text(metric.ValueSuffix, 12, Ink.Hex(palette, "textTertiary")));
        values.Children.Add(number);
        var reset = Face.Text(metric.Kind == MetricKind.Text ? metric.ResetText : Format.ShortReset(metric.ResetAt), 12, Ink.Hex(palette, "textTertiary"));
        reset.HorizontalAlignment = HorizontalAlignment.Right;
        reset.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(reset, 1);
        values.Children.Add(reset);
        block.Children.Add(values);
        block.Children.Add(new Meter { Height = 4, Fraction = Math.Clamp((metric.UsedPercent ?? 0) / 100, 0, 1), Color = Ink.Brush(color), Track = Ink.Brush(Ink.Hex(palette, "track")), Margin = new Thickness(0, 2, 0, 0) });
        return block;
    }

    private static Control Total(ProviderSnapshot usage, Pricing prices, PanelPalette palette, int days, Action<ProviderSnapshot, int> details)
    {
        var today = DateTime.Today;
        var range = UsageDateRange.Recent(days, today);
        var entries = usage.Entries.Where(entry => range.Contains(entry.Time)).ToList();
        long tokens = entries.Sum(entry => entry.Total);
        var cost = usage.UsageAvailable ? prices.Summarize(entries) : null;
        string caption = days == 1 ? Loc.T("今日", "Today") : Loc.T("七日", "Week");
        var row = new Grid { Height = 22 };
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Loc.IsEnglish ? 46 : 36)));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var label = Face.Text(caption, 12, Ink.Hex(palette, "textTertiary"));
        row.Children.Add(label);
        var count = Face.Text(usage.UsageAvailable ? Format.Number(tokens) : "—", 12, Ink.Hex(palette, "textSecondary"));
        Grid.SetColumn(count, 1);
        row.Children.Add(count);
        var amount = Face.Text(cost is { HasValue: true } priced ? Format.Amount(priced) + (priced.Unpriced > 0 ? " +" : "") : usage.UsageAvailable ? "$0.00" : "—", 12, Ink.Hex(palette, "textPrimary"), FontWeight.SemiBold);
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(amount, 2);
        row.Children.Add(amount);
        var link = new Clickable { Child = row, CornerRadius = new CornerRadius(6), Padding = new Thickness(4, 0), Margin = new Thickness(-4, 0) };
        link.Rest(Brushes.Transparent, Ink.Brush(Ink.Hex(palette, "hover")));
        link.Pressed += (_, _) => details(usage, days);
        return link;
    }

    private static Control Note(string text, PanelPalette palette)
        => Face.Text(text, 14, Ink.Hex(palette, "textSecondary"));
}
