using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace StockTracker;

/// <summary>
/// AI 分析结果展示窗口（代码构建UI，无AXAML依赖）
/// 显示滚动文本结果，可全选复制；支持 Escape 关闭、悬停高亮关闭按钮
/// </summary>
public class AnalysisResultWindow : Window
{
    public AnalysisResultWindow(string title, string content)
    {
        Title = title;
        Width = 560;
        Height = 620;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = true;

        var titleBar = new Border
        {
            Background = Brush.Parse("#FF282828"),
            Height = 36,
            Padding = new Thickness(12, 0),
            Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = Brush.Parse("#FFFFCC00"),
                        FontWeight = FontWeight.Bold,
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false  // 不拦截鼠标事件，确保点击标题文字也能拖动窗口
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Spacing = 4,
                        Children =
                        {
                            MakeCloseButton()
                        }
                    }
                }
            }
        };

        var textBox = new TextBox
        {
            Text = content,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brush.Parse("#FF1A1A1A"),
            Foreground = Brush.Parse("#FFD0D0D0"),
            FontFamily = new FontFamily("Courier New"),
            FontSize = 12,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12),
            // 禁用文本选择时的光标变化，让拖动体验更自然
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeAll)
        };

        var scrollViewer = new ScrollViewer
        {
            Content = textBox,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var mainLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        Grid.SetRow(titleBar, 0);
        Grid.SetRow(scrollViewer, 1);
        mainLayout.Children.Add(titleBar);
        mainLayout.Children.Add(scrollViewer);

        Content = new Border
        {
            Background = Brush.Parse("#FF1E1E1E"),
            CornerRadius = new CornerRadius(8),
            BorderBrush = Brush.Parse("#FF444444"),
            BorderThickness = new Thickness(1),
            Child = mainLayout
        };

        // 全窗口任意位置拖动
        // 使用 Tunnel（隧道）策略：事件自上而下传递，Window 先于 TextBox 收到
        // 这样 TextBox 消费事件之前，我们已经调用了 BeginMoveDrag
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            // 沿逻辑树向上检测，排除 Button（关闭按钮）和 ScrollBar（滚动条）
            var src = e.Source as StyledElement;
            while (src != null)
            {
                if (src is Button || src is Avalonia.Controls.Primitives.ScrollBar) return;
                src = src.Parent as StyledElement;
            }

            BeginMoveDrag(e);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // Escape 键关闭
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
                Close();
        };
    }

    private Button MakeCloseButton()
    {
        var btn = new Button
        {
            Content = "✕",
            FontSize = 14,
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Foreground = Brush.Parse("#FFAAAAAA"),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        btn.PointerEntered += (_, _) =>
        {
            btn.Background = Brush.Parse("#FFE53935");
            btn.Foreground = Brushes.White;
        };
        btn.PointerExited += (_, _) =>
        {
            btn.Background = Brushes.Transparent;
            btn.Foreground = Brush.Parse("#FFAAAAAA");
        };

        btn.Click += (_, _) => Close();
        return btn;
    }
}
