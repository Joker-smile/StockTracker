using Avalonia;
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
    private Dictionary<string, (double TotalVolume, double TotalClose, int Count, double RecentTrend, DateTime LastUpdated)> _klineCache = new();
    private FileSystemWatcher? _watcher;
    private bool _isScreenerRunning = false;
    private string _dataSource = "Eastmoney"; // Default to Eastmoney

    public MainWindow()
    {
        InitializeComponent();

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
            if (e.Property.Name == "WindowState" && this.WindowState == WindowState.Normal)
            {
                Dispatcher.UIThread.Post(() => {
                    this.InvalidateMeasure();
                    // 强制触发一次 SizeToContent 重新计算
                    var current = this.SizeToContent;
                    this.SizeToContent = SizeToContent.Manual;
                    this.SizeToContent = current;
                }, DispatcherPriority.Render);
            }
        };
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
        var emItem = new MenuItem { Header = (_dataSource == "Eastmoney" ? "√ " : "  ") + "东方财富" };
        emItem.Click += (s, e) => { _dataSource = "Eastmoney"; };
        var ttItem = new MenuItem { Header = (_dataSource == "Tencent" ? "√ " : "  ") + "腾讯" };
        ttItem.Click += (s, e) => { _dataSource = "Tencent"; };
        
        sourceMenu.Items.Add(emItem);
        sourceMenu.Items.Add(ttItem);
        menu.Items.Add(sourceMenu);

        menu.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "清空全部" };
        clearItem.Click += RemoveStockItem_Click;
        menu.Items.Add(clearItem);

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

        // Sort by turnover descending and take top 500 (增加处理数量)
        var processList = baseStocks.OrderByDescending(s => s.Turnover).Take(500).ToList();
        
        var passedStocks = new List<(string Code, string Name, double Price, double Ma20, double Ma200, double Pe, double MarketCap, string Concepts, string BuyPoint)>();

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
                var techResult = await CheckTechnicalAndMomentumAsync(stock.Code, stock.Name, stock.Price, stock.Pe, stock.MarketCap, stock.Turnover);
                if (techResult.HasValue)
                {
                    string concepts = await GetStockConceptsAsync(stock.Code);
                    passedStocks.Add((stock.Code, stock.Name, stock.Price, techResult.Value.Ma20, techResult.Value.Ma200, stock.Pe, stock.MarketCap, concepts, techResult.Value.BuyPoint));
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

            // Sort by market cap ascending (smallest first)
            passedStocks = passedStocks.OrderBy(s => s.MarketCap).ToList();
            
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
            else
            {
                jsonStr = await _httpClient!.GetStringAsync(url);
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
                    closes.Add(double.Parse(parts[2]));
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
            string url = "http://82.push2.eastmoney.com/api/qt/clist/get?pn=1&pz=10000&po=1&np=1&ut=bd1d9ddb04089700cf9c27f6f7426281&fltt=2&invt=2&fid=f3&fs=m:0+t:6,m:0+t:80,m:1+t:2,m:1+t:23,m:0+t:81+s:2048&fields=f12,f14,f2,f8,f9,f20";
            string jsonStr = await _httpClient!.GetStringAsync(url);
            var root = JObject.Parse(jsonStr);
            var items = root["data"]?["diff"] as JArray;
            
            if (items == null) return new List<StockBasic>();

            var list = new List<StockBasic>();
            foreach (var item in items)
            {
                string name = item["f14"]?.ToString() ?? "";
                if (name.Contains("ST") || name.Contains("退")) continue;

                if (double.TryParse(item["f2"]?.ToString(), out double price) && price > 0 &&
                    double.TryParse(item["f8"]?.ToString(), out double turnover) &&
                    double.TryParse(item["f9"]?.ToString(), out double pe) &&
                    double.TryParse(item["f20"]?.ToString(), out double marketCap))
                {
                    // Core Filter 1: PE > 0 (移除PE上限限制，只排除负PE)
                    if (pe <= 0) continue;

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
                        MarketCap = marketCap
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

    private async Task<(double Ma20, double Ma200, string BuyPoint)?> CheckTechnicalAndMomentumAsync(string symbol, string name, double currentPrice, double pe, double marketCap, double currentTurnover)
    {
        try
        {
            string secid = symbol.StartsWith("6") ? "1." + symbol : "0." + symbol;
            string url = $"http://push2his.eastmoney.com/api/qt/stock/kline/get?secid={secid}&ut=7eea3edcaed734bea9cbbc2440b282fb&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=1&end=20500101&lmt=250";

            string jsonStr = "";
            if (_dataSource == "Tencent")
            {
                jsonStr = await FetchKLinesFromTencentAsync(symbol);
            }
            else
            {
                jsonStr = await _httpClient!.GetStringAsync(url);
            }

            if (string.IsNullOrEmpty(jsonStr)) return null;

            var root = JObject.Parse(jsonStr);
            var klines = root["data"]?["klines"] as JArray;

            if (klines == null || klines.Count < 200) return null; // Listed < 1 year

            var closes = new List<double>();
            var vols = new List<double>();
            var pcts = new List<double>();
            var turnovers = new List<double>();

            foreach (var k in klines)
            {
                var parts = k.ToString().Split(',');
                if (parts.Length >= 9)
                {
                    double close = 0;
                    double.TryParse(parts[2], out close);
                    closes.Add(close);
                    
                    double vol = 0;
                    double.TryParse(parts[5], out vol);
                    vols.Add(vol);

                    double pct = 0;
                    double.TryParse(parts[8], out pct);
                    pcts.Add(pct);
                    
                    double t = 0;
                    if (parts.Length >= 11) double.TryParse(parts[10], out t);
                    turnovers.Add(t);
                }
            }

            int count = closes.Count;
            if (count < 200) return null;
            
            // Note: Use the last close from K-line data instead of live currentPrice 
            // to stay consistent with adjusted averages (ma20/ma200).
            double adjustedCurrent = closes.Last(); 
            double ma200 = closes.Skip(count - 200).Average();
            double ma20 = closes.Skip(count - 20).Average();
            double ma10 = closes.Skip(count - 10).Average();
            double ma5 = closes.Skip(count - 5).Average();
            double avgVol = vols.Skip(count - 20).Average();

            // A: Above MA200 (长期趋势向上)
            if (adjustedCurrent < ma200) return null;

            // A1: 均线多头排列（确保上升趋势）
            if (!(ma5 > ma10 && ma10 > ma20)) return null;

            // B: 连续下跌过滤（避免选入"死猫跳"）
            var recent5Pct = pcts.Skip(count - 5).ToList();

            // 检查连续下跌天数（比累计跌幅更准确）
            int consecutiveDownDays = 0;
            foreach (var pct in recent5Pct)
            {
                if (pct < 0) consecutiveDownDays++;
                else break;
            }
            if (consecutiveDownDays >= 4) return null; // 连续4天下跌，直接排除（提高质量）

            // 同时检查累计跌幅（双重保险）
            double recent5DaySum = recent5Pct.Sum();
            if (recent5DaySum < -10.0) return null; // 5天累计跌幅超过10%，直接排除

            // C: 买点逻辑：精选高质量买点
            string buyPoint = "";
            bool isValidBuyPoint = false;

            double ma20Deviation = (adjustedCurrent / ma20 - 1) * 100;
            var recent5Vol = vols.Skip(count - 5).ToList();
            double lastPct = recent5Pct.Last();
            double lastVol = recent5Vol.Last();

            // 买点1: 回踩买点（最安全）- MA20附近±3%且缩量
            if (Math.Abs(ma20Deviation) <= 3 && lastVol < avgVol * 1.1)
            {
                buyPoint = "回踩买点";
                isValidBuyPoint = true;
            }
            // 买点2: 突破买点（激进）- 突破MA20且放量
            else if (ma20Deviation > 3 && ma20Deviation <= 12 && lastPct > 2 && lastVol > avgVol * 1.3)
            {
                buyPoint = "突破买点";
                isValidBuyPoint = true;
            }
            // 买点3: 多头持有（稳健）- MA20上方且今日上涨
            else if (ma20Deviation > 0 && ma20Deviation <= 6 && lastPct > 0.5)
            {
                buyPoint = "多头持有";
                isValidBuyPoint = true;
            }

            if (!isValidBuyPoint) return null;

            // C: 成交量形态判断
            var recent10Pct = pcts.Skip(count - 10).ToList();
            var recent10Vol = vols.Skip(count - 10).ToList();
            var recent10VolRatio = recent10Vol.Select(v => v / avgVol).ToList();

            // C1: 排除近期放量暴跌
            var ma20Volumes = new List<double>();
            for (int i = count - 10; i < count; i++)
            {
                ma20Volumes.Add(vols.Skip(Math.Max(0, i - 19)).Take(Math.Min(20, i + 1)).Average());
            }

            for (int i = 0; i < 10; i++)
            {
                if (recent10Pct[i] < -5.0 && recent10Vol[i] > ma20Volumes[i] * 1.5)
                {
                    return null; // volume expanded crash found
                }
            }

            // C2: 排除放量滞涨（高位诱多）- 放宽条件
            // 最近3天平均放量 > 2倍，但涨幅 < 1%
            if (buyPoint == "多头持有" || buyPoint == "突破买点" || buyPoint == "趋势跟踪")
            {
                var recent3Vol = recent10Vol.Skip(7).ToList();
                var recent3Pct = recent10Pct.Skip(7).ToList();
                if (recent3Vol.Average() > avgVol * 2.0 && recent3Pct.Average() < 1.0)
                {
                    return null; // 放量滞涨，主力可能在出货
                }
            }

            // C3: 优先选择缩量回踩的健康形态
            if (buyPoint == "回踩买点")
            {
                // 检查最近3天是否缩量
                var recent3Vol = recent10Vol.Skip(7).ToList();
                if (recent3Vol.All(v => v < avgVol * 0.9))
                {
                    buyPoint = "回踩缩量";
                }
            }

            // D: Turnover activity (最近5天有活跃交易) - 提高活跃度要求
            if (_dataSource == "Tencent")
            {
                // Tencent K-line doesn't have historical turnover, use current day's turnover as proxy
                if (currentTurnover <= 4.0) return null; 
            }
            else
            {
                var recent5T = turnovers.Skip(count - 5).ToList();
                if (recent5T.Max() <= 4.0 && recent5T.Average() <= 2.5) return null;
            }

            return (ma20, ma200, buyPoint);
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
            var dayData = root["data"]?[fullSymbol]?["qfqday"] as JArray;
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
                
                // 重新从缓存计算正确的 MA
                if (count > 0)
                {
                    ma20 = cache.TotalClose / count;
                    // 如果缓存数据由于历史原因（旧代码）导致 TotalClose 是总和，这里计算是正确的。
                    // 但我们需要确保逻辑一致。这里假设缓存存储的是总和。
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
                        
                        _klineCache[fullCode] = (totalVolume, historicalCloseSum, count, recentTrend, DateTime.Now);
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
        } catch { }
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


}
