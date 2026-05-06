using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StockTracker
{
    /// <summary>
    /// 回测验证和追踪系统 - 持续优化AI分析准确性
    /// </summary>
    public class AdviceTracker
    {
        public class AdviceRecord
        {
            public DateTime AdviceDate { get; set; }
            public string StockCode { get; set; } = string.Empty;
            public string StockName { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public decimal RecommendedPrice { get; set; }
            public decimal StopLossPrice { get; set; }
            public decimal TargetPrice { get; set; }
            public double ExpectedWinRate { get; set; }
            public double OverallScore { get; set; }
            public double TechnicalScore { get; set; }
            public double FundamentalScore { get; set; }
            public double FundFlowScore { get; set; }
            public MarketCondition MarketCondition { get; set; }

            // 新增5个评分维度（补全9维贝叶斯）
            public double SentimentScore { get; set; }
            public double TrendStrengthScore { get; set; }
            public double ValueScore { get; set; }
            public double SectorStrengthScore { get; set; }
            public double MultiTimeframeScore { get; set; }
            public double DivergenceScore { get; set; }

            public decimal? ActualHighestPrice { get; set; }
            public decimal? ActualLowestPrice { get; set; }
            public decimal? CurrentPrice { get; set; }
            public DateTime? VerifyDate { get; set; }
            public bool? WasSuccessful { get; set; }
            public decimal? ActualProfitLoss { get; set; }
            public string SuccessReason { get; set; } = string.Empty;
            public int HoldingDays { get; set; }
        }

        private static readonly string _trackerFilePath = Path.Combine(AppContext.BaseDirectory, "advice_tracker.json");
        private static readonly string _performanceLogPath = Path.Combine(AppContext.BaseDirectory, "performance_log.json");
        private static readonly string _bayesianCachePath = Path.Combine(AppContext.BaseDirectory, "bayesian_weights.json");

        /// <summary>
        /// 记录AI建议
        /// </summary>
        public static void RecordAdvice(AdviceRecord record)
        {
            try
            {
                var records = LoadRecords();
                records.Add(record);
                SaveRecords(records);

                // 记录到性能日志
                LogPerformance(record);
            }
            catch (Exception ex)
            {
                Program.LogError("Record advice failed", ex);
            }
        }

        /// <summary>
        /// 更新建议的实际表现
        /// </summary>
        public static void UpdateAdvicePerformance(string stockCode, DateTime adviceDate, decimal currentPrice,
            decimal? highestPrice = null, decimal? lowestPrice = null)
        {
            try
            {
                var records = LoadRecords();
                var record = records.FirstOrDefault(r =>
                    r.StockCode == stockCode &&
                    r.Action == "buy" &&
                    !r.WasSuccessful.HasValue &&
                    r.AdviceDate == adviceDate);

                if (record != null)
                {
                    record.CurrentPrice = currentPrice;
                    record.VerifyDate = DateTime.Now;
                    record.HoldingDays = (DateTime.Now - record.AdviceDate).Days;

                    if (highestPrice.HasValue)
                        record.ActualHighestPrice = highestPrice.Value;
                    if (lowestPrice.HasValue)
                        record.ActualLowestPrice = lowestPrice.Value;

                    // 计算盈亏
                    decimal profitLoss = (currentPrice - record.RecommendedPrice) / record.RecommendedPrice * 100m;
                    record.ActualProfitLoss = profitLoss;

                    // 判断成功与否
                    if (profitLoss >= 5) // 5%以上盈利算成功
                    {
                        record.WasSuccessful = true;
                        record.SuccessReason = $"达到目标盈利，盈利{profitLoss:F2}%";
                    }
                    else if (profitLoss <= -8) // 跌破止损
                    {
                        record.WasSuccessful = false;
                        record.SuccessReason = $"跌破止损位，亏损{profitLoss:F2}%";
                    }
                    else if (record.HoldingDays > 30) // 超过30天未达标
                    {
                        record.WasSuccessful = profitLoss > 0;
                        record.SuccessReason = $"持有{record.HoldingDays}天，盈亏{profitLoss:F2}%";
                    }

                    SaveRecords(records);
                    LogPerformance(record);
                }
            }
            catch (Exception ex)
            {
                Program.LogError("Update advice performance failed", ex);
            }
        }

        /// <summary>
        /// 加载所有建议记录
        /// </summary>
        public static List<AdviceRecord> LoadRecords()
        {
            try
            {
                if (File.Exists(_trackerFilePath))
                {
                    var json = File.ReadAllText(_trackerFilePath);
                    return JsonConvert.DeserializeObject<List<AdviceRecord>>(json) ?? new List<AdviceRecord>();
                }
            }
            catch (Exception ex)
            {
                Program.LogError("Load advice tracker failed", ex);
            }
            return new List<AdviceRecord>();
        }

        /// <summary>
        /// 保存建议记录
        /// </summary>
        private static void SaveRecords(List<AdviceRecord> records)
        {
            try
            {
                var json = JsonConvert.SerializeObject(records, Formatting.Indented);
                File.WriteAllText(_trackerFilePath, json);
            }
            catch (Exception ex)
            {
                Program.LogError("Save advice tracker failed", ex);
            }
        }

        /// <summary>
        /// 记录性能日志（9维全量）
        /// </summary>
        private static void LogPerformance(AdviceRecord record)
        {
            try
            {
                var logs = LoadPerformanceLogs();
                var log = new
                {
                    Timestamp = DateTime.Now,
                    StockCode = record.StockCode,
                    StockName = record.StockName,
                    Action = record.Action,
                    ExpectedWinRate = record.ExpectedWinRate,
                    OverallScore = record.OverallScore,
                    TechnicalScore = record.TechnicalScore,
                    FundamentalScore = record.FundamentalScore,
                    FundFlowScore = record.FundFlowScore,
                    SentimentScore = record.SentimentScore,
                    TrendStrengthScore = record.TrendStrengthScore,
                    ValueScore = record.ValueScore,
                    SectorStrengthScore = record.SectorStrengthScore,
                    MultiTimeframeScore = record.MultiTimeframeScore,
                    DivergenceScore = record.DivergenceScore,
                    MarketCondition = record.MarketCondition.ToString(),
                    ActualProfitLoss = record.ActualProfitLoss,
                    WasSuccessful = record.WasSuccessful,
                    HoldingDays = record.HoldingDays
                };

                logs.Add(log);
                var json = JsonConvert.SerializeObject(logs, Formatting.Indented);
                File.WriteAllText(_performanceLogPath, json);
            }
            catch (Exception ex)
            {
                Program.LogError("Log performance failed", ex);
            }
        }

        /// <summary>
        /// 加载性能日志
        /// </summary>
        private static List<object> LoadPerformanceLogs()
        {
            try
            {
                if (File.Exists(_performanceLogPath))
                {
                    var json = File.ReadAllText(_performanceLogPath);
                    return JsonConvert.DeserializeObject<List<object>>(json) ?? new List<object>();
                }
            }
            catch (Exception ex)
            {
                Program.LogError("Load performance logs failed", ex);
            }
            return new List<object>();
        }

        /// <summary>
        /// 计算回测结果
        /// </summary>
        public static BackTestResult CalculateBackTestResults(int? daysBack = null)
        {
            try
            {
                var records = LoadRecords();
                var cutoffDate = daysBack.HasValue ? DateTime.Now.AddDays(-daysBack.Value) : (DateTime?)null;

                var buyRecords = records.Where(r =>
                    r.Action == "buy" &&
                    r.WasSuccessful.HasValue &&
                    (!cutoffDate.HasValue || r.AdviceDate >= cutoffDate.Value)).ToList();

                if (buyRecords.Count == 0)
                    return new BackTestResult { TotalTrades = 0, AnalysisDate = DateTime.Now };

                int successfulTrades = buyRecords.Count(r => r.WasSuccessful.HasValue && r.WasSuccessful.Value);
                double winRate = (double)successfulTrades / buyRecords.Count * 100;

                var profitRecords = buyRecords.Where(r => r.ActualProfitLoss.HasValue).ToList();
                double avgProfit = profitRecords.Any() ?
                    profitRecords.Average(r => (double)r.ActualProfitLoss!.Value) : 0.0;

                double totalProfit = profitRecords.Sum(r => (double)(r.ActualProfitLoss ?? 0.0m));
                double maxProfit = profitRecords.Any() ?
                    (double)profitRecords.Max(r => r.ActualProfitLoss ?? 0.0m) : 0.0;

                double maxLoss = profitRecords.Any() ?
                    (double)profitRecords.Min(r => r.ActualProfitLoss ?? 0.0m) : 0.0;

                // 按评分区间分析
                var scoreAnalysis = AnalyzeByScoreRange(buyRecords);

                // 按市场环境分析
                var marketAnalysis = AnalyzeByMarketCondition(buyRecords);

                return new BackTestResult
                {
                    TotalTrades = buyRecords.Count,
                    SuccessfulTrades = successfulTrades,
                    WinRate = winRate,
                    AverageProfit = avgProfit,
                    TotalProfit = totalProfit,
                    MaxProfit = maxProfit,
                    MaxLoss = maxLoss,
                    ScoreRangeAnalysis = scoreAnalysis,
                    MarketConditionAnalysis = marketAnalysis,
                    AnalysisDate = DateTime.Now,
                    PeriodDays = daysBack
                };
            }
            catch (Exception ex)
            {
                Program.LogError("Calculate backtest results failed", ex);
                return new BackTestResult { TotalTrades = 0, AnalysisDate = DateTime.Now };
            }
        }

        /// <summary>
        /// 按评分区间分析
        /// </summary>
        private static List<ScoreRangeAnalysis> AnalyzeByScoreRange(List<AdviceRecord> records)
        {
            var ranges = new List<ScoreRangeAnalysis>();
            var scoreRanges = new[]
            {
                new { Min = 70.0, Max = 100.0, Name = "70-100分" },
                new { Min = 60.0, Max = 69.9, Name = "60-69分" },
                new { Min = 50.0, Max = 59.9, Name = "50-59分" },
                new { Min = 0.0, Max = 49.9, Name = "0-49分" }
            };

            foreach (var range in scoreRanges)
            {
                var rangeRecords = records.Where(r =>
                    r.OverallScore >= range.Min && r.OverallScore < range.Max).ToList();

                if (rangeRecords.Count() > 0)
                {
                    var successCount = rangeRecords.Count(r => r.WasSuccessful.HasValue && r.WasSuccessful.Value);
                    var avgProfit = rangeRecords.Where(r => r.ActualProfitLoss.HasValue)
                        .Average(r => (double)r.ActualProfitLoss!.Value);

                    ranges.Add(new ScoreRangeAnalysis
                    {
                        ScoreRange = range.Name,
                        TradeCount = rangeRecords.Count(),
                        SuccessCount = successCount,
                        WinRate = (double)successCount / rangeRecords.Count() * 100,
                        AverageProfit = avgProfit
                    });
                }
            }

            return ranges;
        }

        /// <summary>
        /// 按市场环境分析
        /// </summary>
        private static List<MarketConditionAnalysis> AnalyzeByMarketCondition(List<AdviceRecord> records)
        {
            var analyses = new List<MarketConditionAnalysis>();

            foreach (MarketCondition condition in Enum.GetValues(typeof(MarketCondition)))
            {
                var conditionRecords = records.Where(r => r.MarketCondition == condition).ToList();

                if (conditionRecords.Count > 0)
                {
                    var successCount = conditionRecords.Count(r => r.WasSuccessful.HasValue && r.WasSuccessful.Value);
                    var avgProfit = conditionRecords.Where(r => r.ActualProfitLoss.HasValue)
                        .Average(r => (double)r.ActualProfitLoss!.Value);

                    analyses.Add(new MarketConditionAnalysis
                    {
                        MarketCondition = condition.ToString(),
                        TradeCount = conditionRecords.Count(),
                        SuccessCount = successCount,
                        WinRate = (double)successCount / conditionRecords.Count() * 100,
                        AverageProfit = avgProfit
                    });
                }
            }

            return analyses;
        }

        /// <summary>
        /// 获取优化建议
        /// </summary>
        public static List<string> GetOptimizationSuggestions()
        {
            var suggestions = new List<string>();
            var result = CalculateBackTestResults(60); // 分析最近60天

            if (result.TotalTrades < 10)
            {
                suggestions.Add("⚠️ 数据样本不足，需要更多交易数据才能提供准确的优化建议");
                return suggestions;
            }

            // 整体胜率分析
            if (result.WinRate < 40)
            {
                suggestions.Add("🔴 整体胜率过低，建议重新审视评分模型和买入标准");
            }
            else if (result.WinRate < 55)
            {
                suggestions.Add("🟡 胜率偏低，建议提高买入门槛，只操作评分≥70的股票");
            }

            // 评分区间分析
            if (result.ScoreRangeAnalysis != null)
            {
                var highScoreRange = result.ScoreRangeAnalysis.FirstOrDefault(r => r.ScoreRange == "70-100分");
                if (highScoreRange != null && highScoreRange.WinRate < 60)
                {
                    suggestions.Add("🔴 高评分股票胜率不达标，说明评分模型存在问题，需要重新调整评分逻辑");
                }

                var lowScoreRange = result.ScoreRangeAnalysis.FirstOrDefault(r => r.ScoreRange == "50-59分");
                if (lowScoreRange != null && lowScoreRange.WinRate > 50)
                {
                    suggestions.Add("🤔 低评分股票胜率意外不错，可能存在评分标准不合理的情况");
                }
            }

            // 市场环境分析
            if (result.MarketConditionAnalysis != null)
            {
                var crashAnalysis = result.MarketConditionAnalysis.FirstOrDefault(m => m.MarketCondition == "Crash");
                if (crashAnalysis != null && crashAnalysis.TradeCount > 0)
                {
                    suggestions.Add($"⚠️ 市场暴跌时仍然有{crashAnalysis.TradeCount}次交易，建议加强市场环境过滤");
                }

                var weakAnalysis = result.MarketConditionAnalysis.FirstOrDefault(m => m.MarketCondition == "Weak");
                if (weakAnalysis != null && weakAnalysis.WinRate < 40)
                {
                    suggestions.Add("📉 弱势市场胜率过低，建议在弱势环境下进一步减少操作或提高评分门槛");
                }
            }

            // 盈亏分析
            if (result.AverageProfit < 0)
            {
                suggestions.Add("💰 平均亏损，建议暂停交易，重新评估整个交易系统");
            }
            else if (result.AverageProfit < 3)
            {
                suggestions.Add("💰 平均盈利偏低，建议优化目标价设置，提高盈利预期");
            }

            if (result.MaxLoss < -10)
            {
                suggestions.Add("🛑 最大亏损过大，建议检查止损机制是否有效执行");
            }

            return suggestions;
        }

        // ====================== 贝叶斯反馈闭环 ======================

        /// <summary>
        /// 贝叶斯权重调整数据结构（9维全量）
        /// </summary>
        public class BayesianWeightAdjustments
        {
            public double TechnicalMultiplier { get; set; } = 1.0;
            public double FundamentalMultiplier { get; set; } = 1.0;
            public double FundFlowMultiplier { get; set; } = 1.0;
            public double SentimentMultiplier { get; set; } = 1.0;
            public double TrendMultiplier { get; set; } = 1.0;
            public double ValueMultiplier { get; set; } = 1.0;
            public double SectorMultiplier { get; set; } = 1.0;
            public double MultiTimeframeMultiplier { get; set; } = 1.0;
            public double DivergenceMultiplier { get; set; } = 1.0;
            public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        }

        /// <summary>
        /// 获取贝叶斯权重调整
        /// </summary>
        public static BayesianWeightAdjustments? GetBayesianWeightAdjustments()
        {
            try
            {
                if (File.Exists(_bayesianCachePath))
                {
                    var json = File.ReadAllText(_bayesianCachePath);
                    var result = JsonConvert.DeserializeObject<BayesianWeightAdjustments>(json);
                    // 只使用24小时内的缓存
                    if (result != null && (DateTime.Now - result.LastUpdated).TotalHours < 24)
                        return result;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 更新贝叶斯权重（9维全量，基于历史胜负调整各维度权重）
        /// </summary>
        private static void UpdateBayesianWeights()
        {
            try
            {
                var records = LoadRecords();
                var completedRecords = records.Where(r => r.WasSuccessful.HasValue).ToList();
                if (completedRecords.Count < 15) return; // 至少15笔交易

                var adjustment = new BayesianWeightAdjustments();

                var successRecords = completedRecords.Where(r => r.WasSuccessful == true).ToList();
                var failRecords = completedRecords.Where(r => r.WasSuccessful == false).ToList();

                if (successRecords.Count > 0 && failRecords.Count > 0)
                {
                    // ===== 9维度区分度分析 =====
                    var dims = new (string Name, Func<AdviceRecord, double> Scorer, Action<BayesianWeightAdjustments, double> Setter)[]
                    {
                        ("技术面", r => r.TechnicalScore, (adj, v) => adj.TechnicalMultiplier = v),
                        ("基本面", r => r.FundamentalScore, (adj, v) => adj.FundamentalMultiplier = v),
                        ("资金面", r => r.FundFlowScore, (adj, v) => adj.FundFlowMultiplier = v),
                        ("情绪面", r => r.SentimentScore, (adj, v) => adj.SentimentMultiplier = v),
                        ("趋势强度", r => r.TrendStrengthScore, (adj, v) => adj.TrendMultiplier = v),
                        ("估值", r => r.ValueScore, (adj, v) => adj.ValueMultiplier = v),
                        ("板块联动", r => r.SectorStrengthScore, (adj, v) => adj.SectorMultiplier = v),
                        ("多周期共振", r => r.MultiTimeframeScore, (adj, v) => adj.MultiTimeframeMultiplier = v),
                        ("背离信号", r => r.DivergenceScore, (adj, v) => adj.DivergenceMultiplier = v)
                    };

                    double sAvg, fAvg, disc;
                    var discriminations = new List<double>();
                    var discMap = new Dictionary<string, double>();

                    foreach (var (name, scorer, setter) in dims)
                    {
                        sAvg = successRecords.Average(scorer);
                        fAvg = failRecords.Average(scorer);
                        disc = Math.Abs(sAvg - fAvg);
                        discriminations.Add(disc);
                        discMap[name] = disc;
                    }

                    double maxDisc = Math.Max(discriminations.Max(), 1.0);

                    foreach (var (name, _, setter) in dims)
                    {
                        double dimDisc = discMap[name];
                        double multiplier = 1.0 + (dimDisc / maxDisc - 0.33) * 0.6;
                        multiplier = Math.Max(0.7, Math.Min(1.3, multiplier));
                        setter(adjustment, multiplier);
                    }
                }

                adjustment.LastUpdated = DateTime.Now;
                var json = JsonConvert.SerializeObject(adjustment, Formatting.Indented);
                File.WriteAllText(_bayesianCachePath, json);
            }
            catch (Exception ex)
            {
                Program.LogError("Update bayesian weights failed", ex);
            }
        }

        /// <summary>
        /// 获取某只股票的样本量（历史交易次数）
        /// </summary>
        public static int GetStockSampleSize(string stockCode)
        {
            var records = LoadRecords();
            return records.Count(r => r.StockCode == stockCode && r.WasSuccessful.HasValue);
        }

        /// <summary>
        /// 获取连续亏损次数
        /// </summary>
        public static int GetConsecutiveLosses()
        {
            var records = LoadRecords();
            var sortedRecords = records
                .Where(r => r.WasSuccessful.HasValue)
                .OrderByDescending(r => r.AdviceDate)
                .ToList();

            int consecutive = 0;
            foreach (var record in sortedRecords)
            {
                if (record.WasSuccessful == false)
                    consecutive++;
                else
                    break;
            }
            return consecutive;
        }

        /// <summary>
        /// 按维度分析回测胜率（9维全量，用于AI Prompt优化）
        /// </summary>
        public static string GetDimensionPerformanceAnalysis()
        {
            var records = LoadRecords();
            var completed = records.Where(r => r.WasSuccessful.HasValue).ToList();
            if (completed.Count < 10) return "";

            var sb = new System.Text.StringBuilder();
            var successRows = completed.Where(r => r.WasSuccessful == true).ToList();
            var failRows = completed.Where(r => r.WasSuccessful == false).ToList();

            sb.AppendLine("| 维度 | 成功组均分 | 失败组均分 | 区分度 | 建议权重 |");
            sb.AppendLine("|------|-----------|-----------|--------|---------|");

            var dims = new (string Name, Func<AdviceRecord, double> Scorer)[]
            {
                ("技术面", r => r.TechnicalScore),
                ("基本面", r => r.FundamentalScore),
                ("资金面", r => r.FundFlowScore),
                ("情绪面", r => r.SentimentScore),
                ("趋势强度", r => r.TrendStrengthScore),
                ("估值", r => r.ValueScore),
                ("板块联动", r => r.SectorStrengthScore),
                ("多周期共振", r => r.MultiTimeframeScore),
                ("背离信号", r => r.DivergenceScore)
            };

            foreach (var (name, scorer) in dims)
            {
                double sAvg = successRows.Average(scorer);
                double fAvg = failRows.Average(scorer);
                double disc = Math.Abs(sAvg - fAvg);
                string weightSuggestion = disc > 10 ? "↑ 提升" : (disc > 5 ? "→ 维持" : "↓ 降低");
                sb.AppendLine($"| {name} | {sAvg:F1} | {fAvg:F1} | {disc:F1} | {weightSuggestion} |");
            }

            return sb.ToString();
        }
    }

    public class BackTestResult
    {
        public int TotalTrades { get; set; }
        public int SuccessfulTrades { get; set; }
        public double WinRate { get; set; }
        public double AverageProfit { get; set; }
        public double TotalProfit { get; set; }
        public double MaxProfit { get; set; }
        public double MaxLoss { get; set; }
        public List<ScoreRangeAnalysis> ScoreRangeAnalysis { get; set; } = new();
        public List<MarketConditionAnalysis> MarketConditionAnalysis { get; set; } = new();
        public DateTime AnalysisDate { get; set; }
        public int? PeriodDays { get; set; }

        public string GetSummary()
        {
            string period = PeriodDays.HasValue ? $"最近{PeriodDays}天" : "全部历史";
            return $"[{period}] 总交易:{TotalTrades} | 成功:{SuccessfulTrades} | 胜率:{WinRate:F1}% | " +
                   $"平均盈亏:{AverageProfit:F2}% | 总盈亏:{TotalProfit:F2}%";
        }

        public string GetDetailedReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📊 回测分析报告 - {AnalysisDate:yyyy-MM-dd}");
            sb.AppendLine($"📈 统计周期: {GetSummary()}");
            sb.AppendLine($"🎯 最优表现: +{MaxProfit:F2}% | 🛑 最差表现: {MaxLoss:F2}%");
            sb.AppendLine();

            if (ScoreRangeAnalysis != null && ScoreRangeAnalysis.Count > 0)
            {
                sb.AppendLine("📊 评分区间分析:");
                foreach (var range in ScoreRangeAnalysis.OrderByDescending(r => r.WinRate))
                {
                    sb.AppendLine($"  {range.ScoreRange}: {range.TradeCount}次交易 | " +
                                 $"胜率{range.WinRate:F1}% | 平均盈亏{range.AverageProfit:F2}%");
                }
                sb.AppendLine();
            }

            if (MarketConditionAnalysis != null && MarketConditionAnalysis.Count > 0)
            {
                sb.AppendLine("🌍 市场环境影响分析:");
                foreach (var market in MarketConditionAnalysis.OrderByDescending(m => m.WinRate))
                {
                    sb.AppendLine($"  {market.MarketCondition}: {market.TradeCount}次交易 | " +
                                 $"胜率{market.WinRate:F1}% | 平均盈亏{market.AverageProfit:F2}%");
                }
            }

            return sb.ToString();
        }
    }

    public class ScoreRangeAnalysis
    {
        public string ScoreRange { get; set; } = string.Empty;
        public int TradeCount { get; set; }
        public int SuccessCount { get; set; }
        public double WinRate { get; set; }
        public double AverageProfit { get; set; }
    }

    public class MarketConditionAnalysis
    {
        public string MarketCondition { get; set; } = string.Empty;
        public int TradeCount { get; set; }
        public int SuccessCount { get; set; }
        public double WinRate { get; set; }
        public double AverageProfit { get; set; }
    }
}