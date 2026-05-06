using System;
using System.Collections.Generic;
using System.Linq;

namespace StockTracker
{
    /// <summary>
    /// 高胜率策略系统 - 目标胜率80%+
    /// </summary>
    public class HighWinRateStrategies
    {
        /// <summary>
        /// 精准择时评分
        /// </summary>
        public class TimingScore
        {
            public double IntradayScore { get; set; }       // 日内时机评分 (0-100)
            public double VolumePriceScore { get; set; }    // 量价配合评分 (0-100)
            public double EmotionScore { get; set; }        // 情绪指标评分 (0-100)
            public double EventScore { get; set; }          // 事件驱动评分 (0-100)
            public double OverallTimingScore { get; set; }  // 综合择时评分 (0-100)

            public string BestTimingWindow { get; set; } = string.Empty; // 最佳买入时间窗口
            public List<string> TimingSignals { get; set; } = new();     // 择时信号
            public List<string> TimingRisks { get; set; } = new();       // 择时风险
        }

        /// <summary>
        /// 组合优化建议（增强版：行业相关性 + 集中度风控）
        /// </summary>
        public class PortfolioOptimization
        {
            public class StockAllocation
            {
                public string StockCode { get; set; } = string.Empty;
                public string StockName { get; set; } = string.Empty;
                public string Sector { get; set; } = string.Empty;        // 所属行业
                public double OptimalPosition { get; set; }              // 最优仓位比例
                public double CorrelationRisk { get; set; }              // 相关性风险
                public string AllocationReason { get; set; } = string.Empty;
            }

            public List<StockAllocation> Allocations { get; set; } = new();
            public double TotalExpectedReturn { get; set; }
            public double PortfolioRisk { get; set; }
            public double SharpeRatio { get; set; }
            public double MaxDrawdownWarning { get; set; }               // 最大回撤预警线
            public List<string> RiskWarnings { get; set; } = new();      // 组合级风险警告
            public string OptimizationStrategy { get; set; } = string.Empty;
        }

        /// <summary>
        /// 组合风险监控器 - 追踪最大回撤与集中度风险
        /// </summary>
        public class PortfolioRiskMonitor
        {
            public double InitialEquity { get; set; }                    // 初始资产
            public double CurrentEquity { get; set; }                    // 当前资产
            public double PeakEquity { get; set; }                       // 历史峰值
            public double CurrentDrawdown { get; set; }                  // 当前回撤(%)
            public double MaxDrawdown { get; set; }                      // 历史最大回撤(%)
            public DateTime LastUpdate { get; set; }
            public List<string> Alerts { get; set; } = new();            // 风控告警

            public void UpdateEquity(double equity)
            {
                CurrentEquity = equity;
                if (equity > PeakEquity)
                {
                    PeakEquity = equity;
                    CurrentDrawdown = 0;
                }
                else
                {
                    CurrentDrawdown = (PeakEquity - equity) / PeakEquity * 100;
                    MaxDrawdown = Math.Max(MaxDrawdown, CurrentDrawdown);
                }

                // 预警触发
                if (CurrentDrawdown > 15 && CurrentDrawdown <= 20)
                    Alerts.Add($"⚠️ 回撤{CurrentDrawdown:F1}% > 15%，建议减仓防御");
                else if (CurrentDrawdown > 20)
                    Alerts.Add($"🔴 回撤{CurrentDrawdown:F1}% > 20%，强烈建议减仓至半仓以下");
                else if (CurrentDrawdown > 10)
                    Alerts.Add($"🟡 回撤{CurrentDrawdown:F1}% > 10%，关注风险敞口");

                LastUpdate = DateTime.Now;
            }

            public string GetDrawdownReport()
            {
                return $"当前回撤: {CurrentDrawdown:F1}% | 最大回撤: {MaxDrawdown:F1}% | 告警数: {Alerts.Count}";
            }
        }

        /// <summary>
        /// 智能止损止盈
        /// </summary>
        public class SmartStopLoss
        {
            public decimal DynamicStopLoss { get; set; }      // 动态止损价
            public decimal TrailingStop { get; set; }         // 移动止损价
            public decimal TimeStopLoss { get; set; }         // 时间止损价
            public decimal TargetPrice1 { get; set; }         // 第一目标价
            public decimal TargetPrice2 { get; set; }         // 第二目标价
            public decimal TargetPrice3 { get; set; }         // 第三目标价

            public List<string> ExitSignals { get; set; } = new();  // 离场信号
            public List<string> HoldSignals { get; set; } = new();  // 持有信号
            public string ExitStrategy { get; set; } = string.Empty; // 离场策略
        }

        /// <summary>
        /// 计算精准择时评分（增强版：含背离调整 + 板块联动）
        /// </summary>
        public static TimingScore CalculateTimingScore(
            StockDeepAnalysisContext ctx,
            List<double> recentPrices,  // 最近5日价格
            List<double> recentVolumes)  // 最近5日成交量
        {
            var score = new TimingScore();

            // 1. 日内时机评分 (25分)
            score.IntradayScore = CalculateIntradayTiming(ctx, recentPrices);

            // 2. 量价配合评分 (30分)
            score.VolumePriceScore = CalculateVolumePriceTiming(ctx, recentPrices, recentVolumes);

            // 3. 情绪指标评分 (25分)
            score.EmotionScore = CalculateEmotionTiming(ctx);

            // 4. 事件驱动评分 (20分)
            score.EventScore = CalculateEventTiming(ctx);

            // 综合择时评分
            score.OverallTimingScore =
                score.IntradayScore * 0.25 +
                score.VolumePriceScore * 0.30 +
                score.EmotionScore * 0.25 +
                score.EventScore * 0.20;

            // === 背离调整（高权重） ===
            if (ctx.TechScore != null)
            {
                if (ctx.TechScore.HasBearishDivergence)
                    score.OverallTimingScore *= 0.6; // 顶背离扣40%
                if (ctx.TechScore.HasBullishDivergence)
                    score.OverallTimingScore *= 1.25; // 底背离加25%
                if (ctx.TechScore.HasVolumePriceDivergence)
                    score.OverallTimingScore *= 0.75; // 量价背离扣25%
            }

            // 板块联动调整
            if (ctx.SectorPctChange < -2)
                score.OverallTimingScore *= 0.85; // 板块走弱
            if (ctx.RelativeStrengthVsSector > 2)
                score.OverallTimingScore *= 1.1; // 强势领涨

            // 波动率调整
            if (ctx.VolatilityPercentile > 85)
                score.OverallTimingScore *= 0.85; // 极高波动降低择时可信度

            score.OverallTimingScore = Math.Max(0, Math.Min(100, score.OverallTimingScore));

            // 生成择时建议
            GenerateTimingAdvice(score, ctx);

            return score;
        }

        /// <summary>
        /// 计算日内时机评分
        /// </summary>
        private static double CalculateIntradayTiming(StockDeepAnalysisContext ctx, List<double> recentPrices)
        {
            double score = 0;

            if (recentPrices.Count < 2) return 50;

            double currentPrice = ctx.CurrentPrice;
            double yesterdayPrice = recentPrices[^2]; // 倒数第二个
            double dayChange = (currentPrice - yesterdayPrice) / yesterdayPrice * 100;

            // 1. 价格位置评分 (15分)
            if (dayChange > -1 && dayChange < 1) score += 15; // 平盘附近最佳
            else if (dayChange > 1 && dayChange < 2) score += 10; // 小幅上涨可接受
            else if (dayChange < -1 && dayChange > -2) score += 8;  // 小幅下跌可抄底
            else if (dayChange > 3) score -= 10; // 大涨不追
            else if (dayChange < -3) score -= 5; // 大跌观望

            // 2. 乖离率评分 (10分)
            if (ctx.BiasMA5 > 0 && ctx.BiasMA5 < 1) score += 10; // 理想乖离
            else if (ctx.BiasMA5 >= 1 && ctx.BiasMA5 < 2) score += 7;
            else if (ctx.BiasMA5 < 0 && ctx.BiasMA5 > -1) score += 5;
            else if (ctx.BiasMA5 > 3) score -= 10; // 乖离过大

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 计算量价配合评分
        /// </summary>
        private static double CalculateVolumePriceTiming(
            StockDeepAnalysisContext ctx,
            List<double> recentPrices,
            List<double> recentVolumes)
        {
            double score = 0;

            if (recentPrices.Count < 3 || recentVolumes.Count < 3) return 50;

            // 1. 量价关系分析 (20分)
            double priceChange = (recentPrices[^1] - recentPrices[^2]) / recentPrices[^2] * 100;
            double volumeChange = recentVolumes.Count > 1 ?
                (recentVolumes[^1] - recentVolumes[^2]) / recentVolumes[^2] * 100 : 0;

            // 量价齐升 (理想状态)
            if (priceChange > 0.5 && volumeChange > 10) score += 20;
            // 价涨量稳 (良好)
            else if (priceChange > 0.5 && volumeChange > -5 && volumeChange < 10) score += 15;
            // 价跌量缩 (可能筑底)
            else if (priceChange < -0.5 && volumeChange < -10) score += 10;
            // 价涨量缩 (量价背离)
            else if (priceChange > 1 && volumeChange < -10) score -= 10;
            // 价跌量增 (下跌放量)
            else if (priceChange < -1 && volumeChange > 20) score -= 15;

            // 2. 量比分析 (10分)
            if (ctx.VolumeRatio > 1.2 && ctx.VolumeRatio < 2.0) score += 10;
            else if (ctx.VolumeRatio >= 2.0 && ctx.VolumeRatio < 3.0) score += 7;
            else if (ctx.VolumeRatio < 0.8) score -= 5; // 缩量观望

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 计算情绪时机评分
        /// </summary>
        private static double CalculateEmotionTiming(StockDeepAnalysisContext ctx)
        {
            double score = 50; // 基础分

            // 1. 获利盘比例 (40分)
            if (ctx.ProfitRatio > 30 && ctx.ProfitRatio < 60) score += 20; // 健康状态
            else if (ctx.ProfitRatio >= 60 && ctx.ProfitRatio < 75) score += 10;
            else if (ctx.ProfitRatio >= 75) score -= 15; // 获利盘过多
            else if (ctx.ProfitRatio < 25) score += 15; // 深套可能是机会

            // 2. 换手率情绪 (30分)
            if (ctx.TurnoverRate > 5 && ctx.TurnoverRate < 12) score += 15; // 活跃但不疯狂
            else if (ctx.TurnoverRate >= 12 && ctx.TurnoverRate < 20) score += 5;
            else if (ctx.TurnoverRate >= 20) score -= 10; // 过热

            // 3. 主力情绪 (30分) - f62 单位为元
            if (ctx.MainForceNetInflow > 10000000) score += 20;      // > 1000万
            else if (ctx.MainForceNetInflow > 0) score += 10;
            else if (ctx.MainForceNetInflow < -10000000) score -= 20; // < -1000万

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 计算事件驱动评分
        /// </summary>
        private static double CalculateEventTiming(StockDeepAnalysisContext ctx)
        {
            double score = 50; // 基础分

            if (ctx.LatestNews.Count == 0) return score;

            // 分析新闻情绪和时间
            foreach (var news in ctx.LatestNews.Take(3))
            {
                // 利好消息
                if (news.Contains("利好") || news.Contains("突破") || news.Contains("大涨"))
                {
                    // 发布后1-2天最佳，避免追高
                    score += 15;
                }
                // 利空消息
                else if (news.Contains("利空") || news.Contains("风险") || news.Contains("下跌"))
                {
                    score -= 20;
                }
                // 中性消息
                else
                {
                    score += 5;
                }
            }

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 生成择时建议
        /// </summary>
        private static void GenerateTimingAdvice(TimingScore score, StockDeepAnalysisContext ctx)
        {
            // 综合择时评分判断
            if (score.OverallTimingScore >= 75)
            {
                score.TimingSignals.Add("🎯 择时机机极佳，建议立即行动");
                score.BestTimingWindow = "当前时间是最佳买入窗口";
            }
            else if (score.OverallTimingScore >= 60)
            {
                score.TimingSignals.Add("✅ 择时机机良好，可以分批建仓");
                score.BestTimingWindow = "当前时间可建仓，等待更好时机加仓";
            }
            else if (score.OverallTimingScore >= 45)
            {
                score.TimingSignals.Add("⏰ 择时一般，建议等待更好时机");
                score.BestTimingWindow = "建议等待回调或放量突破时再入场";
            }
            else
            {
                score.TimingRisks.Add("⚠️ 当前择时机机较差，建议观望");
                score.BestTimingWindow = "不是好的入场时机，建议等待";
            }

            // 具体择时建议
            if (score.IntradayScore < 50)
            {
                score.TimingRisks.Add("日内时机不佳，建议等待收盘前决策");
            }
            if (score.VolumePriceScore < 50)
            {
                score.TimingRisks.Add("量价配合不佳，建议等待放量确认");
            }
            if (score.EmotionScore < 40)
            {
                score.TimingRisks.Add("市场情绪不佳，建议等待情绪修复");
            }
        }

        /// <summary>
        /// 计算智能止损止盈（含筹码峰支撑、时间止损、回撤保护）
        /// </summary>
        public static SmartStopLoss CalculateSmartStopLoss(
            StockDeepAnalysisContext ctx,
            ImprovedWinRateScoring.EnhancedStockScore score,
            List<double> recentPrices)
        {
            var stopLoss = new SmartStopLoss();

            double currentPrice = ctx.CurrentPrice;
            double atr = CalculateATR(recentPrices, 14, currentPrice); // 平均真实波幅

            // 1. 动态止损 (基于ATR + 筹码峰支撑融合)
            double stopLossMultiplier = 2.0;
            if (score.RiskScore >= 70) stopLossMultiplier = 1.5;
            else if (score.RiskScore < 50) stopLossMultiplier = 2.5;

            double atrStop = currentPrice - atr * stopLossMultiplier;

            // 融合筹码峰支撑位：取ATR止损和筹码峰支撑的较优者
            if (ctx.ChipPeakSupport > 0 && ctx.ChipPeakSupport < currentPrice)
            {
                // 筹码峰支撑在ATR止损上方 → 使用筹码峰支撑（更紧的保护）
                // 筹码峰支撑在ATR止损下方 → 使用筹码峰支撑下方1%作为宽止损
                if (ctx.ChipPeakSupport > atrStop)
                    stopLoss.DynamicStopLoss = (decimal)(ctx.ChipPeakSupport * 0.99); // 略低于筹码峰支撑
                else
                    stopLoss.DynamicStopLoss = (decimal)Math.Min(atrStop, ctx.ChipPeakSupport * 0.97);
            }
            else
            {
                stopLoss.DynamicStopLoss = (decimal)atrStop;
            }

            // 2. 移动止损 (保护利润) - 基于回撤保护
            stopLoss.TrailingStop = (decimal)(currentPrice * 0.93); // 初始7%回撤保护（更紧）

            // 3. 时间止损价 (持有超过指定天数未达标时触发)
            // 以买入价上下2%为时间止损触发区
            stopLoss.TimeStopLoss = (decimal)(currentPrice * 0.98); // 2%成本保本线

            // 4. 分批止盈目标（融合筹码峰压力位）
            double riskAmount = Math.Abs(currentPrice - (double)stopLoss.DynamicStopLoss);

            // 第一目标: 风险1.5倍或筹码峰压力位（取较小值）
            double target1Candidate = currentPrice + riskAmount * 1.5;
            if (ctx.ChipPeakPressure > currentPrice && ctx.ChipPeakPressure < target1Candidate)
            {
                stopLoss.TargetPrice1 = (decimal)(ctx.ChipPeakPressure * 0.99); // 筹码峰压力位下方
            }
            else
            {
                stopLoss.TargetPrice1 = (decimal)Math.Min(target1Candidate, currentPrice * 1.08);
            }

            // 第二目标: 风险的2.5倍或MA60偏离
            double target2Candidate = currentPrice + riskAmount * 2.5;
            stopLoss.TargetPrice2 = (decimal)Math.Min(target2Candidate, currentPrice * 1.15);

            // 第三目标: 风险4倍（长线目标）
            stopLoss.TargetPrice3 = (decimal)Math.Min(currentPrice + riskAmount * 4.0, currentPrice * 1.25);

            // 生成离场策略
            GenerateExitStrategy(stopLoss, ctx, score);

            return stopLoss;
        }

        /// <summary>
        /// 计算ATR (平均真实波幅)
        /// </summary>
        private static double CalculateATR(List<double> prices, int period, double currentPrice)
        {
            if (prices == null || prices.Count == 0) return currentPrice * 0.03; // 默认3%波动
            if (prices.Count < period + 1) return prices[^1] * 0.03; // 默认3%波动

            var trueRanges = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                double high = prices[i];
                double low = prices[i];
                double prevClose = prices[i - 1];

                double tr = Math.Max(high - low, Math.Abs(high - prevClose));
                tr = Math.Max(tr, Math.Abs(low - prevClose));
                trueRanges.Add(tr);
            }

            return trueRanges.TakeLast(period).Average();
        }

        /// <summary>
        /// 生成离场策略（增强版：含时间止损 + 回撤保护 + 筹码峰）
        /// </summary>
        private static void GenerateExitStrategy(
            SmartStopLoss stopLoss,
            StockDeepAnalysisContext ctx,
            ImprovedWinRateScoring.EnhancedStockScore score)
        {
            var strategy = new System.Text.StringBuilder();

            strategy.AppendLine("分批止盈策略:");
            strategy.AppendLine($"- 第一目标({stopLoss.TargetPrice1:F2}): 减仓30%");
            strategy.AppendLine($"- 第二目标({stopLoss.TargetPrice2:F2}): 减仓30%");
            strategy.AppendLine($"- 第三目标({stopLoss.TargetPrice3:F2}): 减仓20%");
            strategy.AppendLine($"- 保留20%作为长期持仓");

            strategy.AppendLine("\n止损策略:");
            strategy.AppendLine($"- ATR+筹码峰止损: {stopLoss.DynamicStopLoss:F2} (严格执行)");
            strategy.AppendLine($"- 移动止损: 价格上涨后动态上移，回撤7%触发");
            strategy.AppendLine($"- 时间止损: 持有>15天涨幅<3%，止损价{stopLoss.TimeStopLoss:F2}（保本出场）");

            // 筹码峰相关信号
            if (ctx.ChipPeakPressure > 0 && ctx.CurrentPrice < ctx.ChipPeakPressure * 0.97)
            {
                stopLoss.ExitSignals.Add($"⚠️ 上方筹码峰压力{ctx.ChipPeakPressure:F2}，到达后必须减仓50%");
            }

            // 背离信号优先级最高
            if (ctx.TechScore?.HasBearishDivergence == true)
            {
                stopLoss.ExitSignals.Add("🔴 MACD顶背离！立即减仓或清仓");
            }

            if (ctx.ProfitRatio > 80)
            {
                stopLoss.ExitSignals.Add("⚠️ 获利盘>80%，达到第一目标后立即减仓50%");
            }

            if (ctx.ProfitRatio > 90)
            {
                stopLoss.ExitSignals.Add("🔴 获利盘>90%，极度危险，建议立即减仓");
            }

            if (ctx.TurnoverRate > 15)
            {
                stopLoss.ExitSignals.Add("⚠️ 换手率过高，建议短期操作，不宜长期持有");
            }

            if (score.RiskScore < 50)
            {
                stopLoss.ExitSignals.Add("⚠️ 风险评分较低，建议严格执行止损，不可侥幸");
            }

            // 回撤保护信号
            stopLoss.HoldSignals.Add("持仓期间若盈利回吐>5%，立即止盈50%");
            stopLoss.HoldSignals.Add("连续3天不创新高，减仓30%锁定利润");

            stopLoss.ExitStrategy = strategy.ToString();
        }

        /// <summary>
        /// 组合优化建议（增强版：行业相关性 + 板块集中度 + 回撤预警）
        /// </summary>
        public static PortfolioOptimization OptimizePortfolio(
            List<ImprovedWinRateScoring.EnhancedStockScore> scores,
            MarketCondition marketCondition,
            Dictionary<string, string>? stockSectors = null,
            PortfolioRiskMonitor? riskMonitor = null)
        {
            var optimization = new PortfolioOptimization();

            // 筛选高质量股票
            var highQualityStocks = scores
                .Where(s => s.OverallScore >= 65 && s.ConfidenceLevel >= 45)
                .OrderByDescending(s => s.OverallScore)
                .Take(5)
                .ToList();

            if (highQualityStocks.Count == 0)
            {
                optimization.OptimizationStrategy = "当前没有符合条件的股票，建议空仓观望";
                return optimization;
            }

            // === 行业集中度分析（基于板块分组） ===
            var sectorGroups = new Dictionary<string, List<ImprovedWinRateScoring.EnhancedStockScore>>();
            foreach (var stock in highQualityStocks)
            {
                string sector = stockSectors?.ContainsKey(stock.StockCode) == true
                    ? stockSectors[stock.StockCode] : "未知";
                if (!sectorGroups.ContainsKey(sector))
                    sectorGroups[sector] = new List<ImprovedWinRateScoring.EnhancedStockScore>();
                sectorGroups[sector].Add(stock);
            }

            // 单行业仓位上限：普通25%，强势市场30%，弱势市场20%
            double maxSectorWeight = marketCondition switch
            {
                MarketCondition.Strong => 0.30,
                MarketCondition.Weak => 0.20,
                MarketCondition.Crash => 0.0,
                _ => 0.25
            };

            // 总仓位上限
            double maxTotalWeight = marketCondition switch
            {
                MarketCondition.Strong => 0.85,
                MarketCondition.Neutral => 0.70,
                MarketCondition.Weak => 0.50,
                MarketCondition.Crash => 0.0,
                _ => 0.60
            };

            // 分配仓位：评分加权，受行业集中度约束
            double totalScore = highQualityStocks.Sum(s => s.OverallScore);
            var sectorUsedWeights = new Dictionary<string, double>();

            foreach (var stock in highQualityStocks)
            {
                string sector = stockSectors?.ContainsKey(stock.StockCode) == true
                    ? stockSectors[stock.StockCode] : "未知";
                if (!sectorUsedWeights.ContainsKey(sector))
                    sectorUsedWeights[sector] = 0;

                // 评分权重
                double weight = stock.OverallScore / totalScore * maxTotalWeight;

                // 行业集中度约束
                double remainingSectorBudget = maxSectorWeight - sectorUsedWeights[sector];
                if (weight > remainingSectorBudget)
                    weight = Math.Max(0, remainingSectorBudget);

                // 个股单一仓位上限（不超过15%）
                weight = Math.Min(weight, 0.15);

                sectorUsedWeights[sector] += weight;

                var allocation = new PortfolioOptimization.StockAllocation
                {
                    StockCode = stock.StockCode,
                    StockName = stock.StockName,
                    Sector = sector,
                    OptimalPosition = weight * 100,
                    CorrelationRisk = CalculateCorrelationRisk(stock, sector, sectorGroups),
                    AllocationReason = GenerateAllocationReason(stock, marketCondition)
                };

                optimization.Allocations.Add(allocation);
            }

            // 计算组合预期收益和风险
            optimization.TotalExpectedReturn = highQualityStocks.Average(s => s.WinProbability);
            optimization.PortfolioRisk = CalculatePortfolioRisk(highQualityStocks, sectorGroups);
            optimization.SharpeRatio = CalculateSharpeRatio(optimization);

            // === 最大回撤预警 ===
            if (riskMonitor != null)
            {
                optimization.MaxDrawdownWarning = riskMonitor.MaxDrawdown;
                optimization.RiskWarnings.AddRange(riskMonitor.Alerts);
            }

            // 组合级风险警告
            optimization.RiskWarnings.AddRange(GeneratePortfolioRiskWarnings(optimization, marketCondition, sectorGroups));

            // 生成优化策略
            optimization.OptimizationStrategy = GenerateOptimizationStrategy(optimization, marketCondition);

            return optimization;
        }

        /// <summary>
        /// 计算相关性风险（基于行业 + 评分）
        /// </summary>
        private static double CalculateCorrelationRisk(
            ImprovedWinRateScoring.EnhancedStockScore stock,
            string sector,
            Dictionary<string, List<ImprovedWinRateScoring.EnhancedStockScore>> sectorGroups)
        {
            double risk = 50;

            // 同行业股票越多 → 相关性风险越高
            if (sectorGroups.ContainsKey(sector) && sectorGroups[sector].Count > 1)
                risk += 15 * (sectorGroups[sector].Count - 1);

            // 评分因子
            if (stock.FundFlowScore > 80) risk -= 10;
            if (stock.TechnicalScore > 80) risk -= 10;
            if (stock.RiskScore < 50) risk += 20;

            // 北向资金持股高 → 与外资同向波动风险
            risk = Math.Max(0, Math.Min(100, risk));
            return risk;
        }

        /// <summary>
        /// 生成组合级风险警告
        /// </summary>
        private static List<string> GeneratePortfolioRiskWarnings(
            PortfolioOptimization optimization,
            MarketCondition marketCondition,
            Dictionary<string, List<ImprovedWinRateScoring.EnhancedStockScore>> sectorGroups)
        {
            var warnings = new List<string>();

            // 行业集中度过高
            foreach (var kvp in sectorGroups)
            {
                double sectorTotalWeight = optimization.Allocations
                    .Where(a => a.Sector == kvp.Key)
                    .Sum(a => a.OptimalPosition);
                if (sectorTotalWeight > 25)
                    warnings.Add($"⚠️ {kvp.Key}板块总仓位{sectorTotalWeight:F1}% > 25%，集中度偏高");
            }

            // 组合整体敞口
            double totalPos = optimization.Allocations.Sum(a => a.OptimalPosition);
            if (totalPos > 80)
                warnings.Add($"⚠️ 组合总仓位{totalPos:F1}% > 80%，风险敞口偏大");
            else if (totalPos < 20)
                warnings.Add($"🟡 组合总仓位{totalPos:F1}% < 20%，过于保守");

            // 市场环境匹配
            if (marketCondition == MarketCondition.Weak && totalPos > 50)
                warnings.Add("🔴 弱势市场总仓位>50%，建议降低至半仓以下");
            else if (marketCondition == MarketCondition.Strong && totalPos < 30)
                warnings.Add("🟡 强势市场仓位不足30%，可适当增加敞口");

            // 回撤预警
            if (optimization.MaxDrawdownWarning > 15)
                warnings.Add($"🔴 最大回撤{optimization.MaxDrawdownWarning:F1}% > 15%，建议全面风控审查");

            return warnings;
        }

        /// <summary>
        /// 生成仓位分配理由
        /// </summary>
        private static string GenerateAllocationReason(
            ImprovedWinRateScoring.EnhancedStockScore stock,
            MarketCondition marketCondition)
        {
            var reason = new System.Text.StringBuilder();

            reason.Append($"综合评分{stock.OverallScore:F1}分");

            if (stock.WinProbability >= 70)
                reason.Append($", 预期胜率{stock.WinProbability:F1}%");

            if (stock.ConfidenceLevel >= 60)
                reason.Append($", 置信度{stock.ConfidenceLevel:F1}%");

            if (marketCondition == MarketCondition.Strong && stock.TechnicalScore > 70)
                reason.Append(", 强势市场技术面优势");

            if (marketCondition == MarketCondition.Weak && stock.FundamentalScore > 70)
                reason.Append(", 弱势市场基本面护城河");

            return reason.ToString();
        }

        /// <summary>
        /// 计算组合风险（综合行业集中度+评分）
        /// </summary>
        private static double CalculatePortfolioRisk(
            List<ImprovedWinRateScoring.EnhancedStockScore> stocks,
            Dictionary<string, List<ImprovedWinRateScoring.EnhancedStockScore>> sectorGroups)
        {
            double avgRiskScore = stocks.Average(s => s.RiskScore);
            double sectorConcentrationPenalty = 0;

            // 行业集中度惩罚
            foreach (var kvp in sectorGroups)
            {
                double sectorWeight = (double)kvp.Value.Count / stocks.Count;
                if (sectorWeight > 0.4)
                    sectorConcentrationPenalty += (sectorWeight - 0.4) * 50;
            }

            return Math.Max(0, Math.Min(100, 100 - avgRiskScore + sectorConcentrationPenalty));
        }

        /// <summary>
        /// 计算夏普比率
        /// </summary>
        private static double CalculateSharpeRatio(PortfolioOptimization optimization)
        {
            double expectedReturn = optimization.TotalExpectedReturn;
            double portfolioRisk = optimization.PortfolioRisk;

            if (portfolioRisk == 0) return 0;

            double riskFreeRate = 3.0;
            return (expectedReturn - riskFreeRate) / portfolioRisk;
        }

        /// <summary>
        /// 生成优化策略描述
        /// </summary>
        private static string GenerateOptimizationStrategy(
            PortfolioOptimization optimization,
            MarketCondition marketCondition)
        {
            var strategy = new System.Text.StringBuilder();

            strategy.AppendLine($"📊 组合优化策略 (市场环境: {marketCondition})");
            strategy.AppendLine($"总仓位建议: {(optimization.Allocations.Sum(a => a.OptimalPosition)):F1}%");
            strategy.AppendLine($"预期胜率: {optimization.TotalExpectedReturn:F1}%");
            strategy.AppendLine($"夏普比率: {optimization.SharpeRatio:F2}");
            strategy.AppendLine($"组合风险评分: {optimization.PortfolioRisk:F1}/100 (越低越安全)");

            if (optimization.MaxDrawdownWarning > 0)
                strategy.AppendLine($"最大回撤记录: {optimization.MaxDrawdownWarning:F1}%");

            strategy.AppendLine();

            // 仓位分配
            strategy.AppendLine("**分仓建议 (含行业集中度控制)**");
            var sectorGroups = optimization.Allocations.GroupBy(a => a.Sector);
            foreach (var group in sectorGroups)
            {
                string sectorWeight = group.Sum(a => a.OptimalPosition).ToString("F1");
                strategy.AppendLine($"- 📂 **{group.Key}** (总仓位 {sectorWeight}%):");
                foreach (var allocation in group)
                {
                    strategy.AppendLine($"  - {allocation.StockName}({allocation.StockCode}): {allocation.OptimalPosition:F1}% " +
                                       $"({allocation.AllocationReason})");
                }
            }

            // 组合风险警告
            if (optimization.RiskWarnings.Count > 0)
            {
                strategy.AppendLine("\n**⚠️ 组合风险警告**");
                foreach (var warning in optimization.RiskWarnings)
                {
                    strategy.AppendLine($"  {warning}");
                }
            }

            if (marketCondition == MarketCondition.Crash)
            {
                strategy.AppendLine("\n🔴 暴跌环境，建议严格执行仓位控制，优先防守");
            }
            else if (marketCondition == MarketCondition.Weak)
            {
                strategy.AppendLine("\n🟡 弱势环境，建议降低仓位，重点关注防御性品种");
            }
            else if (marketCondition == MarketCondition.Strong)
            {
                strategy.AppendLine("\n🟢 强势环境，可适当提高仓位，把握机会");
            }

            return strategy.ToString();
        }

        /// <summary>
        /// 获取高胜率操作建议
        /// </summary>
        public static string GetHighWinRateAdvice(
            ImprovedWinRateScoring.EnhancedStockScore stockScore,
            TimingScore timingScore,
            SmartStopLoss stopLoss,
            bool includeInPortfolio = true)
        {
            var advice = new System.Text.StringBuilder();

            advice.AppendLine($"🎯 {stockScore.StockName} ({stockScore.StockCode}) - 高胜率操作建议");

            // 综合评分
            double combinedScore = (stockScore.OverallScore * 0.6 + timingScore.OverallTimingScore * 0.4);

            advice.AppendLine($"\n📊 综合评分: {combinedScore:F1}/100");
            advice.AppendLine($"- 股票评分: {stockScore.OverallScore:F1} (置信度: {stockScore.ConfidenceLevel:F1}%)");
            advice.AppendLine($"- 择时评分: {timingScore.OverallTimingScore:F1}");

            // 操作建议
            if (combinedScore >= 80 && stockScore.ConfidenceLevel >= 60)
            {
                advice.AppendLine("\n🟢 强烈推荐买入");
                advice.AppendLine($"- 建议仓位: {stockScore.PositionSize:F1}%");
                advice.AppendLine($"- 买入价格: {stockScore.SuggestedBuyPrice:F2}元");
            }
            else if (combinedScore >= 70 && stockScore.ConfidenceLevel >= 50)
            {
                advice.AppendLine("\n🟡 可以谨慎买入");
                advice.AppendLine($"- 建议仓位: {stockScore.PositionSize * 0.7:F1}% (降低仓位)");
                advice.AppendLine($"- 买入价格: {stockScore.SuggestedBuyPrice:F2}元");
            }
            else
            {
                advice.AppendLine("\n🔴 暂不建议操作");
                advice.AppendLine($"- 建议: {timingScore.BestTimingWindow}");
                return advice.ToString();
            }

            // 择时建议
            advice.AppendLine($"\n⏰ 择时建议: {timingScore.BestTimingWindow}");

            // 止损止盈
            advice.AppendLine("\n🎯 止损止盈策略:");
            advice.AppendLine($"- 动态止损: {stopLoss.DynamicStopLoss:F2}元");
            advice.AppendLine($"- 移动止损: {stopLoss.TrailingStop:F2}元");
            advice.AppendLine($"- 第一目标: {stopLoss.TargetPrice1:F2}元 (减仓30%)");
            advice.AppendLine($"- 第二目标: {stopLoss.TargetPrice2:F2}元 (减仓30%)");
            advice.AppendLine($"- 第三目标: {stopLoss.TargetPrice3:F2}元 (减仓20%)");

            // 关键信号
            if (stockScore.PositiveSignals.Count > 0)
            {
                advice.AppendLine("\n✅ 核心优势:");
                foreach (var signal in stockScore.PositiveSignals.Take(3))
                {
                    advice.AppendLine($"- {signal}");
                }
            }

            if (stockScore.RiskSignals.Count > 0)
            {
                advice.AppendLine("\n⚠️ 风险提示:");
                foreach (var signal in stockScore.RiskSignals.Take(3))
                {
                    advice.AppendLine($"- {signal}");
                }
            }

            if (timingScore.TimingSignals.Count > 0)
            {
                advice.AppendLine("\n🕐 择时信号:");
                foreach (var signal in timingScore.TimingSignals.Take(2))
                {
                    advice.AppendLine($"- {signal}");
                }
            }

            return advice.ToString();
        }
    }
}