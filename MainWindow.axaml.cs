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
        _configFile = Path.Combine(exePath ?? AppContext.BaseDirectory, "stocks.txt");
        
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
            string? exeDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName);
            if (exeDir == null) return;
            
            // Resolve the path to screener.py (Assuming it's in StockScreener parallel to publish or up one level)
            // StockTracker/publish/win-x64 -> wait, the dev dir vs publish dir
            // C# _configFile is Path.Combine(exePath ?? AppContext.BaseDirectory, "stocks.txt")
            // In release, it's alongside the exe. 
            // The StockScreener is at d:\wwwroot\StockTracker\StockScreener\screener.py
            string projectRoot = Path.GetFullPath(Path.Combine(exeDir, "..", "..", ".."));
            // Alternatively if published, it might just be 2 levels up. We can just search for it or use absolute if needed, 
            // but relying on relative path in root:
            string screenerPath = Path.Combine(exeDir, "..", "..", "StockScreener", "screener.py");
            if (!File.Exists(screenerPath)) 
            {
                // Fallback for dev mode
                screenerPath = Path.Combine(projectRoot, "StockScreener", "screener.py");
            }
            if (!File.Exists(screenerPath))
            {
                // Absolute fallback just in case
                screenerPath = @"d:\wwwroot\StockTracker\StockScreener\screener.py";
            }

            var psi = new ProcessStartInfo();
            if (OperatingSystem.IsWindows())
            {
                psi.FileName = "python";
            }
            else
            {
                psi.FileName = "python3";
            }
            
            psi.Arguments = $"\"{screenerPath}\" \"{_configFile}\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            var process = Process.Start(psi);
            if (process != null)
            {
                // Wait for it to finish asynchronously so we don't freeze the UI 
                await process.WaitForExitAsync();
            }

            // Ensure the loading UI shows for at least a few seconds to prevent flashing,
            // even if the script fails instantly or finds no new stocks.
            await Task.Delay(3000);
        }
        catch { }
        finally
        {
            _isScreenerRunning = false;
            // FileSystemWatcher should have picked up the changes and reloaded,
            // but just in case it didn't write anything (no new stocks), restore UI:
            Dispatcher.UIThread.Post(async () => {
                LoadConfig();
                await UpdatePrices();
            });
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
                string klineUrl = $"https://quotes.sina.cn/cn/api/json_v2.php/CN_MarketData.getKLineData?symbol={fullCode}&scale=240&ma=no&datalen=5";
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

                string status = "[观察]";
                string result = "";

                if (currentPercent >= limitThreshold)
                {
                    status = "[看多]";
                    if (ratio > 2.0) result = "爆量打板(分歧大/防炸)";
                    else if (ratio < 0.6) result = "极度控盘(缩量一字板)";
                    else result = "强势封板(多头绝对控盘)";
                    return $"{status}{result}";
                }
                if (currentPercent <= -limitThreshold)
                {
                    status = "[看空]";
                    result = ratio > 1.2 ? "恐慌跌停(放量杀跌)" : "情绪雪崩(无量跌停)";
                    return $"{status}{result}";
                }

                double bodyTop = Math.Max(open, currentPrice);
                double bodyBottom = Math.Min(open, currentPrice);
                double upperShadow = high - bodyTop;
                double lowerShadow = bodyBottom - low;
                double bodySize = bodyTop - bodyBottom;
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
                        result = currentPercent > 2.0 ? "多头掌控" : "震荡攀升";
                    }
                }
                else
                {
                    status = currentPercent < -2.0 ? "[看空]" : "[观察]";
                    if (ratio > 1.2) result = currentPercent < -3.0 ? "破位杀跌" : "放量承接";
                    else result = currentPercent < -1.0 ? "阴跌不止" : "弱势震荡";
                }
                return $"{status}{result}";
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
                
                IBrush? pctColor = item.Pct.StartsWith("+") ? Brush.Parse("#FFFF4444") : (item.Pct.StartsWith("-") ? Brush.Parse("#FF44FF44") : null);
                AddCol(item.Pct, 4, HorizontalAlignment.Right, pctColor);
                
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
