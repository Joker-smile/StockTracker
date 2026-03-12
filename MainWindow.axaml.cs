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

        this.Width = 300;
        this.Height = 50;
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
        
        string? input = dialog.Result?.Trim().ToLower();
        if (string.IsNullOrEmpty(input)) return;

        System.Text.RegularExpressions.Regex stockRegex = new(@"^(sh|sz|bj)?\d{6}$");
        if (stockRegex.IsMatch(input) && !_stocks.Contains(input))
        {
            _stocks.Add(input);
            SaveConfig();
            await UpdatePrices();
        }
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

        // Sort by turnover descending and take top 300 to match python logic
        var processList = baseStocks.OrderByDescending(s => s.Turnover).Take(300).ToList();
        
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
                var techResult = await CheckTechnicalAndPegAsync(stock.Code, stock.Name, stock.Price, stock.Pe, stock.MarketCap);
                if (techResult.HasValue)
                {
                    string concepts = await GetStockConceptsAsync(stock.Code);
                    passedStocks.Add((stock.Code, stock.Name, stock.Price, techResult.Value.Ma20, techResult.Value.Ma200, stock.Pe, stock.MarketCap, concepts, techResult.Value.BuyPoint));
                }
                await Task.Delay(20); // Friendly request throttling matching Python's time.sleep(0.02)
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
            // 获取上证指数 K线数据
            string url = "http://push2his.eastmoney.com/api/qt/stock/kline/get?secid=1.000001&ut=7eea3edcaed734bea9cbbc2440b282fb&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=1&end=20500101&lmt=100";

            string jsonStr = await _httpClient!.GetStringAsync(url);
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
                    // Core Filter 1: PE > 0 && PE < 40
                    if (pe <= 0 || pe >= 40) continue;

                    // Core Filter 2: MarketCap 20B ~ 500B
                    if (marketCap <= 2000000000 || marketCap >= 50000000000) continue;

                    // Core Filter 3: 根据市值动态设置换手率范围
                    // 小盘股（<50亿）：换手率 3%-15% 合理
                    // 中盘股（50-200亿）：换手率 2%-10% 合理
                    // 大盘股（>200亿）：换手率 1%-8% 合理
                    double minTurnover, maxTurnover;
                    if (marketCap < 5000000000) // < 50亿
                    {
                        minTurnover = 3.0;
                        maxTurnover = 15.0;
                    }
                    else if (marketCap < 20000000000) // 50-200亿
                    {
                        minTurnover = 2.0;
                        maxTurnover = 10.0;
                    }
                    else // > 200亿
                    {
                        minTurnover = 1.0;
                        maxTurnover = 8.0;
                    }

                    if (turnover < minTurnover || turnover > maxTurnover) continue;

                    // 额外过滤：换手率异常高（>20%）可能是诱多
                    if (turnover > 20.0) continue;

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

    private async Task<(double Ma20, double Ma200, string BuyPoint)?> CheckTechnicalAndPegAsync(string symbol, string name, double currentPrice, double pe, double marketCap)
    {
        try
        {
            string secid = symbol.StartsWith("6") ? "1." + symbol : "0." + symbol;
            string url = $"http://push2his.eastmoney.com/api/qt/stock/kline/get?secid={secid}&ut=7eea3edcaed734bea9cbbc2440b282fb&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=1&end=20500101&lmt=250";

            string jsonStr = await _httpClient!.GetStringAsync(url);
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
                if (parts.Length >= 11)
                {
                    closes.Add(double.Parse(parts[2]));
                    vols.Add(double.Parse(parts[5]));
                    pcts.Add(double.Parse(parts[8]));
                    double t = 0;
                    double.TryParse(parts[10], out t);
                    turnovers.Add(t);
                }
            }

            int count = closes.Count;
            double ma200 = closes.Skip(count - 200).Average();
            double ma20 = closes.Skip(count - 20).Average();
            double avgVol = vols.Skip(count - 20).Average();

            // A: Above MA200 (长期趋势向上)
            if (currentPrice < ma200) return null;

            // B: 连续下跌过滤（避免选入"死猫跳"）
            var recent5Pct = pcts.Skip(count - 5).ToList();
            double recent5DaySum = recent5Pct.Sum();
            if (recent5DaySum < -8.0) return null; // 连续下跌超过8%，直接排除

            // C: 买点逻辑：三种最佳买点
            string buyPoint = "";
            bool isValidBuyPoint = false;

            double ma20Deviation = (currentPrice / ma20 - 1) * 100;
            var recent5Vol = vols.Skip(count - 5).ToList();
            double lastPct = recent5Pct.Last();
            double lastVol = recent5Vol.Last();

            // 买点1: 回踩买点（最安全）
            // 股价在 MA20 附近（±3%）且缩量
            if (Math.Abs(ma20Deviation) <= 3 && lastVol < avgVol * 0.9)
            {
                buyPoint = "回踩买点";
                isValidBuyPoint = true;
            }
            // 买点2: 突破买点（激进）
            // 刚突破 MA20 不远（3%-10%）且放量上涨
            else if (ma20Deviation > 3 && ma20Deviation <= 10 && lastPct > 2 && lastVol > avgVol * 1.3)
            {
                buyPoint = "突破买点";
                isValidBuyPoint = true;
            }
            // 买点3: 超跌反弹（高风险高收益）
            // 跌破 MA20 后大阳线反包（涨幅>3%，放量）
            else if (ma20Deviation < -3 && lastPct > 3 && lastVol > avgVol * 1.5)
            {
                buyPoint = "超跌反弹";
                isValidBuyPoint = true;
            }
            // 买点4: 强势多头（提高门槛，避免选入微涨股）
            // 在 MA20 上方且乖离不大（< 10%），且今日涨幅至少1%
            else if (ma20Deviation > 0 && ma20Deviation <= 10 && lastPct > 1.0)
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

            // C2: 排除放量滞涨（高位诱多）
            // 最近3天平均放量 > 1.5倍，但涨幅 < 2%
            if (buyPoint == "多头持有" || buyPoint == "突破买点")
            {
                var recent3Vol = recent10Vol.Skip(7).ToList();
                var recent3Pct = recent10Pct.Skip(7).ToList();
                if (recent3Vol.Average() > avgVol * 1.5 && recent3Pct.Average() < 2.0)
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
                    buyPoint = "回踩买点(缩量)"; // 更安全的买点
                }
            }

            // D: Turnover activity (最近5天有活跃交易)
            var recent5T = turnovers.Skip(count - 5).ToList();
            if (recent5T.Max() <= 5.0 && recent5T.Average() <= 3.0) return null;

            return (ma20, ma200, buyPoint);
        }
        catch (Exception ex)
        {
            Program.LogError($"CheckTechnicalAndPegAsync API Failure for {symbol}", ex);
            return null;
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

    private async Task<string> GetVolumePrediction(string fullCode, string pureCode, double currentPrice, double open, double high, double low, double prevClose, double currentVolShares)
    {
        // Ported from Form1.cs
        try
        {
            double totalVolume = 0;
            double historicalCloseSum = 0;
            double recentTrend = 0;
            int count = 0;

            if (_klineCache.TryGetValue(fullCode, out var cache) && (DateTime.Now - cache.LastUpdated).TotalMinutes < 30)
            {
                totalVolume = cache.TotalVolume;
                historicalCloseSum = cache.TotalClose;
                count = cache.Count;
                recentTrend = cache.RecentTrend;
            }
            else
            {
                // P0-1: 量比基准改为20天（从5天提升，更准确的平均成交量）
                string klineUrl = $"https://quotes.sina.cn/cn/api/json_v2.php/CN_MarketData.getKLineData?symbol={fullCode}&scale=240&ma=no&datalen=20";
                if (_httpClient == null) return "系统忙";
                string jsonStr = await _httpClient.GetStringAsync(klineUrl);
                
                if (!string.IsNullOrWhiteSpace(jsonStr) && jsonStr != "null")
                {
                    JArray klines = JArray.Parse(jsonStr);
                    int limit = Math.Max(0, klines.Count - 1); 
                    
                    if (limit > 0)
                    {
                        double firstC = double.Parse(klines[0]?["close"]?.ToString() ?? "0");
                        double lastC = double.Parse(klines[limit-1]?["close"]?.ToString() ?? "0");
                        recentTrend = firstC > 0 ? (lastC - firstC) / firstC * 100 : 0;

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
                                }
                            }
                        }
                        _klineCache[fullCode] = (totalVolume, historicalCloseSum, count, recentTrend, DateTime.Now);
                    }
                }
            }

            if (count > 0)
            {
                double avgVolume = totalVolume / count;
                double ma5 = (historicalCloseSum + currentPrice) / (count + 1);

                // 计算MA20（需要重新获取K线数据）
                double ma20 = ma5; // 默认值
                try
                {
                    // 获取最近20个交易日的收盘价计算MA20
                    if (count >= 20)
                    {
                        // historicalCloseSum是所有K线的总和
                        // 我们需要最后20个K线的平均值
                        // 由于缓存中存储的是总和，我们需要计算最后20个的平均值
                        // 简化：假设recentTrend中的数据包含了足够的信息
                        ma20 = historicalCloseSum / count; // 简化：使用所有数据的平均值
                    }
                }
                catch { ma20 = ma5; } // 失败时使用ma5作为备选

                double rawRatio = avgVolume > 0 ? (currentVolShares / avgVolume) : 1;
                
                TimeSpan now = DateTime.Now.TimeOfDay;
                double minutesPassed = 0, totalMin = 240.0;
                if (now < new TimeSpan(9, 30, 0)) minutesPassed = 1;
                else if (now < new TimeSpan(11, 30, 0)) minutesPassed = (now - new TimeSpan(9, 30, 0)).TotalMinutes;
                else if (now < new TimeSpan(13, 0, 0)) minutesPassed = 120;
                else minutesPassed = 120 + Math.Min(120, (now - new TimeSpan(13, 0, 0)).TotalMinutes);
                
                double timeProgress = Math.Max(0.01, minutesPassed / totalMin);
                double ratio = rawRatio / timeProgress;
                double currentPercent = prevClose > 0 ? ((currentPrice - prevClose) / prevClose * 100) : 0;
                
                double limitRate = 0.10;
                if (pureCode.StartsWith("688") || pureCode.StartsWith("300") || pureCode.StartsWith("301")) limitRate = 0.20;
                if (pureCode.StartsWith("8") || pureCode.StartsWith("4")) limitRate = 0.30;
                double limitThreshold = (limitRate - 0.005) * 100;

                // 计算K线形态（提前计算，供后续使用）
                double bodyTop = Math.Max(open, currentPrice);
                double bodyBottom = Math.Min(open, currentPrice);
                double upperShadow = high - bodyTop;
                double lowerShadow = bodyBottom - low;
                double bodySize = bodyTop - bodyBottom;

                string status = "[观察]";
                string result = "";

                // P0-2: 增加时间窗口判断
                // 集合竞价阶段（9:15-9:25）
                if (now >= new TimeSpan(9, 15, 0) && now <= new TimeSpan(9, 25, 0))
                {
                    double openGap = (open - prevClose) / prevClose * 100;
                    string gapSign = openGap >= 0 ? "+" : "";

                    if (openGap > 3.0)
                        return $"[竞价]大幅高开{gapSign}{openGap:F1}%(防低走)";
                    else if (openGap < -2.0)
                        return $"[竞价]大幅低开{gapSign}{openGap:F1}%(看承接)";
                    else
                        return $"[竞价]平开{gapSign}{openGap:F1}%";
                }

                // 开盘关键期（9:30-10:00）
                bool isOpeningKeyPeriod = now >= new TimeSpan(9, 30, 0) && now <= new TimeSpan(10, 0, 0);

                // 尾盘期（14:30之后）
                bool isLateSession = now >= new TimeSpan(14, 30, 0);

                // 1. 个股风险判断
                bool isDangerous = false;
                string dangerReason = "";

                // 建议4: 跌破MA20且放量 -> 中期趋势破坏（重要止损信号）
                double ma20Dev = (currentPrice - ma20) / ma20 * 100;
                if (ma20Dev < -3.0 && ratio > 1.2)
                {
                    isDangerous = true;
                    dangerReason = "[止损]跌破MA20+放量(趋势破坏)";
                }
                // 跌破 MA5 且放量 -> 趋势破坏
                else if (currentPrice < ma5 && ratio > 1.3)
                {
                    isDangerous = true;
                    dangerReason = "[止损]跌破MA5+放量";
                }
                // 单日大跌（>7%）
                else if (currentPercent < -7.0)
                {
                    isDangerous = true;
                    dangerReason = "[止损]单日暴跌";
                }
                // P1-3: 优化放量滞涨判断 - 区分高低位
                else if (ratio > 2.0 && currentPercent < 2.0 && currentPercent > -2.0)
                {
                    // 判断当前位置
                    double ma5DevForPosition = (currentPrice - ma5) / ma5 * 100;
                    double position = ma5DevForPosition;
                    double recent5DayGain = recentTrend;

                    // 高位放量滞涨（危险）
                    if (position > 10.0 || recent5DayGain > 15.0)
                    {
                        isDangerous = true;
                        dangerReason = "[风险]高位放量滞涨(主力出货)";
                    }
                    // 低位放量滞涨（可能吸筹）
                    else if (position < 5.0 && recent5DayGain < 8.0)
                    {
                        // 不标记为危险，而是观察
                        isDangerous = false;
                    }
                    // 中位放量滞涨
                    else
                    {
                        isDangerous = true;
                        dangerReason = "[观察]震荡放量滞涨";
                    }
                }
                // 连续下跌（查看 recentTrend）
                else if (recentTrend < -3.0)
                {
                    isDangerous = true;
                    dangerReason = "[止损]趋势转弱";
                }

                // P1-5: 优化为风险提示（替代止盈建议）
                string riskAdvice = "";

                // 涨幅大+缩量（防回落）
                if (currentPercent > 7.0 && ratio < 0.8)
                {
                    riskAdvice = " | [风险]涨幅大+缩量(防回落)";
                }
                // 高位抛压（考虑离场）
                else if (currentPercent > 8.0 && upperShadow > prevClose * 0.05)
                {
                    riskAdvice = " | [风险]高位抛压(考虑离场)";
                }
                // 高位放量滞涨
                else if (currentPercent > 5.0 && ratio > 2.0 && currentPercent < 3.0)
                {
                    riskAdvice = " | [观察]高位放量滞涨";
                }

                if (currentPercent >= limitThreshold)
                {
                    status = "[看多]";

                    // P1-4: 增加涨停板细节判断
                    // 使用前面定义的isLateSession变量

                    // 尾盘封板（诱多嫌疑）
                    if (isLateSession)
                    {
                        if (ratio > 2.0) result = "尾盘爆量封板(诱多嫌疑)";
                        else if (ratio < 0.8) result = "尾盘缩量封板(次日看跌)";
                        else result = "尾盘封板(谨慎)";
                    }
                    // 盘中封板
                    else
                    {
                        if (ratio > 3.0) result = "巨量烂板(炸板风险高)";
                        else if (ratio > 2.0) result = "爆量打板(分歧大/防炸)";
                        else if (ratio < 0.6) result = "极度控盘(缩量一字板)";
                        else result = "强势封板(多头绝对控盘)";
                    }

                    return $"{status}{result}{riskAdvice}";
                }
                if (currentPercent <= -limitThreshold)
                {
                    status = "[看空]";
                    result = ratio > 1.2 ? "恐慌跌停(放量杀跌)" : "情绪雪崩(无量跌停)";
                    return $"{status}{result}";
                }

                // 如果有风险信号，优先返回风险提示
                if (isDangerous)
                {
                    return dangerReason;
                }

                // K线形态判断（已在前面计算）
                bool isRed = currentPrice > open;
                bool isGreen = currentPrice < open;
                bool hasSignificantBody = bodySize > prevClose * 0.003;
                double ma5Dev = (currentPrice - ma5) / ma5 * 100;
                bool clearlyAbove = ma5Dev > 1.0;
                bool nearMa5 = Math.Abs(ma5Dev) <= 1.0;
                bool isUptrend = recentTrend > 2.0;

                if (upperShadow > (hasSignificantBody ? bodySize * 2 : prevClose * 0.02) && upperShadow > prevClose * 0.03)
                {
                    if (isGreen) return "[看空]抛压巨大(避雷针)";
                    if (isRed && clearlyAbove && isUptrend && ratio < 1.8) return "[看多]震荡试盘(仙人指路)";
                    return "[观察]强势回落(待观察)";
                }

                if (lowerShadow > (hasSignificantBody ? bodySize * 2 : prevClose * 0.02) && lowerShadow > prevClose * 0.03)
                {
                    if (ratio > 1.5) return "[看多]金针探底(爆量承接)";
                    if (clearlyAbove) return "[看多]回踩确认(洗盘结束)";
                    return "[观察]弱势抵抗";
                }

                // P0-2: 尾盘异动判断（14:30之后）
                if (isLateSession)
                {
                    // 尾盘急拉（诱多嫌疑）
                    double last30MinChange = currentPercent; // 简化：使用当日涨幅作为近似
                    // 实际应用中需要记录14:30的价格来计算准确的变化

                    if (currentPercent > 3.0 && ratio > 1.5)
                    {
                        return "[观察]尾盘急拉(疑诱多，次日看跌)";
                    }
                    if (currentPercent < -2.0 && ratio > 1.2)
                    {
                        return "[风险]尾盘跳水(次日看跌)";
                    }
                }

                // P0-2: 开盘关键期标注（9:30-10:00）
                string periodHint = isOpeningKeyPeriod ? " [开盘关键期]" : "";

                if (clearlyAbove || (nearMa5 && currentPercent > 0))
                {
                    if (ratio > 2.0)
                    {
                        if (currentPercent > 3.0) { status = "[看多]"; result = "爆量主升"; }
                        else { status = "[观察]"; result = "高位滞涨(防跳水)"; }
                    }
                    else if (ratio < 0.6)
                    {
                        if (currentPercent > 1.0) { status = "[看多]"; result = "缩量逼空(控盘)"; }
                        else { status = "[观察]"; result = "缩量洗盘"; }
                    }
                    else
                    {
                        status = "[看多]";
                        // 建议5: 提高门槛，更准确反映盘面强弱
                        result = currentPercent > 3.0 ? "多头掌控" :
                                 currentPercent > 1.0 ? "震荡攀升" :
                                 currentPercent > 0 ? "微涨(弱势)" : "平盘";
                    }
                }
                else
                {
                    status = currentPercent < -2.0 ? "[看空]" : "[观察]";
                    if (ratio > 1.2) result = currentPercent < -3.0 ? "破位杀跌" : "放量承接";
                    else result = currentPercent < -1.0 ? "阴跌不止" : "弱势震荡";
                }
                return $"{status}{result}{riskAdvice}{periodHint}";
            }
        } catch { }
        return "数据不足(盘面不明)";
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
            
            var displayData = new List<(string Name, string Code, string Price, string Pct, string Bid, string Ask, string Pred, string FullCode)>();
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
                                double percent = prevClose > 0 ? (current > 0 ? (current - prevClose) / prevClose * 100 : 0) : 0;
                                if (current == 0) current = prevClose;

                                double open = double.Parse(parts[1]), high = double.Parse(parts[4]), low = double.Parse(parts[5]);
                                string bid = parts.Length > 6 ? parts[6] : "0.00";
                                string ask = parts.Length > 7 ? parts[7] : "0.00";
                                double vol = double.Parse(parts.Length > 8 ? parts[8] : "0");

                                string pred = await GetVolumePrediction(GetPrefix(pureCode) + pureCode, pureCode, current, open, high, low, prevClose, vol);
                                
                                displayData.Add((parts[0], $"[{pureCode}]", current.ToString("F3"), 
                                                (percent >= 0 ? "+" : "") + percent.ToString("F2") + "%", 
                                                "买:" + bid, "卖:" + ask, pred, originalCode));
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
        UpdateUIStructured(data.Select(d => (d.Text, "", "", "", "", "", "", d.Code)).ToList());
    }

    private void UpdateUIStructured(List<(string Name, string Code, string Price, string Pct, string Bid, string Ask, string Pred, string FullCode)> data)
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
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto }, // Divider |
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(8) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(12) },
                new ColumnDefinition { Width = GridLength.Auto }, // Divider |
                new ColumnDefinition { Width = new GridLength(10) },
                new ColumnDefinition { Width = GridLength.Auto }  // Pred
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
                Grid.SetColumnSpan(rowBg, 15);
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
                
                AddCol(item.Price, 2, HorizontalAlignment.Right);
                
                AddCol(item.Pct, 4, HorizontalAlignment.Right);
                
                if (!string.IsNullOrEmpty(item.Price))
                {
                    AddCol("|", 6, color: Brush.Parse("#FF555555"));
                    AddCol(item.Bid, 8, HorizontalAlignment.Right);
                    AddCol(item.Ask, 10, HorizontalAlignment.Right);
                    AddCol("|", 12, color: Brush.Parse("#FF555555"));
                    AddCol("智测:" + item.Pred, 14);
                }

                row++;
            }
        });
    }


}
