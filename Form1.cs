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
        private System.Windows.Forms.Timer _timer;
        private List<string> _stocks = new List<string>();
        private Label _displayLabel;
        private ContextMenuStrip _contextMenu;
        private readonly string _configFile;
        private HttpClient _httpClient;
        
        // Cache for K-Line volume and close data to prevent spamming Sina API every 3 seconds
        private Dictionary<string, (double TotalVolume, double TotalClose, int Count, DateTime LastUpdated)> _klineCache = new Dictionary<string, (double, double, int, DateTime)>();

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
            this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - 350, Screen.PrimaryScreen.WorkingArea.Height - 50);

            _displayLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point),
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

        private void AddStockItem_Click(object sender, EventArgs e)
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

        private void RemoveStockItem_Click(object sender, EventArgs e)
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

        private async Task<string> GetVolumePrediction(string fullCode, double currentPrice, double open, double high, double low, double prevClose, double currentVolShares)
        {
            try
            {
                double totalVolume = 0;
                double totalCloseForMa = currentPrice; // Initialize MA5 sum with 'today's' current price
                int count = 0;

                // Check cache first (expire after 30 minutes since historical data doesn't change rapidly, only today's approximation changes which we inject anyway)
                if (_klineCache.TryGetValue(fullCode, out var cache) && (DateTime.Now - cache.LastUpdated).TotalMinutes < 30)
                {
                    totalVolume = cache.TotalVolume;
                    totalCloseForMa = currentPrice + cache.TotalClose; // live price + past 4 days close
                    count = cache.Count;
                }
                else
                {
                    // Fetch last 5 "trading" days of k-line data
                    string klineUrl = $"https://quotes.sina.cn/cn/api/json_v2.php/CN_MarketData.getKLineData?symbol={fullCode}&scale=240&ma=no&datalen=6";
                    string jsonStr = await _httpClient.GetStringAsync(klineUrl);
                    
                    if (!string.IsNullOrWhiteSpace(jsonStr) && jsonStr != "null")
                    {
                        JArray klines = JArray.Parse(jsonStr);
                        
                        double historicalCloseSum = 0; // store past days separately from today's live price

                        int limit = Math.Max(0, klines.Count - 1); // exclude today's incomplete candle
                        for (int i = 0; i < limit; i++)
                        {
                            var kline = klines[i];
                            if (double.TryParse(kline["volume"].ToString(), out double v))
                            {
                                totalVolume += v;
                                count++;
                            }
                            if (double.TryParse(kline["close"].ToString(), out double c))
                            {
                                historicalCloseSum += c;
                            }
                        }

                        // Save to cache
                        _klineCache[fullCode] = (totalVolume, historicalCloseSum, count, DateTime.Now);
                        totalCloseForMa += historicalCloseSum;
                    }
                }

                if (count > 0)
                {
                    double avgVolume = totalVolume / count;
                    double ma5 = totalCloseForMa / (count + 1); // 5-day MA (approximated using past 4 full days + current live price)
                    double ratio = avgVolume > 0 ? (currentVolShares / avgVolume) : 1;
                    double currentPercent = prevClose > 0 ? ((currentPrice - prevClose) / prevClose * 100) : 0;
                    
                    // K-line Shape Analysis
                    double bodyTop = Math.Max(open, currentPrice);
                    double bodyBottom = Math.Min(open, currentPrice);
                    double upperShadow = high - bodyTop;
                    double lowerShadow = bodyBottom - low;
                    double bodySize = bodyTop - bodyBottom;
                    
                    bool isRed = currentPrice > open; // 阳线
                    bool isGreen = currentPrice < open; // 阴线
                        
                        bool aboveMa5 = currentPrice > ma5;
                        bool highVolume = ratio > 1.2;
                        bool shrinkVolume = ratio < 0.6;
                        
                        // A-股 典型特征捕获 (A-share specific patterns)
                        
                        // 1. 炸板 / 严重冲高回落 (长上影线) 
                        if (upperShadow > (bodySize * 2) && upperShadow > (prevClose * 0.03)) 
                        {
                            if (highVolume) return "高空抛压(长上影放量)"; // 放量长上影，主力跑路概率大
                            if (!aboveMa5) return "反弹诱多(长上影遇阻)"; // 均线下方长上影，受压回落
                            return "震荡洗盘(仙人指路?)"; // 上升趋势缩量长上影，可能是试盘
                        }
                        
                        // 2. 金针探底 (长下影线)
                        if (lowerShadow > (bodySize * 2) && lowerShadow > (prevClose * 0.03))
                        {
                            if (highVolume) return "金针探底(爆量承接)"; // 底部放量长下影，极可能见底反转
                            if (aboveMa5) return "单针探底(回踩确认)"; // 均线上方长下影，洗盘结束
                            return "护盘抵抗(仍处于弱势)"; // 均线下方长下影
                        }
                        
                        // 3. 强势涨停或光头阳线 (几乎无上影线的实体大阳)
                        if (isRed && currentPercent > 5.0 && upperShadow < (prevClose * 0.005))
                        {
                            if (highVolume) return "强势攻击(放量大阳)"; 
                            return "控盘拉升(缩量大阳)"; 
                        }
                        
                        // 4. 断头铡刀或光脚大阴线
                        if (isGreen && currentPercent < -5.0 && lowerShadow < (prevClose * 0.005))
                        {
                            if (highVolume) return "主力砸盘(放量长阴)";
                            return "情绪雪崩(缩量阴跌)";
                        }

                        // 综合趋势预测判断
                        if (aboveMa5)
                        {
                            if (highVolume)
                            {
                                if (currentPercent > 3.0) return "多头主升(带量上攻)";
                                if (currentPercent > 0.0) return "温和推涨(量价齐升)";
                                return "高位滞涨(放量不涨)"; // MA5之上，放量却跌了
                            }
                            else if (shrinkVolume)
                            {
                                if (currentPercent > 2.0) return "锁仓拉升(缩量逼空)";
                                if (currentPercent < 0.0) return "缩量洗盘(良性回踩)";
                                return "高位横盘(缩量企稳)";
                            }
                            else
                            {
                                if (currentPercent > 2.0) return "趋势向好(多头掌控)";
                                return "震荡攀升(沿5日线)";
                            }
                        }
                        else // Below MA5 (破位、弱势区间)
                        {
                            if (highVolume)
                            {
                                if (currentPercent < -3.0) return "破位杀跌(恐慌盘出)";
                                if (currentPercent > 0) return "低位抢筹(放量遇阻)"; // 跌破MA5且放量反弹
                                return "放量下跌(寻底中)"; 
                            }
                            else if (shrinkVolume)
                            {
                                if (currentPercent < 0.0) return "阴跌不止(买盘枯竭)";
                                return "弱势反抽(无量遇阻)";
                            }
                            else
                            {
                                if (currentPercent < -2.0) return "趋势走坏(空头掌控)";
                                return "弱势震荡(受压MA5)";
                        }
                    }
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
                                    string pred = await GetVolumePrediction(GetPrefix(pureCode) + pureCode, current, open, high, low, prevClose, currentVolShares);

                                    // Display format example: 贵州茅台[600519] 1500.00 +1.20% | 买:1499 卖:1501 | 智测:强烈看涨(主力介入)
                                    displayTexts.Add($"{name}[{pureCode}] {current:F2} {(percent>0?"+":"")}{percent:F2}% | 买:{bid} 卖:{ask} | 智测:{pred}");
                                }
                            }
                        }
                    }
                }

                if (displayTexts.Count > 0)
                {
                    string combined = string.Join("\n", displayTexts);
                    if (_displayLabel.InvokeRequired)
                        _displayLabel.Invoke(new Action(() => UpdateText(combined)));
                    else
                        UpdateText(combined);
                }
            }
            catch
            {
                // Ignore transient errors
            }
        }

        private void UpdateText(string text)
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
