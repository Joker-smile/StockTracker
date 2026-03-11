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
        private FlowLayoutPanel? _stockContainer;
        private Panel? _header;
        private Label? _btnMin;
        private Label? _btnClose;
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
            this.MinimizeBox = true;
            this.Resize += Form1_Resize;
            this.BackColor = Color.Black;
            this.Opacity = 0.6; // Slightly more visible for the extra text
            this.Size = new Size(300, 20);
            this.StartPosition = FormStartPosition.Manual;
            int screenWidth = Screen.PrimaryScreen?.WorkingArea.Width ?? 1920;
            int screenHeight = Screen.PrimaryScreen?.WorkingArea.Height ?? 1080;
            this.Location = new Point(screenWidth - 350, screenHeight - 50);

            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 18,
                BackColor = Color.FromArgb(40, 40, 40)
            };

            _btnClose = new Label
            {
                Text = "×",
                ForeColor = Color.Gray,
                Width = 20,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _btnClose.Click += (s, e) => Application.Exit();
            _btnClose.MouseEnter += (s, e) => _btnClose.ForeColor = Color.White;
            _btnClose.MouseLeave += (s, e) => _btnClose.ForeColor = Color.Gray;

            _btnMin = new Label
            {
                Text = "—",
                ForeColor = Color.Gray,
                Width = 20,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _btnMin.Click += (s, e) => {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = true;
            };
            _btnMin.MouseEnter += (s, e) => _btnMin.ForeColor = Color.White;
            _btnMin.MouseLeave += (s, e) => _btnMin.ForeColor = Color.Gray;

            _header.Controls.Add(_btnMin);
            _header.Controls.Add(_btnClose);

            _stockContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.Transparent,
                Padding = new Padding(5, 2, 5, 2)
            };

            this.Controls.Add(_stockContainer);
            this.Controls.Add(_header);

            // Context Menu (Move to container for right click)
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add("添加股票", null, AddStockItem_Click);
            _contextMenu.Items.Add("清空列表", null, RemoveStockItem_Click);
            _stockContainer.ContextMenuStrip = _contextMenu;
            _header.ContextMenuStrip = _contextMenu;

            // Dragging (Updated for header and container)
            Control[] draggables = { _header, _stockContainer };
            foreach (var ctrl in draggables)
            {
                ctrl.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragCursorPoint = Cursor.Position; _dragFormPoint = this.Location; } };
                ctrl.MouseMove += (s, e) => { if (_dragging) { Point diff = Point.Subtract(Cursor.Position, new Size(_dragCursorPoint)); this.Location = Point.Add(_dragFormPoint, new Size(diff)); } };
                ctrl.MouseUp += (s, e) => { _dragging = false; };
            }
        }

        private void Form1_Resize(object? sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal && this.ShowInTaskbar)
            {
                this.ShowInTaskbar = false;
            }
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
                string input = textBox.Text.Trim().ToLower();
                // Allow optional prefix (sh/sz/bj) + 6 digits
                System.Text.RegularExpressions.Regex stockRegex = new System.Text.RegularExpressions.Regex(@"^(sh|sz|bj)?\d{6}$");
                if (stockRegex.IsMatch(input) && !_stocks.Contains(input))
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
                    string cleaned = line.Trim().ToLower();
                    if (!string.IsNullOrEmpty(cleaned) && !_stocks.Contains(cleaned))
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
            // If the code already has a prefix, respect it
            if (code.StartsWith("sh") || code.StartsWith("sz") || code.StartsWith("bj"))
                return code.Substring(0, 2);

            if (code.StartsWith("5") || code.StartsWith("6") || code.StartsWith("7") || code.StartsWith("9")) return "sh";
            if (code.StartsWith("0") || code.StartsWith("1") || code.StartsWith("2") || code.StartsWith("3")) return "sz";
            if (code.StartsWith("8") || code.StartsWith("4")) return "bj";
            return "sh"; // fallback
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
                    if (_httpClient == null) return "系统忙";
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
                    string p = GetPrefix(code);
                    string c = code.Length > 6 ? code.Substring(code.Length - 6) : code;
                    prefixedCodes.Add(p + c);
                }

                string url = $"http://hq.sinajs.cn/list={string.Join(",", prefixedCodes)}";
                if (_httpClient == null) return;
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
                                    {
                                        if (current > 0)
                                            percent = (current - prevClose) / prevClose * 100;
                                        else
                                            current = prevClose; // Use prevClose if not yet traded
                                    }

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

                                    // Align fields: Name(10 units = 5 CJK), Price(8), Percent(8), Bid(7), Ask(7)
                                    string formattedPercent = $"{(percent > 0 ? "+" : "")}{percent:F2}%";
                                    string displayName = PadRightVisual(name, 10);
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
                    if (_stockContainer != null)
                    {
                        if (_stockContainer.InvokeRequired)
                            _stockContainer.Invoke(new Action(() => UpdateRows(displayTexts)));
                        else
                            UpdateRows(displayTexts);
                    }
                }
            }
            catch
            {
                // Ignore transient errors
            }
        }

        private void UpdateRows(List<string> rows)
        {
            if (_stockContainer == null) return;

            _stockContainer.SuspendLayout();
            _stockContainer.Controls.Clear();

            foreach (var row in rows)
            {
                Label lbl = new Label
                {
                    Text = row,
                    AutoSize = true,
                    ForeColor = Color.LightGray,
                    Font = new Font("NSimSun", 9F, FontStyle.Regular, GraphicsUnit.Point),
                    Margin = new Padding(0, 3, 0, 3) // Vertical spacing
                };
                // Make row draggable too
                lbl.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragCursorPoint = Cursor.Position; _dragFormPoint = this.Location; } };
                lbl.MouseMove += (s, e) => { if (_dragging) { Point diff = Point.Subtract(Cursor.Position, new Size(_dragCursorPoint)); this.Location = Point.Add(_dragFormPoint, new Size(diff)); } };
                lbl.MouseUp += (s, e) => { _dragging = false; };
                
                // Keep context menu consistent
                lbl.ContextMenuStrip = _contextMenu;

                _stockContainer.Controls.Add(lbl);
            }
            _stockContainer.ResumeLayout();

            // Adjust form size
            int totalWidth = 0;
            int totalHeight = _header?.Height ?? 18;
            foreach (Control ctrl in _stockContainer.Controls)
            {
                totalWidth = Math.Max(totalWidth, ctrl.GetPreferredSize(new Size(1000, 0)).Width);
                totalHeight += ctrl.Height + ctrl.Margin.Top + ctrl.Margin.Bottom;
            }
            totalHeight += _stockContainer.Padding.Top + _stockContainer.Padding.Bottom;
            
            this.Width = Math.Max(300, totalWidth + 20);
            this.Height = totalHeight;
        }

        private void UpdateText(string text)
        {
            // Deprecated but keeping signature for now if needed by other components
            UpdateRows(new List<string> { text });
        }

        private string PadRightVisual(string text, int targetWidth)
        {
            int currWidth = 0;
            foreach (char c in text) currWidth += (c > 127) ? 2 : 1;
            return text + new string(' ', Math.Max(0, targetWidth - currWidth));
        }
    }
}
