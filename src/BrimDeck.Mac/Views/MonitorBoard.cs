using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BrimDeck.Core;
using BrimDeck.Mac.Theme;

namespace BrimDeck.Mac.Views;

public sealed class MonitorBoard : Grid
{
    public void Show(MonitorSnapshot? snapshot, PanelPalette palette)
    {
        Children.Clear();
        RowDefinitions.Clear();
        Margin = new Thickness(16, 2, 16, 10);
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(new GridLength(52)));
        RowDefinitions.Add(new RowDefinition(GridLength.Star));
        var stats = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        for (int i = 0; i < 4; i++) stats.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        stats.Children.Add(Stat(Loc.T("处理器", "CPU"), Percent(snapshot?.Cpu), Ink.Hex(palette, "textPrimary"), palette, 0));
        stats.Children.Add(Stat(Loc.T("内存", "Memory"), Memory(snapshot?.Memory), Ink.Hex(palette, "accent"), palette, 1));
        stats.Children.Add(Stat(Loc.T("下载", "Down"), Rate(snapshot?.Network, true), "#7FD1C4", palette, 2));
        stats.Children.Add(Stat(Loc.T("上传", "Up"), Rate(snapshot?.Network, false), "#E5A385", palette, 3));
        Children.Add(stats);

        var cores = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 8) };
        if (snapshot?.Cpu.TryGet(out var cpu) == true)
        {
            foreach (var core in cpu.Cores.Take(32))
            {
                var column = new Border
                {
                    Width = 8, Height = 44, CornerRadius = new CornerRadius(2),
                    Background = Ink.Brush(Ink.Hex(palette, "fill")),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Child = new Border
                    {
                        Height = Math.Max(2, 44 * Math.Clamp(core.Usage, 0, 1)),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        CornerRadius = new CornerRadius(2),
                        Background = Ink.Brush(core.Usage >= 0.9 ? "#E78284" : core.Usage >= 0.7 ? "#E5C890" : Ink.Hex(palette, "accent"))
                    }
                };
                cores.Children.Add(column);
            }
        }
        Grid.SetRow(cores, 1);
        Children.Add(cores);

        var list = new StackPanel { Spacing = 2 };
        if (snapshot?.Processes.TryGet(out var processes) == true)
        {
            foreach (var process in processes.Top.Take(4))
            {
                var row = new Grid { Height = 20 };
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(52)));
                row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(64)));
                row.Children.Add(Face.Text(process.Name, 12, Ink.Hex(palette, "textSecondary")));
                var cpuText = Face.Text(MonitorFormat.ProcessShare(process.CpuPercent, Math.Max(1, snapshot.LogicalProcessors)) + "%", 12, Ink.Hex(palette, "textPrimary"));
                cpuText.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(cpuText, 1);
                row.Children.Add(cpuText);
                var memory = Face.Text(MonitorFormat.ProcessMemory(process.MemoryBytes), 12, Ink.Hex(palette, "textTertiary"));
                memory.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(memory, 2);
                row.Children.Add(memory);
                list.Children.Add(row);
            }
        }
        else list.Children.Add(Face.Text(snapshot?.Processes.Reason ?? Loc.T("正在读取", "Reading"), 12, Ink.Hex(palette, "textTertiary")));
        Grid.SetRow(list, 2);
        Children.Add(list);
    }

    private static Control Stat(string caption, string value, string color, PanelPalette palette, int column)
    {
        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(Face.Text(caption, 11, Ink.Hex(palette, "textTertiary")));
        stack.Children.Add(Face.Text(value, 20, color, FontWeight.SemiBold));
        Grid.SetColumn(stack, column);
        return stack;
    }

    private static string Percent(Reading<CpuSample>? reading)
        => reading?.TryGet(out var cpu) == true ? MonitorFormat.Percent(cpu.Usage) + "%" : "—";

    private static string Memory(Reading<MemorySample>? reading)
        => reading?.TryGet(out var memory) == true ? MonitorFormat.Percent(memory.UsedFraction) + "%" : "—";

    private static string Rate(Reading<NetworkSample>? reading, bool down)
    {
        if (reading?.TryGet(out var net) != true) return "—";
        var quantity = MonitorFormat.Rate(down ? net.DownBytesPerSecond : net.UpBytesPerSecond);
        return quantity.Number + " " + quantity.Unit;
    }
}
