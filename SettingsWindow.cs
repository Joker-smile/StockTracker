using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;

namespace StockTracker;

/// <summary>
/// 统一配置设置窗口（代码构建UI，无AXAML依赖）
/// 包含四个配置区：AI 统一配置、搜索引擎、通知推送（邮件）、定时任务
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
    private readonly Dictionary<string, string> _platformUrls = new();
    private readonly Dictionary<string, string> _platformModels = new();
    private string _currentPlatform = "";

    // ── 搜索 ──
    private readonly TextBox _tavilyKeyBox;

    // ── 邮件通知 ──
    private readonly TextBox _smtpUserBox;
    private readonly TextBox _smtpPassBox;
    private readonly TextBox _smtpToBox;

    // ── 功能开关与定时任务 ──
    private readonly CheckBox _marketReviewEnabledBox;
    private readonly CheckBox _scheduleEnabledBox;
    private readonly TextBox _scheduleTimeBox;

    public SettingsWindow(AppSettings settings)
    {
        UpdatedSettings = settings;

        Title = "配置设置";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
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

        _platformUrls["Gemini"] = ""; 
        _platformUrls["DeepSeek"] = "https://api.deepseek.com/v1";
        _platformUrls["阿里云百炼 (Qwen)"] = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        _platformUrls["智谱 (GLM)"] = "https://open.bigmodel.cn/api/paas/v4";
        _platformUrls["自定义平台"] = (settings.AiPlatform == "自定义平台") ? (settings.AiBaseUrl ?? "") : "";

        _platformModels["Gemini"] = "gemini-2.5-flash,gemini-3-flash-preview,gemini-2.0-flash,gemini-1.5-flash";
        _platformModels["DeepSeek"] = "deepseek-chat";
        _platformModels["阿里云百炼 (Qwen)"] = "qwen-plus,qwen-max";
        _platformModels["智谱 (GLM)"] = "glm-4-flash";
        _platformModels["自定义平台"] = (settings.AiPlatform == "自定义平台") ? (settings.AiModel ?? "") : "";

        string initPlatform = _platformBox.Items.Cast<string>().FirstOrDefault(x => x.Contains(settings.AiPlatform ?? "Gemini")) ?? "自定义平台";
        _currentPlatform = initPlatform;
        _platformBox.SelectedItem = initPlatform;

        _aiApiKeyBox  = MakeTextBox(_platformKeys[initPlatform] ?? "",  "API Key", '*');
        _aiBaseUrlBox = MakeTextBox(settings.AiBaseUrl ?? "", "留空=默认 | 如: https://api.xxx.com/v1");
        _aiModelBox   = MakeTextBox(settings.AiModel ?? "", "留空=自动 | 支持逗号分隔多模型");

        _platformBox.SelectionChanged += (s, e) =>
        {
            if (_platformBox.SelectedItem is string platform)
            {
                if (!string.IsNullOrEmpty(_currentPlatform))
                {
                    _platformKeys[_currentPlatform] = (_aiApiKeyBox.Text ?? "").Trim();
                    _platformUrls[_currentPlatform] = (_aiBaseUrlBox.Text ?? "").Trim();
                    _platformModels[_currentPlatform] = (_aiModelBox.Text ?? "").Trim();
                }
                _currentPlatform = platform;
                _aiApiKeyBox.Text = _platformKeys.TryGetValue(platform, out string? key) ? key : "";
                _aiBaseUrlBox.Text = _platformUrls.TryGetValue(platform, out string? url) ? url : "";
                _aiModelBox.Text = _platformModels.TryGetValue(platform, out string? model) ? model : "";
            }
        };

        _tavilyKeyBox = MakeTextBox(settings.TavilyApiKey, "Tavily API Key", '*');
        
        // 邮件
        _smtpUserBox = MakeTextBox(settings.EmailUser, "发件人邮箱");
        _smtpPassBox = MakeTextBox(settings.EmailPassword, "授权码", '*');
        _smtpToBox = MakeTextBox(settings.EmailTo, "收件人邮箱（多人逗号分隔）");

        // 开关
        _marketReviewEnabledBox = new CheckBox { Content = "开启大盘 AI 复盘分析", IsChecked = settings.MarketReviewEnabled, Foreground = Brushes.White };
        _scheduleEnabledBox = new CheckBox { Content = "启用定时自动分析", IsChecked = settings.ScheduleEnabled, Foreground = Brushes.LightGray };
        _scheduleTimeBox = MakeTextBox(settings.ScheduleTime, "时间点（如 09:15,15:00）");

        // ── 构建 UI ──
        var mainStack = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
        mainStack.Children.Add(new TextBlock { Text = "⚙️ 配置设置", Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center });

        // AI
        mainStack.Children.Add(MakeSectionLabel("🤖 AI 大模型配置"));
        mainStack.Children.Add(MakeLabeledRow("快捷平台:", _platformBox));
        mainStack.Children.Add(MakeLabeledRow("API Key:", _aiApiKeyBox));
        mainStack.Children.Add(MakeLabeledRow("接口地址:", _aiBaseUrlBox));
        mainStack.Children.Add(MakeLabeledRow("主后备模型:", _aiModelBox));

        // 搜索
        mainStack.Children.Add(MakeDivider());
        mainStack.Children.Add(MakeSectionLabel("🔍 增强数据配置 (Tavily)"));
        mainStack.Children.Add(MakeLabeledRow("Search Key:", _tavilyKeyBox));

        // 邮件
        mainStack.Children.Add(MakeDivider());
        mainStack.Children.Add(MakeSectionLabel("📧 邮件推送配置"));
        mainStack.Children.Add(MakeLabeledRow("发件邮箱:", _smtpUserBox));
        mainStack.Children.Add(MakeLabeledRow("授权码:", _smtpPassBox));
        mainStack.Children.Add(MakeLabeledRow("收件人:", _smtpToBox));

        // 功能与调度
        mainStack.Children.Add(MakeDivider());
        mainStack.Children.Add(MakeSectionLabel("🛠️ 分析与定时任务"));
        mainStack.Children.Add(_marketReviewEnabledBox);
        mainStack.Children.Add(_scheduleEnabledBox);
        mainStack.Children.Add(MakeLabeledRow("执行时间:", _scheduleTimeBox));

        // 按钮
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        var btnCancel = new Button { Content = "取消", Background = Brush.Parse("#FF3E3E42"), Foreground = Brushes.White, Padding = new Thickness(18, 6) };
        btnCancel.Click += (_, _) => { Saved = false; Close(); };
        var btnSave = new Button { Content = "💾 保存配置", Background = Brush.Parse("#FF007ACC"), Foreground = Brushes.White, Padding = new Thickness(18, 6) };
        btnSave.Click += OnSave;
        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnSave);
        mainStack.Children.Add(btnRow);

        Content = new Border
        {
            Background = Brush.Parse("#FF1E1E1E"), CornerRadius = new CornerRadius(8), BorderBrush = Brush.Parse("#FF444444"), BorderThickness = new Thickness(1),
            Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 700, Content = mainStack }
        };
    }

    private static TextBox MakeTextBox(string value, string watermark, char? passwordChar = null)
    {
        var tb = new TextBox { Text = value, Watermark = watermark, Background = Brush.Parse("#FF2D2D2D"), Foreground = Brushes.White, BorderBrush = Brush.Parse("#FF555555"), CaretBrush = Brushes.White, FontSize = 12 };
        if (passwordChar.HasValue) tb.PasswordChar = passwordChar.Value;
        return tb;
    }

    private static TextBlock MakeSectionLabel(string text) => new TextBlock { Text = text, Foreground = Brush.Parse("#FFFFCC00"), FontWeight = FontWeight.Bold, FontSize = 13, Margin = new Thickness(0, 5, 0, 0) };
    private static Border MakeDivider() => new Border { Height = 1, Background = Brush.Parse("#FF3A3A3A"), Margin = new Thickness(0, 6) };
    private static Grid MakeLabeledRow(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*"), Margin = new Thickness(0, 2) };
        var lbl = new TextBlock { Text = label, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        Grid.SetColumn(lbl, 0); Grid.SetColumn(control, 1);
        grid.Children.Add(lbl); grid.Children.Add(control);
        return grid;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        string selectedPlatform = (_platformBox.SelectedItem as string) ?? "Gemini";
        _platformKeys[selectedPlatform] = (_aiApiKeyBox.Text ?? "").Trim();
        
        UpdatedSettings = new AppSettings
        {
            AiPlatform = selectedPlatform,
            GeminiApiKey = _platformKeys.TryGetValue("Gemini", out var k1) ? k1 : "",
            DeepSeekApiKey = _platformKeys.TryGetValue("DeepSeek", out var k2) ? k2 : "",
            QwenApiKey = _platformKeys.TryGetValue("阿里云百炼 (Qwen)", out var k3) ? k3 : "",
            GlmApiKey = _platformKeys.TryGetValue("智谱 (GLM)", out var k4) ? k4 : "",
            CustomApiKey = _platformKeys.TryGetValue("自定义平台", out var k5) ? k5 : "",

            AiBaseUrl = (_aiBaseUrlBox.Text ?? "").Trim(),
            AiModel = (_aiModelBox.Text ?? "").Trim(),
            TavilyApiKey = (_tavilyKeyBox.Text ?? "").Trim(),
            
            EmailSmtpHost = UpdatedSettings.EmailSmtpHost,
            EmailSmtpPort = UpdatedSettings.EmailSmtpPort,
            EmailSmtpSsl = UpdatedSettings.EmailSmtpSsl,
            EmailUser = (_smtpUserBox.Text ?? "").Trim(),
            EmailPassword = (_smtpPassBox.Text ?? "").Trim(),
            EmailTo = (_smtpToBox.Text ?? "").Trim(),

            MarketReviewEnabled = _marketReviewEnabledBox.IsChecked ?? false,
            ScheduleEnabled = _scheduleEnabledBox.IsChecked ?? false,
            ScheduleTime = (_scheduleTimeBox.Text ?? "09:00").Trim()
        };
        Saved = true;
        Close();
    }
}
