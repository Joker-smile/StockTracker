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
        /// 组合优化建议
        /// </summary>
        public class PortfolioOptimization
        {
            public class StockAllocation
            {
                public string StockCode { get; set; } = string.Empty;
                public string StockName { get; set; } = string.Empty;
                public double OptimalPosition { get; set; }     // 最优仓位比例
                public double CorrelationRisk { get; set; }     // 相关性风险
                public string AllocationReason { get; set; } = string.Empty;
            }

            public List<StockAllocation> Allocations { get; set; } = new();
            public double TotalExpectedReturn { get; set; }
            public double PortfolioRisk { get; set; }
            public double SharpeRatio { get; set; }
            public string OptimizationStrategy { get; set; } = string.Empty;
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
        /// 计算精准择时评分
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

            // 3. 主力情绪 (30分)
            if (ctx.MainForceNetInflow > 500) score += 20;
            else if (ctx.MainForceNetInflow > 0) score += 10;
            else if (ctx.MainForceNetInflow < -500) score -= 20;

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
        /// 计算智能止损止盈
        /// </summary>
        public static SmartStopLoss CalculateSmartStopLoss(
            StockDeepAnalysisContext ctx,
            ImprovedWinRateScoring.EnhancedStockScore score,
            List<double> recentPrices)
        {
            var stopLoss = new SmartStopLoss();

            double currentPrice = ctx.CurrentPrice;
            double atr = CalculateATR(recentPrices, 14); // 平均真实波幅

            // 1. 动态止损 (基于ATR)
            double stopLossMultiplier = 2.0; // 2倍ATR止损
            if (score.RiskScore >= 70) stopLossMultiplier = 1.5; // 低风险可用更紧止损
            else if (score.RiskScore < 50) stopLossMultiplier = 2.5; // 高风险需要更宽止损

            stopLoss.DynamicStopLoss = (decimal)(currentPrice - atr * stopLossMultiplier);

            // 2. 移动止损 (保护利润)
            stopLoss.TrailingStop = (decimal)(currentPrice * 0.95); // 初始5%移动止损

            // 3. 时间止损 (持有超过20天未达标)
            // 这需要在实际持有过程中动态调整

            // 4. 分批止盈目标
            double riskAmount = currentPrice - (double)stopLoss.DynamicStopLoss;

            // 第一目标: 风险的1.5倍
            stopLoss.TargetPrice1 = (decimal)(currentPrice + riskAmount * 1.5);
            // 第二目标: 风险的2.5倍
            stopLoss.TargetPrice2 = (decimal)(currentPrice + riskAmount * 2.5);
            // 第三目标: 风险的4倍
            stopLoss.TargetPrice3 = (decimal)(currentPrice + riskAmount * 4.0);

            // 生成离场策略
            GenerateExitStrategy(stopLoss, ctx, score);

            return stopLoss;
        }

        /// <summary>
        /// 计算ATR (平均真实波幅)
        /// </summary>
        private static double CalculateATR(List<double> prices, int period)
        {
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
        /// 生成离场策略
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
            strategy.AppendLine($"- 动态止损: {stopLoss.DynamicStopLoss:F2} (严格执行)");
            strategy.AppendLine($"- 移动止损: 价格上涨后动态上移，保护利润");

            if (ctx.ProfitRatio > 80)
            {
                stopLoss.ExitSignals.Add("⚠️ 获利盘过多，达到第一目标后立即减仓50%");
            }

            if (ctx.TurnoverRate > 15)
            {
                stopLoss.ExitSignals.Add("⚠️ 换手率过高，建议短期操作，不宜长期持有");
            }

            if (score.RiskScore < 50)
            {
                stopLoss.ExitSignals.Add("⚠️ 风险评分较低，建议严格执行止损，不可侥幸");
            }

            stopLoss.ExitStrategy = strategy.ToString();
        }

        /// <summary>
        /// 组合优化建议
        /// </summary>
        public static PortfolioOptimization OptimizePortfolio(
            List<ImprovedWinRateScoring.EnhancedStockScore> scores,
            MarketCondition marketCondition)
        {
            var optimization = new PortfolioOptimization();

            // 筛选高质量股票
            var highQualityStocks = scores
                .Where(s => s.OverallScore >= 70 && s.ConfidenceLevel >= 50)
                .OrderByDescending(s => s.OverallScore)
                .Take(5) // 最多选择5只股票
                .ToList();

            if (highQualityStocks.Count == 0)
            {
                optimization.OptimizationStrategy = "当前没有符合条件的股票，建议空仓观望";
                return optimization;
            }

            // 计算最优仓位分配
            double totalWeight = 0;
            double totalScore = highQualityStocks.Sum(s => s.OverallScore);

            foreach (var stock in highQualityStocks)
            {
                double weight = stock.OverallScore / totalScore;
                double adjustedWeight = weight * 0.8; // 总仓位80%，留20%现金

                var allocation = new PortfolioOptimization.StockAllocation
                {
                    StockCode = stock.StockCode,
                    StockName = stock.StockName,
                    OptimalPosition = adjustedWeight * 100,
                    CorrelationRisk = CalculateCorrelationRisk(stock),
                    AllocationReason = GenerateAllocationReason(stock, marketCondition)
                };

                optimization.Allocations.Add(allocation);
                totalWeight += adjustedWeight;
            }

            // 计算组合预期收益和风险
            optimization.TotalExpectedReturn = highQualityStocks.Average(s => s.WinProbability);
            optimization.PortfolioRisk = CalculatePortfolioRisk(highQualityStocks);
            optimization.SharpeRatio = CalculateSharpeRatio(optimization);

            // 生成优化策略
            optimization.OptimizationStrategy = GenerateOptimizationStrategy(optimization, marketCondition);

            return optimization;
        }

        /// <summary>
        /// 计算相关性风险
        /// </summary>
        private static double CalculateCorrelationRisk(ImprovedWinRateScoring.EnhancedStockScore stock)
        {
            // 简化版相关性风险评估
            double risk = 50;

            // 根据市值、行业等因素调整
            if (stock.FundFlowScore > 80) risk -= 10; // 资金流向好降低相关性风险
            if (stock.TechnicalScore > 80) risk -= 10; // 技术面好降低相关性风险
            if (stock.RiskScore < 50) risk += 20; // 风险高增加相关性风险

            return Math.Max(0, Math.Min(100, risk));
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
        /// 计算组合风险
        /// </summary>
        private static double CalculatePortfolioRisk(List<ImprovedWinRateScoring.EnhancedStockScore> stocks)
        {
            // 简化版组合风险计算
            double avgRiskScore = stocks.Average(s => s.RiskScore);
            return 100 - avgRiskScore; // 风险评分越高，组合风险越低
        }

        /// <summary>
        /// 计算夏普比率
        /// </summary>
        private static double CalculateSharpeRatio(PortfolioOptimization optimization)
        {
            // 简化版夏普比率计算
            double expectedReturn = optimization.TotalExpectedReturn;
            double portfolioRisk = optimization.PortfolioRisk;

            if (portfolioRisk == 0) return 0;

            // 假设无风险收益率为3%
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
            strategy.AppendLine();

            strategy.AppendLine("分仓建议:");
            foreach (var allocation in optimization.Allocations)
            {
                strategy.AppendLine($"- {allocation.StockName}: {allocation.OptimalPosition:F1}% " +
                                   $"({allocation.AllocationReason})");
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