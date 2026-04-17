using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace StockTracker;

/// <summary>
/// 统一配置设置窗口（代码构建UI，无AXAML依赖）
/// 包含三个配置区：Gemini API、邮件SMTP、定时任务
/// </summary>
public class SettingsWindow : Window
{
    public AppSettings UpdatedSettings { get; private set; }
    public bool Saved { get; private set; } = false;

    private readonly TextBox _geminiKeyBox;
    private readonly TextBox _smtpUserBox;
    private readonly TextBox _smtpPassBox;
    private readonly TextBox _smtpToBox;
    private readonly CheckBox _scheduleEnabledBox;
    private readonly TextBox _scheduleTimeBox;
    private readonly TextBox _tavilyKeyBox;

    public SettingsWindow(AppSettings settings)
    {
        UpdatedSettings = settings;

        // 窗口基础设置（与项目中 AddStockWindow 风格一致）
        Title = "配置设置";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = true;

        // 初始化所有控件
        _geminiKeyBox   = MakeTextBox(settings.GeminiApiKey,  "输入 Gemini API Key", '*');
        _smtpUserBox    = MakeTextBox(settings.EmailUser,      "发件人邮箱");
        _smtpPassBox    = MakeTextBox(settings.EmailPassword,  "密码或授权码", '*');
        _smtpToBox      = MakeTextBox(settings.EmailTo,        "收件人邮箱（多人用逗号分隔）");
        _scheduleEnabledBox = new CheckBox { Content = "启用定时自动分析", IsChecked = settings.ScheduleEnabled, Foreground = Brushes.LightGray };
        _scheduleTimeBox = MakeTextBox(settings.ScheduleTime, "执行时间，格式 HH:mm (如 09:30)");
        _tavilyKeyBox    = MakeTextBox(settings.TavilyApiKey, "输入 Tavily API Key", '*');

        // 构建 UI
        var mainStack = new StackPanel { Margin = new Thickness(16), Spacing = 10 };

        mainStack.Children.Add(new TextBlock
        {
            Text = "⚙️ 配置设置",
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        // ── Gemini 配置 ──
        mainStack.Children.Add(MakeSectionLabel("🤖 Gemini AI 配置"));
        mainStack.Children.Add(MakeLabeledRow("API Key :", _geminiKeyBox));
        mainStack.Children.Add(new TextBlock
        {
            Text = "获取地址: https://aistudio.google.com/app/apikey",
            Foreground = Brush.Parse("#FF888888"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        mainStack.Children.Add(MakeDivider());

        // ── 搜索引擎配置 ──
        mainStack.Children.Add(MakeSectionLabel("🔍 搜索引擎配置"));
        mainStack.Children.Add(MakeLabeledRow("Tavily Key:", _tavilyKeyBox));
        mainStack.Children.Add(new TextBlock
        {
            Text = "不填则默认降级使用网页新浪抓取模式。申请: https://tavily.com",
            Foreground = Brush.Parse("#FF888888"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });
        
        mainStack.Children.Add(MakeDivider());

        // ── 邮件配置 ──
        mainStack.Children.Add(MakeSectionLabel("📧 邮件发送配置"));
        mainStack.Children.Add(new TextBlock
        {
            Text = "* 系统会自动识别邮箱类型匹配服务器，只需填收发件人和授权码即可 *",
            Foreground = Brush.Parse("#FF888888"),
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 4)
        });
        mainStack.Children.Add(MakeLabeledRow("发件邮箱 :", _smtpUserBox));
        mainStack.Children.Add(MakeLabeledRow("密码/授权码:", _smtpPassBox));
        mainStack.Children.Add(MakeLabeledRow("收件人 :", _smtpToBox));

        mainStack.Children.Add(MakeDivider());

        // ── 定时任务配置 ──
        mainStack.Children.Add(MakeSectionLabel("⏰ 定时任务配置"));
        mainStack.Children.Add(_scheduleEnabledBox);
        mainStack.Children.Add(MakeLabeledRow("执行时间 :", _scheduleTimeBox));
        mainStack.Children.Add(new TextBlock
        {
            Text = "每天在指定时间自动运行 AI 分析自选股并推送邮件通知",
            Foreground = Brush.Parse("#FF888888"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        mainStack.Children.Add(MakeDivider());

        // ── 按钮行 ──
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var btnCancel = new Button
        {
            Content = "取消",
            Background = Brush.Parse("#FF3E3E42"),
            Foreground = Brushes.White,
            Padding = new Thickness(18, 6)
        };
        btnCancel.Click += (_, _) => { Saved = false; Close(); };

        var btnSave = new Button
        {
            Content = "💾 保存",
            Background = Brush.Parse("#FF007ACC"),
            Foreground = Brushes.White,
            Padding = new Thickness(18, 6)
        };
        btnSave.Click += OnSave;

        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnSave);
        mainStack.Children.Add(btnRow);

        Content = new Border
        {
            Background = Brush.Parse("#FF1E1E1E"),
            CornerRadius = new CornerRadius(8),
            BorderBrush = Brush.Parse("#FF444444"),
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 600,
                Content = mainStack
            }
        };
    }

    // ── 通用创建方法 ──

    private static TextBox MakeTextBox(string value, string watermark, char? passwordChar = null)
    {
        var tb = new TextBox
        {
            Text = value,
            Watermark = watermark,
            Background = Brush.Parse("#FF2D2D2D"),
            Foreground = Brushes.White,
            BorderBrush = Brush.Parse("#FF555555"),
            CaretBrush = Brushes.White,
            FontSize = 12
        };
        if (passwordChar.HasValue)
            tb.PasswordChar = passwordChar.Value;
        return tb;
    }

    private static TextBlock MakeSectionLabel(string text) => new TextBlock
    {
        Text = text,
        Foreground = Brush.Parse("#FFFFCC00"),
        FontWeight = FontWeight.Bold,
        FontSize = 13
    };

    private static Border MakeDivider() => new Border
    {
        Height = 1,
        Background = Brush.Parse("#FF3A3A3A"),
        Margin = new Thickness(0, 4)
    };

    private static Grid MakeLabeledRow(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*") };
        var lbl = new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(control);
        return grid;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        UpdatedSettings = new AppSettings
        {
            GeminiApiKey   = (_geminiKeyBox.Text ?? "").Trim(),
            TavilyApiKey   = (_tavilyKeyBox.Text ?? "").Trim(),
            EmailSmtpHost  = "",
            EmailSmtpPort  = 0,
            EmailSmtpSsl   = true,
            EmailUser      = (_smtpUserBox.Text ?? "").Trim(),
            EmailPassword  = (_smtpPassBox.Text ?? "").Trim(),
            EmailTo        = (_smtpToBox.Text ?? "").Trim(),
            ScheduleEnabled = _scheduleEnabledBox.IsChecked ?? false,
            ScheduleTime   = (_scheduleTimeBox.Text ?? "09:00").Trim()
        };
        Saved = true;
        Close();
    }
}
