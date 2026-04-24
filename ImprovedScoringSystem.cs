using System;
using System.Collections.Generic;
using System.Linq;

namespace StockTracker
{
    /// <summary>
    /// 改进的量化评分系统 - 整合背离检测、资金分级、情绪量化、板块联动、多周期共振
    /// </summary>
    public class ImprovedWinRateScoring
    {
        public class EnhancedStockScore
        {
            public string StockCode { get; set; } = string.Empty;
            public string StockName { get; set; } = string.Empty;

            // 增强的维度评分
            public double TechnicalScore { get; set; }      // 技术面评分 (0-100)
            public double FundamentalScore { get; set; }    // 基本面评分 (0-100)
            public double FundFlowScore { get; set; }       // 资金面评分 (0-100)
            public double SentimentScore { get; set; }      // 情绪面评分 (0-100)
            public double RiskScore { get; set; }           // 风险评分 (0-100, 越高越安全)

            // 新增评分维度
            public double TrendStrengthScore { get; set; }  // 趋势强度评分 (0-100)
            public double ValueScore { get; set; }          // 估值评分 (0-100)
            public double SectorStrengthScore { get; set; } // 板块联动评分 (0-100)
            public double MultiTimeframeScore { get; set; } // 多周期共振评分 (0-100)
            public double DivergenceScore { get; set; }     // 背离信号评分 (0-100)

            // 综合评分（动态权重）
            public double OverallScore { get; set; }        // 综合评分 (0-100)

            // 胜率预测
            public double WinProbability { get; set; }      // 预期胜率 (0-100%)
            public string RecommendationLevel { get; set; } = "观望"; // 推荐级别

            // 风险评估
            public List<string> RiskSignals { get; set; } = new();
            public List<string> PositiveSignals { get; set; } = new();

            // 操作建议
            public string ActionAdvice { get; set; } = "观望";
            public decimal SuggestedBuyPrice { get; set; }
            public decimal StopLossPrice { get; set; }
            public decimal TargetPrice { get; set; }
            public double PositionSize { get; set; }        // 动态仓位建议(凯利公式)

            // 新增：置信度
            public double ConfidenceLevel { get; set; }     // 预测置信度 (0-100%)

            // 新增：凯利仓位
            public double KellyPosition { get; set; }       // 凯利最优仓位
            public double KellyFraction { get; set; }       // 凯利仓位比例(已打折)
        }

        /// <summary>
        /// 计算增强的胜率评分（整合所有新指标）
        /// </summary>
        public static EnhancedStockScore CalculateEnhancedScore(
            StockDeepAnalysisContext ctx,
            MarketCondition marketCondition,
            Dictionary<string, double>? historicalPerformance = null)
        {
            var score = new EnhancedStockScore
            {
                StockCode = ctx.Code,
                StockName = ctx.Name
            };

            // 数据质量检查
            if (!IsDataValid(ctx))
            {
                score.OverallScore = 0;
                score.ActionAdvice = "数据异常";
                score.WinProbability = 0;
                score.ConfidenceLevel = 0;
                score.RiskSignals.Add("⚠️ 数据获取不完整，建议暂缓操作");
                return score;
            }

            // === 1. 多维度评分计算 ===

            // 趋势强度评分
            score.TrendStrengthScore = CalculateTrendStrengthScore(ctx);

            // 技术面评分（含背离调整）
            score.TechnicalScore = CalculateImprovedTechnicalScore(ctx);

            // 基本面评分
            score.FundamentalScore = CalculateImprovedFundamentalScore(ctx);

            // 资金面评分（含主力分级）
            score.FundFlowScore = CalculateImprovedFundFlowScore(ctx);

            // 情绪面评分（含新闻量化）
            score.SentimentScore = CalculateNewsSentimentScore(ctx);

            // 估值评分
            score.ValueScore = CalculateValueScore(ctx);

            // 板块联动评分（新增）
            score.SectorStrengthScore = CalculateSectorStrengthScore(ctx);

            // 多周期共振评分（新增）
            score.MultiTimeframeScore = CalculateMultiTimeframeScore(ctx);

            // 背离信号评分（新增）
            score.DivergenceScore = CalculateDivergenceScore(ctx);

            // 风险评分（增强）
            score.RiskScore = CalculateImprovedRiskScore(ctx);

            // === 2. 动态权重计算（含贝叶斯反馈） ===
            var weights = CalculateDynamicWeights(marketCondition, ctx);

            // === 3. 综合评分计算（动态权重 + 新增维度） ===
            score.OverallScore =
                score.TechnicalScore * weights.TechnicalWeight +
                score.FundamentalScore * weights.FundamentalWeight +
                score.FundFlowScore * weights.FundFlowWeight +
                score.SentimentScore * weights.SentimentWeight +
                score.TrendStrengthScore * weights.TrendWeight +
                score.ValueScore * weights.ValueWeight +
                score.SectorStrengthScore * weights.SectorWeight +
                score.MultiTimeframeScore * weights.MultiTimeframeWeight +
                score.DivergenceScore * weights.DivergenceWeight;

            // === 4. 历史表现调整（贝叶斯先验修正） ===
            ApplyBayesianPriorAdjustment(score, ctx, historicalPerformance);

            // === 5. 胜率预测 ===
            score.WinProbability = CalculateImprovedWinProbability(
                score.OverallScore, score.RiskScore, marketCondition, ctx);

            // === 6. 置信度计算 ===
            score.ConfidenceLevel = CalculateConfidenceLevel(score, ctx);

            // === 7. 凯利公式仓位计算 ===
            CalculateKellyPosition(score, ctx, marketCondition);

            // === 8. 推荐级别 ===
            score.RecommendationLevel = GetImprovedRecommendationLevel(
                score.OverallScore, score.RiskScore, score.ConfidenceLevel);

            // === 9. 操作建议 ===
            GenerateImprovedActionAdvice(score, ctx, marketCondition);

            // === 10. 信号收集 ===
            CollectImprovedSignals(score, ctx);

            return score;
        }

        // ====================== 核心评分方法 ======================

        private static bool IsDataValid(StockDeepAnalysisContext ctx)
        {
            int validFields = 0;
            int totalFields = 0;

            totalFields++; if (ctx.CurrentPrice > 0) validFields++;
            totalFields++; if (ctx.MA5 > 0 || ctx.MA10 > 0 || ctx.MA20 > 0) validFields++;
            totalFields++; if (ctx.PE > 0 || ctx.PB > 0 || ctx.ROE > 0) validFields++;
            totalFields++; if (ctx.TurnoverRate > 0 || ctx.VolumeRatio > 0) validFields++;

            return validFields >= totalFields * 0.5;
        }

        private static double CalculateTrendStrengthScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 均线趋势强度 (40分) - 含MA60
            if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20 && ctx.MA20 > ctx.MA60)
                score += 40; // 强势多头排列
            else if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20)
                score += 30; // 标准多头排列
            else if (ctx.MA5 > ctx.MA20)
                score += 15; // 部分多头
            else if (ctx.MA5 < ctx.MA10 && ctx.MA10 < ctx.MA20 && ctx.MA20 < ctx.MA60)
                score -= 35; // 强势空头排列
            else if (ctx.MA5 < ctx.MA10 && ctx.MA10 < ctx.MA20)
                score -= 25; // 空头排列

            // 2. 价格位置强度 (30分)
            if (ctx.CurrentPrice > ctx.MA5 && ctx.BiasMA5 > 0 && ctx.BiasMA5 < 3)
                score += 30; // 站在MA5之上且乖离适中
            else if (ctx.CurrentPrice > ctx.MA10 && ctx.BiasMA5 < 0 && ctx.CurrentPrice > ctx.MA10)
                score += 15; // 回踩MA10支撑
            else if (ctx.BiasMA5 > 5)
                score -= 20; // 乖离过大
            else if (ctx.BiasMA60 < -10)
                score -= 15; // 大幅低于MA60

            // 3. 量价配合强度 (30分)
            if (ctx.VolumeRatio > 1.5 && ctx.VolumeRatio < 3.0 && ctx.PriceChangeRatio > 1.0)
                score += 30; // 放量上涨
            else if (ctx.VolumeRatio > 1.2 && ctx.PriceChangeRatio > 0)
                score += 20; // 温和放量
            else if (ctx.VolumeRatio > 3.0 && ctx.PriceChangeRatio < 2.0)
                score -= 25; // 放量滞涨

            return Math.Max(0, Math.Min(100, score));
        }

        private static double CalculateImprovedTechnicalScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 均线系统 (25分)
            if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20) score += 15;
            if (ctx.MA5 > ctx.MA10 && ctx.MA20 > ctx.MA60) score += 10; // 中长期趋势健康
            if (ctx.CurrentPrice > ctx.MA5) score += 5;

            // 2. 乖离率控制 (20分)
            if (ctx.BiasMA5 > 0 && ctx.BiasMA5 < 2) score += 20;
            else if (ctx.BiasMA5 >= 2 && ctx.BiasMA5 < 4) score += 12;
            else if (ctx.BiasMA5 > 5) score -= 15;
            else if (ctx.BiasMA5 < -2 && ctx.BiasMA5 > -4) score += 8;

            // 3. 量价关系 (20分)
            if (ctx.VolumeRatio > 1.2 && ctx.VolumeRatio < 2.5) score += 12;
            if (ctx.VolumeChangeRatio > 1.0 && ctx.PriceChangeRatio > 0) score += 8;
            else if (ctx.VolumeRatio > 3.0) score -= 10;

            // 4. 换手率分析 (15分)
            if (ctx.TurnoverRate > 3 && ctx.TurnoverRate < 12) score += 15;
            else if (ctx.TurnoverRate >= 12 && ctx.TurnoverRate < 20) score += 8;
            else if (ctx.TurnoverRate >= 20) score -= 15;

            // 5. 波动率锥调整 (10分) - 高波动环境下降低技术信号可靠度
            if (ctx.VolatilityPercentile > 80) score -= 5; // 波动率处于历史高位
            else if (ctx.VolatilityPercentile < 20) score += 5; // 低波动环境，技术信号更可靠

            // 6. 长期趋势 (10分)
            if (ctx.CurrentPrice > ctx.MA60 && ctx.BiasMA60 > 0 && ctx.BiasMA60 < 10) score += 10;
            else if (ctx.CurrentPrice < ctx.MA60 && ctx.BiasMA60 < -5) score -= 10;

            return Math.Max(0, Math.Min(100, score));
        }

        private static double CalculateImprovedFundamentalScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 盈利能力 (40分)
            if (ctx.ROE > 20) score += 40;
            else if (ctx.ROE > 15) score += 35;
            else if (ctx.ROE > 10) score += 25;
            else if (ctx.ROE > 5) score += 15;
            else if (ctx.ROE > 0) score += 5;

            // 2. 财务健康度 (30分)
            if (ctx.NetProfit > 0 && ctx.OperatingRevenue > 0) score += 15;
            if (ctx.OperatingCashFlowPerShare > 0) score += 15;

            // 3. 估值合理度 (30分)
            if (ctx.PE > 0 && ctx.PE < 20) score += 30;
            else if (ctx.PE >= 20 && ctx.PE < 35) score += 25;
            else if (ctx.PE >= 35 && ctx.PE < 50) score += 10;
            else if (ctx.PE >= 50) score -= 10;

            if (ctx.TotalMarketValue > 20 && ctx.TotalMarketValue < 200) score += 5;

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 资金面评分（含主力资金分级）
        /// </summary>
        private static double CalculateImprovedFundFlowScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 主力资金分级分析 (40分)
            // 超大单 + 大单 = 真正主力
            double mainForceTotal = ctx.SuperLargeOrderInflow + ctx.LargeOrderInflow;
            double retailTotal = ctx.MediumOrderInflow + ctx.SmallOrderInflow;

            if (mainForceTotal > 50000000) score += 40;      // 主力合计 > 5000万
            else if (mainForceTotal > 20000000) score += 35; // > 2000万
            else if (mainForceTotal > 5000000) score += 25;  // > 500万
            else if (mainForceTotal > 0) score += 15;
            else if (mainForceTotal > -5000000) score += 5;
            else if (mainForceTotal > -20000000) score -= 10;
            else score -= 25; // 主力大幅出逃

            // 1.5 主力/散户分歧判断 (10分)
            if (mainForceTotal > 0 && retailTotal < 0)
                score += 10; // 主力买散户卖 → 收集筹码
            else if (mainForceTotal < 0 && retailTotal > 0)
                score -= 15; // 主力卖散户买 → 主力出货

            // 1.6 主力净流入占成交额比例 (10分)
            if (ctx.MainForceInflowRatio > 10) score += 10;   // 占比>10%
            else if (ctx.MainForceInflowRatio > 5) score += 7;
            else if (ctx.MainForceInflowRatio > 0) score += 3;
            else if (ctx.MainForceInflowRatio < -10) score -= 10;

            // 2. 筹码结构 (25分)
            if (ctx.ProfitRatio > 30 && ctx.ProfitRatio < 70) score += 15;
            else if (ctx.ProfitRatio >= 70 && ctx.ProfitRatio < 85) score += 8;
            else if (ctx.ProfitRatio >= 85) score -= 15;
            else if (ctx.ProfitRatio < 20) score -= 8;

            if (ctx.ChipConcentration90 < 15) score += 10;
            else if (ctx.ChipConcentration90 > 30) score -= 5;

            // 3. 换手率与量比 (15分)
            if (ctx.TurnoverRate > 5 && ctx.TurnoverRate < 15 && ctx.VolumeRatio > 1.2) score += 15;
            else if (ctx.TurnoverRate > 3 && ctx.TurnoverRate < 20) score += 8;

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 新闻情绪量化评分
        /// </summary>
        private static double CalculateNewsSentimentScore(StockDeepAnalysisContext ctx)
        {
            // 优先使用分析后的情绪评分
            if (ctx.NewsSentimentScore > 0 || ctx.NewsImpactScore > 0)
            {
                // 综合考虑情绪分数和影响力
                double weightedScore = ctx.NewsSentimentScore * 0.7 + ctx.NewsImpactScore * 0.3;
                return Math.Max(0, Math.Min(100, weightedScore));
            }

            // 降级到简单关键词分析
            double score = 50;
            if (ctx.LatestNews.Count > 0)
            {
                foreach (var news in ctx.LatestNews)
                {
                    if (news.Contains("利好") || news.Contains("上涨") || news.Contains("突破"))
                        score += 8;
                    if (news.Contains("利空") || news.Contains("下跌") || news.Contains("风险"))
                        score -= 12;
                }
            }
            return Math.Max(0, Math.Min(100, score));
        }

        private static double CalculateValueScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            if (ctx.PE > 0 && ctx.PB > 0)
            {
                if (ctx.PB < 1.5 && ctx.PE < 20) score += 50;
                else if (ctx.PB < 2.0 && ctx.PE < 30) score += 40;
                else if (ctx.PB < 3.0 && ctx.PE < 40) score += 30;
                else if (ctx.PE < 50) score += 20;
                else score += 10;
            }
            else if (ctx.PE > 0)
            {
                if (ctx.PE < 20) score += 40;
                else if (ctx.PE < 35) score += 30;
                else if (ctx.PE < 50) score += 15;
            }

            if (ctx.ROE > 0 && ctx.PB > 0)
            {
                double roeToPbRatio = ctx.ROE / ctx.PB;
                if (roeToPbRatio > 10) score += 30;
                else if (roeToPbRatio > 5) score += 20;
                else if (roeToPbRatio > 2) score += 10;
            }

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 板块联动评分
        /// </summary>
        private static double CalculateSectorStrengthScore(StockDeepAnalysisContext ctx)
        {
            double score = 50; // 中性基准

            // 个股相对板块强度
            if (ctx.RelativeStrengthVsSector != 0)
            {
                if (ctx.RelativeStrengthVsSector > 3)
                    score += 25; // 大幅强于板块 → 领涨龙头
                else if (ctx.RelativeStrengthVsSector > 1)
                    score += 15; // 强于板块
                else if (ctx.RelativeStrengthVsSector > 0)
                    score += 5;  // 略强于板块
                else if (ctx.RelativeStrengthVsSector < -2)
                    score -= 20; // 弱于板块 → 跟风弱势股
                else if (ctx.RelativeStrengthVsSector < 0)
                    score -= 10;
            }

            // 板块涨跌
            if (ctx.SectorPctChange > 2)
                score += 15; // 板块领涨
            else if (ctx.SectorPctChange > 0)
                score += 8;  // 板块走强
            else if (ctx.SectorPctChange < -2)
                score -= 15; // 板块领跌
            else if (ctx.SectorPctChange < 0)
                score -= 8;

            // 板块内排名
            if (ctx.SectorRankPercent < 20)
                score += 10; // 板块内前20%
            else if (ctx.SectorRankPercent > 80)
                score -= 5;  // 板块内末20%

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 多周期共振评分
        /// </summary>
        private static double CalculateMultiTimeframeScore(StockDeepAnalysisContext ctx)
        {
            double score = 50;

            if (ctx.TechScore != null && ctx.TechScore60Min != null && ctx.TechScore15Min != null)
            {
                // 应用多周期共振分析
                AdvancedTechnicalIndicators.AnalyzeMultiTimeframeResonance(
                    ctx.TechScore, ctx.TechScore60Min, ctx.TechScore15Min);

                if (ctx.TechScore.IsMultiTimeframeBullish)
                    score += 40; // 日线+60'+15' MACD同步金叉
                else if (ctx.TechScore.IsMultiTimeframeBearish)
                    score -= 40; // 同步死叉
                else
                {
                    // 部分共振
                    if (ctx.TechScore.MACD > ctx.TechScore.MACDSignal)
                        score += 10; // 日线看多
                    if (ctx.TechScore60Min.MACD > ctx.TechScore60Min.MACDSignal)
                        score += 10; // 60'看多
                    if (ctx.TechScore15Min.MACD > ctx.TechScore15Min.MACDSignal)
                        score += 10; // 15'看多
                }
            }
            else
            {
                // 无多周期数据，仅用日线
                if (ctx.TechScore?.MACD > ctx.TechScore?.MACDSignal)
                    score += 15;
            }

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 背离信号评分（高权重决策因子）
        /// </summary>
        private static double CalculateDivergenceScore(StockDeepAnalysisContext ctx)
        {
            double score = 50; // 中性基准

            if (ctx.TechScore != null)
            {
                // 底背离 → 强烈看多
                if (ctx.TechScore.HasBullishDivergence)
                    score += 35;
                if (ctx.TechScore.HasRSIBullishDivergence)
                    score += 20;

                // 顶背离 → 强烈看空
                if (ctx.TechScore.HasBearishDivergence)
                    score -= 35;
                if (ctx.TechScore.HasRSIBearishDivergence)
                    score -= 20;

                // 量价背离
                if (ctx.TechScore.HasVolumePriceDivergence)
                    score -= 25;
            }

            // 同步到context
            if (ctx.TechScore != null)
            {
                ctx.HasBearishDivergence = ctx.TechScore.HasBearishDivergence;
                ctx.HasBullishDivergence = ctx.TechScore.HasBullishDivergence;
                ctx.DivergenceDetail = ctx.TechScore.DivergenceDetail;
            }

            return Math.Max(0, Math.Min(100, score));
        }

        private static double CalculateImprovedRiskScore(StockDeepAnalysisContext ctx)
        {
            double score = 100;

            // 技术风险
            if (ctx.BiasMA5 > 5) score -= 25;
            if (ctx.BiasMA5 > 8) score -= 15;

            // 量价风险
            if (ctx.VolumeRatio > 3.0 && ctx.PriceChangeRatio < 2.0) score -= 30;
            if (ctx.VolumeRatio > 5.0) score -= 20;

            // 筹码风险
            if (ctx.ProfitRatio > 85) score -= 25;
            if (ctx.ProfitRatio > 90) score -= 15;

            // 背离风险
            if (ctx.TechScore != null)
            {
                if (ctx.TechScore.HasBearishDivergence) score -= 30;
                if (ctx.TechScore.HasRSIBearishDivergence) score -= 20;
                if (ctx.TechScore.HasVolumePriceDivergence) score -= 20;
            }

            // 资金风险（含分级判断）
            double mainForceTotal = ctx.SuperLargeOrderInflow + ctx.LargeOrderInflow;
            if (mainForceTotal < -30000000) score -= 30; // 主力大单出逃 > 3000万
            if (mainForceTotal < -10000000) score -= 15; // 主力中单出逃 > 1000万

            // 主力出货给散户
            if (mainForceTotal < -5000000 && ctx.MediumOrderInflow > 0)
                score -= 15; // 主力卖、散户接盘

            // 估值风险
            if (ctx.PE > 80) score -= 15;
            if (ctx.PE < 0) score -= 10;

            // 换手率风险
            if (ctx.TurnoverRate > 25) score -= 20;
            if (ctx.TurnoverRate > 30) score -= 15;

            // 波动率风险
            if (ctx.VolatilityPercentile > 85) score -= 10; // 极高波动环境

            // 板块风险
            if (ctx.SectorPctChange < -3) score -= 15;
            if (ctx.RelativeStrengthVsSector < -3) score -= 10;

            return Math.Max(0, score);
        }

        // ====================== 动态权重（含贝叶斯反馈） ======================

        private static (double TechnicalWeight, double FundamentalWeight, double FundFlowWeight,
                 double SentimentWeight, double TrendWeight, double ValueWeight,
                 double SectorWeight, double MultiTimeframeWeight, double DivergenceWeight)
                 CalculateDynamicWeights(MarketCondition marketCondition, StockDeepAnalysisContext ctx)
        {
            double technicalWeight = 0.15;
            double fundamentalWeight = 0.15;
            double fundFlowWeight = 0.15;
            double sentimentWeight = 0.10;
            double trendWeight = 0.10;
            double valueWeight = 0.05;
            double sectorWeight = 0.10;
            double multiTimeframeWeight = 0.10;
            double divergenceWeight = 0.10;

            switch (marketCondition)
            {
                case MarketCondition.Strong:
                    technicalWeight = 0.20;
                    trendWeight = 0.15;
                    multiTimeframeWeight = 0.15;
                    fundamentalWeight = 0.10;
                    fundFlowWeight = 0.10;
                    break;

                case MarketCondition.Weak:
                    fundamentalWeight = 0.20;
                    valueWeight = 0.15;
                    fundFlowWeight = 0.15;
                    sectorWeight = 0.15;
                    technicalWeight = 0.10;
                    multiTimeframeWeight = 0.05;
                    break;

                case MarketCondition.Crash:
                    fundFlowWeight = 0.25;
                    fundamentalWeight = 0.20;
                    divergenceWeight = 0.15; // 背离在暴跌中更有效
                    valueWeight = 0.15;
                    technicalWeight = 0.05;
                    multiTimeframeWeight = 0.05;
                    trendWeight = 0.05;
                    break;
            }

            // 个股特性调整
            if (ctx.TotalMarketValue < 50)
            {
                technicalWeight += 0.03;
                fundFlowWeight += 0.03;
                fundamentalWeight -= 0.03;
                valueWeight -= 0.03;
            }
            else if (ctx.TotalMarketValue > 500)
            {
                fundamentalWeight += 0.03;
                valueWeight += 0.03;
                technicalWeight -= 0.03;
                fundFlowWeight -= 0.03;
            }

            // 量价背离时提升背离权重
            if (ctx.TechScore != null &&
                (ctx.TechScore.HasBearishDivergence || ctx.TechScore.HasBullishDivergence))
            {
                divergenceWeight += 0.10;
                technicalWeight -= 0.05;
                trendWeight -= 0.05;
            }

            // 应用贝叶斯反馈（如果有历史数据）
            var bayesAdj = AdviceTracker.GetBayesianWeightAdjustments();
            if (bayesAdj != null)
            {
                technicalWeight *= bayesAdj.TechnicalMultiplier;
                fundamentalWeight *= bayesAdj.FundamentalMultiplier;
                fundFlowWeight *= bayesAdj.FundFlowMultiplier;
            }

            // 权重归一化
            double totalWeight = technicalWeight + fundamentalWeight + fundFlowWeight +
                                sentimentWeight + trendWeight + valueWeight +
                                sectorWeight + multiTimeframeWeight + divergenceWeight;

            return (technicalWeight / totalWeight, fundamentalWeight / totalWeight,
                   fundFlowWeight / totalWeight, sentimentWeight / totalWeight,
                   trendWeight / totalWeight, valueWeight / totalWeight,
                   sectorWeight / totalWeight, multiTimeframeWeight / totalWeight,
                   divergenceWeight / totalWeight);
        }

        // ====================== 贝叶斯先验修正 ======================

        private static void ApplyBayesianPriorAdjustment(
            EnhancedStockScore score,
            StockDeepAnalysisContext ctx,
            Dictionary<string, double>? historicalPerformance)
        {
            // 贝叶斯更新：后验 = (先验 * 似然) / 归一化
            // 先验 = 个股历史表现
            // 似然 = 当前量化评分

            if (historicalPerformance != null && historicalPerformance.ContainsKey(ctx.Code))
            {
                double prior = historicalPerformance[ctx.Code]; // 历史胜率 0-1
                double likelihood = score.OverallScore / 100.0; // 当前评分归一化

                // 贝叶斯加权：历史表现权重随样本量增加而提升
                int sampleSize = AdviceTracker.GetStockSampleSize(ctx.Code);
                double priorWeight = Math.Min(0.4, sampleSize * 0.02); // 最多40%先验权重
                double likelihoodWeight = 1.0 - priorWeight;

                double bayesianScore = prior * priorWeight + likelihood * likelihoodWeight;
                score.OverallScore = bayesianScore * 100;
            }
            else if (historicalPerformance != null && historicalPerformance.ContainsKey(ctx.Code))
            {
                double stockHistoricalPerformance = historicalPerformance[ctx.Code];
                score.OverallScore *= (1 + (stockHistoricalPerformance - 0.5) * 0.2);
            }
        }

        // ====================== 凯利公式仓位计算 ======================

        private static void CalculateKellyPosition(
            EnhancedStockScore score,
            StockDeepAnalysisContext ctx,
            MarketCondition marketCondition)
        {
            // 凯利公式: f = (bp - q) / b
            // b = 盈亏比 (止盈目标 / 止损)
            // p = 胜率
            // q = 败率 (1-p)

            double winProb = score.WinProbability / 100.0;

            // 估计盈亏比
            double riskAmount = 0;
            if (ctx.SmartStop?.DynamicStopLoss > 0)
            {
                riskAmount = Math.Max(ctx.CurrentPrice * 0.02,
                    (double)(ctx.CurrentPrice - (double)ctx.SmartStop.DynamicStopLoss));
            }
            else
            {
                riskAmount = ctx.CurrentPrice * 0.05; // 默认5%风险
            }

            double rewardAmount = riskAmount * 2.5; // 默认2.5倍盈亏比
            double b = rewardAmount / riskAmount; // 盈亏比

            // 凯利公式
            double kellyF = (winProb * b - (1 - winProb)) / b;
            kellyF = Math.Max(0, Math.Min(kellyF, 0.5)); // 上限50%仓位

            // 凯利折扣：使用半凯利（Half-Kelly）降低回撤
            score.KellyPosition = kellyF * 100;
            score.KellyFraction = kellyF * 0.5 * 100; // Half-Kelly

            // 市场环境折扣
            double marketDiscount = marketCondition switch
            {
                MarketCondition.Strong => 1.0,
                MarketCondition.Neutral => 0.8,
                MarketCondition.Weak => 0.5,
                MarketCondition.Crash => 0.0,
                _ => 0.6
            };

            score.KellyFraction *= marketDiscount;

            // 连续亏损时进一步缩减仓位
            int consecutiveLosses = AdviceTracker.GetConsecutiveLosses();
            if (consecutiveLosses >= 3)
                score.KellyFraction *= 0.5; // 连亏3次后仓位减半
            if (consecutiveLosses >= 5)
                score.KellyFraction *= 0.3; // 连亏5次后仓位缩为30%
        }

        // ====================== 胜率与置信度 ======================

        private static double CalculateImprovedWinProbability(
            double overallScore, double riskScore, MarketCondition marketCondition,
            StockDeepAnalysisContext ctx)
        {
            double baseProbability = overallScore * 0.75;

            double riskAdjustment = riskScore / 100.0;
            baseProbability *= riskAdjustment;

            double marketMultiplier = marketCondition switch
            {
                MarketCondition.Strong => 1.15,
                MarketCondition.Neutral => 1.0,
                MarketCondition.Weak => 0.65,
                MarketCondition.Crash => 0.25,
                _ => 1.0
            };

            baseProbability *= marketMultiplier;

            // 背离调整（高权重）
            if (ctx.TechScore?.HasBearishDivergence == true)
                baseProbability *= 0.5; // 顶背离 → 胜率减半
            if (ctx.TechScore?.HasBullishDivergence == true)
                baseProbability *= 1.3; // 底背离 → 胜率+30%
            if (ctx.TechScore?.HasRSIBearishDivergence == true)
                baseProbability *= 0.7;
            if (ctx.TechScore?.HasRSIBullishDivergence == true)
                baseProbability *= 1.2;
            if (ctx.TechScore?.HasVolumePriceDivergence == true)
                baseProbability *= 0.65;

            // 多周期共振
            if (ctx.TechScore?.IsMultiTimeframeBullish == true)
                baseProbability *= 1.15;
            if (ctx.TechScore?.IsMultiTimeframeBearish == true)
                baseProbability *= 0.8;

            // 波动率极值调整
            if (ctx.VolatilityPercentile > 90)
                baseProbability *= 0.75; // 极高波动 → 信号不可靠

            // 筹码峰调整
            if (ctx.ChipPeakPressure > 0 && ctx.CurrentPrice < ctx.ChipPeakPressure * 0.97)
                baseProbability *= 0.85; // 上方有筹码峰压力
            if (ctx.ChipPeakSupport > 0 && ctx.CurrentPrice > ctx.ChipPeakSupport &&
                ctx.CurrentPrice < ctx.ChipPeakSupport * 1.03)
                baseProbability *= 1.1;  // 靠近筹码峰支撑

            // 个股特殊调整
            if (ctx.ProfitRatio > 90) baseProbability *= 0.7;
            if (ctx.BiasMA5 > 6) baseProbability *= 0.8;
            if (ctx.MainForceInflowRatio < -5) baseProbability *= 0.7;

            // 板块拖累
            if (ctx.SectorPctChange < -2 && ctx.RelativeStrengthVsSector < 1)
                baseProbability *= 0.85;

            return Math.Max(10, Math.Min(90, baseProbability));
        }

        private static double CalculateConfidenceLevel(EnhancedStockScore score, StockDeepAnalysisContext ctx)
        {
            double confidence = 50;

            int dataFields = 0;
            if (ctx.CurrentPrice > 0) dataFields++;
            if (ctx.MA5 > 0) dataFields++;
            if (ctx.MA60 > 0) dataFields++; // 新增
            if (ctx.PE > 0) dataFields++;
            if (ctx.ROE > 0) dataFields++;
            if (ctx.MainForceNetInflow != 0) dataFields++;
            if (ctx.TurnoverRate > 0) dataFields++;
            if (ctx.SuperLargeOrderInflow != 0 || ctx.LargeOrderInflow != 0) dataFields++; // 新增
            if (ctx.Prices60Min.Count > 0) dataFields++; // 新增
            double maxFields = 9.0;

            confidence += (dataFields / maxFields) * 25;

            double scoreVariance = Math.Abs(score.TechnicalScore - score.FundamentalScore) +
                                  Math.Abs(score.FundFlowScore - score.SentimentScore);
            if (scoreVariance < 30) confidence += 15;

            // 多周期一致性加分
            if (ctx.TechScore?.IsMultiTimeframeBullish == true ||
                ctx.TechScore?.IsMultiTimeframeBearish == true)
                confidence += 10; // 多周期信号一致

            // 背离信号加分（背离是强信号）
            if (ctx.TechScore?.HasBearishDivergence == true ||
                ctx.TechScore?.HasBullishDivergence == true)
                confidence += 8;

            // 波动率锥：低波动时信号更可靠
            if (ctx.VolatilityPercentile > 0 && ctx.VolatilityPercentile < 30)
                confidence += 5;
            else if (ctx.VolatilityPercentile > 80)
                confidence -= 5;

            if (score.OverallScore > 80) confidence += 10;
            else if (score.OverallScore < 50) confidence -= 15;

            if (score.RiskScore < 50) confidence -= 20;

            return Math.Max(0, Math.Min(100, confidence));
        }

        // ====================== 推荐与操作建议 ======================

        private static string GetImprovedRecommendationLevel(double overallScore, double riskScore, double confidence)
        {
            if (overallScore >= 75 && riskScore >= 65 && confidence >= 60)
                return "⭐⭐⭐⭐⭐ 强烈推荐";
            if (overallScore >= 70 && riskScore >= 60 && confidence >= 50)
                return "⭐⭐⭐⭐ 推荐";
            if (overallScore >= 60 && riskScore >= 50 && confidence >= 40)
                return "⭐⭐⭐ 谨慎推荐";
            if (overallScore >= 50 && riskScore >= 45 && confidence >= 30)
                return "⭐⭐ 观望";
            return "⭐ 不建议操作";
        }

        private static void GenerateImprovedActionAdvice(
            EnhancedStockScore score,
            StockDeepAnalysisContext ctx,
            MarketCondition marketCondition)
        {
            if (marketCondition == MarketCondition.Crash)
            {
                score.ActionAdvice = "空仓观望";
                score.PositionSize = 0;
                return;
            }

            // 背离优先判定
            if (ctx.TechScore?.HasBearishDivergence == true && score.OverallScore < 70)
            {
                score.ActionAdvice = "顶背离减仓";
                score.PositionSize = 0;
                return;
            }

            if (score.OverallScore >= 75 && score.RiskScore >= 65 && score.ConfidenceLevel >= 60)
            {
                score.ActionAdvice = "买入";
                // 优先使用凯利公式仓位
                if (score.KellyFraction > 0)
                    score.PositionSize = score.KellyFraction;
                else
                    score.PositionSize = marketCondition == MarketCondition.Strong ? 25 : 15;

                score.SuggestedBuyPrice = (decimal)(ctx.CurrentPrice * 0.97);
                score.StopLossPrice = 0;
                score.TargetPrice = 0;
            }
            else if (score.OverallScore >= 65 && score.RiskScore >= 55 && score.ConfidenceLevel >= 50)
            {
                score.ActionAdvice = "谨慎买入";
                // 凯利仓位折扣
                if (score.KellyFraction > 0)
                    score.PositionSize = score.KellyFraction * 0.6; // 折扣60%
                else
                    score.PositionSize = 8;

                score.SuggestedBuyPrice = (decimal)(ctx.CurrentPrice * 0.94);
                score.StopLossPrice = 0;
                score.TargetPrice = 0;
            }
            else if (ctx.TechScore?.HasBullishDivergence == true && score.OverallScore >= 55)
            {
                // 底背离值得关注，即使评分不够高
                score.ActionAdvice = "底背离信号关注";
                score.PositionSize = 3; // 极小仓位试探
                score.SuggestedBuyPrice = (decimal)(ctx.CurrentPrice * 0.96);
            }
            else
            {
                score.ActionAdvice = "观望";
                score.PositionSize = 0;
            }
        }

        private static void CollectImprovedSignals(EnhancedStockScore score, StockDeepAnalysisContext ctx)
        {
            // 积极信号
            if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20 && ctx.MA20 > ctx.MA60)
                score.PositiveSignals.Add("✅ 强势多头排列(含MA60)");
            else if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20)
                score.PositiveSignals.Add("✅ 完整多头排列");

            // 主力资金分级信号
            double mainForceTotal = ctx.SuperLargeOrderInflow + ctx.LargeOrderInflow;
            if (mainForceTotal > 10000000)
            {
                string flowDisplay = mainForceTotal >= 100000000 ? $"{mainForceTotal / 100000000:F2}亿" : $"{mainForceTotal / 10000:F0}万";
                score.PositiveSignals.Add($"✅ 主力大单净流入{flowDisplay}");
            }
            if (mainForceTotal > 0 && ctx.MediumOrderInflow + ctx.SmallOrderInflow < 0)
                score.PositiveSignals.Add("✅ 主力吸筹散户离场");

            if (ctx.ROE > 20)
                score.PositiveSignals.Add($"✅ 优秀ROE({ctx.ROE:F2}%)");
            if (ctx.TurnoverRate > 5 && ctx.TurnoverRate < 15)
                score.PositiveSignals.Add("✅ 活跃换手率");
            if (ctx.BiasMA5 > 0 && ctx.BiasMA5 < 2)
                score.PositiveSignals.Add("✅ 理想乖离率");

            // 背离积极信号
            if (ctx.TechScore?.HasBullishDivergence == true)
                score.PositiveSignals.Add("🟢 MACD底背离(反转信号)");
            if (ctx.TechScore?.HasRSIBullishDivergence == true)
                score.PositiveSignals.Add("🟢 RSI底背离(动能积聚)");

            // 多周期积极信号
            if (ctx.TechScore?.IsMultiTimeframeBullish == true)
                score.PositiveSignals.Add("🟢 多周期MACD共振做多");

            // 板块联动积极信号
            if (ctx.RelativeStrengthVsSector > 2)
                score.PositiveSignals.Add($"✅ 领涨板块(Sector+{ctx.RelativeStrengthVsSector:F1}%)");
            if (ctx.SectorPctChange > 2)
                score.PositiveSignals.Add($"✅ 板块强势({ctx.SectorPctChange:F1}%)");

            // 新闻情绪
            if (ctx.NewsSentimentScore > 65)
                score.PositiveSignals.Add($"✅ 新闻情绪积极({ctx.NewsSentimentScore:F0}/100)");

            // === 风险信号 ===
            if (ctx.BiasMA5 > 5) score.RiskSignals.Add($"⚠️ 乖离率过大({ctx.BiasMA5:F2}%)");
            if (ctx.VolumeRatio > 3.0 && ctx.PriceChangeRatio < 2.0)
                score.RiskSignals.Add("⚠️ 放量滞涨风险");
            if (ctx.ProfitRatio > 85) score.RiskSignals.Add("⚠️ 获利盘过多");
            if (mainForceTotal < -5000000)
            {
                double outWan = Math.Abs(mainForceTotal) / 10000.0;
                string outDisplay = outWan >= 10000 ? $"{outWan / 10000:F2}亿" : $"{outWan:F0}万";
                score.RiskSignals.Add($"⚠️ 主力大单流出{outDisplay}");
            }
            if (mainForceTotal < 0 && ctx.MediumOrderInflow > 0)
                score.RiskSignals.Add("⚠️ 主力出货散户接盘");
            if (ctx.TurnoverRate > 25) score.RiskSignals.Add("⚠️ 换手率过热");
            if (ctx.PE > 60) score.RiskSignals.Add("⚠️ 估值偏高");

            // 背离风险信号
            if (ctx.TechScore?.HasBearishDivergence == true)
                score.RiskSignals.Add("🔴 MACD顶背离(强烈看空)");
            if (ctx.TechScore?.HasRSIBearishDivergence == true)
                score.RiskSignals.Add("🔴 RSI顶背离(动能衰竭)");
            if (ctx.TechScore?.HasVolumePriceDivergence == true)
                score.RiskSignals.Add("⚠️ 量价背离(趋势不可持续)");

            // 多周期风险信号
            if (ctx.TechScore?.IsMultiTimeframeBearish == true)
                score.RiskSignals.Add("🔴 多周期MACD共振做空");

            // 板块风险
            if (ctx.RelativeStrengthVsSector < -3)
                score.RiskSignals.Add($"⚠️ 大幅弱于板块({ctx.RelativeStrengthVsSector:F1}%)");
            if (ctx.SectorPctChange < -3)
                score.RiskSignals.Add($"⚠️ 板块领跌({ctx.SectorPctChange:F1}%)");

            // 波动率风险
            if (ctx.VolatilityPercentile > 85)
                score.RiskSignals.Add($"⚠️ 波动率历史高位({ctx.VolatilityPercentile:F0}%)");

            // 新闻风险
            if (ctx.NewsSentimentScore < 35)
                score.RiskSignals.Add($"⚠️ 新闻情绪负面({ctx.NewsSentimentScore:F0}/100)");
        }
    }
}
