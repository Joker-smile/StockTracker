using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace StockTracker
{
    public partial class Form1 : Form
    {
        private System.Windows.Forms.Timer? _timer;
        private List<string> _stocks = new List<string>();
        private Label? _displayLabel;
        private ContextMenuStrip? _contextMenu;
        private readonly string _configFile;
        private HttpClient? _httpClient;
        
        // Cache for K-Line volume and close data to prevent spamming Sina API every 3 seconds
        // (TotalVolume, HistoricalCloseSum, Count, RecentTrendPercent, LastUpdated)
        private Dictionary<string, (double TotalVolume, double TotalClose, int Count, double RecentTrend, DateTime LastUpdated)> _klineCache = 
            new Dictionary<string, (double, double, int, double, DateTime)>();

        // Custom dragging
        private Point _dragCursorPoint;
        private Point _dragFormPoint;
        private bool _dragging;

        public Form1()
        {
            InitializeComponent();
            
            // Register GB2312 support
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            _configFile = Path.Combine(Application.StartupPath, "stocks.txt");
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Referer", "http://finance.sina.com.cn/");
            
            LoadConfig();
            SetupUI();
            
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 3000;
            _timer.Tick += async (s, e) => await UpdatePrices();
            _timer.Start();

            // Run first update soon
            _ = UpdatePrices();
        }

        private void SetupUI()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.Opacity = 0.6; // Slightly more visible for the extra text
            this.Size = new Size(300, 20);
            this.StartPosition = FormStartPosition.Manual;
            int screenWidth = Screen.PrimaryScreen?.WorkingArea.Width ?? 1920;
            int screenHeight = Screen.PrimaryScreen?.WorkingArea.Height ?? 1080;
            this.Location = new Point(screenWidth - 350, screenHeight - 50);

            _displayLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "...",
                BackColor = Color.Transparent
            };
            this.Controls.Add(_displayLabel);

            // Context Menu
            _contextMenu = new ContextMenuStrip();
            var addStockItem = new ToolStripMenuItem("添加股票代码");
            addStockItem.Click += AddStockItem_Click;
            
            var removeStockItem = new ToolStripMenuItem("清空股票");
            removeStockItem.Click += RemoveStockItem_Click;

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => Application.Exit();

            _contextMenu.Items.Add(addStockItem);
            _contextMenu.Items.Add(removeStockItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(exitItem);

            _displayLabel.ContextMenuStrip = _contextMenu;

            // Draggable
            _displayLabel.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    _dragging = true;
                    _dragCursorPoint = Cursor.Position;
                    _dragFormPoint = this.Location;
                }
            };
            _displayLabel.MouseMove += (s, e) => {
                if (_dragging) {
                    Point currentCursorPosition = Cursor.Position;
                    Point newLocation = new Point(
                        _dragFormPoint.X + (currentCursorPosition.X - _dragCursorPoint.X),
                        _dragFormPoint.Y + (currentCursorPosition.Y - _dragCursorPoint.Y));
                    this.Location = newLocation;
                }
            };
            _displayLabel.MouseUp += (s, e) => { _dragging = false; };
        }

        private void AddStockItem_Click(object? sender, EventArgs e)
        {
            Form prompt = new Form()
            {
                Width = 250,
                Height = 120,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "添加股票",
                StartPosition = FormStartPosition.CenterScreen,
                TopMost = true
            };
            Label textLabel = new Label() { Left = 10, Top = 10, Width = 200, Text = "输入6位纯数字代码(如: 000001):" };
            TextBox textBox = new TextBox() { Left = 10, Top = 30, Width = 200 };
            Button confirmation = new Button() { Text = "确定", Left = 135, Top = 55, Width = 75, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                string input = textBox.Text.Trim();
                if (input.Length == 6 && IsDigitsOnly(input) && !_stocks.Contains(input))
                {
                    _stocks.Add(input);
                    SaveConfig();
                    _ = UpdatePrices();
                }
            }
        }

        private bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        private void RemoveStockItem_Click(object? sender, EventArgs e)
        {
            _stocks.Clear();
            SaveConfig();
            UpdateText("暂无自选，请右键添加");
        }

        private void LoadConfig()
        {
            if (File.Exists(_configFile))
            {
                // We stored it as just the 6 digit string, but potentially old config has "sh600519"
                var lines = File.ReadAllLines(_configFile);
                _stocks = new List<string>();
                foreach (var line in lines)
                {
                    string cleaned = line.Trim();
                    // Clean old prefixes
                    if (cleaned.Length > 6)
                    {
                        cleaned = cleaned.Substring(cleaned.Length - 6);
                    }
                    if (cleaned.Length == 6 && IsDigitsOnly(cleaned))
                    {
                        _stocks.Add(cleaned);
                    }
                }
            }
            
            if (_stocks.Count == 0)
            {
                _stocks = new List<string> { "000001" }; // Default Code
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            try {
                File.WriteAllLines(_configFile, _stocks);
            } catch { }
        }
        
        private string GetPrefix(string code)
        {
            if (code.StartsWith("6")) return "sh";
            if (code.StartsWith("0") || code.StartsWith("3")) return "sz";
            if (code.StartsWith("8") || code.StartsWith("4")) return "bj";
            return "sh"; // fallback
        }

        private string GetSector(string code)
        {
            if (code.StartsWith("688")) return "科创板";
            if (code.StartsWith("6")) return "上证主板";
            if (code.StartsWith("3")) return "创业板";
            if (code.StartsWith("0")) return "深证主板";
            if (code.StartsWith("8") || code.StartsWith("4")) return "北交所";
            return "A股";
        }

        private async Task<string> GetVolumePrediction(string fullCode, string pureCode, double currentPrice, double open, double high, double low, double prevClose, double currentVolShares)
        {
            try
            {
                double totalVolume = 0;
                double historicalCloseSum = 0;
                double recentTrend = 0;
                int count = 0;

                // 1. Correct MA5 & Trend Data (Fetch datalen=5 -> 4 historical days + 1 today)
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
                    string jsonStr = await _httpClient.GetStringAsync(klineUrl);
                    
                    if (!string.IsNullOrWhiteSpace(jsonStr) && jsonStr != "null")
                    {
                        JArray klines = JArray.Parse(jsonStr);
                        int limit = Math.Max(0, klines.Count - 1); // exclude today
                        
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
                    
                    // 2. Time-Normalized Volume Ratio (Annualized)
                    TimeSpan now = DateTime.Now.TimeOfDay;
                    double minutesPassed = 0, totalMin = 240.0;
                    if (now < new TimeSpan(9, 30, 0)) minutesPassed = 1;
                    else if (now < new TimeSpan(11, 30, 0)) minutesPassed = (now - new TimeSpan(9, 30, 0)).TotalMinutes;
                    else if (now < new TimeSpan(13, 0, 0)) minutesPassed = 120;
                    else minutesPassed = 120 + Math.Min(120, (now - new TimeSpan(13, 0, 0)).TotalMinutes);
                    
                    double timeProgress = Math.Max(0.01, minutesPassed / totalMin);
                    double ratio = rawRatio / timeProgress;
                    
                    double currentPercent = prevClose > 0 ? ((currentPrice - prevClose) / prevClose * 100) : 0;
                    
                    // 3. Market-Specific Limit Thresholds
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

                    // 4. K-line Shape & Doji Guard
                    double bodyTop = Math.Max(open, currentPrice);
                    double bodyBottom = Math.Min(open, currentPrice);
                    double upperShadow = high - bodyTop;
                    double lowerShadow = bodyBottom - low;
                    double bodySize = bodyTop - bodyBottom;
                    bool isRed = currentPrice > open;
                    bool isGreen = currentPrice < open;
                    bool hasSignificantBody = bodySize > prevClose * 0.003;

                    // 5. MA5 Deviation Band
                    double ma5Dev = (currentPrice - ma5) / ma5 * 100;
                    bool clearlyAbove = ma5Dev > 1.0;
                    bool nearMa5 = Math.Abs(ma5Dev) <= 1.0;
                    bool clearlyBelow = ma5Dev < -1.0;

                    // 6. Refined Patterns (Trend aware)
                    bool isUptrend = recentTrend > 2.0;

                    // Long Upper Shadow
                    if (upperShadow > (hasSignificantBody ? bodySize * 2 : prevClose * 0.02) && upperShadow > prevClose * 0.03)
                    {
                        if (isGreen) return "[看空]抛压巨大(避雷针)";
                        if (isRed && clearlyAbove && isUptrend && ratio < 1.8) return "[看多]震荡试盘(仙人指路)";
                        return "[观察]强势回落(待观察)";
                    }

                    // Long Lower Shadow
                    if (lowerShadow > (hasSignificantBody ? bodySize * 2 : prevClose * 0.02) && lowerShadow > prevClose * 0.03)
                    {
                        if (ratio > 1.5) return "[看多]金针探底(爆量承接)";
                        if (clearlyAbove) return "[看多]回踩确认(洗盘结束)";
                        return "[观察]弱势抵抗";
                    }

                    // 7. General Trend Prediction
                    if (clearlyAbove || (nearMa5 && currentPercent > 0))
                    {
                        status = "[看多]";
                        if (ratio > 2.0) result = currentPercent > 3.0 ? "爆量主升" : "高位滞涨";
                        else if (ratio < 0.6) result = currentPercent > 1.0 ? "缩量逼空" : "缩量洗盘";
                        else result = currentPercent > 2.0 ? "多头掌控" : "震荡攀升";
                    }
                    else
                    {
                        status = currentPercent < -2.0 ? "[看空]" : "[观察]";
                        if (ratio > 1.2) result = currentPercent < -3.0 ? "破位杀跌" : "低位抢筹";
                        else result = currentPercent < -1.0 ? "阴跌不止" : "弱势震荡";
                    }
                    return $"{status}{result}";
                }
            }
            catch { }
            return "数据不足(盘面不明)";
        }


        private async Task UpdatePrices()
        {
            if (_stocks.Count == 0)
            {
                UpdateText("右键添加股票");
                return;
            }

            try
            {
                // Prefix the codes for standard Sina api logic
                List<string> prefixedCodes = new List<string>();
                foreach(var code in _stocks)
                {
                    prefixedCodes.Add(GetPrefix(code) + code);
                }

                string url = $"http://hq.sinajs.cn/list={string.Join(",", prefixedCodes)}";
                var bytes = await _httpClient.GetByteArrayAsync(url);
                
                // Get GB2312 to parse Chinese characters correctly
                Encoding gb2312 = Encoding.GetEncoding("GB2312");
                string response = gb2312.GetString(bytes);

                var lines = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> displayTexts = new List<string>();

                foreach (var line in lines)
                {
                    int start = line.IndexOf("=\"");
                    if (start != -1)
                    {
                        string codePart = line.Substring(0, start);
                        string pureCode = "";
                        if (codePart.Length > 8)
                        {
                            pureCode = codePart.Substring(codePart.Length - 6); 
                        }

                        int end = line.IndexOf("\";", start);
                        if (end != -1)
                        {
                            string data = line.Substring(start + 2, end - start - 2);
                            var parts = data.Split(',');
                            if (parts.Length > 3)
                            {
                                string name = parts[0];
                                if (double.TryParse(parts[2], out double prevClose) &&
                                    double.TryParse(parts[3], out double current))
                                {
                                    double percent = 0;
                                    if (prevClose > 0)
                                        percent = (current - prevClose) / prevClose * 100;
                                    
                                    if (current == 0 && prevClose > 0) current = prevClose;

                                    string sector = GetSector(pureCode);
                                    // Extract K-line basics from the Sina real-time row
                                    double open = 0, high = 0, low = 0;
                                    double.TryParse(parts[1], out open);
                                    double.TryParse(parts[4], out high);
                                    double.TryParse(parts[5], out low);

                                    string bid = parts.Length > 6 ? parts[6] : "0.00";
                                    string ask = parts.Length > 7 ? parts[7] : "0.00";
                                    string volStr = parts.Length > 8 ? parts[8] : "0";

                                    double currentVolShares = 0;
                                    double.TryParse(volStr, out currentVolShares);

                                    // Async advanced analysis
                                    string pred = await GetVolumePrediction(GetPrefix(pureCode) + pureCode, pureCode, current, open, high, low, prevClose, currentVolShares);

                                    // Align fields: Name(8), Price(8), Percent(8), Bid(7), Ask(7)
                                    string formattedPercent = $"{(percent > 0 ? "+" : "")}{percent:F2}%";
                                    // Use a simpler approach for name alignment: ensure consistent length
                                    string displayName = name.Length > 4 ? name.Substring(0, 4) : name.PadRight(4, '　');
                                    string displayRow = string.Format("{0} [{1}] {2,8:F2} {3,8} | 买:{4,7} 卖:{5,7} | 智测:{6}",
                                        displayName,
                                        pureCode,
                                        current,
                                        formattedPercent,
                                        bid,
                                        ask,
                                        pred);
                                    
                                    displayTexts.Add(displayRow);
                                }
                            }
                        }
                    }
                }

                if (displayTexts.Count > 0)
                {
                    string combined = string.Join("\n", displayTexts);
                    if (_displayLabel != null)
                    {
                        if (_displayLabel.InvokeRequired)
                            _displayLabel.Invoke(new Action(() => UpdateText(combined)));
                        else
                            UpdateText(combined);
                    }
                }
            }
            catch
            {
                // Ignore transient errors
            }
        }

        private void UpdateText(string text)
        {
            if (_displayLabel != null)
            {
                if (_displayLabel.InvokeRequired)
                {
                    _displayLabel.Invoke(new Action(() => UpdateText(text)));
                    return;
                }
                _displayLabel.Text = text;

                using (Graphics g = CreateGraphics())
                {
                    SizeF size = g.MeasureString(text, _displayLabel.Font);
                    this.Width = Math.Max(200, (int)size.Width + 20);
                    this.Height = Math.Max(20, (int)size.Height + 5);
                }
            }
        }
    }
}
