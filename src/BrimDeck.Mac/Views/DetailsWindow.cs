using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BrimDeck.Core;
using BrimDeck.Mac.Drawing;
using BrimDeck.Mac.Hosting;
using BrimDeck.Mac.Theme;

namespace BrimDeck.Mac.Views;

public sealed class DetailsWindow : Window
{
    public DetailsWindow(Session session, ProviderSnapshot snapshot, int days)
    {
        Title = snapshot.Name;
        Width = 560;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ink.Brush(session.Palette.Surface.ToString());
        FontFamily = Face.Family;
        var palette = session.Palette;
        var range = UsageDateRange.Recent(days, DateTime.Today);
        var result = ModelUsage.Filter([snapshot], new HashSet<ProviderId> { snapshot.Id }, range);
        var root = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        root.Children.Add(Face.Text(Loc.T($"{snapshot.Name} · {(days == 1 ? "今日" : "七日")}", $"{snapshot.Name} · {(days == 1 ? "today" : "7 days")}"), 18, Ink.Hex(palette, "textPrimary"), FontWeight.SemiBold));
        if (result.Rows.Count == 0) root.Children.Add(Face.Text(Loc.T("这段时间没有用量记录", "No usage in this period"), 13, Ink.Hex(palette, "textSecondary")));
        foreach (var row in result.Rows.Take(20))
        {
            var line = new Grid { Height = 28 };
            line.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            line.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(90)));
            line.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(90)));
            line.Children.Add(Face.Text(row.Model, 13, Ink.Hex(palette, "textSecondary")));
            var tokens = Face.Text(Format.Number(row.Entries.Sum(entry => entry.Total)), 13, Ink.Hex(palette, "textPrimary"));
            tokens.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(tokens, 1);
            line.Children.Add(tokens);
            var cost = session.Prices.Summarize(row.Entries);
            var amount = Face.Text(cost.HasValue ? Format.Amount(cost) : "—", 13, Ink.Hex(palette, "textPrimary"), FontWeight.SemiBold);
            amount.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(amount, 2);
            line.Children.Add(amount);
            root.Children.Add(line);
        }
        Content = new ScrollViewer { Content = root };
    }
}
