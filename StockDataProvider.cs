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
            var context = new StockDeepAnalysisContext { Code = code };
            
            // 并发获取三大维度数据
            var tencentTask = FetchTencentRealtimeAsync(code, context);
            var klineTask = FetchEastMoneyKlinesAsync(code, context);
            var newsTask = FetchTavilyNewsAsync(code, context, tavilyApiKey);
            var flowTask = FetchMainForceFlowAsync(code, context);
            var reportTask = FetchFinancialReportAsync(code, context);

            await Task.WhenAll(tencentTask, klineTask, newsTask, flowTask, reportTask);
            return context;
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tencent data fetch failed: {ex.Message}");
            }
        }

        // 2. 获取东财历史 K 线以精确推演均线
        private static async Task FetchEastMoneyKlinesAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                // 近 250 个交易日的价量数据以推演均线、筹码分布、波动率锥
                string url = $"http://push2his.eastmoney.com/api/qt/stock/kline/get?secid={market}.{code}&klt=101&fqt=1&end=20500101&lmt=250&fields1=f1&fields2=f51,f52,f53,f54,f55,f56";
                var jsonStr = await _httpClient.GetStringAsync(url);
                var jsonObj = JObject.Parse(jsonStr);

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
                    try
                    {
                        // 60分钟K线
                        string url60min = $"http://push2his.eastmoney.com/api/qt/stock/kline/get?secid={market}.{code}&klt=60&fqt=1&end=20500101&lmt=100&fields1=f1&fields2=f51,f52,f53,f54,f55,f56";
                        var resp60 = await _httpClient.GetStringAsync(url60min);
                        var obj60 = JObject.Parse(resp60);
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
                            if (closes60.Count >= 20)
                            {
                                context.TechScore60Min = AdvancedTechnicalIndicators.ComprehensiveTechnicalAnalysis(
                                    closes60, vols60, highs60, lows60);
                            }
                        }

                        // 15分钟K线
                        string url15min = $"http://push2his.eastmoney.com/api/qt/stock/kline/get?secid={market}.{code}&klt=15&fqt=1&end=20500101&lmt=100&fields1=f1&fields2=f51,f52,f53,f54,f55,f56";
                        var resp15 = await _httpClient.GetStringAsync(url15min);
                        var obj15 = JObject.Parse(resp15);
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
                            if (closes15.Count >= 20)
                            {
                                context.TechScore15Min = AdvancedTechnicalIndicators.ComprehensiveTechnicalAnalysis(
                                    closes15, vols15, highs15, lows15);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Multi-timeframe data fetch failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EastMoney kline/chip fetch failed: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sina news fetch failed: {ex.Message}");
            }
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
                // f62=主力净流入, f64=超大单净流入, f70=大单净流入, f72=中单净流入, f74=小单净流入, f66=成交额
                string url = $"http://push2.eastmoney.com/api/qt/stock/get?fltt=2&secid={market}.{code}&fields=f62,f64,f66,f70,f72,f74";

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var jsonStr = await _httpClient.GetStringAsync(url, cts.Token);
                var jsonObj = JObject.Parse(jsonStr);
                var data = jsonObj["data"];
                if (data != null && data.Type != JTokenType.Null)
                {
                    if (double.TryParse(data["f62"]?.ToString(), out double inflow))
                        context.MainForceNetInflow = inflow;
                    if (double.TryParse(data["f64"]?.ToString(), out double superLarge))
                        context.SuperLargeOrderInflow = superLarge;
                    if (double.TryParse(data["f70"]?.ToString(), out double large))
                        context.LargeOrderInflow = large;
                    if (double.TryParse(data["f72"]?.ToString(), out double medium))
                        context.MediumOrderInflow = medium;
                    if (double.TryParse(data["f74"]?.ToString(), out double small))
                        context.SmallOrderInflow = small;
                    if (double.TryParse(data["f66"]?.ToString(), out double amount))
                        context.TurnoverAmount = amount;

                    // 计算主力净流入占成交额比例
                    if (context.TurnoverAmount > 0)
                        context.MainForceInflowRatio = context.MainForceNetInflow / context.TurnoverAmount * 100;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainForceFlow fetch failed: {ex.Message}");
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
                var jsonStr = await _httpClient.GetStringAsync(url, cts.Token);
                var jsonObj = JObject.Parse(jsonStr);
                
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
                     var fbStr = await _httpClient.GetStringAsync(fallbackUrl);
                     var fbObj = JObject.Parse(fbStr);
                     
                     var fbData = fbObj["data"];
                     if (fbData != null && fbData.Type != JTokenType.Null)
                     {
                         if (double.TryParse(fbData["f173"]?.ToString(), out var fallbackRoe))
                             context.ROE = fallbackRoe; 
                             
                         // 标注为降级状态，其他字段妥协留空
                     }
                 }
                 catch (Exception fbEx)
                 {
                     System.Diagnostics.Debug.WriteLine($"Financial fallback also failed: {fbEx.Message}");
                 }
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
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var jsonStr = await _httpClient.GetStringAsync(url, cts.Token);
                var jsonObj = JObject.Parse(jsonStr);
                
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
                var jsonStr = await NetworkHelper.HttpGetWithRetryAsync(url, 2);
                if (string.IsNullOrEmpty(jsonStr)) return overview;

                var jsonObj = JObject.Parse(jsonStr);
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
                    var idxStr = await NetworkHelper.HttpGetWithRetryAsync(indexUrl, 1);
                    if (!string.IsNullOrEmpty(idxStr))
                    {
                        var idxObj = JObject.Parse(idxStr);
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
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fetch index volume failed: {ex.Message}");
                    overview.TotalAmount = totalAmount / 100000000.0; // fallback
                }

                // B. 获取板块排行（完整列表用于个股板块匹配）
                string sectorUrl = "http://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=100&po=1&np=1&fields=f3,f14&fs=m:90+t:2+f:!2";
                var sectorStr = await NetworkHelper.HttpGetWithRetryAsync(sectorUrl, 1);
                if (!string.IsNullOrEmpty(sectorStr))
                {
                    var sectorObj = JObject.Parse(sectorStr);
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

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var jsonStr = await _httpClient.GetStringAsync(url, cts.Token);
                var jsonObj = JObject.Parse(jsonStr);
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
