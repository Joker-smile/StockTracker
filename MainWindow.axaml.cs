using Avalonia;
using Markdig;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace StockTracker;

public partial class MainWindow : Window
{
    private DispatcherTimer? _timer;
    private List<string> _stocks = new();
    private readonly string _configFile;
    private HttpClient? _httpClient;
    private Dictionary<string, (double TotalVolume, double TotalClose, int Count, double RecentTrend, double MA5, DateTime LastUpdated)> _klineCache = new();
    private FileSystemWatcher? _watcher;
    private bool _isScreenerRunning = false;
    private string _dataSource = "Tencent"; // Default to Tencent
    private readonly Random _random = new Random();

    // ═══════════════════════════════════════════
    // AI 分析 / 邮件 / 定时任务 - 新增字段区
    // ═══════════════════════════════════════════
    private AppSettings _appSettings = new();
    private string _settingsFile = "";
    private DispatcherTimer? _scheduleTimer;
    private bool _isAiAnalysisRunning = false;
    private DateTime _lastScheduleRunDate = DateTime.MinValue;
    private HashSet<string> _triggeredTimesToday = new();
    private List<TimeSpan> _targetScheduleTimes = new();
    private List<(string Name, string Code, string Price, string Pct, string Sector, string Pred)> _lastDisplayData = new();
    private bool _isContextMenuOpen = false;

    // 带重试机制的HTTP请求
    private async Task<string> HttpGetWithRetryAsync(string url, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await _httpClient!.GetStringAsync(url);
                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (ex is HttpRequestException httpEx && httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return ""; // 404不重试
                }
                
                if (i < maxRetries - 1)
                {
                    // 网络错误或超时，等待后重试
                    await Task.Delay(1000 * (i + 1)); // 1s, 2s, 3s递增延迟
                    Program.LogError($"HTTP request failed for {url}, retry {i + 1}/{maxRetries}", ex);
                }
                else
                {
                    throw;
                }
            }
        }
        return ""; // 所有重试都失败
    }

    public MainWindow()
    {
        InitializeComponent();
        TitleBlock.Text = $"StockTracker {Program.APP_VERSION}";

        // Register GB2312 support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        string? exePath = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName);
        
        // Green Software preference: Try to put stocks.txt right next to the exe first
        string localConfig = Path.Combine(exePath ?? AppContext.BaseDirectory, "stocks.txt");
        try 
        {
            // Test if we have write access to the local directory
            if (!File.Exists(localConfig)) File.WriteAllText(localConfig, "");
            _configFile = localConfig;
        }
        catch (UnauthorizedAccessException)
        {
            // Fallback for macOS / strict environments (e.g. C:\Program Files\)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string configDir = Path.Combine(appData, "StockTracker");
            if (!Directory.Exists(configDir))
            {
                try { Directory.CreateDirectory(configDir); } catch { }
            }
            _configFile = Path.Combine(configDir, "stocks.txt");
        }
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Referer", "http://finance.sina.com.cn/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        LoadConfig();
        
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _timer.Tick += async (s, e) => await UpdatePrices();
        _timer.Start();

        // Initial update
        _ = UpdatePrices();

        SetupWindowEvents();
        SetupWatcher();

        // 新增：加载 AI/邮件/定时任务配置并启动调度器
        LoadSettings();
        SetupScheduleTimer();
        SetupTrayIcon();
    }

    private void SetupWatcher()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_configFile);
            string? file = Path.GetFileName(_configFile);
            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
            {
                _watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += _watcher_Changed;
            }
        }
        catch { }
    }

    private void _watcher_Changed(object sender, FileSystemEventArgs e)
    {
        // Add a small delay/debounce to avoid file lock issues when python is writing
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(500); // 500ms debounce
            LoadConfig();
            await UpdatePrices();
        });
    }

    private void SetupWindowEvents()
    {
        var header = this.FindControl<Border>("HeaderBar");
        var container = this.FindControl<Grid>("StockContainer");

        if (header != null)
        {
            header.PointerPressed += OnWindowDragPointerPressed;
        }

        if (container != null)
        {
            container.PointerPressed += OnWindowDragPointerPressed;
            var sharedMenu = CreateSharedContextMenu(null);
            container.ContextMenu = sharedMenu;

            var placeholder = this.FindControl<TextBlock>("PlaceholderText");
            if (placeholder != null)
            {
                placeholder.ContextMenu = sharedMenu;
            }
        }

        this.MinWidth = 300;
        this.MinHeight = 50;

        // 监听窗口状态变化，确保从最小化恢复时重新计算布局
        this.PropertyChanged += (s, e) => {
            if (e.Property.Name == "WindowState")
            {
                if (this.WindowState == WindowState.Normal)
                {
                    Dispatcher.UIThread.Post(() => {
                        this.InvalidateMeasure();
                        // 强制触发一次 SizeToContent 重新计算
                        var current = this.SizeToContent;
                        this.SizeToContent = SizeToContent.Manual;
                        this.SizeToContent = current;
                    }, DispatcherPriority.Render);
                }
                else if (this.WindowState == WindowState.Minimized)
                {
                    if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    {
                        Dispatcher.UIThread.Post(() => {
                            this.Hide();
                        });
                    }
                }
            }
        };
    }

    private void SetupTrayIcon()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            var trayIcon = new TrayIcon
            {
                ToolTipText = "StockTracker"
            };

            try
            {
                using var stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://StockTracker/icon.ico"));
                trayIcon.Icon = new WindowIcon(stream);
            }
            catch (Exception ex)
            {
                Program.LogError("Load TrayIcon Error", ex);
            }

            var menu = new NativeMenu();
            var showItem = new NativeMenuItem("显示主界面");
            showItem.Click += (s, e) => { this.Show(); this.WindowState = WindowState.Normal; };
            menu.Items.Add(showItem);

            var exitItem = new NativeMenuItem("退出");
            exitItem.Click += (s, e) => BtnClose_Click(null, new RoutedEventArgs());
            menu.Items.Add(exitItem);

            trayIcon.Menu = menu;
            trayIcon.Clicked += (s, e) => { this.Show(); this.WindowState = WindowState.Normal; };

            var trayIcons = new TrayIcons { trayIcon };
            if (Application.Current != null)
            {
                TrayIcon.SetIcons(Application.Current, trayIcons);
            }
        }
    }

    private ContextMenu CreateSharedContextMenu(string? targetCode)
    {
        var menu = new ContextMenu();
        
        var addItem = new MenuItem { Header = "添加股票" };
        addItem.Click += AddStockItem_Click;
        menu.Items.Add(addItem);
        
        var autoPickItem = new MenuItem { Header = "自动选股" };
        autoPickItem.Click += AutoPickItem_Click;
        menu.Items.Add(autoPickItem);
        
        menu.Items.Add(new Separator());

        if (!string.IsNullOrEmpty(targetCode))
        {
            var delItem = new MenuItem { Header = $"删除 [{targetCode}]" };
            delItem.Click += (s, e) => {
                _stocks.Remove(targetCode);
                SaveConfig();
                _ = UpdatePrices();
            };
            menu.Items.Add(delItem);
        }

        menu.Items.Add(new Separator());

        var sourceMenu = new MenuItem { Header = "选股数据源" };
        var emItem = new MenuItem { Header = "东方财富" };
        emItem.Click += (s, e) => { _dataSource = "Eastmoney"; _ = UpdatePrices(); };
        var ttItem = new MenuItem { Header = "腾讯" };
        ttItem.Click += (s, e) => { _dataSource = "Tencent"; _ = UpdatePrices(); };
        var yhItem = new MenuItem { Header = "雅虎财经" };
        yhItem.Click += (s, e) => { _dataSource = "Yahoo"; _ = UpdatePrices(); };
        
        sourceMenu.Items.Add(emItem);
        sourceMenu.Items.Add(ttItem);
        sourceMenu.Items.Add(yhItem);
        menu.Items.Add(sourceMenu);

        menu.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "清空全部" };
        clearItem.Click += RemoveStockItem_Click;
        menu.Items.Add(clearItem);

        // ─── 新增：AI 分析 & 配置设置 ───
        menu.Items.Add(new Separator());

        var aiItem = new MenuItem { Header = "🔬 AI 分析自选股" };
        // 手动点击：既要看到界面（hideUi: false），又能触发邮件发送（如果配置了的话）
        aiItem.Click += async (s, e) => await RunAiStockAnalysisAsync(sendEmail: true, hideUi: false);
        menu.Items.Add(aiItem);

        var settingsMenu = new MenuItem { Header = "⚙️ 配置设置" };

        var geminiCfg = new MenuItem { Header = "🤖 AI / 邮件 / 定时任务" };
        geminiCfg.Click += async (s, e) => await ShowSettingsWindowAsync();
        settingsMenu.Items.Add(geminiCfg);

        var marketReviewItem = new MenuItem { Header = "🔬 AI 大盘复盘分析" };
        marketReviewItem.Click += async (s, e) => await RunMarketAnalysisAsync(sendNotification: true, hideUi: false);
        menu.Items.Add(marketReviewItem);

        var scheduleToggle = new MenuItem { Header = "⏰ 定时任务" };
        scheduleToggle.Click += (s, e) =>
        {
            _appSettings.ScheduleEnabled = !_appSettings.ScheduleEnabled;
            SaveSettings();
            SetupScheduleTimer();
        };
        settingsMenu.Items.Add(scheduleToggle);

        menu.Items.Add(settingsMenu);

        // 动态刷新：每次菜单在展开前计算最新的状态表现，避免静态绑定导致的不刷新问题
        menu.Opening += (s, e) =>
        {
            _isContextMenuOpen = true;
            emItem.Header = (_dataSource == "Eastmoney" ? "√ " : "  ") + "东方财富";
            ttItem.Header = (_dataSource == "Tencent" ? "√ " : "  ") + "腾讯";
            yhItem.Header = (_dataSource == "Yahoo" ? "√ " : "  ") + "雅虎财经";
            scheduleToggle.Header = _appSettings.ScheduleEnabled ? "⏰ 定时任务: 开启 (点击关闭)" : "⏰ 定时任务: 关闭 (点击开启)";
        };

        menu.Closing += (s, e) => 
        {
            _isContextMenuOpen = false;
        };

        return menu;
    }

    #region Window Dragging

    private void OnWindowDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    #endregion

    private void BtnMin_Click(object? sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _scheduleTimer?.Stop(); // 新增：关闭定时任务
        _httpClient?.Dispose();
        this.Close();
    }

    private async void AddStockItem_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new AddStockWindow();
        await dialog.ShowDialog(this);
        
        string? input = dialog.Result?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        System.Text.RegularExpressions.Regex codeRegex = new(@"^\d{6}$");
        System.Text.RegularExpressions.Regex fullCodeRegex = new(@"^(sh|sz|bj)\d{6}$");
        
        string? targetCode = null;

        if (codeRegex.IsMatch(input) || fullCodeRegex.IsMatch(input.ToLower()))
        {
            targetCode = input.ToLower();
        }
        else
        {
            // Try searching by name
            targetCode = await SearchStockCode(input);
            if (targetCode == null)
            {
                // Simple hint using placeholder text or just ignoring
                // For better UX, we could re-open dialog or show a temporary row
                Dispatcher.UIThread.Post(() => {
                    var placeholder = this.FindControl<TextBlock>("PlaceholderText");
                    if (placeholder != null) {
                        placeholder.Text = $"未找到: {input}";
                        placeholder.Foreground = Brushes.Red;
                        Task.Delay(3000).ContinueWith(_ => Dispatcher.UIThread.Post(() => {
                            placeholder.Text = "右键添加股票";
                            placeholder.Foreground = Brush.Parse("#FFB0B0B0");
                        }));
                    }
                });
                return;
            }
        }

        if (targetCode != null && !_stocks.Contains(targetCode))
        {
            _stocks.Add(targetCode);
            SaveConfig();
            await UpdatePrices();
        }
    }

    private async Task<string?> SearchStockCode(string input)
    {
        try
        {
            // Use Sina Suggest API
            string url = $"http://suggest3.sinajs.cn/suggest/type=11,12,31&key={Uri.EscapeDataString(input)}";
            if (_httpClient == null) return null;
            
            var bytes = await _httpClient.GetByteArrayAsync(url);
            string response = Encoding.GetEncoding("GB2312").GetString(bytes);

            // var suggestdata="贵州茅台,11,600519,sh600519,贵州茅台,,贵州茅台,99";
            int start = response.IndexOf('"');
            int end = response.LastIndexOf('"');
            if (start != -1 && end > start)
            {
                string data = response.Substring(start + 1, end - start - 1);
                if (string.IsNullOrWhiteSpace(data)) return null;

                string first = data.Split(';')[0];
                var parts = first.Split(',');
                if (parts.Length >= 3)
                {
                    return parts[2]; // 6-digit code
                }
            }
        }
        catch { }
        return null;
    }

    private async void RemoveStockItem_Click(object? sender, RoutedEventArgs e)
    {
        _stocks.Clear();
        SaveConfig();
        await UpdatePrices();
    }

    private async void AutoPickItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_isScreenerRunning) return;
        _isScreenerRunning = true;
        
        // Show loading state by replacing all rows with a single loading message
        Dispatcher.UIThread.Invoke(() =>
        {
            var container = this.FindControl<Grid>("StockContainer");
            if (container != null)
            {
                container.Children.Clear();
                container.RowDefinitions.Clear();
                container.ColumnDefinitions.Clear();
                
                var tb = new TextBlock
                {
                    Text = "⏳ 正在全网寻妖(约需15秒)...",
                    Foreground = Brush.Parse("#FFFFCC00"), // Highlighted yellow
                    FontSize = 12,
                    FontFamily = new FontFamily("Courier New"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5)
                };
                container.Children.Add(tb);
            }
        });

        try
        {
            var container = this.FindControl<Grid>("StockContainer");
            await RunNativeScreener(container);
        }
        catch (Exception ex) 
        { 
            Program.LogError("AutoPickItem_Click Exception", ex);
        }
        finally
        {
            _isScreenerRunning = false;
            // Reload config and pricing after the native screener finishes
            Dispatcher.UIThread.Post(async () => {
                LoadConfig();
                await UpdatePrices();
            });
        }
    }

    private async Task RunNativeScreener(Grid? container)
    {
        void UpdateLoadingText(string msg)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (container != null && container.Children.Count > 0 && container.Children[0] is TextBlock tb)
                {
                    tb.Text = msg;
                }
            });
        }

        UpdateLoadingText("⏳ 正在分析大盘环境...");

        // 0. 检查大盘环境
        var marketEnv = await CheckMarketEnvironmentAsync();
        if (!marketEnv.IsValid)
        {
            UpdateLoadingText("⚠️ 大盘环境恶劣，建议空仓观望");
            await Task.Delay(2000);
            return;
        }

        string marketStatus = marketEnv.IsBullish ? "🟢 大盘多头，可积极参与" :
                              marketEnv.IsNeutral ? "🟡 大盘震荡，谨慎参与" :
                              "🔴 大盘转弱，控制仓位";
        UpdateLoadingText($"{marketStatus} | 正在筛选个股...");

        // 1. Get Base Stocks
        var baseStocks = await GetBaseStocksAsync();
        if (baseStocks.Count == 0)
        {
            UpdateLoadingText("❌ 基础池获取失败，请检查网络");
            await Task.Delay(2000);
            return;
        }

        // 改进 7: 候选池排序策略 - 使用真实涨幅优先选启动初期的票
        // 真实涨幅区间优先级：-2%~+3%（启动初期） > +3%~+5%（强势启动） > +5%~+8%（加速中） > 其他
        var processList = baseStocks
            .OrderBy(s =>
            {
                // 使用真实涨幅（ChangePercent）而不是换手率反推
                if (s.ChangePercent >= -2.0 && s.ChangePercent <= 3.0) return 0; // 启动初期，最优
                if (s.ChangePercent > 3.0 && s.ChangePercent <= 5.0) return 1; // 强势启动
                if (s.ChangePercent > 5.0 && s.ChangePercent <= 8.0) return 2; // 加速中
                return 3; // 追高风险或弱势
            })
            .ThenBy(s => Math.Abs(s.ChangePercent)) // 同组内涨幅小的优先
            .Take(500).ToList();
        
        var passedStocks = new List<(string Code, string Name, double Price, double Ma20, double Ma200, double Pe, double MarketCap, string Concepts, string BuyPoint, double Score)>();

        int tested = 0;
        foreach (var stock in processList)
        {
            tested++;
            if (tested % 10 == 0)
            {
                UpdateLoadingText($"⏳ K线深度体检中 [{tested}/{processList.Count}]... 已发现 {passedStocks.Count} 只");
            }

            try
            {
                var techResult = await CheckTechnicalAndMomentumAsync(stock.Code, stock.Name, stock.Price, stock.Pe, stock.MarketCap, stock.Turnover, marketEnv);
                if (techResult.HasValue)
                {
                    string concepts = await GetStockConceptsAsync(stock.Code);
                    passedStocks.Add((stock.Code, stock.Name, stock.Price, techResult.Value.Ma20, techResult.Value.Ma200, stock.Pe, stock.MarketCap, concepts, techResult.Value.BuyPoint, techResult.Value.Score));
                }
                await Task.Delay(200 + Random.Shared.Next(100)); // Adaptive throttling to avoid IP block (150ms-350ms)
            }
            catch (Exception ex)
            {
                Program.LogError($"Screener loop Error for {stock.Code} {stock.Name}:", ex);
            }
        }

        if (passedStocks.Count > 0)
        {
            UpdateLoadingText($"✅ 筛选完毕！找到 {passedStocks.Count} 只强势标的。正在注入...");
            await Task.Delay(1000);

            // 改进：按综合评分降序排序（Score越高越好），同分内按市值升序
            passedStocks = passedStocks.OrderByDescending(s => s.Score).ThenBy(s => s.MarketCap).ToList();
            
            bool anyNew = false;
            foreach (var s in passedStocks)
            {
                if (!_stocks.Contains(s.Code))
                {
                    _stocks.Add(s.Code);
                    anyNew = true;
                }
            }
            
            if (anyNew)
            {
                SaveConfig();
            }
        }
        else
        {
            UpdateLoadingText("⚠️ 盘面极度弱势或无符合条件的标的，空仓观望");
            await Task.Delay(2000);
        }
    }

    private class StockBasic
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public double Price { get; set; }
        public double Turnover { get; set; }
        public double Pe { get; set; }
        public double MarketCap { get; set; }
        public double ChangePercent { get; set; } // 新增：真实涨幅
    }

    private class MarketEnvironment
    {
        public bool IsValid { get; set; }
        public bool IsBullish { get; set; }
        public bool IsNeutral { get; set; }
        public bool IsBearish { get; set; }
        public double IndexMa20 { get; set; }
        public double IndexMa60 { get; set; }
        public double CurrentIndex { get; set; }
    }

    private async Task<MarketEnvironment> CheckMarketEnvironmentAsync()
    {
        try
        {
            // 获取上证指数 K线数据 (000001)
            string url = "http://push2his.eastmoney.com/api/qt/stock/kline/get?secid=1.000001&ut=7eea3edcaed734bea9cbbc2440b282fb&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=1&end=20500101&lmt=100";

            string jsonStr = "";
            if (_dataSource == "Tencent")
            {
                // Specifically use SH Composite Index code for Tencent
                jsonStr = await FetchKLinesFromTencentAsync("sh000001");
            }
            else if (_dataSource == "Yahoo")
            {
                jsonStr = await FetchKLinesFromYahooAsync("000001.SS");
            }
            else
            {
                // 使用重试机制获取大盘数据
                jsonStr = await HttpGetWithRetryAsync(url, maxRetries: 2);
            }

            if (string.IsNullOrEmpty(jsonStr))
            {
                return new MarketEnvironment { IsValid = true, IsNeutral = true };
            }

            var root = JObject.Parse(jsonStr);
            var klines = root["data"]?["klines"] as JArray;

            if (klines == null || klines.Count < 60)
            {
                return new MarketEnvironment { IsValid = true, IsNeutral = true }; // 数据不足，允许通过
            }

            var closes = new List<double>();
            foreach (var k in klines)
            {
                var parts = k.ToString().Split(',');
                if (parts.Length >= 3)
                {
                    // parts[0]是日期，parts[1]是开盘，parts[2]是收盘价
                    if (double.TryParse(parts[2], out double closeVal))
                    {
                        closes.Add(closeVal);
                    }
                }
            }

            int count = closes.Count;
            double current = closes[count - 1];
            double ma20 = closes.Skip(count - 20).Average();
            double ma60 = closes.Skip(count - 60).Average();

            // 大盘环境判断逻辑
            var env = new MarketEnvironment
            {
                IsValid = true,
                CurrentIndex = current,
                IndexMa20 = ma20,
                IndexMa60 = ma60
            };

            // 多头：指数在 MA20 上方，且 MA20 > MA60
            if (current > ma20 && ma20 > ma60)
            {
                env.IsBullish = true;
            }
            // 空头：指数跌破 MA20
            else if (current < ma20)
            {
                env.IsBearish = true;
            }
            // 震荡：指数在 MA20 和 MA60 之间
            else
            {
                env.IsNeutral = true;
            }

            return env;
        }
        catch (Exception ex)
        {
            Program.LogError("CheckMarketEnvironmentAsync Error", ex);
            // 网络错误时允许通过，避免完全无法使用
            return new MarketEnvironment { IsValid = true, IsNeutral = true };
        }
    }

    private async Task<List<StockBasic>> GetBaseStocksAsync()
    {
        try
        {
            // 使用 push2 而不是 82.push2 提高稳定性
            string url = "http://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=10000&po=1&np=1&ut=bd1d9ddb04089700cf9c27f6f7426281&fltt=2&invt=2&fid=f3&fs=m:0+t:6,m:0+t:80,m:1+t:2,m:1+t:23,m:0+t:81+s:2048&fields=f12,f14,f2,f3,f8,f9,f20";
            string jsonStr = await HttpGetWithRetryAsync(url, maxRetries: 2);
            var root = JObject.Parse(jsonStr);
            var items = root["data"]?["diff"] as JArray;

            if (items == null) return new List<StockBasic>();

            var list = new List<StockBasic>();
            foreach (var item in items)
            {
                string name = item["f14"]?.ToString() ?? "";
                if (name.Contains("ST") || name.Contains("退")) continue;

                if (double.TryParse(item["f2"]?.ToString(), out double price) && price > 0 &&
                    double.TryParse(item["f3"]?.ToString(), out double changePercent) &&
                    double.TryParse(item["f8"]?.ToString(), out double turnover) &&
                    double.TryParse(item["f9"]?.ToString(), out double pe) &&
                    double.TryParse(item["f20"]?.ToString(), out double marketCap))
                {
                    // Core Filter 1: PE (放开限制，妖股往往亏损，或者稍微过滤极大负数即可)
                    // if (pe <= 0) continue;

                    // Core Filter 2: 市值 15亿 ~ 500亿 (排除过小或过大的公司)
                    if (marketCap <= 1500000000 || marketCap >= 50000000000) continue;

                    // Core Filter 3: 根据市值动态设置换手率范围（放宽换手率限制）
                    // 小盘股（<50亿）：换手率 2%-20% 合理
                    // 中盘股（50-200亿）：换手率 1.5%-15% 合理
                    // 大盘股（>200亿）：换手率 0.8%-12% 合理
                    double minTurnover, maxTurnover;
                    if (marketCap < 5000000000) // < 50亿
                    {
                        minTurnover = 2.0;
                        maxTurnover = 20.0;
                    }
                    else if (marketCap < 20000000000) // 50-200亿
                    {
                        minTurnover = 1.5;
                        maxTurnover = 15.0;
                    }
                    else // > 200亿 (及至500亿上限)
                    {
                        minTurnover = 0.8;
                        maxTurnover = 12.0;
                    }

                    if (turnover < minTurnover || turnover > maxTurnover) continue;

                    // 额外过滤：换手率异常高（>25%）可能是诱多
                    if (turnover > 25.0) continue;

                    list.Add(new StockBasic
                    {
                        Code = item["f12"]?.ToString() ?? "",
                        Name = name,
                        Price = price,
                        Turnover = turnover,
                        Pe = pe,
                        MarketCap = marketCap,
                        ChangePercent = changePercent // 新增：真实涨幅
                    });
                }
            }
            return list;
        }
        catch (Exception ex) 
        { 
            Program.LogError("GetBaseStocksAsync API Failure", ex);
            return new List<StockBasic>(); 
        }
    }

    private async Task<(double Ma20, double Ma200, string BuyPoint, double Score)?> CheckTechnicalAndMomentumAsync(string symbol, string name, double currentPrice, double pe, double marketCap, double currentTurnover, MarketEnvironment? marketEnv = null)
    {
        try
        {
            // 根据代码判断市场前缀: 1=上海, 0=深圳/北京
            // 上交所: 5/6/7/9 开头; 深交所: 0/1/2/3 开头; 北交所: 4/8 开头
            string marketPrefix = (symbol.StartsWith("6") || symbol.StartsWith("9") ||
                                   symbol.StartsWith("5") || symbol.StartsWith("7")) ? "1" : "0";
            string secid = marketPrefix + "." + symbol;
            string url = $"http://push2his.eastmoney.com/api/qt/stock/kline/get?secid={secid}&ut=7eea3edcaed734bea9cbbc2440b282fb&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=1&end=20500101&lmt=250";

            string jsonStr = "";
            if (_dataSource == "Tencent")
            {
                jsonStr = await FetchKLinesFromTencentAsync(symbol);
            }
            else if (_dataSource == "Yahoo")
            {
                // 雅虎未收录北交所（8/9/4字头），提前拦截以防浪费请求
                if (symbol.StartsWith("8") || symbol.StartsWith("9") || symbol.StartsWith("4")) return null;

                string suffix = (symbol.StartsWith("6") || symbol.StartsWith("11") || symbol.StartsWith("5")) ? ".SS" : ".SZ";
                jsonStr = await FetchKLinesFromYahooAsync(symbol + suffix);
            }
            else
            {
                // 使用重试机制获取K线数据
                jsonStr = await HttpGetWithRetryAsync(url, maxRetries: 2);
            }

            if (string.IsNullOrEmpty(jsonStr)) return null;

            var root = JObject.Parse(jsonStr);
            var klines = root["data"]?["klines"] as JArray;

            if (klines == null || klines.Count < 200) return null; // Listed < 1 year

            var closes = new List<double>();
            var vols = new List<double>();
            var pcts = new List<double>();
            var highs = new List<double>();
            var lows = new List<double>();
            var opens = new List<double>();
            var turnovers = new List<double>();

            foreach (var k in klines)
            {
                var parts = k.ToString().Split(',');
                if (parts.Length >= 9)
                {
                    // parts[0]是日期，跳过；parts[1]是开盘价，parts[2]是收盘价，parts[3]是最高价，parts[4]是最低价
                    // 东方财富格式: "日期,开盘,收盘,最高,最低,成交量,..."
                    // 注意：parts[0]是日期字符串，不是数值

                    // 确保所有必需字段都能成功解析，否则跳过这条K线
                    if (!double.TryParse(parts[1], out double openVal) ||
                        !double.TryParse(parts[2], out double closeVal) ||
                        !double.TryParse(parts[3], out double highVal) ||
                        !double.TryParse(parts[4], out double lowVal) ||
                        !double.TryParse(parts[5], out double volVal) ||
                        !double.TryParse(parts[8], out double pctVal))
                    {
                        continue; // 跳过解析失败的K线
                    }

                    opens.Add(openVal);
                    closes.Add(closeVal);
                    highs.Add(highVal);
                    lows.Add(lowVal);
                    vols.Add(volVal);
                    pcts.Add(pctVal);

                    double t = 0;
                    if (parts.Length >= 11) double.TryParse(parts[10], out t);
                    turnovers.Add(t);
                }
            }

            int count = closes.Count;
            double adjustedCurrent = closes.Last();
            double ma200 = closes.Skip(count - 200).Average();
            double ma20 = closes.Skip(count - 20).Average();
            double ma10 = closes.Skip(count - 10).Average();
            double ma5 = closes.Skip(count - 5).Average();
            double avgVol = vols.Skip(count - 20).Average();

            // 综合评分系统（初始分100）
            double finalScore = 100.0;

            // --- 改造为“即将启动、涨跌幅不大”核心过滤 ---
            // 核心风控：大A追高胜率极低，直接排除涨幅过大的票，寻找蓄势起跳板
            double gain5d = pcts.Skip(count - 5).Sum();
            double gain10d = pcts.Skip(count - 10).Sum();
            double gain20d = pcts.Skip(count - 20).Sum();

            // 1. 压制顶部涨幅，确保还没起飞 (放宽以适应A股本身的高波动)
            if (gain5d < -8.0 || gain5d > 15.0) return null;
            if (gain10d > 25.0) return null;
            if (gain20d > 40.0 || gain20d < -18.0) return null;

            // 2. 拒绝过去5天内有过连续拉升的（改为减分而非硬淘汰，避免与MACD金叉矛盾）
            var recent5PctEarlyCheck = pcts.Skip(count - 5).ToList();
            int bigDayCount = recent5PctEarlyCheck.Count(p => p > 8.0);
            if (bigDayCount >= 2) return null; // 2次以上大阳 → 已起飞，淘汰
            if (bigDayCount == 1) finalScore -= 15; // 有1次大阳但其他条件好 → 减分不放弃

            // 3. 核心蓄势特征：近10日纯横盘振幅测算（改为减分，避免与金叉矛盾）
            var recent10High = highs.Skip(count - 10).ToList();
            var recent10Low = lows.Skip(count - 10).ToList();
            double highest10 = recent10High.Max();
            double lowest10 = recent10Low.Min();
            double consolidationRange = (highest10 - lowest10) / lowest10;
            // A股10天内振幅，>22% 说明已经起飞或剧烈波动 → 淘汰
            if (consolidationRange > 0.22) return null;
            if (consolidationRange > 0.15) finalScore -= 10; // 振幅偏大减分不淘汰

            // 长期高位过滤：偏离半年线(MA200)超过50%说明已进入高位长期盘整区，抛弃
            if (adjustedCurrent > ma200 * 1.5) return null;

            // --- 改进 1: MACD 策略优化（支持水下金叉拐点）---
            double ema12 = closes[0], ema26 = closes[0];
            double dea = 0;
            var difs = new List<double>();
            for (int i = 0; i < count; i++) {
                ema12 = ema12 * 11/13 + closes[i] * 2/13;
                ema26 = ema26 * 25/27 + closes[i] * 2/27;
                double dif = ema12 - ema26;
                difs.Add(dif);
                dea = dea * 8/10 + dif * 2/10;
            }
            double finalDif = difs.Last();
            double prevDif5 = difs.Count > 5 ? difs[^5] : difs.First();

            // 模式A：水上金叉（稳健）
            bool isGoldenCrossAboveWater = finalDif > 0 && finalDif > dea;
            // 模式B：水下金叉拐点（进攻，胜率更高）
            bool isGoldenCrossBelowWater = finalDif < 0 && finalDif > dea && (finalDif - prevDif5) > 0.01;

            if (!isGoldenCrossAboveWater && !isGoldenCrossBelowWater) return null;

            // 底背离检查：股价创新低但MACD未创新低
            if (count >= 20) {
                var recent20Closes = closes.Skip(count - 20).ToList();
                var recent20Difs = difs.Skip(count - 20).ToList();
                double priceMin = recent20Closes.Min();
                double difMin = recent20Difs.Min();
                int priceMinIdx = recent20Closes.IndexOf(priceMin);
                int difMinIdx = recent20Difs.IndexOf(difMin);

                // 股价近期创新低但DIF未创新低 → 底背离
                if (priceMinIdx > difMinIdx && priceMinIdx >= 15) {
                    finalScore += 15; // 底背离加分
                }
            }

            // 水上金叉加分，水下拐点中性
            if (isGoldenCrossAboveWater) finalScore += 10;
            else finalScore += 5; // 水下拐点少量加分

            // --- 改进 2: ATR 波动率过滤与评分 ---
            double totalTr = 0;
            for (int i = count - 20; i < count; i++) {
                double tr = Math.Max(highs[i] - lows[i], Math.Max(Math.Abs(highs[i] - (i > 0 ? closes[i-1] : opens[i])), Math.Abs(lows[i] - (i > 0 ? closes[i-1] : opens[i]))));
                totalTr += tr;
            }
            double atr = totalTr / 20;
            double atrRatio = (atr / adjustedCurrent) * 100;
            // 缩窄限制：寻找底部蓄势票，容许极低波动(0.3%起)，拒绝剧烈波动(>8%)
            if (atrRatio > 8.0 || atrRatio < 0.3) return null;

            // ATR在1-3%区间最理想，死寂状态
            if (atrRatio <= 3.0) finalScore += 5;

            // --- 改进 3: 量能形态识别（新增）---
            // 阶梯放量：近10日成交量呈递增趋势
            if (count >= 10) {
                var recent10Vol = vols.Skip(count - 10).ToList();
                bool isAscending = true;
                for (int i = 1; i < 10; i++) {
                    if (recent10Vol[i] <= recent10Vol[i-1] * 0.9) {
                        isAscending = false;
                        break;
                    }
                }
                if (isAscending) finalScore += 10; // 阶梯放量建仓信号
            }

            // 堆量形态：近5日量 > 均量 且 振幅 < 5%
            if (count >= 5) {
                var recent5Vol = vols.Skip(count - 5).ToList();
                var recent5High = highs.Skip(count - 5).ToList();
                var recent5Low = lows.Skip(count - 5).ToList();
                bool isHighVol = recent5Vol.All(v => v > avgVol * 0.9);
                bool isLowVolatility = true;
                for (int i = 0; i < 5; i++) {
                    double amplitude = ((recent5High[i] - recent5Low[i]) / closes[count - 5 + i]) * 100;
                    if (amplitude > 5.0) {
                        isLowVolatility = false;
                        break;
                    }
                }
                if (isHighVol && isLowVolatility) finalScore += 8; // 堆量蓄势
            }

            // 缩量横盘：近10日股价横盘(±3%) 且 量能递减
            if (count >= 10) {
                var recent10Close = closes.Skip(count - 10).ToList();
                var recent10Vol = vols.Skip(count - 10).ToList();
                double maxPrice = recent10Close.Max();
                double minPrice = recent10Close.Min();
                double priceRange = ((maxPrice - minPrice) / minPrice) * 100;

                bool isPriceConsolidation = priceRange < 3.0;
                bool isVolumeDecreasing = true;
                for (int i = 1; i < 10; i++) {
                    if (recent10Vol[i] >= recent10Vol[i-1] * 1.1) {
                        isVolumeDecreasing = false;
                        break;
                    }
                }
                if (isPriceConsolidation && isVolumeDecreasing) finalScore += 12; // 洗盘完成
            } 

            // --- A: 趋势基础 ---
            if (adjustedCurrent < ma200) return null;
            // 修复矛盾点：要求MA5>MA10>MA20且乖离率小是矛盾的，这里放宽为只要站上MA20且长期多头即可
            if (adjustedCurrent < ma20 * 0.98) return null;

            // --- B: 连续下跌过滤 ---
            var recent5Pct = pcts.Skip(count - 5).ToList();
            int consecutiveDownDays = 0;
            foreach (var pct in recent5Pct) { if (pct < 0) consecutiveDownDays++; else break; }
            if (consecutiveDownDays >= 4) return null;

            // --- 改进 4: 盈亏比计算（新增风控）---
            // 修复：止损价应该是近10日最低价或MA20的较低者（或最低支撑），而不是最高价
            double stopLossPrice = Math.Min(ma20, lows.Skip(count - 10).Min());
            double targetPrice = adjustedCurrent * 1.08;
            double riskRewardRatio = (targetPrice - adjustedCurrent) / Math.Max(0.01, adjustedCurrent - stopLossPrice);

            if (riskRewardRatio < 1.5) return null; // 盈亏比 >= 1.5

            // 盈亏比越高，加分越多
            if (riskRewardRatio >= 3.0) finalScore += 10;
            else if (riskRewardRatio >= 2.0) finalScore += 5;

            // --- C: 优化买点逻辑 (收紧偏差到极小，要求伏击位置) ---
            string buyPoint = "";
            bool isValidBuyPoint = false;
            double ma20Deviation = (adjustedCurrent / ma20 - 1) * 100;
            double lastPct = recent5Pct.Last();
            double lastVol = vols.Last();

            // 针对蓄势潜伏，放宽偏差到 -6% ~ +10%（原 -4%~+6% 过于严苛）
            if (ma20Deviation < -6.0 || ma20Deviation > 10.0) return null;

            // 1. 静谧潜伏：横盘不跌，量能极度萎缩
            if (lastVol < avgVol * 0.85 && Math.Abs(lastPct) < 3.0)
            {
                buyPoint = "缩量潜伏";
                isValidBuyPoint = true;
                finalScore += 15;
            }
            // 2. 试盘苗头：今天收阳，量能温和放大，开始起启动预演
            else if (lastPct > 0.5 && lastPct < 4.5 && lastVol > avgVol * 1.1)
            {
                buyPoint = "温和试盘";
                isValidBuyPoint = true;
                finalScore += 12;
            }
            // 3. 其他平稳横盘状态兜底
            else if (Math.Abs(lastPct) < 3.0)
            {
                buyPoint = "横盘蓄势";
                isValidBuyPoint = true;
                finalScore += 5;
            }

            // 如果连横盘特征都不符合（当天波动剧烈），直接废弃
            if (!isValidBuyPoint) return null;

            // --- 改进 5: 连板风险识别（已放宽，妖股本身就是连板居多）---
            // 注释掉原有的严格剔除，让强势股能够进入候选池
            // int limitUps = pcts.Skip(count - 5).Count(p => p > 9.7);
            // if (limitUps >= 2) return null; // 5日内2板以上直接剔除
            
            // 检查连续3日大涨（加速赶顶）
            var recent3Pct = pcts.Skip(count - 3).ToList();
            if (recent3Pct.Sum() > 40.0) return null; // 放宽为连续3天涨幅超过40%（比如20cm三连板）才剔除

            // 检查连续涨停（今日涨停+昨日涨停）
            // if (recent5Pct.Count >= 2) {
            //     bool isTodayLimitUp = lastPct > 9.7;
            //     bool isYesterdayLimitUp = recent5Pct[^2] > 9.7;
            //     if (isTodayLimitUp && isYesterdayLimitUp) return null; // 连板风险
            // }

            // --- 改进 6: 量价健康度评分 (5日量价一致性) ---
            int volumePriceScore = 0;
            var vpRecent5Vol = vols.Skip(count - 5).ToList();
            var recent5Price = closes.Skip(count - 5).ToList();
            var prevPrice = closes[count - 6];

            for (int i = 0; i < 5; i++)
            {
                double currentP = recent5Price[i];
                double currentV = vpRecent5Vol[i];
                double prevP = i == 0 ? prevPrice : recent5Price[i - 1];
                double prevV = i == 0 ? vols[count - 6] : vpRecent5Vol[i - 1];

                bool isPriceUp = currentP > prevP;
                bool isVolUp = currentV > prevV;

                if (isPriceUp && isVolUp) volumePriceScore += 2; // 价涨量增，健康
                else if (isPriceUp && !isVolUp) volumePriceScore += 1; // 缩量上涨，洗盘
                else if (!isPriceUp && isVolUp) volumePriceScore -= 2; // 放量下杀，风险
            }
            // 严格执行计划：总分 < 0 排除
            if (volumePriceScore < 0) return null;

            // --- D: 活跃度要求 ---
            if (_dataSource == "Tencent" || _dataSource == "Yahoo") {
                // 放宽腾讯和雅虎源活跃度限制，二者历史K线接口不含换手率
                if (currentTurnover <= 2.0) return null;
            }
            else {
                var recent5T = turnovers.Skip(count - 5).ToList();
                if (recent5T.Max() <= 3.5 || recent5T.Average() <= 1.8) return null;
            }

            // --- E: 大盘环境过滤 (熊市不选股) ---
            if (marketEnv != null)
            {
                if (marketEnv.IsBearish)
                {
                    finalScore *= 0.7; // 熊市打7折，显著降低通过率
                }
                else if (marketEnv.IsNeutral)
                {
                    finalScore *= 0.9; // 震荡市打9折
                }
                // 多头市场不折扣
            }

            return (ma20, ma200, buyPoint, finalScore);
        }
        catch (Exception ex)
        {
            Program.LogError($"CheckTechnicalAndMomentumAsync API Failure for {symbol}", ex);
            return null;
        }
    }

    private async Task<string> FetchKLinesFromTencentAsync(string symbol)
    {
        try
        {
            string fullSymbol = symbol;
            if (!symbol.StartsWith("sh") && !symbol.StartsWith("sz"))
            {
                string prefix = (symbol.StartsWith("6") || symbol.StartsWith("9") || symbol.StartsWith("11")) ? "sh" : "sz";
                fullSymbol = prefix + symbol;
            }
            
            string url = $"http://web.ifzq.gtimg.cn/appstock/app/fqkline/get?_var=kline_dayqfq&param={fullSymbol},day,,,255,qfq";
            
            var response = await _httpClient!.GetStringAsync(url);
            if (string.IsNullOrEmpty(response)) return "";

            // Remove variable prefix if exists
            if (response.Contains("=")) response = response.Substring(response.IndexOf('=') + 1);
            
            var root = JObject.Parse(response);
            var dayData = root["data"]?[fullSymbol]?["qfqday"] as JArray ?? root["data"]?[fullSymbol]?["day"] as JArray;
            if (dayData == null) return "";

            // Convert Tencent format [date, open, close, high, low, vol] 
            // to Eastmoney format "date,open,close,high,low,vol,..."
            var emKlines = new JArray();
            double prevClose = -1;
            foreach (var k in dayData)
            {
                var p = k as JArray;
                if (p != null && p.Count >= 6)
                {
                    double close = 0;
                    double.TryParse(p[2]?.ToString(), out close);
                    
                    double pct = 0;
                    if (prevClose > 0)
                    {
                        pct = (close / prevClose - 1) * 100;
                    }
                    prevClose = close;

                    // Map to: f51(0), f52(1), f53(2), f54(3), f55(4), f56(5), f57(6), f58(7), f59(8)
                    // Index 8 is PCT (normalized to f59)
                    string emLine = $"{p[0]},{p[1]},{p[2]},{p[3]},{p[4]},{p[5]},0,0,{pct:F2},0,0"; 
                    emKlines.Add(emLine);
                }
            }

            var emRoot = new JObject
            {
                ["data"] = new JObject
                {
                    ["klines"] = emKlines
                }
            };

            return emRoot.ToString();
        }
        catch (Exception ex)
        {
            Program.LogError($"FetchKLinesFromTencentAsync Error for {symbol}", ex);
            return "";
        }
    }

    private async Task<string> FetchKLinesFromYahooAsync(string symbol)
    {
        try
        {
            string url = $"https://query2.finance.yahoo.com/v8/finance/chart/{symbol}?interval=1d&range=1y";
            var response = await HttpGetWithRetryAsync(url, maxRetries: 2);
            if (string.IsNullOrEmpty(response)) return "";

            var root = JObject.Parse(response);
            var result = root["chart"]?["result"] as JArray;
            if (result == null || result.Count == 0) return "";

            var timestamps = result[0]?["timestamp"] as JArray;
            var quote = result[0]?["indicators"]?["quote"] as JArray;
            
            if (timestamps == null || quote == null || quote.Count == 0) return "";

            var opens = quote[0]?["open"] as JArray;
            var closes = quote[0]?["close"] as JArray;
            var highs = quote[0]?["high"] as JArray;
            var lows = quote[0]?["low"] as JArray;
            var volumes = quote[0]?["volume"] as JArray;

            if (opens == null || closes == null || highs == null || lows == null || volumes == null) return "";

            var emKlines = new JArray();
            int count = Math.Min(timestamps.Count, closes.Count);
            double prevClose = -1;

            for (int i = 0; i < count; i++)
            {
                if (closes[i] == null || closes[i].Type == JTokenType.Null) continue;

                double close = 0;
                double.TryParse(closes[i]?.ToString(), out close);

                double pct = 0;
                if (prevClose > 0)
                {
                    pct = (close / prevClose - 1) * 100;
                }
                prevClose = close;

                long ts = 0;
                long.TryParse(timestamps[i]?.ToString(), out ts);
                var dt = DateTimeOffset.FromUnixTimeSeconds(ts).ToString("yyyy-MM-dd");

                string emLine = $"{dt},{opens[i]},{closes[i]},{highs[i]},{lows[i]},{volumes[i]},0,0,{pct:F2},0,0"; 
                emKlines.Add(emLine);
            }

            var emRoot = new JObject
            {
                ["data"] = new JObject
                {
                    ["klines"] = emKlines
                }
            };

            return emRoot.ToString();
        }
        catch (Exception ex)
        {
            Program.LogError($"FetchKLinesFromYahooAsync Error for {symbol}", ex);
            return "";
        }
    }

    private async Task<string> GetStockConceptsAsync(string symbol)
    {
        try
        {
            string secucode = (symbol.StartsWith("0") || symbol.StartsWith("3")) ? $"{symbol}.SZ" : $"{symbol}.SH";
            string url = $"https://datacenter-web.eastmoney.com/api/data/v1/get?reportName=RPT_F10_CORETHEME_BOARDTYPE&columns=BOARD_NAME&filter=(SECUCODE=%22{secucode}%22)&pageNumber=1&pageSize=50";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0");
            
            var response = await _httpClient!.SendAsync(request);
            string jsonStr = await response.Content.ReadAsStringAsync();
            var root = JObject.Parse(jsonStr);
            
            var data = root["result"]?["data"] as JArray;
            if (data == null) return "无";

            var blacklist = new[] { "融资融券", "深股通", "沪股通", "标普走势", "MSCI中国", "富时罗素", " HS300", "深证100" };
            var tags = new List<string>();

            foreach (var item in data)
            {
                string boardName = item["BOARD_NAME"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(boardName) && !blacklist.Any(b => boardName.Contains(b)))
                {
                    tags.Add(boardName);
                }
            }

            return tags.Count > 0 ? string.Join(",", tags.Take(4)) : "无";
        }
        catch (Exception ex) 
        { 
            Program.LogError($"GetStockConceptsAsync API Failure for {symbol}", ex);
            return ""; 
        }
    }

    private void LoadConfig()
    {
        if (File.Exists(_configFile))
        {
            var lines = File.ReadAllLines(_configFile);
            _stocks = lines.Select(l => l.Trim().ToLower())
                          .Where(l => !string.IsNullOrEmpty(l))
                          .Distinct()
                          .ToList();
        }

        if (_stocks.Count == 0)
        {
            _stocks = new List<string> { "000001" };
            SaveConfig();
        }
    }

    private void SaveConfig()
    {
        try { File.WriteAllLines(_configFile, _stocks); } catch { }
    }

    private string GetPrefix(string code)
    {
        if (code.StartsWith("sh") || code.StartsWith("sz") || code.StartsWith("bj"))
            return code.Substring(0, 2);

        if (code.StartsWith("5") || code.StartsWith("6") || code.StartsWith("7") || code.StartsWith("9")) return "sh";
        if (code.StartsWith("0") || code.StartsWith("1") || code.StartsWith("2") || code.StartsWith("3")) return "sz";
        if (code.StartsWith("8") || code.StartsWith("4")) return "bj";
        return "sh";
    }

    private string GetSector(string code)
    {
        string pureCode = code.Length > 6 ? code.Substring(code.Length - 6) : code;
        string prefix = code.Length > 6 ? code.Substring(0, 2) : GetPrefix(code);

        if (prefix == "sh" && (pureCode.StartsWith("000") || pureCode.StartsWith("001"))) return "上证指数";
        if (prefix == "sz" && pureCode.StartsWith("399")) return "深证指数";
        
        if (pureCode.StartsWith("51") || pureCode.StartsWith("58")) return "沪市ETF";
        if (pureCode.StartsWith("15")) return "深市ETF";
        if (pureCode.StartsWith("16")) return "深市LOF";
        if (pureCode.StartsWith("501")) return "沪市LOF";
        if (pureCode.StartsWith("508")) return "沪市REITs";
        if (pureCode.StartsWith("180")) return "深市REITs";
        if (pureCode.StartsWith("50")) return "沪市基金";
        if (pureCode.StartsWith("18")) return "深市基金";
        if (pureCode.StartsWith("11") || pureCode.StartsWith("12")) return "可转债";
        if (pureCode.StartsWith("688")) return "科创板";
        if (pureCode.StartsWith("6")) return "上证主板";
        if (pureCode.StartsWith("3")) return "创业板";
        if (pureCode.StartsWith("0")) return "深证主板";
        if (pureCode.StartsWith("8") || pureCode.StartsWith("4")) return "北交所";
        return "A股";
    }

    private async Task<string> GetVolumePrediction(string fullCode, string pureCode, string stockName, double currentPrice, double open, double high, double low, double prevClose, double currentVolShares)
    {
        try
        {
            double totalVolume = 0;
            double recentTrend = 0;
            double ma20 = 0;
            double ma5 = 0;
            int count = 0;

            if (_klineCache.TryGetValue(fullCode, out var cache) && (DateTime.Now - cache.LastUpdated).TotalMinutes < 30)
            {
                totalVolume = cache.TotalVolume;
                count = cache.Count;
                recentTrend = cache.RecentTrend;
                ma5 = cache.MA5;

                // 重新从缓存计算正确的 MA
                if (count > 0)
                {
                    ma20 = cache.TotalClose / count;
                }
            }
            else
            {
                string klineUrl = $"https://quotes.sina.cn/cn/api/json_v2.php/CN_MarketData.getKLineData?symbol={fullCode}&scale=240&ma=no&datalen=20";
                if (_httpClient == null) return "系统忙";
                string jsonStr = await _httpClient.GetStringAsync(klineUrl);

                if (!string.IsNullOrWhiteSpace(jsonStr) && jsonStr != "null")
                {
                    JArray klines = JArray.Parse(jsonStr);
                    int limit = klines.Count;

                    if (limit > 0)
                    {
                        double historicalCloseSum = 0;
                        double ma5Sum = 0;
                        
                        // 计算 5 日趋势（更灵敏）
                        int trendStart = Math.Max(0, limit - 5);
                        double trendOpen = double.Parse(klines[trendStart]?["open"]?.ToString() ?? "0");
                        double trendClose = double.Parse(klines[limit-1]?["close"]?.ToString() ?? "0");
                        recentTrend = trendOpen > 0 ? (trendClose - trendOpen) / trendOpen * 100 : 0;

                        for (int i = 0; i < limit; i++)
                        {
                            var kline = klines[i];
                            if (kline != null)
                            {
                                if (double.TryParse(kline["volume"]?.ToString(), out double v))
                                {
                                    totalVolume += v;
                                    count++;
                                }
                                if (double.TryParse(kline["close"]?.ToString(), out double c))
                                {
                                    historicalCloseSum += c;
                                    // 计算最后5天的均值
                                    if (i >= limit - 5) ma5Sum += c;
                                }
                            }
                        }
                        
                        ma20 = historicalCloseSum / count;
                        ma5 = ma5Sum / Math.Min(5, count);
                        
                        _klineCache[fullCode] = (totalVolume, historicalCloseSum, count, recentTrend, ma5, DateTime.Now);
                    }
                }
            }

            if (count > 0)
            {
                // 如果是从缓存读取，且 ma5 还没算（首次运行或旧缓存结构）
                if (ma5 == 0) ma5 = (ma20 + currentPrice) / 2.0; 

                double avgVolume = totalVolume / count;
                double rawRatio = avgVolume > 0 ? (currentVolShares / avgVolume) : 1;
                
                // 使用北京时间 (UTC+8) 避免时区干扰
                DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
                TimeSpan cstTime = nowUtc.ToOffset(TimeSpan.FromHours(8)).TimeOfDay;

                // 集合竞价前 (9:15之前)
                if (cstTime < new TimeSpan(9, 15, 0))
                {
                    return "[待机]等待竞价";
                }

                // 1. 时间进度与量比平滑 (Smoothing for early market volatility)
                double minutesPassed = 0, totalMin = 240.0;
                if (cstTime < new TimeSpan(9, 30, 0)) minutesPassed = 1;
                else if (cstTime < new TimeSpan(11, 30, 0)) minutesPassed = (cstTime - new TimeSpan(9, 30, 0)).TotalMinutes;
                else if (cstTime < new TimeSpan(13, 0, 0)) minutesPassed = 120;
                else minutesPassed = 120 + Math.Min(120, (cstTime - new TimeSpan(13, 0, 0)).TotalMinutes);
                
                // 开盘前15分钟量比通常虚高，进行平滑处理
                double timeProgress = minutesPassed / totalMin;
                double smoothedProgress = minutesPassed < 15 ? (timeProgress * 0.5 + 0.05) : timeProgress;
                double ratio = rawRatio / Math.Max(0.01, smoothedProgress);

                double currentPercent = prevClose > 0 ? ((currentPrice - prevClose) / prevClose * 100) : 0;
                
                // 2. 涨跌停精确计算与判定
                double limitRate = (stockName.Contains("ST") || stockName.Contains("退")) ? 0.05 :
                                   (pureCode.StartsWith("688") || pureCode.StartsWith("300") || pureCode.StartsWith("301")) ? 0.20 :
                                   (pureCode.StartsWith("8") || pureCode.StartsWith("4")) ? 0.30 : 0.10;
                
                decimal limitPrice = Math.Round((decimal)(prevClose * (1 + limitRate)), 2, MidpointRounding.AwayFromZero);
                decimal floorPrice = Math.Round((decimal)(prevClose * (1 - limitRate)), 2, MidpointRounding.AwayFromZero);
                decimal currentDec = (decimal)currentPrice;

                double bodyTop = Math.Max(open, currentPrice);
                double bodyBottom = Math.Min(open, currentPrice);
                double upperShadow = high - bodyTop;
                double lowerShadow = bodyBottom - low;
                double bodySize = bodyTop - bodyBottom;
                
                // A股特有：乖离率判断 (Bias) & 位阶分析
                double ma5Bias = (currentPrice - ma5) / ma5 * 100;
                bool isHighPosition = ma5Bias > 12 || recentTrend > 20; // 处于高位或近期涨幅过大

                // 集合竞价阶段（北京时间 9:15-9:25）
                if (cstTime >= new TimeSpan(9, 15, 0) && cstTime <= new TimeSpan(9, 25, 30))
                {
                    double auctionPrice = open > 0 ? open : currentPrice;
                    double openGap = prevClose > 0 ? (auctionPrice - prevClose) / prevClose * 100 : 0;
                    string gapSign = openGap >= 0 ? "+" : "";
                    
                    if (cstTime <= new TimeSpan(9, 20, 0)) return $"[竞价]{gapSign}{openGap:F1}%";
                    else return $"[竞价]{(openGap > 4.5 ? "强势高开" : openGap > 2.5 ? "小幅高开" : openGap < -2.5 ? "大幅低开" : "平淡开盘")}{gapSign}{openGap:F1}%";
                }

                // 竞价结束到正式开盘 (9:25:30 - 9:30:00)
                if (cstTime > new TimeSpan(9, 25, 30) && cstTime < new TimeSpan(9, 30, 0))
                {
                    return "[竞价]等待开盘";
                }

                string period = (minutesPassed < 30) ? " (早盘)" : (cstTime >= new TimeSpan(14, 30, 0)) ? " (尾盘)" : "";

                // ========== 1. 极致行情与炸板判定 (Decimal Precision) ==========
                if (currentDec >= limitPrice - 0.001m) 
                {
                    if (period == " (尾盘)") return ratio > 2.5 ? "[涨停]分歧封板" : "[涨停]稳稳封死";
                    if (ratio < 0.6) return "[涨停]一字板";
                    if (ratio > 4.5) return "[涨停]爆量烂板";
                    return "[涨停]强势封板";
                }
                
                // 炸板捕捉：最高价曾触及涨停，但现价显著回落
                if (high >= (double)limitPrice - 0.005 && currentDec < limitPrice * 0.995m)
                {
                    return "[风险]炸板回落" + period;
                }

                if (currentDec <= floorPrice + 0.001m)
                {
                    return "[跌停]" + (ratio > 1.2 ? "放量杀跌" : "缩量封死");
                }

                // ========== 2. 异常异动预警 (胜率压制逻辑) ==========
                // 极限定投：量比 > 5 往往是短期见顶
                if (ratio > 5.0 && currentPercent < 5) return "[风险]爆量过热";

                // 高位滞涨：在高位时即便量大也不看多
                if (isHighPosition)
                {
                    if (ratio > 2.8 && currentPercent < 2) return "[风险]高位放量滞涨";
                    if (upperShadow > (prevClose * 0.035)) return "[风险]见顶回落";
                    if (currentPercent < 0) return "[警惕]高位派发";
                }

                // 破位预警
                if (currentPrice < ma5 && ratio > 2.2 && currentPercent < -3) return "[脱逃]放量破位";

                // ========== 3. A股经典K线组合 ==========
                // 试盘/蓄势
                if (upperShadow > (bodySize * 2.0) && upperShadow > (prevClose * 0.025))
                {
                    if (!isHighPosition && currentPercent > 0) return "[看多]长上影试盘";
                    return "[观察]冲高回落";
                }
                // 深V/探底
                if (lowerShadow > (bodySize * 2.0) && lowerShadow > (prevClose * 0.025))
                {
                    if (currentPrice > ma5) return "[看多]探底回升";
                    return "[观察]谷底支撑";
                }

                // ========== 4. 量价共振分析 (核心胜率) ==========
                string status = "[观察]";
                if (currentPrice > ma5) // 多头
                {
                    if (ratio > 2.0 && currentPercent > 3.5) 
                    {
                        status = isHighPosition ? "[警惕]高位放量" : "[看多]放量上攻";
                    }
                    else if (ratio < 0.35 && currentPercent > 0.5) 
                    {
                        status = "[看多]缩量稳涨"; // 锁仓或洗盘完成
                    }
                    else if (ma5Bias < 2 && currentPercent > -0.5) 
                    {
                        status = "[看多]回踩均线"; // 安全买点
                    }
                    else if (currentPercent > 0)
                    {
                        status = "[趋势]多头占优";
                    }
                    else status = "[趋势]均线支撑";
                }
                else // 空头/整理
                {
                    if (ratio < 0.3) status = "[观察]地量筑底";
                    else if (currentPercent < -5 && ratio > 1.5) status = "[风险]恐慌杀跌";
                    else if (currentPercent < 0) status = "[盘整]弱势震荡";
                    else status = "[观察]试图止跌";
                }

                return $"{status}{period}";
            }
        }
        catch (Exception ex)
        {
            Program.LogError($"GetVolumePrediction failed for {fullCode}", ex);
        }
        return "分析中...";
    }


    private async Task UpdatePrices()
    {
        if (_stocks.Count == 0)
        {
            UpdateUI(new List<(string, string)> { ("右键添加股票", "") });
            return;
        }

        try
        {
            var prefixedCodes = _stocks.Select(c => GetPrefix(c) + (c.Length > 6 ? c.Substring(c.Length - 6) : c));
            string url = $"http://hq.sinajs.cn/list={string.Join(",", prefixedCodes)}";
            
            var bytes = await _httpClient!.GetByteArrayAsync(url);
            string response = Encoding.GetEncoding("GB2312").GetString(bytes);
            var lines = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            var displayData = new List<(string Name, string Code, string Price, string Pct, string Bid, string Ask, string Pred, string FullCode, string Sector)>();
            for (int i = 0; i < lines.Length && i < _stocks.Count; i++)
            {
                string line = lines[i];
                string originalCode = _stocks[i];
                int start = line.IndexOf("=\"");
                if (start != -1)
                {
                    int end = line.IndexOf("\";", start);
                    if (end != -1)
                    {
                        string data = line.Substring(start + 2, end - start - 2);
                        var parts = data.Split(',');
                        if (parts.Length > 3)
                        {
                            string pureCode = originalCode.Length > 6 ? originalCode.Substring(originalCode.Length - 6) : originalCode;
                            if (double.TryParse(parts[2], out double prevClose) && double.TryParse(parts[3], out double current))
                            {
                                // Handle call auction price virtualization (9:15-9:25)
                                DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
                                TimeSpan cstTime = nowUtc.ToOffset(TimeSpan.FromHours(8)).TimeOfDay;
                                bool isCallAuction = cstTime >= new TimeSpan(9, 15, 0) && cstTime <= new TimeSpan(9, 25, 30);
                                
                                if (isCallAuction && (current == 0 || Math.Abs(current - prevClose) < 0.001))
                                {
                                    // In Sina API, parts[6] (bid) and parts[7] (ask) often hold the match price durante auction
                                    if (parts.Length > 6 && double.TryParse(parts[6], out double bPrice) && bPrice > 0) 
                                    {
                                        current = bPrice;
                                    }
                                    else if (parts.Length > 7 && double.TryParse(parts[7], out double aPrice) && aPrice > 0) 
                                    {
                                        current = aPrice;
                                    }
                                }

                                double percent = prevClose > 0 ? (current > 0 ? (current - prevClose) / prevClose * 100 : 0) : 0;
                                if (current == 0) current = prevClose;

                                double open = double.Parse(parts[1]), high = double.Parse(parts[4]), low = double.Parse(parts[5]);
                                string bid = parts.Length > 6 ? parts[6] : "0.00";
                                string ask = parts.Length > 7 ? parts[7] : "0.00";
                                double vol = double.Parse(parts.Length > 8 ? parts[8] : "0");

                                string pred = await GetVolumePrediction(GetPrefix(pureCode) + pureCode, pureCode, parts[0], current, open, high, low, prevClose, vol);
                                
                                displayData.Add((parts[0], $"[{pureCode}]", current.ToString("F3"), 
                                                (percent >= 0 ? "+" : "") + percent.ToString("F2") + "%", 
                                                bid, ask, pred, originalCode, GetSector(originalCode)));
                            }
                        }
                    }
                }
            }

            if (displayData.Any()) UpdateUIStructured(displayData);
        }
        catch { }
    }

    private void UpdateUI(List<(string Text, string Code)> data)
    {
        // For initial state/compatibility
        UpdateUIStructured(data.Select(d => (d.Text, "", "", "", "", "", "", d.Code, "")).ToList());
    }

    private void UpdateUIStructured(List<(string Name, string Code, string Price, string Pct, string Bid, string Ask, string Pred, string FullCode, string Sector)> data)
    {
        _lastDisplayData = data.Select(d => (d.Name, d.Code, d.Price, d.Pct, d.Sector, d.Pred)).ToList();
        
        // 如果当前有右键菜单打开，跳过本次UI重绘，防止菜单被强制关闭断开交互
        if (_isContextMenuOpen) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var container = this.FindControl<Grid>("StockContainer");
            if (container == null) return;

            container.Children.Clear();
            container.RowDefinitions.Clear();
            
            // Define global columns once for the entire table
            container.ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition { Width = GridLength.Auto }, // 0: Name Code
                new ColumnDefinition { Width = new GridLength(8) },
                new ColumnDefinition { Width = GridLength.Auto }, // 2: Sector
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto }, // 4: Price
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto }, // 6: Pct
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto }, // 8: Divider |
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto }, // 10: Buy Label "买:"
                new ColumnDefinition { Width = GridLength.Auto }, // 11: Bid Value
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto }, // 13: Sell Label "卖:"
                new ColumnDefinition { Width = GridLength.Auto }, // 14: Ask Value
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto }, // 16: Divider |
                new ColumnDefinition { Width = new GridLength(10) },
                new ColumnDefinition { Width = GridLength.Auto }  // 18: Pred
            };

            int row = 0;
            foreach (var item in data)
            {
                container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                // Add an invisible border to capture right-clicks anywhere on the row
                var rowBg = new Border
                {
                    Background = Brushes.Transparent,
                    ContextMenu = CreateSharedContextMenu(item.FullCode)
                };
                Grid.SetRow(rowBg, row);
                Grid.SetColumnSpan(rowBg, 19);
                container.Children.Add(rowBg);

                void AddCol(string text, int col, HorizontalAlignment align = HorizontalAlignment.Left, IBrush? color = null)
                {
                    var tb = new TextBlock
                    {
                        Text = text,
                        Foreground = color ?? Brushes.LightGray,
                        FontSize = 12,
                        FontFamily = new FontFamily("Courier New"),
                        HorizontalAlignment = align,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 1)
                    };
                    
                    tb.ContextMenu = CreateSharedContextMenu(item.FullCode); // Fallback context menu
                    
                    Grid.SetRow(tb, row);
                    Grid.SetColumn(tb, col);
                    container.Children.Add(tb);
                }

                // Combined Name + Code
                AddCol(string.IsNullOrEmpty(item.Code) ? item.Name : $"{item.Name} {item.Code}", 0);

                // Sector immediately after
                AddCol(item.Sector, 2);

                AddCol(item.Price, 4, HorizontalAlignment.Right);

                AddCol(item.Pct, 6, HorizontalAlignment.Right);

                if (!string.IsNullOrEmpty(item.Price))
                {
                    AddCol("|", 8, color: Brush.Parse("#FF555555"));
                    
                    AddCol("买:", 10, color: Brushes.LightGray);
                    AddCol(item.Bid, 11, HorizontalAlignment.Right);
                    
                    AddCol("卖:", 13, color: Brushes.LightGray);
                    AddCol(item.Ask, 14, HorizontalAlignment.Right);
                    
                    AddCol("|", 16, color: Brush.Parse("#FF555555"));
                    AddCol("智测:" + item.Pred, 18);
                }

                row++;
            }

            // 每次 UI 更新后，如果窗口是可见的且未最小化，强制触发一次测量更新
            if (this.WindowState == WindowState.Normal && this.IsVisible)
            {
                this.InvalidateMeasure();
            }
        });
    }

    // ═══════════════════════════════════════════
    // AI 配置与业务逻辑区
    // ═══════════════════════════════════════════
    
    private void LoadSettings()
    {
        try
        {
            string? exePath = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName);
            string localConfig = Path.Combine(exePath ?? AppContext.BaseDirectory, "appsettings.json");
            
            try
            {
                if (!File.Exists(localConfig)) File.WriteAllText(localConfig, "{}");
                _settingsFile = localConfig;
            }
            catch (UnauthorizedAccessException)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string configDir = Path.Combine(appData, "StockTracker");
                if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
                _settingsFile = Path.Combine(configDir, "appsettings.json");
            }

            if (File.Exists(_settingsFile))
            {
                string json = File.ReadAllText(_settingsFile);
                _appSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Program.LogError("LoadSettings Failure", ex);
        }
    }

    private void SaveSettings()
    {
        try
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(_appSettings, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_settingsFile, json);
        }
        catch (Exception ex)
        {
            Program.LogError("SaveSettings Failure", ex);
        }
    }

    private async Task ShowSettingsWindowAsync()
    {
        var settingsWin = new SettingsWindow(_appSettings);
        await settingsWin.ShowDialog(this);
        if (settingsWin.Saved)
        {
            _appSettings = settingsWin.UpdatedSettings;
            SaveSettings();
            SetupScheduleTimer();
        }
    }

    private void SetupScheduleTimer()
    {
        _scheduleTimer?.Stop();
        _targetScheduleTimes.Clear();
        
        if (!_appSettings.ScheduleEnabled || string.IsNullOrWhiteSpace(_appSettings.ScheduleTime))
            return;

        // 解析逗号分隔的时间点，并去重
        var timeParts = _appSettings.ScheduleTime.Split(new[] { ',', ';', '，', '；' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in timeParts)
        {
            string cleanPart = part.Trim();
            if (TimeSpan.TryParseExact(cleanPart, "h\\:mm", null, out TimeSpan targetTime))
            {
                if (!_targetScheduleTimes.Contains(targetTime)) _targetScheduleTimes.Add(targetTime);
            }
            else if (TimeSpan.TryParse(cleanPart, out targetTime))
            {
                if (!_targetScheduleTimes.Contains(targetTime)) _targetScheduleTimes.Add(targetTime);
            }
        }

        if (_targetScheduleTimes.Count == 0) return;

        _scheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _scheduleTimer.Tick += async (s, e) =>
        {
            var now = DateTime.Now;
            
            // 跨天检查：如果日期变了，清空今日已触发记录
            if (now.Date > _lastScheduleRunDate.Date)
            {
                _triggeredTimesToday.Clear();
                _lastScheduleRunDate = now.Date;
            }

            foreach (var target in _targetScheduleTimes)
            {
                string timeKey = $"{target.Hours:D2}:{target.Minutes:D2}";
                
                if (now.Hour == target.Hours && 
                    now.Minute == target.Minutes && 
                    !_triggeredTimesToday.Contains(timeKey))
                {
                    _triggeredTimesToday.Add(timeKey);
                    await RunAiStockAnalysisAsync(sendEmail: true, hideUi: true); // 后台定时执行
                }
            }
        };
        _scheduleTimer.Start();
    }

    /// <summary>
    /// 执行大盘 AI 复盘分析
    /// </summary>
    private async Task RunMarketAnalysisAsync(bool sendNotification, bool hideUi = false)
    {
        if (_isAiAnalysisRunning) return;
        _isAiAnalysisRunning = true;

        AnalysisResultWindow? resultWindow = null;
        if (!hideUi)
        {
            Dispatcher.UIThread.Post(() => 
            {
                resultWindow = new AnalysisResultWindow("AI 大盘分析中", "正在抓取全市场涨跌分布、板块排行及宏观要闻，请稍候...");
                resultWindow.Show(this);
            });
        }

        try
        {
            // 1. 抓取大盘数据
            var overview = await StockDataProvider.FetchMarketOverviewAsync(_appSettings.TavilyApiKey);
            var marketIndices = await StockDataProvider.FetchMarketIndexAsync();
            var indexDataList = new List<MarketEnvironmentAnalyzer.MarketIndexData>();
            if (marketIndices != null)
            {
                foreach(var idx in marketIndices) indexDataList.Add(new MarketEnvironmentAnalyzer.MarketIndexData { Name = idx.Name, Price = idx.Price, PctChange = idx.PctChange });
            }

            // 2. 分析市场环境并构建 Prompt
            var marketCondition = MarketEnvironmentAnalyzer.AnalyzeMarketCondition(indexDataList);
            string prompt = EnhancedAiPromptBuilder.BuildMarketReviewPrompt(overview, indexDataList, marketCondition);
            string aiResponse = await CallAiApiAsync(prompt);
            string finalReport = $"大盘复盘时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{aiResponse}";

            // 3. 展示结果
            if (!hideUi)
            {
                Dispatcher.UIThread.Post(() => 
                {
                    if (resultWindow != null) resultWindow.Close(); 
                    new AnalysisResultWindow("A 股大盘 AI 复盘诊断", finalReport).Show(this);
                });
            }

            // 4. 推送通知
            if (sendNotification)
            {
                // 邮件推送
                await SendEmailAsync($"[StockTracker] A 股大盘 AI 复盘诊断 {DateTime.Now:yyyy-MM-dd}", finalReport);
            }
        }
        catch (Exception ex)
        {
            Program.LogError("MarketAnalysis Failure", ex);
            if (!hideUi)
            {
                Dispatcher.UIThread.Post(() => { if (resultWindow != null) resultWindow.Close(); });
            }
        }
        finally
        {
            _isAiAnalysisRunning = false;
        }
    }

    private async Task RunAiStockAnalysisAsync(bool sendEmail, bool hideUi = false)
    {
        if (_isAiAnalysisRunning) return;

        if (string.IsNullOrWhiteSpace(_appSettings.ApiKey))
        {
            Dispatcher.UIThread.Post(() => 
            {
                var tip = new AnalysisResultWindow("配置错误",
                    "请先右键选择 [⚙️ 配置设置] 配置 AI API Key。\n" +
                    "支持 Gemini / DeepSeek / 千问 / GLM 等。");
                tip.Show(this);
            });
            return;
        }

        _isAiAnalysisRunning = true;
        AnalysisResultWindow? resultWindow = null;
        
        if (!hideUi)
        {
            Dispatcher.UIThread.Post(() => 
            {
                resultWindow = new AnalysisResultWindow("AI 个股分析中", "正在抓取自选股行情数据并调用 AI 分析接口，请稍候...");
                resultWindow.Show(this);
            });
        }

        try
        {
            // === 第零步：前置大盘复盘 (如果开启) ===
            if (_appSettings.MarketReviewEnabled)
            {
                // 注意：由于 _isAiAnalysisRunning 锁的存在，这里不能直接 await RunMarketAnalysisAsync
                // 我们在内部手动耦合一次大盘数据的逻辑，或者先放开锁再运行。
                // 推荐做法：在 RunAiStockAnalysisAsync 内部直接处理大盘逻辑。
                var overview = await StockDataProvider.FetchMarketOverviewAsync(_appSettings.TavilyApiKey);
                var marketIndicesCheck = await StockDataProvider.FetchMarketIndexAsync();
                var indexListCheck = marketIndicesCheck?.Select(idx => new MarketEnvironmentAnalyzer.MarketIndexData { Name = idx.Name, Price = idx.Price, PctChange = idx.PctChange }).ToList() ?? new();
                
                string marketPrompt = EnhancedAiPromptBuilder.BuildMarketReviewPrompt(overview, indexListCheck);
                string marketAiResponse = await CallAiApiAsync(marketPrompt);
                
                if (sendEmail)
                {
                    await SendEmailAsync($"[StockTracker] A 股大盘 AI 复盘诊断 {DateTime.Now:yyyy-MM-dd}", $"大盘分析执行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{marketAiResponse}");
                }
            }

            // === 第一步：获取市场环境 ===
            var marketIndices = await StockDataProvider.FetchMarketIndexAsync();
            var indexDataList = new List<MarketEnvironmentAnalyzer.MarketIndexData>();

            if (marketIndices != null && marketIndices.Count > 0)
            {
                foreach (var idx in marketIndices)
                {
                    indexDataList.Add(new MarketEnvironmentAnalyzer.MarketIndexData
                    {
                        Name = idx.Name,
                        Price = idx.Price,
                        PctChange = idx.PctChange
                    });
                }
            }

            var marketCondition = MarketEnvironmentAnalyzer.AnalyzeMarketCondition(indexDataList);

            // === 第二步：获取市场全景数据及板块排行 ===
            var allSectors = new List<SectorRanking>();
            try
            {
                var overview = await StockDataProvider.FetchMarketOverviewAsync(_appSettings.TavilyApiKey);
                if (overview != null)
                {
                    allSectors = overview.AllSectors ?? new List<SectorRanking>();
                }
            }
            catch { }

            // === 第三步：获取股票数据并量化评分 ===
            var stockScores = new List<ImprovedWinRateScoring.EnhancedStockScore>();
            var stockDataContexts = new Dictionary<string, StockDeepAnalysisContext>();
            var dataQualityResults = new Dictionary<string, DataQualityValidator.ValidationResult>();
            var sectors = new Dictionary<string, string>();

            foreach (var code in _stocks)
            {
                var displayItem = _lastDisplayData.FirstOrDefault(d => d.Code != null && d.Code.Contains(code));
                string stockName = displayItem.Name ?? $"股票{code}";
                string sector = displayItem.Sector ?? "未知板块";

                var ctx = await StockDataProvider.FetchDeepDataAsync(code, _appSettings.TavilyApiKey);
                if (string.IsNullOrEmpty(ctx.Name)) ctx.Name = stockName;

                // 板块联动数据
                var (sectorName, sectorPct, sectorRank) = await StockDataProvider.FetchStockSectorAsync(code, allSectors);
                ctx.SectorName = sectorName;
                ctx.SectorPctChange = sectorPct;
                ctx.SectorRankPercent = sectorRank;
                ctx.RelativeStrengthVsSector = ctx.PctChange - sectorPct;

                // 保存数据供后续使用
                stockDataContexts[code] = ctx;
                sectors[code] = sector;

                // 数据质量验证
                var quality = DataQualityValidator.ValidateStockData(ctx);
                dataQualityResults[code] = quality;

                // 多周期共振分析
                AdvancedTechnicalIndicators.AnalyzeMultiTimeframeResonance(
                    ctx.TechScore, ctx.TechScore60Min, ctx.TechScore15Min);

                // 使用增强的量化评分系统
                var score = ImprovedWinRateScoring.CalculateEnhancedScore(ctx, marketCondition);
                stockScores.Add(score);

                // 计算精准择时与智能止损策略
                ctx.Timing = HighWinRateStrategies.CalculateTimingScore(ctx, ctx.RecentPrices, ctx.RecentVolumes);
                ctx.SmartStop = HighWinRateStrategies.CalculateSmartStopLoss(ctx, score, ctx.RecentPrices);
            }

            // === 第四步：获取回测历史表现数据 ===
            var backtestResult = AdviceTracker.CalculateBackTestResults(60); // 最近60天

            // === 第五步：构建完整的深度分析 AI 提示词 ===
            string prompt = EnhancedAiPromptBuilder.BuildCompleteAnalysisPrompt(
                stockScores, 
                marketCondition, 
                indexDataList, 
                stockDataContexts, 
                dataQualityResults, 
                backtestResult,
                sectors);

            // 请求 AI 接口（Gemini / OpenAI 兼容）
            string aiResponse = await CallAiApiAsync(prompt);
            string finalReport = $"分析执行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{aiResponse}";

            // 处理结果弹窗展示
            if (!hideUi)
            {
                Dispatcher.UIThread.Post(() => 
                {
                    if (resultWindow != null) resultWindow.Close(); 
                    var newWin = new AnalysisResultWindow("AI 个股深度诊断报告", finalReport);
                    newWin.Show(this);
                });
            }

            // 处理结果邮件发送
            if (sendEmail)
            {
                await SendEmailAsync($"[StockTracker] 自选股 AI 盘面分析 {DateTime.Now:yyyy-MM-dd}", finalReport);
            }
        }
        catch (Exception ex)
        {
            Program.LogError("RunAiStockAnalysisAsync Failure", ex);
            Dispatcher.UIThread.Post(() => 
            {
                if (resultWindow != null) resultWindow.Close();
                // 构建完整错误信息（含 InnerException 链，便于排查根因）
                var errMsg = new System.Text.StringBuilder();
                errMsg.AppendLine(ex.Message);
                var inner = ex.InnerException;
                while (inner != null)
                {
                    errMsg.AppendLine($"\n原因: {inner.Message}");
                    inner = inner.InnerException;
                }
                if (!hideUi)
                {
                    new AnalysisResultWindow("AI 分析报错", $"发生异常：\n{errMsg}").Show(this);
                }
                else
                {
                    new AnalysisResultWindow("定时分析故障提醒", $"后台邮件或AI诊断异常：\n{errMsg}").Show(this);
                }
            });
        }
        finally
        {
            _isAiAnalysisRunning = false;
        }
    }

    /// <summary>
    /// 统一 AI 调用入口。
    /// - Base URL 为空或含 generativelanguage.googleapis.com → Gemini 原生协议
    /// - 其余 → OpenAI Chat Completions 兼容协议（DeepSeek / 千问 / GLM / 中转站等）
    /// </summary>
    private async Task<string> CallAiApiAsync(string prompt)
    {
        string apiKey  = _appSettings.ApiKey;
        string baseUrl = _appSettings.ResolvedBaseUrl;
        string[] models = _appSettings.ResolvedModels;

        if (models.Length == 0)
        {
            return "AI 配置错误：使用 OpenAI 兼容接口时必须在【配置设置】中填写模型名称。\n" +
                   "例如：deepseek-chat / qwen-plus / glm-4-flash";
        }

        var errorRecords = new List<string>();

        foreach (string model in models)
        {
            try
            {
                string rawJson;

                if (_appSettings.IsGeminiProtocol)
                {
                    // ── Gemini 原生: POST /v1beta/models/{model}:generateContent?key=... ──
                    string endpoint = $"{baseUrl}/models/{model}:generateContent?key={apiKey}";
                    var payload = new
                    {
                        contents = new[] { new { parts = new[] { new { text = prompt } } } },
                        generationConfig = new { maxOutputTokens = 8192, temperature = 0.7 }
                    };
                    rawJson = await NetworkHelper.HttpPostWithRetryAsync(
                        endpoint,
                        Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                        maxRetries: 2, timeoutSeconds: 120);

                    try
                    {
                        var root = Newtonsoft.Json.Linq.JObject.Parse(rawJson);
                        var text = root["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text)) return text;
                        Program.LogError($"Gemini 返回结果为空 [{model}]", new Exception(rawJson));
                        continue;
                    }
                    catch (Newtonsoft.Json.JsonReaderException jsonEx)
                    {
                        Program.LogError($"Gemini 响应解析失败 [{model}]", jsonEx);
                        continue;
                    }
                }
                else
                {
                    // ── OpenAI Chat Completions 兼容: POST /chat/completions ──
                    // 兼容 DeepSeek / 千问 / GLM / 任意 OpenAI 兼容中转站
                    string endpoint = $"{baseUrl}/chat/completions";
                    var payload = new
                    {
                        model,
                        messages = new[]
                        {
                            new { role = "user", content = prompt }
                        },
                        max_tokens = 8192,
                        temperature = 0.7,
                        stream = false
                    };
                    string payloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

                    // OpenAI 兼容协议用 Bearer Token 鉴权，需要在 HttpClient 上设置 Authorization 头
                    rawJson = await NetworkHelper.HttpPostWithRetryAsync(
                        endpoint,
                        payloadJson,
                        maxRetries: 2, timeoutSeconds: 120,
                        bearerToken: apiKey);

                    try
                    {
                        var root = Newtonsoft.Json.Linq.JObject.Parse(rawJson);
                        var text = root["choices"]?[0]?["message"]?["content"]?.ToString();
                        if (!string.IsNullOrEmpty(text)) return text;
                        // 检查错误响应
                        var errMsg = root["error"]?["message"]?.ToString();
                        if (!string.IsNullOrEmpty(errMsg))
                            Program.LogError($"AI 接口错误 [{model}]: {errMsg}", new Exception(rawJson));
                        else
                            Program.LogError($"AI 返回结果为空 [{model}]", new Exception(rawJson));
                        continue;
                    }
                    catch (Newtonsoft.Json.JsonReaderException jsonEx)
                    {
                        Program.LogError($"AI 响应解析失败 [{model}]", jsonEx);
                        continue;
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                Program.LogError($"AI 网络错误 [{model}]", httpEx);
                string errTxt = httpEx.InnerException?.Message ?? httpEx.Message;
                errorRecords.Add($"[{model}] 接口异常：{errTxt}");
                continue;
            }
            catch (Exception ex)
            {
                Program.LogError($"AI 通用错误 [{model}]", ex);
                errorRecords.Add($"[{model}] 系统异常：{ex.Message}");
                continue;
            }
        }

        return GenerateDetailedErrorMessage(errorRecords);
    }

    private string GenerateDetailedErrorMessage(List<string> errorRecords)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("AI 请求失败：所有可用模型均无法响应。");
        sb.AppendLine();
        
        if (errorRecords != null && errorRecords.Count > 0)
        {
            sb.AppendLine("【详细错误拦截记录】");
            foreach(var err in errorRecords)
            {
                string displayErr = err;
                string lowerErr = err.ToLowerInvariant();
                string prefix = err.Contains("]") ? err.Split(']')[0] + "]" : "[]";

                // --- 1. 余额不足 / 欠费 / 免费额度耗尽 ---
                if (lowerErr.Contains("insufficient balance") || 
                    lowerErr.Contains("paymentrequired") || 
                    lowerErr.Contains("arrears") || 
                    lowerErr.Contains("quota exceeded") || 
                    lowerErr.Contains("\"code\": \"1004\"") || lowerErr.Contains("\"code\":1004")) // GLM 欠费
                {
                    displayErr = $"❌ {prefix} 账号余额或免费额度不足，请充值或更换 API Key。({err.Replace(prefix, "").Trim()})";
                }
                // --- 2. 授权失败 / API Key 无效 ---
                else if (lowerErr.Contains("invalid_api_key") || 
                         lowerErr.Contains("unauthorized") || 
                         lowerErr.Contains("invalidapikey") || 
                         lowerErr.Contains("api_key_invalid") ||
                         lowerErr.Contains("\"code\": \"1301\"") || lowerErr.Contains("\"code\":1301") || // GLM key报错
                         lowerErr.Contains("valid api key") ||
                         lowerErr.Contains("authentication failed"))
                {
                    displayErr = $"❌ {prefix} API Key 错误或未授权，请检查设置。({err.Replace(prefix, "").Trim()})";
                }
                // --- 3. 频率限制 / 请求过快 ---
                else if (lowerErr.Contains("rate_limit_exceeded") ||
                         lowerErr.Contains("throttling") ||
                         lowerErr.Contains("429") ||
                         lowerErr.Contains("too many requests") ||
                         lowerErr.Contains("\"code\": \"1302\"") || lowerErr.Contains("\"code\":1302"))
                {
                    displayErr = $"⚠️ {prefix} 接口请求频率过高被限流，建议稍晚再试。({err.Replace(prefix, "").Trim()})";
                }
                // --- 4. 网络/代理/主机超时错误 ---
                else if (lowerErr.Contains("taskcanceled") || lowerErr.Contains("timeout") || lowerErr.Contains("connection"))
                {
                    displayErr = $"📶 {prefix} 网络连接超时或代理断开，无法到达平台服务器。({err.Replace(prefix, "").Trim()})";
                }
                else 
                {
                    // 未匹配的原始错误信息
                    displayErr = $"❓ {err}";
                }
                
                sb.AppendLine(" " + displayErr);
            }
            sb.AppendLine("\n【排查建议】");
        }
        else
        {
            sb.AppendLine("【可能原因】");
            sb.AppendLine("1. API Key 不正确或已过期");
            sb.AppendLine("2. 网络连接问题或防火墙/代理拦截");
            sb.AppendLine("3. SSL/TLS 证书验证失败");
            sb.AppendLine("4. AI 服务暂时不可用或触发速率限制");
            sb.AppendLine("5. Base URL 填写有误（OpenAI 兼容模式）");
            sb.AppendLine("\n【排查建议】");
        }

        sb.AppendLine("• 右键 → 配置设置，确认 API Key / Base URL / 模型名是否正确");
        sb.AppendLine("• Gemini：留空 Base URL，使用 generativelanguage.googleapis.com");
        sb.AppendLine("• DeepSeek：https://api.deepseek.com/v1  模型 deepseek-chat");
        sb.AppendLine("• 千问：https://dashscope.aliyuncs.com/compatible-mode/v1  模型 qwen-plus");
        sb.AppendLine("• GLM：https://open.bigmodel.cn/api/paas/v4  模型 glm-4-flash");
        sb.AppendLine("• 查看同目录 error_log.txt 获取详细错误信息");
        return sb.ToString();
    }

    private async Task SendEmailAsync(string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_appSettings.EmailUser) || string.IsNullOrWhiteSpace(_appSettings.EmailPassword))
        {
            Dispatcher.UIThread.Post(() => 
            {
                new AnalysisResultWindow("邮件发送中止", "请至少在配置中填写【发件邮箱】和【授权码/密码】").Show(this);
            });
            return;
        }

        // 自动提取配置（构建平滑用户体验）
        string host = (_appSettings.EmailSmtpHost ?? "").Trim();
        int port = _appSettings.EmailSmtpPort;

        // 如果用户没填 SMTP 服务器，自动根据邮箱后缀识别
        if (string.IsNullOrWhiteSpace(host) && _appSettings.EmailUser.Contains("@"))
        {
            string domain = _appSettings.EmailUser.Split('@').Last().ToLower();
            switch (domain)
            {
                case "qq.com":
                case "foxmail.com":
                    host = "smtp.qq.com"; port = 465; break; // MailKit 完美原生支持 465 隐式SSL
                case "163.com":
                    host = "smtp.163.com"; port = 465; break;
                case "126.com":
                    host = "smtp.126.com"; port = 465; break;
                case "gmail.com":
                    host = "smtp.gmail.com"; port = 465; break;
                case "outlook.com":
                case "hotmail.com":
                case "live.com":
                    host = "smtp-mail.outlook.com"; port = 587; break; // Outlook 使用 587 STARTTLS
                case "aliyun.com":
                    host = "smtp.aliyun.com"; port = 465; break;
                default:
                    host = $"smtp.{domain}"; port = 465; break;
            }
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            Dispatcher.UIThread.Post(() => 
            {
                new AnalysisResultWindow("邮件发送失败", "未能自动识别该邮箱类型的SMTP服务器。\n请在【配置设置】中手动填写您的 SMTP 服务器地址（如 smtp.xx.com）。").Show(this);
            });
            return;
        }

        try
        {
            // 采用 MailKit.MimeMessage 全新构建（解决一切中文 Header 和编码风控问题）
            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress("StockTracker助手", _appSettings.EmailUser));
            
            var targets = _appSettings.EmailTo.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in targets) 
                message.To.Add(new MimeKit.MailboxAddress(t.Trim(), t.Trim()));
            
            message.Subject = subject;

            var bodyBuilder = new MimeKit.BodyBuilder();
            
            // 使用 Markdig 完美解析 Markdown 为 HTML 结构
            var pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            string parsedHtml = Markdig.Markdown.ToHtml(body, pipeline);
            
            // 注入美化 CSS 样式表
            string cssStyle = @"
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; line-height: 1.5; color: #24292e; font-size: 14px; padding: 15px; max-width: 900px; margin: 0 auto; }
                h1 { font-size: 20px; border-bottom: 1px solid #eaecef; padding-bottom: 0.3em; margin-top: 1.2em; margin-bottom: 0.8em; color: #0366d6; }
                h2 { font-size: 18px; border-bottom: 1px solid #eaecef; padding-bottom: 0.3em; margin-top: 1.0em; margin-bottom: 0.6em; }
                h3 { font-size: 16px; margin-top: 0.8em; margin-bottom: 0.4em; }
                p { margin-top: 0; margin-bottom: 8px; }
                table { border-collapse: collapse; width: 100%; margin: 12px 0; font-size: 13px; display: block; overflow-x: auto; }
                th, td { border: 1px solid #dfe2e5; padding: 6px 10px; text-align: left; }
                th { background-color: #f6f8fa; font-weight: 600; }
                tr:nth-child(2n) { background-color: #f8f8f8; }
                blockquote { color: #6a737d; border-left: 0.25em solid #dfe2e5; padding: 0 1em; margin: 0 0 10px 0; }
                hr { height: 0.25em; padding: 0; margin: 16px 0; background-color: #e1e4e8; border: 0; }
                ul, ol { padding-left: 20px; margin-bottom: 10px; }
                li { margin: 2px 0; }
                code { background-color: rgba(27,31,35,0.05); padding: 0.2em 0.4em; border-radius: 3px; font-family: Consolas, 'Liberation Mono', monospace; font-size: 85%; }
                strong { font-weight: 600; }
            ";

            string fullHtmlDocument = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>{cssStyle}</style>
            </head>
            <body>{parsedHtml}</body>
            </html>";

            bodyBuilder.HtmlBody = fullHtmlDocument;
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new MailKit.Net.Smtp.SmtpClient();
            client.Timeout = 30000;
            
            // 智能加密策略: 465使用隐式SSL，其他端口尝试STARTTLS
            var options = port == 465 
                ? MailKit.Security.SecureSocketOptions.SslOnConnect 
                : MailKit.Security.SecureSocketOptions.StartTls;

            // 连接并认证
            await client.ConnectAsync(host, port > 0 ? port : 465, options);
            await client.AuthenticateAsync(_appSettings.EmailUser, _appSettings.EmailPassword);
            
            // 发送邮件
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            
            // 已取消发送成功的弹窗，遵从“成功不需要提示”的静默体验原则
        }
        catch (Exception ex)
        {
            Program.LogError("MailKit Send Failure", ex);
            Dispatcher.UIThread.Post(() => 
            {
                string realReason = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                new AnalysisResultWindow("邮件发送拦截", $"异常详情：\n{realReason}\n\n【排障指南】\n1. 请检查您的 授权码 是否输入错误，或邮箱是否开启了SMTP服务。\n2. 如果是云服务器，请确认 465 端口是否在安全组中被放行。").Show(this);
            });
        }
    }
}
