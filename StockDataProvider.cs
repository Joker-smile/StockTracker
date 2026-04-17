using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        public double BiasMA5 { get; set; }
        public double BiasMA10 { get; set; }
        public double BiasMA20 { get; set; }
        public string MAAlignment { get; set; } = string.Empty; // 多头/空头
        
        // 舆情情报 (最新新闻)
        public List<string> LatestNews { get; set; } = new();

        // 筹码与大势分析
        public double ProfitRatio { get; set; } // 获利比例(%)
        public double ChipAvgCost { get; set; } // 平均成本
        public double ChipConcentration90 { get; set; } // 90筹码集中度
        public double MainForceNetInflow { get; set; } // 主力净流入金额
        
        // 财报基础 (价值底座)
        public double ROE { get; set; } // 净资产收益率
        public double OperatingRevenue { get; set; } // 营业总收入(亿)
        public double NetProfit { get; set; } // 归母净利润(亿)
        public double OperatingCashFlowPerShare { get; set; } // 每股经营现金流
        
        // 昨日对比异动
        public double VolumeChangeRatio { get; set; } // 成交量较昨日变化倍数
        public double PriceChangeRatio { get; set; } // 价格较昨日变化
    }

    public static class StockDataProvider
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string[] _userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36"
        };
        private static readonly Random _rand = new Random();

        static StockDataProvider()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
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
            if (code.StartsWith("6")) return "sh";
            if (code.StartsWith("0") || code.StartsWith("3")) return "sz";
            if (code.StartsWith("8") || code.StartsWith("4")) return "bj";
            return "sz"; // fallback
        }

        private static string GetEastMoneyMarketPrefix(string code)
        {
            if (code.StartsWith("6")) return "1"; // SH
            if (code.StartsWith("0") || code.StartsWith("3")) return "0"; // SZ
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
                // 近 200 个交易日的价量数据以推演均线和筹码分布
                string url = $"http://push2his.eastmoney.com/api/qt/stock/kline/get?secid={market}.{code}&klt=101&fqt=1&end=20500101&lmt=200&fields1=f1&fields2=f51,f53,f56";
                var jsonStr = await _httpClient.GetStringAsync(url);
                var jsonObj = JObject.Parse(jsonStr);
                
                var klines = jsonObj["data"]?["klines"] as JArray;
                if (klines != null && klines.Count > 0)
                {
                    var closes = new List<double>();
                    var volumes = new List<double>();
                    foreach (var k in klines)
                    {
                        var parts = k.ToString().Split(','); // "2024-01-01,10.00,12345"
                        if (parts.Length >= 3 && double.TryParse(parts[1], out var c) && double.TryParse(parts[2], out var v))
                        {
                            closes.Add(c);
                            volumes.Add(v);
                        }
                    }

                    // 取最后一个价 (今日实时)
                    double currentPrice = closes.Last();
                    if (context.CurrentPrice == 0) context.CurrentPrice = currentPrice;

                    // 计算 MA5
                    if (closes.Count >= 5)
                        context.MA5 = closes.Skip(closes.Count - 5).Average();
                    
                    // 计算 MA10
                    if (closes.Count >= 10)
                        context.MA10 = closes.Skip(closes.Count - 10).Average();
                    else 
                        context.MA10 = context.MA5; // 数据不足

                    // 计算 MA20
                    if (closes.Count >= 20)
                        context.MA20 = closes.Skip(closes.Count - 20).Average();
                    else
                        context.MA20 = context.MA10; // 数据不足

                    context.BiasMA5 = context.MA5 > 0 ? (currentPrice - context.MA5) / context.MA5 * 100 : 0;
                    context.BiasMA10 = context.MA10 > 0 ? (currentPrice - context.MA10) / context.MA10 * 100 : 0;
                    context.BiasMA20 = context.MA20 > 0 ? (currentPrice - context.MA20) / context.MA20 * 100 : 0;

                    // 判断均线走势
                    if (context.MA5 > context.MA10 && context.MA10 > context.MA20)
                        context.MAAlignment = "多头排列 (强势上涨)";
                    else if (context.MA5 < context.MA10 && context.MA10 < context.MA20)
                        context.MAAlignment = "空头排列 (弱势下跌)";
                    else
                        context.MAAlignment = "震荡缠绕 (方向不明)";
                    
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
                        
                    // ======= C# 仿真筹码分布 (CYQ) 计算 =======
                    if (closes.Count > 0 && volumes.Count > 0 && closes.Count == volumes.Count)
                    {
                        double totalVol = volumes.Sum();
                        if (totalVol > 0)
                        {
                            // 获利盘比例: 收盘价 <= 当前价的累计成交量占比
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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EastMoney kline/chip fetch failed: {ex.Message}");
            }
        }

        // 3. 获取新闻舆情 (优先 Tavily 高级检索，降级新浪免签引擎)
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
                
                // Regex 提取 <div class="datelist"> 里的新闻链接 
                // 示例: <a href="http://finance.sina..." target="_blank">东方财富第一季度净利...</a>
                var match = Regex.Match(html, @"<div\s+class=""datelist"">(.*?)</div>", RegexOptions.Singleline);
                if (match.Success)
                {
                    string content = match.Groups[1].Value;
                    // 拉取前 5 条 A 标签
                    var linkMatches = Regex.Matches(content, @"<a\s+href=""[^""]+""[^>]*>(.*?)</a>");
                    int maxNews = 5;
                    foreach (Match m in linkMatches)
                    {
                        var title = m.Groups[1].Value.Trim();
                        // 过滤太短或者是无意义的
                        if (title.Length > 5 && !title.Contains("关于") && !title.Contains("公告"))
                        {
                            newsList.Add($"- {title}");
                        }
                        if (newsList.Count >= maxNews) break;
                    }
                }
                
                context.LatestNews = newsList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sina news fetch failed: {ex.Message}");
            }
        }

        // 4. 获取主力资金流向
        private static async Task FetchMainForceFlowAsync(string code, StockDeepAnalysisContext context)
        {
            try
            {
                string market = GetEastMoneyMarketPrefix(code);
                string url = $"http://push2.eastmoney.com/api/qt/stock/get?secid={market}.{code}&fields=f62";
                
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var jsonStr = await _httpClient.GetStringAsync(url, cts.Token);
                var jsonObj = JObject.Parse(jsonStr);
                var data = jsonObj["data"];
                if (data != null && data.Type != JTokenType.Null)
                {
                    if (double.TryParse(data["f62"]?.ToString(), out double inflow))
                    {
                        context.MainForceNetInflow = inflow;
                    }
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
                     string fallbackUrl = $"http://push2.eastmoney.com/api/qt/stock/get?secid={market}.{code}&fields=f173";
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
    }
}
