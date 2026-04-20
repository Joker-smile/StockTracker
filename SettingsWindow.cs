using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace StockTracker;

/// <summary>
/// 统一配置设置窗口（代码构建UI，无AXAML依赖）
/// 包含四个配置区：AI 统一配置、搜索引擎、邮件SMTP、定时任务
/// </summary>
public class SettingsWindow : Window
{
    public AppSettings UpdatedSettings { get; private set; }
    public bool Saved { get; private set; } = false;

    // ── AI 统一配置 ──
    private readonly ComboBox _platformBox;
    private readonly TextBox _aiApiKeyBox;
    private readonly TextBox _aiBaseUrlBox;
    private readonly TextBox _aiModelBox;

    private readonly Dictionary<string, string> _platformKeys = new();
    private string _currentPlatform = "";

    // ── 搜索 ──
    private readonly TextBox _tavilyKeyBox;

    // ── 邮件 ──
    private readonly TextBox _smtpUserBox;
    private readonly TextBox _smtpPassBox;
    private readonly TextBox _smtpToBox;

    // ── 定时任务 ──
    private readonly CheckBox _scheduleEnabledBox;
    private readonly TextBox _scheduleTimeBox;

    public SettingsWindow(AppSettings settings)
    {
        UpdatedSettings = settings;

        Title = "配置设置";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = true;

        // ── 初始化控件 ──
        _platformBox = new ComboBox 
        { 
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brush.Parse("#FF2D2D2D"),
            Foreground = Brushes.White,
        };
        _platformBox.Items.Add("Gemini");
        _platformBox.Items.Add("DeepSeek");
        _platformBox.Items.Add("阿里云百炼 (Qwen)");
        _platformBox.Items.Add("智谱 (GLM)");
        _platformBox.Items.Add("自定义平台");
        
        _platformKeys["Gemini"] = settings.GeminiApiKey ?? "";
        _platformKeys["DeepSeek"] = settings.DeepSeekApiKey ?? "";
        _platformKeys["阿里云百炼 (Qwen)"] = settings.QwenApiKey ?? "";
        _platformKeys["智谱 (GLM)"] = settings.GlmApiKey ?? "";
        _platformKeys["自定义平台"] = settings.CustomApiKey ?? "";

        // 恢复选中状态
        string initPlatform = _platformBox.Items.Cast<string>().FirstOrDefault(x => x.Contains(settings.AiPlatform ?? "Gemini")) ?? "自定义平台";
        _currentPlatform = initPlatform;
        _platformBox.SelectedItem = initPlatform;

        _aiApiKeyBox  = MakeTextBox(_platformKeys[initPlatform],  "填入 API Key（必填）", '*');
        _aiBaseUrlBox = MakeTextBox(settings.AiBaseUrl, "留空=平台默认 | 其他如: https://api.deepseek.com/v1");
        _aiModelBox   = MakeTextBox(settings.AiModel, "留空=自动默认 | 支持逗号分隔填入多模型(自动降级)");

        // 联动事件：选择平台自动带出默认值
        _platformBox.SelectionChanged += (s, e) =>
        {
            if (_platformBox.SelectedItem is string platform)
            {
                // 先保存当前输入框的 Key 到旧平台缓存中
                if (!string.IsNullOrEmpty(_currentPlatform))
                {
                    _platformKeys[_currentPlatform] = (_aiApiKeyBox.Text ?? "").Trim();
                }

                _currentPlatform = platform;
                
                // 将被选新平台的 Key 加载进输入框（若无则默认为空）
                _aiApiKeyBox.Text = _platformKeys.TryGetValue(platform, out string? key) ? key : "";

                // 重置地址与模型
                if (platform == "Gemini") {
                    _aiBaseUrlBox.Text = ""; _aiModelBox.Text = "gemini-2.5-flash,gemini-3-flash-preview,gemini-2.0-flash,gemini-1.5-flash";
                }
                else if (platform == "DeepSeek") {
                    _aiBaseUrlBox.Text = "https://api.deepseek.com/v1"; _aiModelBox.Text = "deepseek-chat";
                }
                else if (platform == "阿里云百炼 (Qwen)") {
                    _aiBaseUrlBox.Text = "https://dashscope.aliyuncs.com/compatible-mode/v1"; _aiModelBox.Text = "qwen-plus,qwen-max";
                }
                else if (platform == "智谱 (GLM)") {
                    _aiBaseUrlBox.Text = "https://open.bigmodel.cn/api/paas/v4"; _aiModelBox.Text = "glm-4-flash";
                }
            }
        };

        _tavilyKeyBox      = MakeTextBox(settings.TavilyApiKey,   "输入 Tavily API Key（可留空）", '*');
        _smtpUserBox       = MakeTextBox(settings.EmailUser,      "发件人邮箱");
        _smtpPassBox       = MakeTextBox(settings.EmailPassword,  "密码或授权码", '*');
        _smtpToBox         = MakeTextBox(settings.EmailTo,        "收件人邮箱（多人用逗号分隔）");
        _scheduleEnabledBox = new CheckBox
        {
            Content = "启用定时自动分析",
            IsChecked = settings.ScheduleEnabled,
            Foreground = Brushes.LightGray
        };
        _scheduleTimeBox = MakeTextBox(settings.ScheduleTime, "执行时间，格式 HH:mm（如 09:30）");

        // ── 构建 UI ──
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

        // ── AI 统一配置区 ──
        mainStack.Children.Add(MakeSectionLabel("🤖 AI 大模型配置"));

        mainStack.Children.Add(MakeLabeledRow("快捷平台 :", _platformBox));
        mainStack.Children.Add(MakeLabeledRow("API Key :", _aiApiKeyBox));
        mainStack.Children.Add(MakeLabeledRow("接口地址 :", _aiBaseUrlBox));
        mainStack.Children.Add(MakeLabeledRow("主后备模型 :", _aiModelBox));

        mainStack.Children.Add(new TextBlock
        {
            Text = "支持填入多个模型（用逗号分隔），在请求失败时系统会自动轮询使用后备模型兜底验证。",
            Foreground = Brush.Parse("#FF888888"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        mainStack.Children.Add(MakeDivider());

        // ── 搜索配置区 ──
        mainStack.Children.Add(MakeSectionLabel("🔍 新闻搜索配置（可选）"));
        mainStack.Children.Add(MakeLabeledRow("Tavily Key:", _tavilyKeyBox));
        mainStack.Children.Add(new TextBlock
        {
            Text = "不填则降级使用新浪网页抓取。申请: https://tavily.com",
            Foreground = Brush.Parse("#FF888888"),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        });

        mainStack.Children.Add(MakeDivider());

        // ── 邮件配置区 ──
        mainStack.Children.Add(MakeSectionLabel("📧 邮件发送配置"));
        mainStack.Children.Add(new TextBlock
        {
            Text = "* 系统自动识别邮箱类型，只需填发件邮箱和授权码即可 *",
            Foreground = Brush.Parse("#FF888888"),
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 4)
        });
        mainStack.Children.Add(MakeLabeledRow("发件邮箱 :", _smtpUserBox));
        mainStack.Children.Add(MakeLabeledRow("密码/授权码:", _smtpPassBox));
        mainStack.Children.Add(MakeLabeledRow("收件人 :", _smtpToBox));

        mainStack.Children.Add(MakeDivider());

        // ── 定时任务区 ──
        mainStack.Children.Add(MakeSectionLabel("⏰ 定时任务配置"));
        mainStack.Children.Add(_scheduleEnabledBox);
        mainStack.Children.Add(MakeLabeledRow("执行时间 :", _scheduleTimeBox));
        mainStack.Children.Add(new TextBlock
        {
            Text = "每天在指定时间自动运行 AI 分析并推送邮件通知",
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
                MaxHeight = 640,
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
        string selectedPlatform = (_platformBox.SelectedItem as string) ?? "Gemini";
        
        // 保存当前 UI 输入的 Key 到字典中
        _platformKeys[selectedPlatform] = (_aiApiKeyBox.Text ?? "").Trim();
        
        UpdatedSettings = new AppSettings
        {
            AiPlatform    = selectedPlatform,
            GeminiApiKey     = _platformKeys["Gemini"],
            DeepSeekApiKey   = _platformKeys["DeepSeek"],
            QwenApiKey       = _platformKeys["阿里云百炼 (Qwen)"],
            GlmApiKey        = _platformKeys["智谱 (GLM)"],
            CustomApiKey     = _platformKeys["自定义平台"],
            AiBaseUrl     = (_aiBaseUrlBox.Text ?? "").Trim(),
            AiModel       = (_aiModelBox.Text   ?? "").Trim(),
            TavilyApiKey  = (_tavilyKeyBox.Text ?? "").Trim(),
            EmailSmtpHost = "",
            EmailSmtpPort = 0,
            EmailSmtpSsl  = true,
            EmailUser     = (_smtpUserBox.Text  ?? "").Trim(),
            EmailPassword = (_smtpPassBox.Text  ?? "").Trim(),
            EmailTo       = (_smtpToBox.Text    ?? "").Trim(),
            ScheduleEnabled = _scheduleEnabledBox.IsChecked ?? false,
            ScheduleTime  = (_scheduleTimeBox.Text ?? "09:00").Trim()
        };
        Saved = true;
        Close();
    }
}
