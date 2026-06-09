using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace StockTracker
{
    public class DataPointMeta
    {
        public string FieldName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string TradeDate { get; set; } = string.Empty;
        public DateTime FetchedAt { get; set; } = DateTime.Now;
        public bool IsMissing { get; set; }
        public double Confidence { get; set; } = 1.0;
        public string Note { get; set; } = string.Empty;
    }

    public class StockEventInfo
    {
        public string EventDate { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int RiskLevel { get; set; }

        public bool IsHighRisk => RiskLevel >= 70;
    }

    public class StockDeepAnalysisContext
    {
        // 基础标识
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        // 实时行情增强
        public double CurrentPrice { get; set; }
        public double PctChange { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double VolumeRatio { get; set; } // 量比
        public double TurnoverRate { get; set; } // 换手率
        public double PE { get; set; } // 市盈率
        public double PB { get; set; } // 市净率
        public double TotalMarketValue { get; set; } // 总市值

        // 均线与趋势分析 (精准推演)
        public double MA5 { get; set; }
        public double MA10 { get; set; }
        public double MA20 { get; set; }
        public double MA60 { get; set; } // 新增：MA60
        public double MA120 { get; set; } // 新增：MA120
        public double BiasMA5 { get; set; }
        public double BiasMA10 { get; set; }
        public double BiasMA20 { get; set; }
        public double BiasMA60 { get; set; } // 新增：MA60乖离
        public string MAAlignment { get; set; } = string.Empty; // 多头/空头

        // 舆情情报 (最新新闻)
        public List<string> LatestNews { get; set; } = new();

        // 筹码与大势分析
        public double ProfitRatio { get; set; } // 获利比例(%)
        public double ChipAvgCost { get; set; } // 平均成本
        public double ChipConcentration90 { get; set; } // 90筹码集中度
        public double ChipPeakPressure { get; set; } // 新增：筹码峰压力位
        public double ChipPeakSupport { get; set; } // 新增：筹码峰支撑位
        public double MainForceNetInflow { get; set; } // 主力净流入金额

        // === 新增：主力资金分级 ===
        public double SuperLargeOrderInflow { get; set; } // 超大单净流入
        public double LargeOrderInflow { get; set; }       // 大单净流入
        public double MediumOrderInflow { get; set; }      // 中单净流入
        public double SmallOrderInflow { get; set; }       // 小单净流入
        public double MainForceInflowRatio { get; set; }   // 主力净流入占成交额比例(%)

        // 财报基础 (价值底座)
        public double ROE { get; set; } // 净资产收益率
        public double OperatingRevenue { get; set; } // 营业总收入(亿)
        public double NetProfit { get; set; } // 归母净利润(亿)
        public double OperatingCashFlowPerShare { get; set; } // 每股经营现金流

        // 昨日对比异动
        public double VolumeChangeRatio { get; set; } // 成交量较昨日变化倍数
        public double PriceChangeRatio { get; set; } // 价格较昨日变化
        public double TurnoverAmount { get; set; } // 新增：成交额

        // === 新增：高级量化指标与策略 ===
        public TechnicalAnalysisScore TechScore { get; set; } = new();
        public HighWinRateStrategies.TimingScore Timing { get; set; } = new();
        public HighWinRateStrategies.SmartStopLoss SmartStop { get; set; } = new();

        public List<double> RecentPrices { get; set; } = new();
        public List<double> RecentVolumes { get; set; } = new();

        // === 新增：板块联动数据 ===
        public string SectorName { get; set; } = string.Empty; // 所属板块
        public double SectorPctChange { get; set; } // 板块涨跌幅
        public double SectorRankPercent { get; set; } // 个股在板块内排名百分比
        public double RelativeStrengthVsSector { get; set; } // 相对板块强度

        // === 新增：新闻情绪量化 ===
        public double NewsSentimentScore { get; set; } // 新闻情绪评分 (0-100, 50为中性)
        public double NewsImpactScore { get; set; } // 新闻影响力评分 (0-100)

        // === 新增：多时间框架数据 ===
        public List<double> Prices60Min { get; set; } = new(); // 60分钟级别价格
        public List<double> Volumes60Min { get; set; } = new(); // 60分钟级别成交量
        public List<double> Prices15Min { get; set; } = new(); // 15分钟级别价格
        public List<double> Volumes15Min { get; set; } = new(); // 15分钟级别成交量
        public TechnicalAnalysisScore TechScore60Min { get; set; } = new(); // 60分钟技术指标
        public TechnicalAnalysisScore TechScore15Min { get; set; } = new(); // 15分钟技术指标

        // === 新增：波动率锥数据 ===
        public double Volatility20Day { get; set; } // 20日波动率
        public double Volatility60Day { get; set; } // 60日波动率
        public double Volatility120Day { get; set; } // 120日波动率
        public double VolatilityPercentile { get; set; } // 当前波动率在历史中的分位数

        // === 新增：量价背离标记 ===
        public bool HasBearishDivergence { get; set; } // 顶背离
        public bool HasBullishDivergence { get; set; } // 底背离
        public string DivergenceDetail { get; set; } = string.Empty; // 背离详情

        // === 新增 P0：北向资金数据 ===
        public double NorthBoundNetInflow { get; set; }        // 北向资金当日净流入(万元)
        public double NorthBoundPositionChange { get; set; }   // 北向持股比例变化(百分点)
        public double NorthBoundTotalPosition { get; set; }    // 北向持股占总股本比例

        // === 新增 P0：融资融券数据 ===
        public double MarginBalance { get; set; }              // 融资余额(万元)
        public double ShortBalance { get; set; }               // 融券余额(万元)
        public double MarginBuyRatio { get; set; }             // 融资买入占比(%)
        public double MarginBalanceChange { get; set; }        // 融资余额日变化(万元)

        // === 新增 P1：股东人数变化 ===
        public double ShareholderCountChange { get; set; }     // 股东人数变化率(%)(负值=筹码集中)
        public int ShareholderCountLatest { get; set; }        // 最新股东人数
        public int ShareholderCountPrev { get; set; }          // 上期股东人数
        public string ShareholderUpdateDate { get; set; } = string.Empty;

        // === 新增 P1：限售股解禁数据 ===
        public string NearestUnlockDate { get; set; } = string.Empty;  // 最近解禁日期
        public double UnlockRatio { get; set; }                        // 解禁占流通股本比例(%)
        public double UnlockAmount { get; set; }                       // 解禁股数(万股)
        public int DaysToUnlock { get; set; } = 999;                   // 距解禁天数

        // === 数据可信度与事件风险 ===
        public DateTime DataFetchedAt { get; set; } = DateTime.Now;
        public List<DataPointMeta> DataPoints { get; set; } = new();
        public List<StockEventInfo> ImportantEvents { get; set; } = new();

        public double DataReliabilityScore
        {
            get
            {
                if (DataPoints.Count == 0) return 0;
                string[] coreFields =
                {
                    "CurrentPrice", "RecentPrices", "RecentVolumes", "MA5", "MA20", "MA60",
                    "TechScore", "Prices60Min", "Prices15Min", "MainForceNetInflow",
                    "SuperLargeOrderInflow", "LargeOrderInflow", "TurnoverAmount"
                };
                var corePoints = DataPoints
                    .Where(p => coreFields.Contains(p.FieldName))
                    .ToList();
                var points = corePoints.Count > 0 ? corePoints : DataPoints;
                return points.Average(p => p.IsMissing ? 0 : Math.Max(0, Math.Min(1, p.Confidence))) * 100.0;
            }
        }
    }

    public class MarketOverviewData
    {
        public string Date { get; set; } = string.Empty;
        public int UpCount { get; set; }
        public int DownCount { get; set; }
        public int FlatCount { get; set; }
        public int LimitUpCount { get; set; }
        public int LimitDownCount { get; set; }
        public double TotalAmount { get; set; } // 亿元
        public List<SectorRanking> TopSectors { get; set; } = new();
        public List<SectorRanking> BottomSectors { get; set; } = new();
        public List<SectorRanking> AllSectors { get; set; } = new(); // 完整板块列表
        public List<string> MarketNews { get; set; } = new();
    }

    public class SectorRanking
    {
        public string Name { get; set; } = string.Empty;
        public double ChangePct { get; set; }
    }

    public static class StockDataProvider
    {
        private const string EastMoneyUt = "7eea3edcaed734bea9cbbc2440b282fb";
        private static readonly HttpClient _httpClient = CreateHttpClient();
        private static readonly string[] _userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36"
        };
        private static readonly Random _rand = new Random();

        /// <summary>
        /// 创建配置了SSL的HttpClient
        /// </summary>
        private static HttpClient CreateHttpClient()
        {
            // 创建支持SSL的HttpClientHandler
            var handler = new HttpClientHandler();

            try
            {
                handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;

                // 忽略SSL证书验证错误（仅用于开发调试）
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // 开发环境可以放宽验证，生产环境应该严格验证
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                        return true;

                    // 对于自签名证书等开发环境，可以选择返回true
                    // 但要注意这会降低安全性
                    return true;
                };

                // 设置支持的重定向
                handler.AllowAutoRedirect = true;
                handler.MaxAutomaticRedirections = 10;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HttpClient SSL配置警告: {ex.Message}");
            }

            var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30); // 增加超时时间

            // 设置默认请求头
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");

            return client;
        }

        private static void RotateUserAgent()
        {
            var ua = _userAgents[_rand.Next(_userAgents.Length)];
            _httpClient.DefaultRequestHeaders.Remove("User-Agent");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", ua);
        }

        public static async Task<StockDeepAnalysisContext> FetchDeepDataAsync(string code, string tavilyApiKey = "")
        {
            RotateUserAgent(); // 反爬混淆
            var context = new StockDeepAnalysisContext { Code = code, DataFetchedAt = DateTime.Now };
            
            // 并发获取全维度数据
            var tencentTask = FetchTencentRealtimeAsync(code, context);
            var klineTask = FetchEastMoneyKlinesAsync(code, context);
            var newsTask = FetchTavilyNewsAsync(code, context, tavilyApiKey);
            var eventsTask = FetchImportantEventsAsync(code, context);
            var flowTask = FetchMainForceFlowAsync(code, context);
            var reportTask = FetchFinancialReportAsync(code, context);
            var northTask = FetchNorthBoundFlowAsync(code, context);
            var marginTask = FetchMarginDataAsync(code, context);
            var shareholderTask = FetchShareholderDataAsync(code, context);
            var unlockTask = FetchUnlockDataAsync(code, context);

            await Task.WhenAll(tencentTask, klineTask, newsTask, eventsTask, flowTask, reportTask, northTask, marginTask, shareholderTask, unlockTask);
            return context;
        }

        private static void MarkData(StockDeepAnalysisContext context, string fieldName, string source, bool hasValue, double confidence = 1.0, string tradeDate = "", string note = "")
        {
            lock (context.DataPoints)
            {
                context.DataPoints.RemoveAll(p => p.FieldName == fieldName && p.Source == source);
                context.DataPoints.Add(new DataPointMeta
                {
                    FieldName = fieldName,
                    Source = source,
                    TradeDate = tradeDate,
                    FetchedAt = DateTime.Now,
                    IsMissing = !hasValue,
                    Confidence = hasValue ? confidence : 0,
                    Note = note
                });
            }
        }

        private static void MarkMany(StockDeepAnalysisContext context, string source, params (string Field, bool HasValue)[] fields)
        {
            foreach (var field in fields)
            {
                MarkData(context, field.Field, source, field.HasValue);
            }
        }

        private static string WithEastMoneyUt(string url)
        {
            if (url.Contains("ut=", StringComparison.OrdinalIgnoreCase)) return url;
            return url + (url.Contains('?') ? "&" : "?") + $"ut={EastMoneyUt}";
        }

        private static async Task<JObject> FetchEastMoneyJsonAsync(string url, int timeoutSeconds = 8)
        {
            string requestUrl = WithEastMoneyUt(url.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase));
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Referrer = new Uri("https://quote.eastmoney.com/");
            request.Headers.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");
            request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");

            using var response = await _httpClient.SendAsync(request, cts.Token);
            string raw = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"EastMoney HTTP {(int)response.StatusCode}: {TrimForLog(raw)}");
            }

            string trimmed = raw.Trim();
            if (trimmed.Contains("风险警示") ||
                trimmed.Contains("访问过于频繁") ||
                trimmed.StartsWith("<", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"EastMoney returned non-json/risk-warning response: {TrimForLog(trimmed)}");
            }

            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace > 0 && lastBrace > firstBrace)
            {
                trimmed = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            var obj = JObject.Parse(trimmed);
            int rc = int.TryParse(obj["rc"]?.ToString(), out var parsedRc) ? parsedRc : 0;
            if (rc != 0)
            {
                throw new InvalidOperationException($"EastMoney rc={rc}: {TrimForLog(trimmed)}");
            }
            return obj;
        }

        private static string TrimForLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= 180 ? text : text.Substring(0, 180);
        }

        private static string GetPrefix(string code)
        {
            if (code.StartsWith("6") || code.StartsWith("9") || code.StartsWith("5") || code.StartsWith("7")) return "sh";
            if (code.StartsWith("0") || code.StartsWith("3") || code.StartsWith("1") || code.StartsWith("2")) return "sz";
            if (code.StartsWith("8") || code.StartsWith("4")) return "bj";
            return "sh"; // fallback
        }

        private static string GetEastMoneyMarketPrefix(string code)
        {
            if (code.StartsWith("6") || code.StartsWith("9") || code.StartsWith("5") || code.StartsWith("7")) return "1"; // SH
            if (code.StartsWith("0") || code.StartsWith("3") || code.StartsWith("1") || code.StartsWith("2")) return "0"; // SZ
            if (code.StartsWith("8") || code.StartsWith("4")) return "0"; // BJ
            return "0"; // fallback
        }

        // 1. 获取腾讯高级实时行情（量比、换手率等）
        private static async Task FetchTencentRealtimeAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string prefix = GetPrefix(code);
                string url = $"http://qt.gtimg.cn/q={prefix}{code}";
                var response = await _httpClient.GetStringAsync(url);
                var parts = response.Split('~');
                
                if (parts.Length > 70)
                {
                    context.Name = parts[1];
                    context.CurrentPrice = double.TryParse(parts[3], out var p) ? p : 0;
                    context.Open = double.TryParse(parts[5], out var o) ? o : 0;
                    context.PctChange = double.TryParse(parts[32], out var pct) ? pct : 0;
                    context.High = double.TryParse(parts[33], out var h) ? h : 0;
                    context.Low = double.TryParse(parts[34], out var l) ? l : 0;
                    context.TurnoverRate = double.TryParse(parts[38], out var tr) ? tr : 0; // 换手率
                    context.PE = double.TryParse(parts[39], out var pe) ? pe : 0; // 市盈率
                    context.PB = double.TryParse(parts[46], out var pb) ? pb : 0; // 市净率
                    context.VolumeRatio = double.TryParse(parts[73], out var vr) ? vr : 0; // 量比
                    context.TotalMarketValue = double.TryParse(parts[45], out var tmv) ? tmv : 0; // 总市值/亿
                    MarkMany(context, "TencentRealtime",
                        ("Name", !string.IsNullOrWhiteSpace(context.Name)),
                        ("CurrentPrice", context.CurrentPrice > 0),
                        ("PctChange", context.CurrentPrice > 0),
                        ("TurnoverRate", context.TurnoverRate > 0),
                        ("PE", context.PE != 0),
                        ("PB", context.PB > 0),
                        ("VolumeRatio", context.VolumeRatio > 0),
                        ("TotalMarketValue", context.TotalMarketValue > 0));
                }
                else
                {
                    MarkData(context, "TencentRealtime", "TencentRealtime", false, note: "Tencent realtime response has insufficient fields");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tencent data fetch failed: {ex.Message}");
                MarkData(context, "TencentRealtime", "TencentRealtime", false, note: ex.Message);
            }
        }

        // 2. 获取东财历史 K 线以精确推演均线
        private static async Task FetchEastMoneyKlinesAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                // 近 250 个交易日的价量数据以推演均线、筹码分布、波动率锥
                string url = $"https://push2his.eastmoney.com/api/qt/stock/kline/get?secid={market}.{code}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=1&end=20500101&lmt=250";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 10);

                var klines = jsonObj["data"]?["klines"] as JArray;
                if (klines != null && klines.Count > 0)
                {
                    var opens = new List<double>();
                    var closes = new List<double>();
                    var highs = new List<double>();
                    var lows = new List<double>();
                    var volumes = new List<double>();
                    foreach (var k in klines)
                    {
                        var parts = k.ToString().Split(','); // "Date,Open,Close,High,Low,Volume"
                        if (parts.Length >= 6 &&
                            double.TryParse(parts[1], out var o) &&
                            double.TryParse(parts[2], out var c) &&
                            double.TryParse(parts[3], out var h) &&
                            double.TryParse(parts[4], out var l) &&
                            double.TryParse(parts[5], out var v))
                        {
                            opens.Add(o);
                            closes.Add(c);
                            highs.Add(h);
                            lows.Add(l);
                            volumes.Add(v);
                        }
                    }

                    // 取最后一个价 (今日实时)
                    double currentPrice = closes.Last();
                    if (context.CurrentPrice == 0) context.CurrentPrice = currentPrice;

                    // 计算 MA5/MA10/MA20/MA60/MA120
                    if (closes.Count >= 5)
                        context.MA5 = closes.Skip(closes.Count - 5).Average();
                    if (closes.Count >= 10)
                        context.MA10 = closes.Skip(closes.Count - 10).Average();
                    else
                        context.MA10 = context.MA5;
                    if (closes.Count >= 20)
                        context.MA20 = closes.Skip(closes.Count - 20).Average();
                    else
                        context.MA20 = context.MA10;
                    if (closes.Count >= 60)
                        context.MA60 = closes.Skip(closes.Count - 60).Average();
                    else if (closes.Count >= 20)
                        context.MA60 = context.MA20;
                    if (closes.Count >= 120)
                        context.MA120 = closes.Skip(closes.Count - 120).Average();
                    else if (closes.Count >= 60)
                        context.MA120 = context.MA60;

                    context.BiasMA5 = context.MA5 > 0 ? (currentPrice - context.MA5) / context.MA5 * 100 : 0;
                    context.BiasMA10 = context.MA10 > 0 ? (currentPrice - context.MA10) / context.MA10 * 100 : 0;
                    context.BiasMA20 = context.MA20 > 0 ? (currentPrice - context.MA20) / context.MA20 * 100 : 0;
                    context.BiasMA60 = context.MA60 > 0 ? (currentPrice - context.MA60) / context.MA60 * 100 : 0;

                    // 计算高级技术指标 (MACD, RSI, 布林带, 支撑阻力, 背离等)
                    if (closes.Count > 0)
                    {
                        context.TechScore = AdvancedTechnicalIndicators.ComprehensiveTechnicalAnalysis(closes, volumes, highs, lows);
                        context.RecentPrices = closes;
                        context.RecentVolumes = volumes;
                        MarkMany(context, "EastMoneyDailyKline",
                            ("RecentPrices", context.RecentPrices.Count > 0),
                            ("RecentVolumes", context.RecentVolumes.Count > 0),
                            ("MA5", context.MA5 > 0),
                            ("MA20", context.MA20 > 0),
                            ("MA60", context.MA60 > 0),
                            ("MA120", context.MA120 > 0),
                            ("TechScore", context.TechScore.IsComputed));
                    }

                    // 均线排列判断（含 MA60/MA120）
                    if (context.MA5 > context.MA10 && context.MA10 > context.MA20 && context.MA20 > context.MA60)
                        context.MAAlignment = "强势多头排列";
                    else if (context.MA5 > context.MA10 && context.MA10 > context.MA20)
                        context.MAAlignment = "多头排列";
                    else if (context.MA5 < context.MA10 && context.MA10 < context.MA20 && context.MA20 < context.MA60)
                        context.MAAlignment = "强势空头排列";
                    else if (context.MA5 < context.MA10 && context.MA10 < context.MA20)
                        context.MAAlignment = "空头排列";
                    else
                        context.MAAlignment = "震荡交织";

                    // ======= 昨日量价异动追踪 =======
                    if (closes.Count >= 2 && volumes.Count >= 2)
                    {
                        double todayVol = volumes.Last();
                        double yesterdayVol = volumes[volumes.Count - 2];
                        if (yesterdayVol > 0)
                            context.VolumeChangeRatio = todayVol / yesterdayVol;

                        double todayPrice = closes.Last();
                        double yesterdayPrice = closes[closes.Count - 2];
                        if (yesterdayPrice > 0)
                            context.PriceChangeRatio = (todayPrice - yesterdayPrice) / yesterdayPrice * 100.0;
                    }

                    // ======= C# 仿真筹码分布 (CYQ) 计算 (增强版) =======
                    if (closes.Count > 0 && volumes.Count > 0 && closes.Count == volumes.Count)
                    {
                        double totalVol = volumes.Sum();
                        if (totalVol > 0)
                        {
                            double profitVol = 0;
                            double costProduct = 0;
                            var distribution = new List<(double Price, double Vol)>();

                            for (int i = 0; i < closes.Count; i++)
                            {
                                if (closes[i] <= currentPrice) profitVol += volumes[i];
                                costProduct += closes[i] * volumes[i];
                                distribution.Add((closes[i], volumes[i]));
                            }

                            context.ProfitRatio = profitVol / totalVol * 100.0;
                            context.ChipAvgCost = costProduct / totalVol;

                            // 90% 集中度
                            distribution = distribution.OrderBy(d => d.Price).ToList();
                            double sumV = 0;
                            double p5 = 0, p95 = 0;
                            bool foundP5 = false, foundP95 = false;
                            foreach (var d in distribution)
                            {
                                sumV += d.Vol;
                                double ratio = sumV / totalVol;
                                if (!foundP5 && ratio >= 0.05) { p5 = d.Price; foundP5 = true; }
                                if (!foundP95 && ratio >= 0.95) { p95 = d.Price; foundP95 = true; }
                            }

                            if (p95 + p5 > 0)
                                context.ChipConcentration90 = (p95 - p5) / (p95 + p5) * 100.0;

                            // === 筹码峰压力位/支撑位分析 ===
                            // 找到当前价上方最近的筹码密集峰（压力位）
                            var aboveDistribution = distribution.Where(d => d.Price > currentPrice).ToList();
                            if (aboveDistribution.Count > 0)
                            {
                                var maxVolAbove = aboveDistribution.OrderByDescending(d => d.Vol).First();
                                context.ChipPeakPressure = maxVolAbove.Price;
                            }
                            else
                            {
                                context.ChipPeakPressure = currentPrice * 1.1; // 默认上方10%
                            }

                            // 找到当前价下方最近的筹码密集峰（支撑位）
                            var belowDistribution = distribution.Where(d => d.Price < currentPrice).ToList();
                            if (belowDistribution.Count > 0)
                            {
                                var maxVolBelow = belowDistribution.OrderByDescending(d => d.Vol).First();
                                context.ChipPeakSupport = maxVolBelow.Price;
                            }
                            else
                            {
                                context.ChipPeakSupport = currentPrice * 0.9; // 默认下方10%
                            }
                        }
                    }

                    // === 波动率锥计算 ===
                    if (closes.Count >= 20)
                    {
                        context.Volatility20Day = AdvancedTechnicalIndicators.CalculateVolatility(
                            closes.TakeLast(20).ToList());
                    }
                    if (closes.Count >= 60)
                    {
                        context.Volatility60Day = AdvancedTechnicalIndicators.CalculateVolatility(
                            closes.TakeLast(60).ToList());
                    }
                    if (closes.Count >= 120)
                    {
                        context.Volatility120Day = AdvancedTechnicalIndicators.CalculateVolatility(
                            closes.TakeLast(120).ToList());
                    }
                    // 波动率分位数（当前20日波动率在全部滚动20日波动率中的分位）
                    if (closes.Count >= 40)
                    {
                        var rollingVols = new List<double>();
                        for (int i = 20; i < closes.Count; i++)
                        {
                            var slice = closes.Skip(i - 20).Take(20).ToList();
                            rollingVols.Add(AdvancedTechnicalIndicators.CalculateVolatility(slice));
                        }
                        if (rollingVols.Count > 0 && context.Volatility20Day > 0)
                        {
                            rollingVols.Sort();
                            int rank = rollingVols.BinarySearch(context.Volatility20Day);
                            if (rank < 0) rank = ~rank;
                            context.VolatilityPercentile = (double)rank / rollingVols.Count * 100;
                        }
                    }

                    // === 多时间框架数据获取 (60分钟和15分钟) ===
                    // 各周期独立 try-catch，避免一个失败导致全部丢失

                    // --- 60分钟K线 ---
                    try
                    {
                        string url60min = $"https://push2his.eastmoney.com/api/qt/stock/kline/get?secid={market}.{code}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=60&fqt=1&end=20500101&lmt=100";
                        var obj60 = await FetchEastMoneyJsonAsync(url60min, 8);
                        var klines60 = obj60["data"]?["klines"] as JArray;
                        if (klines60 != null && klines60.Count > 0)
                        {
                            var closes60 = new List<double>();
                            var highs60 = new List<double>();
                            var lows60 = new List<double>();
                            var vols60 = new List<double>();
                            foreach (var k in klines60)
                            {
                                var parts = k.ToString().Split(',');
                                if (parts.Length >= 6 &&
                                    double.TryParse(parts[2], out var c60) &&
                                    double.TryParse(parts[3], out var h60) &&
                                    double.TryParse(parts[4], out var l60) &&
                                    double.TryParse(parts[5], out var v60))
                                {
                                    closes60.Add(c60);
                                    highs60.Add(h60);
                                    lows60.Add(l60);
                                    vols60.Add(v60);
                                }
                            }
                            context.Prices60Min = closes60;
                            context.Volumes60Min = vols60;
                            if (closes60.Count >= 10) // 降低门槛：10根即可部分计算
                            {
                                context.TechScore60Min = AdvancedTechnicalIndicators.ComprehensiveTechnicalAnalysis(
                                    closes60, vols60, highs60, lows60);
                            }
                            MarkMany(context, "EastMoney60MinKline",
                                ("Prices60Min", context.Prices60Min.Count > 0),
                                ("Volumes60Min", context.Volumes60Min.Count > 0),
                                ("TechScore60Min", context.TechScore60Min.IsComputed));
                        }
                        else
                        {
                            MarkData(context, "Prices60Min", "EastMoney60MinKline", false, note: "No 60min kline returned");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"60min kline fetch failed for {code}: {ex.Message}");
                        MarkData(context, "Prices60Min", "EastMoney60MinKline", false, note: ex.Message);
                        // TechScore60Min.IsComputed 保持 false，多周期分析会正确标注"60分钟数据缺失"
                    }

                    // --- 15分钟K线 ---
                    try
                    {
                        string url15min = $"https://push2his.eastmoney.com/api/qt/stock/kline/get?secid={market}.{code}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=15&fqt=1&end=20500101&lmt=100";
                        var obj15 = await FetchEastMoneyJsonAsync(url15min, 8);
                        var klines15 = obj15["data"]?["klines"] as JArray;
                        if (klines15 != null && klines15.Count > 0)
                        {
                            var closes15 = new List<double>();
                            var highs15 = new List<double>();
                            var lows15 = new List<double>();
                            var vols15 = new List<double>();
                            foreach (var k in klines15)
                            {
                                var parts = k.ToString().Split(',');
                                if (parts.Length >= 6 &&
                                    double.TryParse(parts[2], out var c15) &&
                                    double.TryParse(parts[3], out var h15) &&
                                    double.TryParse(parts[4], out var l15) &&
                                    double.TryParse(parts[5], out var v15))
                                {
                                    closes15.Add(c15);
                                    highs15.Add(h15);
                                    lows15.Add(l15);
                                    vols15.Add(v15);
                                }
                            }
                            context.Prices15Min = closes15;
                            context.Volumes15Min = vols15;
                            if (closes15.Count >= 10) // 降低门槛：10根即可部分计算
                            {
                                context.TechScore15Min = AdvancedTechnicalIndicators.ComprehensiveTechnicalAnalysis(
                                    closes15, vols15, highs15, lows15);
                            }
                            MarkMany(context, "EastMoney15MinKline",
                                ("Prices15Min", context.Prices15Min.Count > 0),
                                ("Volumes15Min", context.Volumes15Min.Count > 0),
                                ("TechScore15Min", context.TechScore15Min.IsComputed));
                        }
                        else
                        {
                            MarkData(context, "Prices15Min", "EastMoney15MinKline", false, note: "No 15min kline returned");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"15min kline fetch failed for {code}: {ex.Message}");
                        MarkData(context, "Prices15Min", "EastMoney15MinKline", false, note: ex.Message);
                        // TechScore15Min.IsComputed 保持 false，多周期分析会正确标注"15分钟数据缺失"
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EastMoney kline/chip fetch failed: {ex.Message}");
                MarkData(context, "EastMoneyDailyKline", "EastMoneyDailyKline", false, note: ex.Message);
            }
        }

        // 3. 获取新闻舆情 (优先 Tavily 高级检索，降级新浪免签引擎，含情绪评分)
        private static async Task FetchTavilyNewsAsync(string code, StockDeepAnalysisContext context, string tavilyApiKey)
        {
            if (string.IsNullOrWhiteSpace(tavilyApiKey))
            {
                await FetchSinaNewsAsync(code, context);
                return;
            }

            try
            {
                string query = $"{(string.IsNullOrEmpty(context.Name) ? code : context.Name)} 最新重大新闻 利好 利空";
                var payload = new
                {
                    api_key = tavilyApiKey,
                    query = query,
                    topic = "news",
                    days = 3,
                    search_depth = "basic",
                    max_results = 5
                };

                string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.PostAsync("https://api.tavily.com/search", content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Tavily response error: {response.StatusCode}. Falling back to Sina.");
                    await FetchSinaNewsAsync(code, context);
                    return;
                }

                var jsonStr = await response.Content.ReadAsStringAsync();
                var jsonObj = JObject.Parse(jsonStr);

                var newsList = new List<string>();
                var allNewsText = new List<string>();
                var results = jsonObj["results"] as JArray;
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        var title = result["title"]?.ToString();
                        var desc = result["content"]?.ToString();
                        if (!string.IsNullOrEmpty(title))
                        {
                            string snippet = desc != null && desc.Length > 80 ? desc.Substring(0, 80) + "..." : (desc ?? "");
                            newsList.Add($"- {title}: {snippet}");
                            allNewsText.Add(title + " " + (desc ?? ""));
                        }
                    }
                }

                if (newsList.Count == 0)
                {
                    await FetchSinaNewsAsync(code, context);
                }
                else
                {
                    context.LatestNews = newsList;
                    // 新闻情绪量化评分
                    AnalyzeNewsSentiment(context, allNewsText);
                    MarkData(context, "LatestNews", "Tavily", true, 0.9);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tavily fetch failed: {ex.Message}. Falling back to Sina.");
                await FetchSinaNewsAsync(code, context);
            }
        }

        // --- 降级备用: 获取新浪财经新闻舆情 ---
        private static async Task FetchSinaNewsAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string prefix = GetPrefix(code);
                // 抓取新浪个股实时新闻
                string url = $"https://vip.stock.finance.sina.com.cn/corp/view/vCB_AllNewsStock.php?symbol={prefix}{code}";
                var html = await _httpClient.GetStringAsync(url);

                var newsList = new List<string>();
                var allNewsText = new List<string>();

                // Regex 提取 <div class="datelist"> 里的新闻链接
                var match = Regex.Match(html, @"<div\s+class=""datelist"">(.*?)</div>", RegexOptions.Singleline);
                if (match.Success)
                {
                    string content = match.Groups[1].Value;
                    var linkMatches = Regex.Matches(content, @"<a\s+href=""[^""]+""[^>]*>(.*?)</a>");
                    int maxNews = 5;
                    foreach (Match m in linkMatches)
                    {
                        var title = m.Groups[1].Value.Trim();
                        if (title.Length > 5 && !title.Contains("关于") && !title.Contains("公告"))
                        {
                            newsList.Add($"- {title}");
                            allNewsText.Add(title);
                        }
                        if (newsList.Count >= maxNews) break;
                    }
                }

                context.LatestNews = newsList;
                // 新闻情绪量化评分
                AnalyzeNewsSentiment(context, allNewsText);
                MarkData(context, "LatestNews", "SinaNews", newsList.Count > 0, newsList.Count > 0 ? 0.65 : 0.0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sina news fetch failed: {ex.Message}");
                MarkData(context, "LatestNews", "SinaNews", false, note: ex.Message);
            }
        }

        private static async Task FetchImportantEventsAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                string secid = $"{market}.{code}";
                string url = $"https://np-anotice-stock.eastmoney.com/api/security/ann?sr=-1&page_size=20&page_index=1&ann_type=A&client_source=web&stock_list={secid}";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                var items = jsonObj["data"]?["list"] as JArray;
                var events = new List<StockEventInfo>();

                if (items != null)
                {
                    foreach (var item in items.Take(20))
                    {
                        string title = item["title"]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        string date = item["notice_date"]?.ToString() ?? item["eiTime"]?.ToString() ?? "";
                        string artCode = item["art_code"]?.ToString() ?? item["artCode"]?.ToString() ?? "";
                        string urlLink = string.IsNullOrEmpty(artCode)
                            ? ""
                            : $"https://data.eastmoney.com/notices/detail/{code}/{artCode}.html";

                        int risk = EstimateEventRisk(title);
                        if (risk >= 30 || IsMaterialEvent(title))
                        {
                            events.Add(new StockEventInfo
                            {
                                EventDate = date.Length >= 10 ? date.Substring(0, 10) : date,
                                Title = title,
                                Source = "EastMoneyNotice",
                                Url = urlLink,
                                RiskLevel = risk
                            });
                        }
                    }
                }

                context.ImportantEvents = events
                    .OrderByDescending(e => e.RiskLevel)
                    .ThenByDescending(e => e.EventDate)
                    .Take(8)
                    .ToList();
                MarkData(context, "ImportantEvents", "EastMoneyNotice", true, context.ImportantEvents.Count > 0 ? 0.85 : 0.65,
                    note: context.ImportantEvents.Count == 0 ? "No recent material notices found" : "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Important events fetch failed for {code}: {ex.Message}");
                MarkData(context, "ImportantEvents", "EastMoneyNotice", false, note: ex.Message);
            }
        }

        private static int EstimateEventRisk(string title)
        {
            int risk = 0;
            var highRisk = new[] { "减持", "问询", "监管", "处罚", "调查", "诉讼", "仲裁", "冻结", "质押", "担保", "退市", "ST", "亏损", "下滑", "终止", "解除", "违约", "立案" };
            var mediumRisk = new[] { "解禁", "定增", "重组", "停牌", "商誉", "计提", "业绩预告", "业绩快报", "可转债", "限售股" };
            var positive = new[] { "回购", "增持", "中标", "签订", "预增", "扭亏", "分红", "派息", "高送转" };

            foreach (var word in highRisk)
                if (title.Contains(word)) risk += 35;
            foreach (var word in mediumRisk)
                if (title.Contains(word)) risk += 20;
            foreach (var word in positive)
                if (title.Contains(word)) risk -= 10;

            return Math.Max(0, Math.Min(100, risk));
        }

        private static bool IsMaterialEvent(string title)
        {
            string[] materialWords =
            {
                "减持", "增持", "回购", "分红", "派息", "业绩预告", "业绩快报", "定增", "重组",
                "解禁", "限售股", "问询", "监管", "处罚", "调查", "诉讼", "仲裁", "质押",
                "担保", "中标", "签订", "停牌", "复牌", "退市", "ST"
            };
            return materialWords.Any(title.Contains);
        }

        /// <summary>
        /// 新闻情绪量化评分（基于关键词加权）
        /// </summary>
        private static void AnalyzeNewsSentiment(StockDeepAnalysisContext context, List<string> newsTexts)
        {
            if (newsTexts == null || newsTexts.Count == 0)
            {
                context.NewsSentimentScore = 50;
                context.NewsImpactScore = 30;
                return;
            }

            // 正面关键词及权重
            var positiveWords = new Dictionary<string, double>
            {
                { "涨停", 15 }, { "利好", 12 }, { "突破", 10 }, { "大增", 10 }, { "增长", 8 },
                { "上涨", 7 }, { "预增", 12 }, { "签订", 8 }, { "中标", 10 }, { "扭亏", 15 },
                { "分拆上市", 12 }, { "高送转", 10 }, { "回购", 8 }, { "增持", 10 }, { "业绩", 5 },
                { "创新高", 12 }, { "涨停板", 15 }, { "机构买入", 12 }, { "北向资金加仓", 12 },
                { "底部反弹", 8 }, { "筑底", 6 }, { "反转", 8 }, { "放量", 6 }, { "加速", 5 }
            };

            // 负面关键词及权重
            var negativeWords = new Dictionary<string, double>
            {
                { "跌停", 15 }, { "利空", 12 }, { "下跌", 7 }, { "减持", 12 }, { "亏损", 12 },
                { "风险", 8 }, { "退市", 20 }, { "调查", 15 }, { "处罚", 12 }, { "爆雷", 18 },
                { "诉讼", 10 }, { "业绩下滑", 10 }, { "终止", 8 }, { "暂停上市", 18 }, { "ST", 15 },
                { "连续跌停", 18 }, { "大幅回调", 6 }, { "破位", 10 }, { "机构出逃", 14 },
                { "北向资金减仓", 12 }, { "高位", 4 }, { "泡沫", 8 }
            };

            double sentimentScore = 50; // 中性基准
            double totalWeight = 0;
            int keywordCount = 0;

            foreach (var text in newsTexts)
            {
                foreach (var kvp in positiveWords)
                {
                    if (text.Contains(kvp.Key))
                    {
                        sentimentScore += kvp.Value;
                        totalWeight += kvp.Value;
                        keywordCount++;
                    }
                }
                foreach (var kvp in negativeWords)
                {
                    if (text.Contains(kvp.Key))
                    {
                        sentimentScore -= kvp.Value;
                        totalWeight += kvp.Value;
                        keywordCount++;
                    }
                }
            }

            context.NewsSentimentScore = Math.Max(0, Math.Min(100, sentimentScore));
            // 影响力评分：基于关键词命中数量和权重
            context.NewsImpactScore = Math.Max(0, Math.Min(100, keywordCount * 8 + totalWeight));
        }

        // 4. 获取主力资金流向（含超大/大/中/小单分级）
        private static async Task FetchMainForceFlowAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                string secid = $"{market}.{code}";
                string url = $"https://push2.eastmoney.com/api/qt/ulist.np/get?fltt=2&secids={secid}&fields=f6,f62,f184,f66,f69,f72,f75,f78,f81,f84,f87";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                var data = jsonObj["data"]?["diff"]?.FirstOrDefault();
                if (data != null && data.Type != JTokenType.Null)
                {
                    if (double.TryParse(data["f62"]?.ToString(), out double inflow))
                        context.MainForceNetInflow = inflow;
                    if (double.TryParse(data["f66"]?.ToString(), out double superLarge))
                        context.SuperLargeOrderInflow = superLarge;
                    if (double.TryParse(data["f72"]?.ToString(), out double large))
                        context.LargeOrderInflow = large;
                    if (double.TryParse(data["f78"]?.ToString(), out double medium))
                        context.MediumOrderInflow = medium;
                    if (double.TryParse(data["f84"]?.ToString(), out double small))
                        context.SmallOrderInflow = small;
                    if (double.TryParse(data["f6"]?.ToString(), out double amount))
                        context.TurnoverAmount = amount;

                    // 计算主力净流入占成交额比例
                    if (double.TryParse(data["f184"]?.ToString(), out double ratio))
                        context.MainForceInflowRatio = ratio;
                    else if (context.TurnoverAmount > 0)
                        context.MainForceInflowRatio = context.MainForceNetInflow / context.TurnoverAmount * 100;

                    MarkMany(context, "EastMoneyFundFlow",
                        ("MainForceNetInflow", context.MainForceNetInflow != 0),
                        ("SuperLargeOrderInflow", context.SuperLargeOrderInflow != 0),
                        ("LargeOrderInflow", context.LargeOrderInflow != 0),
                        ("TurnoverAmount", context.TurnoverAmount > 0),
                        ("MainForceInflowRatio", context.MainForceInflowRatio != 0));
                }
                else
                {
                    await FetchMainForceFlowFallbackAsync(code, context);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainForceFlow fetch failed: {ex.Message}");
                try
                {
                    await FetchMainForceFlowFallbackAsync(code, context);
                }
                catch (Exception fallbackEx)
                {
                    MarkData(context, "MainForceNetInflow", "EastMoneyFundFlow", false, note: $"{ex.Message}; fallback: {fallbackEx.Message}");
                }
            }
        }

        private static async Task FetchMainForceFlowFallbackAsync(string code, StockDeepAnalysisContext context)
        {
            string market = GetEastMoneyMarketPrefix(code);
            string url = $"https://push2.eastmoney.com/api/qt/stock/fflow/daykline/get?lmt=1&klt=101&fields1=f1,f2,f3,f7&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61,f62,f63&secid={market}.{code}";
            var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
            var line = jsonObj["data"]?["klines"]?.FirstOrDefault()?.ToString();
            if (string.IsNullOrWhiteSpace(line))
            {
                MarkData(context, "MainForceNetInflow", "EastMoneyFundFlowFallback", false, note: "No fflow daykline returned");
                return;
            }

            var parts = line.Split(',');
            if (parts.Length >= 6)
            {
                context.MainForceNetInflow = double.TryParse(parts[1], out var main) ? main : 0;
                context.SmallOrderInflow = double.TryParse(parts[2], out var small) ? small : 0;
                context.MediumOrderInflow = double.TryParse(parts[3], out var medium) ? medium : 0;
                context.LargeOrderInflow = double.TryParse(parts[4], out var large) ? large : 0;
                context.SuperLargeOrderInflow = double.TryParse(parts[5], out var superLarge) ? superLarge : 0;
                if (parts.Length > 6 && double.TryParse(parts[6], out var ratio))
                    context.MainForceInflowRatio = ratio;

                MarkMany(context, "EastMoneyFundFlowFallback",
                    ("MainForceNetInflow", context.MainForceNetInflow != 0),
                    ("SuperLargeOrderInflow", context.SuperLargeOrderInflow != 0),
                    ("LargeOrderInflow", context.LargeOrderInflow != 0),
                    ("MediumOrderInflow", context.MediumOrderInflow != 0),
                    ("SmallOrderInflow", context.SmallOrderInflow != 0),
                    ("MainForceInflowRatio", context.MainForceInflowRatio != 0));
            }
        }

        // 4.5 获取深度核心财报 (ROE/净利润)
        private static async Task FetchFinancialReportAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetPrefix(code).ToUpper(); // SH000001
                string url = $"https://emweb.securities.eastmoney.com/PC_HSF10/FinanceAnalysis/ZYZBAjaxNew?type=0&code={market}{code}";
                
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                
                var dataList = jsonObj["data"] as JArray;
                if (dataList != null && dataList.Count > 0)
                {
                    var latest = dataList[0];
                    if (double.TryParse(latest["ROEJQ"]?.ToString(), out var roe))
                        context.ROE = roe;
                        
                    if (double.TryParse(latest["TOTALOPERATEREVE"]?.ToString(), out var rev))
                        context.OperatingRevenue = rev / 100000000.0; // 转换为亿
                        
                    if (double.TryParse(latest["PARENTNETPROFIT"]?.ToString(), out var np))
                        context.NetProfit = np / 100000000.0; // 转换为亿
                        
                    if (double.TryParse(latest["MGJYXJJE"]?.ToString(), out var cfps))
                        context.OperatingCashFlowPerShare = cfps;

                    MarkMany(context, "EastMoneyFinance",
                        ("ROE", context.ROE > 0),
                        ("OperatingRevenue", context.OperatingRevenue > 0),
                        ("NetProfit", context.NetProfit != 0),
                        ("OperatingCashFlowPerShare", context.OperatingCashFlowPerShare != 0));
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Financial report fetch failed: {ex.Message}, trying fallback...");
                 
                 // 备用数据源降级 (Fallback)：当核心财报接口被限流时，退化到行情长连接抓取简版核心数据
                 try
                 {
                     string market = GetEastMoneyMarketPrefix(code);
                     string fallbackUrl = $"http://push2.eastmoney.com/api/qt/stock/get?fltt=2&secid={market}.{code}&fields=f173";
                     var fbObj = await FetchEastMoneyJsonAsync(fallbackUrl, 8);
                     
                     var fbData = fbObj["data"];
                     if (fbData != null && fbData.Type != JTokenType.Null)
                     {
                         if (double.TryParse(fbData["f173"]?.ToString(), out var fallbackRoe))
                             context.ROE = fallbackRoe; 
                             
                         // 标注为降级状态，其他字段妥协留空
                         MarkData(context, "ROE", "EastMoneyFinanceFallback", context.ROE > 0, 0.55);
                     }
                 }
                 catch (Exception fbEx)
                 {
                     System.Diagnostics.Debug.WriteLine($"Financial fallback also failed: {fbEx.Message}");
                     MarkData(context, "FinancialReport", "EastMoneyFinance", false, note: fbEx.Message);
                 }
            }
        }

        /// <summary>
        /// 获取北向资金流入数据（从东方财富）
        /// </summary>
        private static async Task FetchNorthBoundFlowAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                // 东财北向持股接口: f52=北向持股量(股), f53=北向持股占流通股比(%), f55=北向当日净买入(万元)
                string url = $"http://push2.eastmoney.com/api/qt/stock/get?fltt=2&secid={market}.{code}&fields=f51,f52,f53,f54,f55,f56,f57,f58";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                var data = jsonObj["data"];
                if (data != null && data.Type != JTokenType.Null)
                {
                    // f55 = 北向当日净买入(万元) — 过滤"-"等无效值
                    var f55Str = data["f55"]?.ToString();
                    if (!string.IsNullOrEmpty(f55Str) && f55Str != "-" && double.TryParse(f55Str, out double netInflow))
                        context.NorthBoundNetInflow = netInflow;
                    // f53 = 北向持股占流通股比例
                    var f53Str = data["f53"]?.ToString();
                    if (!string.IsNullOrEmpty(f53Str) && f53Str != "-" && double.TryParse(f53Str, out double positionRatio))
                        context.NorthBoundTotalPosition = positionRatio;
                    // f57/f58 可用于计算变化量
                    var f52Str = data["f52"]?.ToString();
                    if (!string.IsNullOrEmpty(f52Str) && f52Str != "-" && double.TryParse(f52Str, out double currShares))
                    {
                        // 尝试获取上一期持股量 (f54 = 上期持股)
                        var f54Str = data["f54"]?.ToString();
                        if (!string.IsNullOrEmpty(f54Str) && f54Str != "-" && double.TryParse(f54Str, out double prevShares) && prevShares > 0)
                            context.NorthBoundPositionChange = (currShares - prevShares) / prevShares * 100;
                    }

                    MarkMany(context, "EastMoneyNorthBound",
                        ("NorthBoundNetInflow", context.NorthBoundNetInflow != 0),
                        ("NorthBoundTotalPosition", context.NorthBoundTotalPosition > 0),
                        ("NorthBoundPositionChange", context.NorthBoundPositionChange != 0));
                }
                else
                {
                    MarkData(context, "NorthBound", "EastMoneyNorthBound", false, note: "No northbound data returned");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NorthBound fetch failed for {code}: {ex.Message}");
                MarkData(context, "NorthBound", "EastMoneyNorthBound", false, note: ex.Message);
            }
        }

        /// <summary>
        /// 获取融资融券数据（从东方财富）
        /// </summary>
        private static async Task FetchMarginDataAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                // 东财融资融券接口
                string url = $"http://push2.eastmoney.com/api/qt/stock/get?fltt=2&secid={market}.{code}&fields=f164,f165,f166,f167,f168,f169,f170";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                var data = jsonObj["data"];
                if (data != null && data.Type != JTokenType.Null)
                {
                    // f164=融资余额, f165=融资买入额, f166=融资偿还额, f167=融券余额
                    // f168=融券卖出量, f169=融资余额变化, f170=融券余额变化
                    if (double.TryParse(data["f164"]?.ToString(), out double marginBal))
                        context.MarginBalance = marginBal;
                    if (double.TryParse(data["f167"]?.ToString(), out double shortBal))
                        context.ShortBalance = shortBal;
                    if (double.TryParse(data["f169"]?.ToString(), out double marginChg))
                        context.MarginBalanceChange = marginChg;
                    if (double.TryParse(data["f165"]?.ToString(), out double marginBuy) &&
                        context.TurnoverAmount > 0)
                        context.MarginBuyRatio = marginBuy / context.TurnoverAmount * 100;

                    MarkMany(context, "EastMoneyMargin",
                        ("MarginBalance", context.MarginBalance > 0),
                        ("ShortBalance", context.ShortBalance > 0),
                        ("MarginBalanceChange", context.MarginBalanceChange != 0),
                        ("MarginBuyRatio", context.MarginBuyRatio > 0));
                }
                else
                {
                    MarkData(context, "MarginBalance", "EastMoneyMargin", false, note: "No margin data returned");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Margin fetch failed for {code}: {ex.Message}");
                MarkData(context, "MarginBalance", "EastMoneyMargin", false, note: ex.Message);
            }
        }

        /// <summary>
        /// 获取股东人数变化数据（从东方财富）
        /// </summary>
        private static async Task FetchShareholderDataAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                // 东财股东人数接口
                string url = $"http://push2.eastmoney.com/api/qt/stock/get?fltt=2&secid={market}.{code}&fields=f100,f108,f109,f110,f111";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                var data = jsonObj["data"];
                if (data != null && data.Type != JTokenType.Null)
                {
                    // f108=最新股东人数, f109=上期股东人数, f110=股东人数变化(%)
                    if (int.TryParse(data["f108"]?.ToString(), out int latest))
                        context.ShareholderCountLatest = latest;
                    if (int.TryParse(data["f109"]?.ToString(), out int prev))
                        context.ShareholderCountPrev = prev;
                    if (double.TryParse(data["f110"]?.ToString(), out double change))
                        context.ShareholderCountChange = change;
                    context.ShareholderUpdateDate = data["f111"]?.ToString() ?? "";

                    MarkMany(context, "EastMoneyShareholder",
                        ("ShareholderCountLatest", context.ShareholderCountLatest > 0),
                        ("ShareholderCountPrev", context.ShareholderCountPrev > 0),
                        ("ShareholderCountChange", context.ShareholderCountChange != 0));
                }
                else
                {
                    MarkData(context, "ShareholderCountLatest", "EastMoneyShareholder", false, note: "No shareholder data returned");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Shareholder fetch failed for {code}: {ex.Message}");
                MarkData(context, "ShareholderCountLatest", "EastMoneyShareholder", false, note: ex.Message);
            }
        }

        /// <summary>
        /// 获取限售股解禁数据（从东方财富）
        /// </summary>
        private static async Task FetchUnlockDataAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                // 东财解禁接口
                string url = $"http://push2.eastmoney.com/api/qt/stock/get?fltt=2&secid={market}.{code}&fields=f114,f115,f116,f117,f118";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                var data = jsonObj["data"];
                if (data != null && data.Type != JTokenType.Null)
                {
                    // f114=最近解禁日期, f115=解禁股数(万股), f116=解禁占流通股本比
                    // f117=距解禁天数, f118=解禁市值(万元)
                    context.NearestUnlockDate = data["f114"]?.ToString() ?? "";
                    if (double.TryParse(data["f115"]?.ToString(), out double amount))
                        context.UnlockAmount = amount;
                    if (double.TryParse(data["f116"]?.ToString(), out double ratio))
                        context.UnlockRatio = ratio;
                    if (int.TryParse(data["f117"]?.ToString(), out int days))
                        context.DaysToUnlock = days;

                    MarkMany(context, "EastMoneyUnlock",
                        ("NearestUnlockDate", !string.IsNullOrWhiteSpace(context.NearestUnlockDate)),
                        ("UnlockAmount", context.UnlockAmount > 0),
                        ("UnlockRatio", context.UnlockRatio > 0),
                        ("DaysToUnlock", context.DaysToUnlock < 999));
                }
                else
                {
                    MarkData(context, "NearestUnlockDate", "EastMoneyUnlock", false, note: "No unlock data returned");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unlock fetch failed for {code}: {ex.Message}");
                MarkData(context, "NearestUnlockDate", "EastMoneyUnlock", false, note: ex.Message);
            }
        }

        public class MarketIndexInfo
        {
            public string Name { get; set; } = string.Empty;
            public double Price { get; set; }
            public double PctChange { get; set; }
        }

        // 5. 获取大盘情绪指数环境
        public static async Task<List<MarketIndexInfo>> FetchMarketIndexAsync()
        {
            var list = new List<MarketIndexInfo>();
            try
            {
                RotateUserAgent();
                string url = "http://push2.eastmoney.com/api/qt/ulist.np/get?fltt=2&fields=f2,f3,f14&secids=1.000001,0.399001,0.399006";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                
                var diff = jsonObj["data"]?["diff"] as JArray;
                if (diff != null)
                {
                    foreach (var item in diff)
                    {
                        list.Add(new MarketIndexInfo
                        {
                            Name = item["f14"]?.ToString() ?? "",
                            Price = double.TryParse(item["f2"]?.ToString(), out var p) ? p : 0,
                            PctChange = double.TryParse(item["f3"]?.ToString(), out var pct) ? pct : 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Market index fetch failed: {ex.Message}");
                // Fallback to Sina
                try
                {
                    string fallbackUrl = "https://hq.sinajs.cn/list=s_sh000001,s_sz399001,s_sz399006";
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var resp = await _httpClient.GetAsync(fallbackUrl, cts.Token);
                    
                    var decoder = System.Text.Encoding.GetEncoding("gb2312");
                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                    var content = decoder.GetString(bytes);

                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[] { '=', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 4)
                        {
                            list.Add(new MarketIndexInfo
                            {
                                Name = parts[1].Replace("\"", ""),
                                Price = double.TryParse(parts[2], out var p) ? p : 0,
                                PctChange = double.TryParse(parts[4].Replace("\"", "").Replace(";", ""), out var pct) ? pct : 0
                            });
                        }
                    }
                }
                catch (Exception sinex)
                {
                    System.Diagnostics.Debug.WriteLine($"Market index fallback failed: {sinex.Message}");
                }
            }
            return list;
        }

        // 6. 获取大盘全貌数据（涨跌分布、板块排行、宏观新闻）
        public static async Task<MarketOverviewData> FetchMarketOverviewAsync(string tavilyApiKey = "")
        {
            var overview = new MarketOverviewData
            {
                Date = DateTime.Now.ToString("yyyy-MM-dd")
            };

            try
            {
                RotateUserAgent();
                // A. 获取全市场股票以计算涨跌分布 (同步 A 股逻辑)
                // 仅获取关键字段: f3(涨跌幅), f12(代码), f14(名称), f17(昨收), f2(现价), f6(成交额)
                // fltt=2 参数可能会导致 clist/get 接口 502 报错或连接重置，暂时回滚此参数
                string url = "http://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=6000&po=1&np=1&fields=f2,f3,f12,f14,f17,f6&fs=m:0+t:6,m:0+t:80,m:1+t:2,m:1+t:23";
                var jsonObj = await FetchEastMoneyJsonAsync(url, 12);
                var diff = jsonObj["data"]?["diff"] as JArray;

                double totalAmount = 0;
                if (diff != null)
                {
                    foreach (var item in diff)
                    {
                        if (!double.TryParse(item["f3"]?.ToString(), out var rawPct)) continue;
                        if (!double.TryParse(item["f2"]?.ToString(), out var current)) continue;
                        if (!double.TryParse(item["f17"]?.ToString(), out var preClose)) continue;
                        if (!double.TryParse(item["f6"]?.ToString(), out var amount)) continue;

                        double pct = rawPct / 100.0; // 东财不加 fltt=2 时，f3 是放大100倍的值

                        // 过滤停牌、退市股票（无现价或无成交）
                        if (current <= 0 || amount <= 0) continue;

                        string code = item["f12"]?.ToString() ?? "";
                        string name = item["f14"]?.ToString() ?? "";

                        totalAmount += amount;

                        if (pct > 0) overview.UpCount++;
                        else if (pct < 0) overview.DownCount++;
                        else overview.FlatCount++;

                        // 精确判断涨跌停: 使用昨收计算理论涨跌停价
                        double ratio = 0.1;
                        if (code.StartsWith("688") || code.StartsWith("30")) ratio = 0.2;
                        else if (code.StartsWith("92") || code.StartsWith("43") || code.StartsWith("8") || code.StartsWith("4")) ratio = 0.3;
                        else if (name.Contains("ST")) ratio = 0.05;

                        // 修复：pct已经是百分比形式(如0.099代表9.9%), ratio是比例(如0.1代表10%)
                        // 涨跌停判断: current/preClose - 1 与 ratio 比较
                        double actualChange = (current - preClose) / preClose;
                        if (actualChange >= (ratio - 0.001)) overview.LimitUpCount++;
                        if (actualChange <= -(ratio - 0.001)) overview.LimitDownCount++;
                    }
                }

                // 获取官方准确的两市成交额 (上证+深证)
                try
                {
                    string indexUrl = "http://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=10&po=1&np=1&fields=f6&fs=i:1.000001,i:0.399001";
                    var idxObj = await FetchEastMoneyJsonAsync(indexUrl, 8);
                    var idxDiff = idxObj["data"]?["diff"] as JArray;
                    if (idxDiff != null)
                    {
                        double exactTotalAmount = 0;
                        foreach (var idx in idxDiff)
                        {
                            if (double.TryParse(idx["f6"]?.ToString(), out var amt))
                                exactTotalAmount += amt;
                        }
                        overview.TotalAmount = exactTotalAmount / 100000000.0;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fetch index volume failed: {ex.Message}");
                    overview.TotalAmount = totalAmount / 100000000.0; // fallback
                }

                // B. 获取板块排行（完整列表用于个股板块匹配）
                string sectorUrl = "http://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=100&po=1&np=1&fields=f3,f14&fs=m:90+t:2+f:!2";
                var sectorObj = await FetchEastMoneyJsonAsync(sectorUrl, 8);
                var sectorDiff = sectorObj["data"]?["diff"] as JArray;
                if (sectorDiff != null)
                {
                    overview.AllSectors = sectorDiff.Select(s => new SectorRanking
                    {
                        Name = s["f14"]?.ToString() ?? "",
                        ChangePct = (double.TryParse(s["f3"]?.ToString(), out var p) ? p : 0) / 100.0
                    }).ToList();

                    overview.TopSectors = overview.AllSectors.OrderByDescending(s => s.ChangePct).Take(5).ToList();
                    overview.BottomSectors = overview.AllSectors.OrderBy(s => s.ChangePct).Take(5).ToList();
                }

                // C. 获取宏观新闻（含降级方案）
                await FetchMarketNewsAsync(overview, tavilyApiKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Market overview fetch failed: {ex.Message}");
            }

            return overview;
        }

        /// <summary>
        /// 获取市场宏观新闻（Tavily优先，降级到新浪）
        /// </summary>
        private static async Task FetchMarketNewsAsync(MarketOverviewData overview, string tavilyApiKey)
        {
            // 方案一：Tavily API
            if (!string.IsNullOrEmpty(tavilyApiKey))
            {
                try
                {
                    var newsPayLoad = new { api_key = tavilyApiKey, query = "今日A股市场行情 宏观经济利好利空 行业热点", topic = "news", days = 1, max_results = 8 };
                    var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(newsPayLoad), Encoding.UTF8, "application/json");
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var newsResp = await _httpClient.PostAsync("https://api.tavily.com/search", content, cts.Token);
                    if (newsResp.IsSuccessStatusCode)
                    {
                        var newsJson = await newsResp.Content.ReadAsStringAsync();
                        var newsObj = JObject.Parse(newsJson);
                        var results = newsObj["results"] as JArray;
                        if (results != null)
                        {
                            foreach (var r in results)
                                overview.MarketNews.Add($"- {r["title"]}: {r["content"]}");
                        }
                    }
                    if (overview.MarketNews.Count > 0) return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Tavily news fetch failed: {ex.Message}");
                }
            }

            // 方案二：降级到新浪财经要闻抓取
            try
            {
                string sinaUrl = "https://finance.sina.com.cn/stock/";
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                var html = await _httpClient.GetStringAsync(sinaUrl, cts.Token);

                // 简单提取标题
                var titleMatches = System.Text.RegularExpressions.Regex.Matches(html, @"<a[^>]*>([^<]{10,80})</a>");
                int count = 0;
                foreach (System.Text.RegularExpressions.Match m in titleMatches)
                {
                    string title = m.Groups[1].Value.Trim();
                    if (title.Contains("股市") || title.Contains("A股") || title.Contains("板块") ||
                        title.Contains("涨停") || title.Contains("跌停") || title.Contains("大盘") ||
                        title.Contains("成交") || title.Contains("指数"))
                    {
                        overview.MarketNews.Add($"- {title}");
                        count++;
                        if (count >= 8) break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sina news fallback failed: {ex.Message}");
            }

            // 方案三：最终兜底
            if (overview.MarketNews.Count == 0)
            {
                overview.MarketNews.Add("- 暂无宏观新闻数据源，建议关注财联社、同花顺等资讯平台。");
                overview.MarketNews.Add("- 建议手动查看当日涨停/跌停板块分布。");
            }
        }

        /// <summary>
        /// 获取个股所属板块涨跌幅数据（用于板块联动分析）
        /// </summary>
        public static async Task<(string SectorName, double SectorPctChange, double SectorRankPercent)> FetchStockSectorAsync(string code, List<SectorRanking>? allSectors = null)
        {
            try
            {
                // 尝试从EastMoney获取个股板块信息
                string market = GetEastMoneyMarketPrefix(code);
                string url = $"http://push2.eastmoney.com/api/qt/stock/get?fltt=2&secid={market}.{code}&fields=f100,f102,f104";

                var jsonObj = await FetchEastMoneyJsonAsync(url, 8);
                var data = jsonObj["data"];

                string sectorName = "";
                if (data != null && data.Type != JTokenType.Null)
                {
                    sectorName = data["f100"]?.ToString() ?? "";
                }

                // 从已获取的板块排行中匹配
                if (allSectors != null && !string.IsNullOrEmpty(sectorName))
                {
                    // 模糊匹配板块名称
                    var matched = allSectors.FirstOrDefault(s =>
                        sectorName.Contains(s.Name) || s.Name.Contains(sectorName));
                    if (matched != null)
                    {
                        double sectorPct = matched.ChangePct;
                        // 估算排名百分比（按涨跌幅排）
                        var sortedList = allSectors.OrderByDescending(s => s.ChangePct).ToList();
                        int rank = sortedList.FindIndex(s => s.Name == matched.Name) + 1;
                        double rankPct = sortedList.Count > 0 ? (double)rank / sortedList.Count * 100 : 50;

                        return (sectorName, sectorPct, rankPct);
                    }
                    else
                    {
                        // 将全部板块列表的中位数作为默认板块表现
                        double medianPct = allSectors.OrderBy(s => s.ChangePct).ElementAt(allSectors.Count / 2).ChangePct;
                        return (sectorName, medianPct, 50);
                    }
                }

                return (sectorName, 0, 50);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FetchStockSectorAsync failed for {code}: {ex.Message}");
                return ("", 0, 50);
            }
        }
    }
}
