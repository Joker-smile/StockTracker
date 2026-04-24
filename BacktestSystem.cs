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
            public string Action { get; set; } = string.Empty;           // buy/sell/hold
            public decimal RecommendedPrice { get; set; }
            public decimal StopLossPrice { get; set; }
            public decimal TargetPrice { get; set; }
            public double ExpectedWinRate { get; set; }  // 预期胜率
            public double OverallScore { get; set; }     // 综合评分
            public double TechnicalScore { get; set; }   // 技术面评分
            public double FundamentalScore { get; set; } // 基本面评分
            public double FundFlowScore { get; set; }    // 资金面评分
            public MarketCondition MarketCondition { get; set; } // 市场环境

            // 后续验证
            public decimal? ActualHighestPrice { get; set; }  // 建议后最高价
            public decimal? ActualLowestPrice { get; set; }   // 建议后最低价
            public decimal? CurrentPrice { get; set; }        // 当前价格
            public DateTime? VerifyDate { get; set; }         // 验证日期
            public bool? WasSuccessful { get; set; }          // 是否成功
            public decimal? ActualProfitLoss { get; set; }    // 实际盈亏%
            public string SuccessReason { get; set; } = string.Empty; // 成功/失败原因
            public int HoldingDays { get; set; }             // 持有天数
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
        /// 记录性能日志
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
        /// 贝叶斯权重调整数据结构
        /// </summary>
        public class BayesianWeightAdjustments
        {
            public double TechnicalMultiplier { get; set; } = 1.0;
            public double FundamentalMultiplier { get; set; } = 1.0;
            public double FundFlowMultiplier { get; set; } = 1.0;
            public double SentimentMultiplier { get; set; } = 1.0;
            public double TrendMultiplier { get; set; } = 1.0;
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
        /// 更新贝叶斯权重（基于历史胜负，调整各维度权重）
        /// </summary>
        private static void UpdateBayesianWeights()
        {
            try
            {
                var records = LoadRecords();
                var completedRecords = records.Where(r => r.WasSuccessful.HasValue).ToList();
                if (completedRecords.Count < 10) return; // 至少10笔交易

                var adjustment = new BayesianWeightAdjustments();

                // 分析成功交易的技术面评分特征
                var successRecords = completedRecords.Where(r => r.WasSuccessful == true).ToList();
                var failRecords = completedRecords.Where(r => r.WasSuccessful == false).ToList();

                if (successRecords.Count > 0 && failRecords.Count > 0)
                {
                    // 成功交易各维度平均分 vs 失败交易各维度平均分
                    double successTech = successRecords.Average(r => r.TechnicalScore);
                    double failTech = failRecords.Average(r => r.TechnicalScore);
                    double successFund = successRecords.Average(r => r.FundamentalScore);
                    double failFund = failRecords.Average(r => r.FundamentalScore);
                    double successFlow = successRecords.Average(r => r.FundFlowScore);
                    double failFlow = failRecords.Average(r => r.FundFlowScore);

                    // 区分度越大，该维度权重越高
                    // 基础权重 * 区分度系数
                    double techDiscrimination = Math.Abs(successTech - failTech);
                    double fundDiscrimination = Math.Abs(successFund - failFund);
                    double flowDiscrimination = Math.Abs(successFlow - failFlow);

                    // 归一化区分度并映射到1±0.3
                    double maxDisc = Math.Max(techDiscrimination, Math.Max(fundDiscrimination, flowDiscrimination));
                    maxDisc = Math.Max(maxDisc, 1);

                    adjustment.TechnicalMultiplier = 1.0 + (techDiscrimination / maxDisc - 0.33) * 0.6;
                    adjustment.FundamentalMultiplier = 1.0 + (fundDiscrimination / maxDisc - 0.33) * 0.6;
                    adjustment.FundFlowMultiplier = 1.0 + (flowDiscrimination / maxDisc - 0.33) * 0.6;

                    // 限制范围
                    adjustment.TechnicalMultiplier = Math.Max(0.7, Math.Min(1.3, adjustment.TechnicalMultiplier));
                    adjustment.FundamentalMultiplier = Math.Max(0.7, Math.Min(1.3, adjustment.FundamentalMultiplier));
                    adjustment.FundFlowMultiplier = Math.Max(0.7, Math.Min(1.3, adjustment.FundFlowMultiplier));
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
        /// 按维度分析回测胜率（用于AI Prompt优化）
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

            var dims = new[] {
                ("技术面", (Func<AdviceRecord,double>)(r => r.TechnicalScore)),
                ("基本面", r => r.FundamentalScore),
                ("资金面", r => r.FundFlowScore)
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