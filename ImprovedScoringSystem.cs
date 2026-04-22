using System;
using System.Collections.Generic;
using System.Linq;

namespace StockTracker
{
    /// <summary>
    /// 改进的量化评分系统 - 更科学的胜率预测模型
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
            public double PositionSize { get; set; }        // 动态仓位建议

            // 新增：置信度
            public double ConfidenceLevel { get; set; }     // 预测置信度 (0-100%)
        }

        /// <summary>
        /// 计算增强的胜率评分
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

            // 趋势强度评分（新增）
            score.TrendStrengthScore = CalculateTrendStrengthScore(ctx);

            // 技术面评分（改进）
            score.TechnicalScore = CalculateImprovedTechnicalScore(ctx);

            // 基本面评分（改进）
            score.FundamentalScore = CalculateImprovedFundamentalScore(ctx);

            // 资金面评分（改进）
            score.FundFlowScore = CalculateImprovedFundFlowScore(ctx);

            // 情绪面评分
            score.SentimentScore = CalculateSentimentScore(ctx);

            // 估值评分（新增）
            score.ValueScore = CalculateValueScore(ctx);

            // 风险评分
            score.RiskScore = CalculateImprovedRiskScore(ctx);

            // === 2. 动态权重计算 ===
            var weights = CalculateDynamicWeights(marketCondition, ctx);

            // === 3. 综合评分计算（动态权重） ===
            score.OverallScore =
                score.TechnicalScore * weights.TechnicalWeight +
                score.FundamentalScore * weights.FundamentalWeight +
                score.FundFlowScore * weights.FundFlowWeight +
                score.SentimentScore * weights.SentimentWeight +
                score.TrendStrengthScore * weights.TrendWeight +
                score.ValueScore * weights.ValueWeight;

            // === 4. 历史表现调整 ===
            if (historicalPerformance != null && historicalPerformance.ContainsKey(ctx.Code))
            {
                double stockHistoricalPerformance = historicalPerformance[ctx.Code];
                // 历史表现好的股票适当加分，表现差的适当减分
                score.OverallScore *= (1 + (stockHistoricalPerformance - 0.5) * 0.2); // ±10%调整
            }

            // === 5. 胜率预测（改进） ===
            score.WinProbability = CalculateImprovedWinProbability(
                score.OverallScore,
                score.RiskScore,
                marketCondition,
                ctx);

            // === 6. 置信度计算（新增） ===
            score.ConfidenceLevel = CalculateConfidenceLevel(score, ctx);

            // === 7. 推荐级别 ===
            score.RecommendationLevel = GetImprovedRecommendationLevel(
                score.OverallScore,
                score.RiskScore,
                score.ConfidenceLevel);

            // === 8. 操作建议（改进） ===
            GenerateImprovedActionAdvice(score, ctx, marketCondition);

            // === 9. 信号收集 ===
            CollectImprovedSignals(score, ctx);

            return score;
        }

        /// <summary>
        /// 数据有效性检查（增强）
        /// </summary>
        private static bool IsDataValid(StockDeepAnalysisContext ctx)
        {
            int validFields = 0;
            int totalFields = 0;

            // 检查价格数据
            totalFields++;
            if (ctx.CurrentPrice > 0) validFields++;

            // 检查均线数据
            totalFields++;
            if (ctx.MA5 > 0 || ctx.MA10 > 0 || ctx.MA20 > 0) validFields++;

            // 检查基本面数据
            totalFields++;
            if (ctx.PE > 0 || ctx.PB > 0 || ctx.ROE > 0) validFields++;

            // 检查资金面数据
            totalFields++;
            if (ctx.TurnoverRate > 0 || ctx.VolumeRatio > 0) validFields++;

            // 至少50%的字段有效才认为数据可用
            return validFields >= totalFields * 0.5;
        }

        /// <summary>
        /// 计算趋势强度评分（新增）
        /// </summary>
        private static double CalculateTrendStrengthScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 均线趋势强度 (40分)
            if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20)
            {
                score += 40; // 标准多头排列
            }
            else if (ctx.MA5 > ctx.MA20)
            {
                score += 25; // 部分多头
            }
            else if (ctx.MA5 < ctx.MA10 && ctx.MA10 < ctx.MA20)
            {
                score -= 30; // 空头排列扣分
            }

            // 2. 价格位置强度 (30分)
            if (ctx.CurrentPrice > ctx.MA5 && ctx.BiasMA5 > 0 && ctx.BiasMA5 < 3)
            {
                score += 30; // 站在MA5之上且乖离适中
            }
            else if (ctx.CurrentPrice > ctx.MA10 && ctx.BiasMA5 < 0)
            {
                score += 15; // 回踩MA10
            }
            else if (ctx.BiasMA5 > 5)
            {
                score -= 20; // 乖离过大
            }

            // 3. 量价配合强度 (30分)
            if (ctx.VolumeRatio > 1.5 && ctx.VolumeRatio < 3.0 && ctx.PriceChangeRatio > 1.0)
            {
                score += 30; // 放量上涨
            }
            else if (ctx.VolumeRatio > 1.2 && ctx.PriceChangeRatio > 0)
            {
                score += 20; // 温和放量
            }
            else if (ctx.VolumeRatio > 3.0 && ctx.PriceChangeRatio < 2.0)
            {
                score -= 25; // 放量滞涨
            }

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 计算改进的技术面评分
        /// </summary>
        private static double CalculateImprovedTechnicalScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 均线系统 (30分)
            if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20) score += 20; // 多头排列
            if (ctx.CurrentPrice > ctx.MA5) score += 10; // 价格站上短期均线

            // 2. 乖离率控制 (25分)
            if (ctx.BiasMA5 > 0 && ctx.BiasMA5 < 2) score += 15; // 理想乖离
            else if (ctx.BiasMA5 >= 2 && ctx.BiasMA5 < 4) score += 10; // 可接受乖离
            else if (ctx.BiasMA5 > 5) score -= 15; // 乖离过大风险
            else if (ctx.BiasMA5 < -2 && ctx.BiasMA5 > -4) score += 5; // 轻度超卖

            // 3. 量价关系 (25分)
            if (ctx.VolumeRatio > 1.2 && ctx.VolumeRatio < 2.5) score += 15; // 适度放量
            if (ctx.VolumeChangeRatio > 1.0 && ctx.PriceChangeRatio > 0) score += 10; // 量价齐升
            else if (ctx.VolumeRatio > 3.0) score -= 10; // 异常放量

            // 4. 换手率分析 (20分)
            if (ctx.TurnoverRate > 3 && ctx.TurnoverRate < 12) score += 20; // 活跃但不过热
            else if (ctx.TurnoverRate >= 12 && ctx.TurnoverRate < 20) score += 10; // 较活跃
            else if (ctx.TurnoverRate >= 20) score -= 15; // 过热风险

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 计算改进的基本面评分
        /// </summary>
        private static double CalculateImprovedFundamentalScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 盈利能力 (40分) - 提高权重
            if (ctx.ROE > 20) score += 40;
            else if (ctx.ROE > 15) score += 35;
            else if (ctx.ROE > 10) score += 25;
            else if (ctx.ROE > 5) score += 15;
            else if (ctx.ROE > 0) score += 5;

            // 2. 财务健康度 (30分) - 新增
            if (ctx.NetProfit > 0 && ctx.OperatingRevenue > 0) score += 15; // 盈利
            if (ctx.OperatingCashFlowPerShare > 0) score += 15; // 正现金流

            // 3. 估值合理度 (30分) - 调整估值标准
            if (ctx.PE > 0 && ctx.PE < 20) score += 30; // 低估值
            else if (ctx.PE >= 20 && ctx.PE < 35) score += 25; // 合理估值
            else if (ctx.PE >= 35 && ctx.PE < 50) score += 10; // 偏高估值
            else if (ctx.PE >= 50) score -= 10; // 高估值风险

            // 市值因素（小盘股适当加分）
            if (ctx.TotalMarketValue > 20 && ctx.TotalMarketValue < 200) score += 5;

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 计算改进的资金面评分
        /// </summary>
        private static double CalculateImprovedFundFlowScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 主力资金动向 (50分) - 提高权重
            if (ctx.MainForceNetInflow > 2000) score += 50;
            else if (ctx.MainForceNetInflow > 1000) score += 40;
            else if (ctx.MainForceNetInflow > 500) score += 30;
            else if (ctx.MainForceNetInflow > 0) score += 15;
            else if (ctx.MainForceNetInflow > -500) score += 0;
            else score -= 30; // 大幅流出

            // 2. 筹码结构 (30分)
            if (ctx.ProfitRatio > 30 && ctx.ProfitRatio < 70) score += 20; // 健康获利盘
            else if (ctx.ProfitRatio >= 70 && ctx.ProfitRatio < 85) score += 10; // 获利盘较多
            else if (ctx.ProfitRatio >= 85) score -= 15; // 获利盘过多风险
            else if (ctx.ProfitRatio < 20) score -= 10; // 深套

            if (ctx.ChipConcentration90 < 15) score += 10; // 筹码集中
            else if (ctx.ChipConcentration90 > 30) score -= 5; // 筹码分散

            // 3. 换手率与量比 (20分)
            if (ctx.TurnoverRate > 5 && ctx.TurnoverRate < 15 && ctx.VolumeRatio > 1.2) score += 20;
            else if (ctx.TurnoverRate > 3 && ctx.TurnoverRate < 20) score += 10;

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 计算估值评分（新增）
        /// </summary>
        private static double CalculateValueScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 综合估值评估
            if (ctx.PE > 0 && ctx.PB > 0)
            {
                // PB < 1.5 且 PE < 20 为低估值
                if (ctx.PB < 1.5 && ctx.PE < 20) score += 50;
                else if (ctx.PB < 2.0 && ctx.PE < 30) score += 40;
                else if (ctx.PB < 3.0 && ctx.PE < 40) score += 30;
                else if (ctx.PE < 50) score += 20;
                else score += 10;
            }
            else if (ctx.PE > 0)
            {
                // 仅PE可用
                if (ctx.PE < 20) score += 40;
                else if (ctx.PE < 35) score += 30;
                else if (ctx.PE < 50) score += 15;
            }

            // ROE/PB 比率（巴菲特指标）
            if (ctx.ROE > 0 && ctx.PB > 0)
            {
                double roeToPbRatio = ctx.ROE / ctx.PB;
                if (roeToPbRatio > 10) score += 30; // 优秀
                else if (roeToPbRatio > 5) score += 20; // 良好
                else if (roeToPbRatio > 2) score += 10; // 一般
            }

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 计算改进的风险评分
        /// </summary>
        private static double CalculateImprovedRiskScore(StockDeepAnalysisContext ctx)
        {
            double score = 100; // 从满分开始扣分

            // 技术风险
            if (ctx.BiasMA5 > 5) score -= 25; // 乖离率过大
            if (ctx.BiasMA5 > 8) score -= 15; // 极度乖离

            // 量价风险
            if (ctx.VolumeRatio > 3.0 && ctx.PriceChangeRatio < 2.0) score -= 30; // 放量滞涨
            if (ctx.VolumeRatio > 5.0) score -= 20; // 异常放量

            // 筹码风险
            if (ctx.ProfitRatio > 85) score -= 25; // 获利盘过多
            if (ctx.ProfitRatio > 90) score -= 15; // 极度获利

            // 资金风险
            if (ctx.MainForceNetInflow < -1000) score -= 30; // 主力大幅流出
            if (ctx.MainForceNetInflow < -2000) score -= 20; // 主力出逃

            // 估值风险
            if (ctx.PE > 80) score -= 15; // 极高估值
            if (ctx.PE < 0) score -= 10; // 亏损

            // 换手率风险
            if (ctx.TurnoverRate > 25) score -= 20; // 极度活跃
            if (ctx.TurnoverRate > 30) score -= 15; // 过度投机

            return Math.Max(0, score);
        }

        /// <summary>
        /// 计算动态权重（根据市场环境和股票特性）
        /// </summary>
        private static (double TechnicalWeight, double FundamentalWeight, double FundFlowWeight,
                 double SentimentWeight, double TrendWeight, double ValueWeight)
                 CalculateDynamicWeights(MarketCondition marketCondition, StockDeepAnalysisContext ctx)
        {
            // 基础权重
            double technicalWeight = 0.25;
            double fundamentalWeight = 0.25;
            double fundFlowWeight = 0.20;
            double sentimentWeight = 0.10;
            double trendWeight = 0.15;
            double valueWeight = 0.05;

            // 根据市场环境调整
            switch (marketCondition)
            {
                case MarketCondition.Strong:
                    // 强势市场：技术面和趋势面更重要
                    technicalWeight = 0.30;
                    trendWeight = 0.25;
                    fundamentalWeight = 0.20;
                    fundFlowWeight = 0.15;
                    break;

                case MarketCondition.Weak:
                    // 弱势市场：基本面和估值更重要
                    fundamentalWeight = 0.35;
                    valueWeight = 0.15;
                    technicalWeight = 0.15;
                    fundFlowWeight = 0.20;
                    trendWeight = 0.10;
                    break;

                case MarketCondition.Crash:
                    // 暴跌市场：资金面和风险控制最重要
                    fundFlowWeight = 0.30;
                    fundamentalWeight = 0.30;
                    technicalWeight = 0.10;
                    valueWeight = 0.20;
                    trendWeight = 0.05;
                    break;
            }

            // 根据股票特性调整
            if (ctx.TotalMarketValue < 50) // 小盘股
            {
                // 小盘股：技术面和资金面更重要
                technicalWeight += 0.05;
                fundFlowWeight += 0.05;
                fundamentalWeight -= 0.05;
                valueWeight -= 0.05;
            }
            else if (ctx.TotalMarketValue > 500) // 大盘股
            {
                // 大盘股：基本面和估值更重要
                fundamentalWeight += 0.05;
                valueWeight += 0.05;
                technicalWeight -= 0.05;
                fundFlowWeight -= 0.05;
            }

            return (technicalWeight, fundamentalWeight, fundFlowWeight,
                   sentimentWeight, trendWeight, valueWeight);
        }

        /// <summary>
        /// 计算改进的胜率预测
        /// </summary>
        private static double CalculateImprovedWinProbability(
            double overallScore, double riskScore, MarketCondition marketCondition,
            StockDeepAnalysisContext ctx)
        {
            double baseProbability = overallScore * 0.75; // 基础胜率

            // 风险调整（更保守）
            double riskAdjustment = riskScore / 100.0;
            baseProbability *= riskAdjustment;

            // 市场环境调整（更严格）
            double marketMultiplier = marketCondition switch
            {
                MarketCondition.Strong => 1.15,
                MarketCondition.Neutral => 1.0,
                MarketCondition.Weak => 0.65,
                MarketCondition.Crash => 0.25,
                _ => 1.0
            };

            baseProbability *= marketMultiplier;

            // 个股特殊情况调整
            if (ctx.ProfitRatio > 90) baseProbability *= 0.7; // 获利盘过多
            if (ctx.BiasMA5 > 6) baseProbability *= 0.8; // 乖离过大
            if (ctx.MainForceNetInflow < -1000) baseProbability *= 0.6; // 主力流出

            return Math.Max(10, Math.Min(90, baseProbability)); // 限制在10%-90%之间
        }

        /// <summary>
        /// 计算预测置信度（新增）
        /// </summary>
        private static double CalculateConfidenceLevel(EnhancedStockScore score, StockDeepAnalysisContext ctx)
        {
            double confidence = 50; // 基础置信度

            // 数据完整性加分
            int dataFields = 0;
            if (ctx.CurrentPrice > 0) dataFields++;
            if (ctx.MA5 > 0) dataFields++;
            if (ctx.PE > 0) dataFields++;
            if (ctx.ROE > 0) dataFields++;
            if (ctx.MainForceNetInflow != 0) dataFields++;
            if (ctx.TurnoverRate > 0) dataFields++;

            confidence += (dataFields / 6.0) * 20; // 最多加20分

            // 评分一致性加分
            double scoreVariance = Math.Abs(score.TechnicalScore - score.FundamentalScore) +
                                  Math.Abs(score.FundFlowScore - score.SentimentScore);
            if (scoreVariance < 30) confidence += 15; // 各维度评分一致

            // 综合评分置信度
            if (score.OverallScore > 80) confidence += 10;
            else if (score.OverallScore < 50) confidence -= 15;

            // 风险评分调整
            if (score.RiskScore < 50) confidence -= 20;

            return Math.Max(0, Math.Min(100, confidence));
        }

        /// <summary>
        /// 获取改进的推荐级别
        /// </summary>
        private static string GetImprovedRecommendationLevel(double overallScore, double riskScore, double confidence)
        {
            // 更严格的推荐标准
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

        /// <summary>
        /// 生成改进的操作建议
        /// </summary>
        private static void GenerateImprovedActionAdvice(
            EnhancedStockScore score,
            StockDeepAnalysisContext ctx,
            MarketCondition marketCondition)
        {
            // 更保守的操作策略
            if (marketCondition == MarketCondition.Crash)
            {
                score.ActionAdvice = "空仓观望";
                score.PositionSize = 0;
                return;
            }

            if (score.OverallScore >= 75 && score.RiskScore >= 65 && score.ConfidenceLevel >= 60)
            {
                score.ActionAdvice = "买入";
                // 动态仓位：根据市场环境和置信度调整
                double basePosition = marketCondition == MarketCondition.Strong ? 25 : 15;
                score.PositionSize = basePosition * (score.ConfidenceLevel / 100.0);

                score.SuggestedBuyPrice = (decimal)(ctx.CurrentPrice * 0.97); // 基础参考，具体见 SmartStop
                score.StopLossPrice = 0; // 废弃，由 HighWinRateStrategies.SmartStop 替代
                score.TargetPrice = 0;   // 废弃，由 HighWinRateStrategies.SmartStop 替代
            }
            else if (score.OverallScore >= 65 && score.RiskScore >= 55 && score.ConfidenceLevel >= 50)
            {
                score.ActionAdvice = "谨慎买入";
                score.PositionSize = 8;
                score.SuggestedBuyPrice = (decimal)(ctx.CurrentPrice * 0.94); 
                score.StopLossPrice = 0;
                score.TargetPrice = 0;
            }
            else
            {
                score.ActionAdvice = "观望";
                score.PositionSize = 0;
            }
        }

        /// <summary>
        /// 收集改进的信号
        /// </summary>
        private static void CollectImprovedSignals(EnhancedStockScore score, StockDeepAnalysisContext ctx)
        {
            // 积极信号（更精细）
            if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20)
                score.PositiveSignals.Add("✅ 完整多头排列");
            if (ctx.MainForceNetInflow > 1000)
                score.PositiveSignals.Add($"✅ 强势主力流入{(ctx.MainForceNetInflow / 10000.0):F2}万");
            if (ctx.ROE > 20)
                score.PositiveSignals.Add($"✅ 优秀ROE({ctx.ROE:F2}%)");
            if (ctx.TurnoverRate > 5 && ctx.TurnoverRate < 15)
                score.PositiveSignals.Add("✅ 活跃换手率");
            if (ctx.BiasMA5 > 0 && ctx.BiasMA5 < 2)
                score.PositiveSignals.Add("✅ 理想乖离率");

            // 风险信号（更全面）
            if (ctx.BiasMA5 > 5) score.RiskSignals.Add($"⚠️ 乖离率过大({ctx.BiasMA5:F2}%)");
            if (ctx.VolumeRatio > 3.0 && ctx.PriceChangeRatio < 2.0)
                score.RiskSignals.Add("⚠️ 放量滞涨风险");
            if (ctx.ProfitRatio > 85) score.RiskSignals.Add("⚠️ 获利盘过多");
            if (ctx.MainForceNetInflow < -800) score.RiskSignals.Add($"⚠️ 主力流出{(Math.Abs(ctx.MainForceNetInflow) / 10000.0):F2}万");
            if (ctx.TurnoverRate > 25) score.RiskSignals.Add("⚠️ 换手率过热");
            if (ctx.PE > 60) score.RiskSignals.Add("⚠️ 估值偏高");
        }

        // 保留原有的简单评分方法供其他地方使用
        private static double CalculateSentimentScore(StockDeepAnalysisContext ctx)
        {
            double score = 50;
            if (ctx.LatestNews.Count > 0)
            {
                foreach (var news in ctx.LatestNews)
                {
                    if (news.Contains("利好") || news.Contains("上涨")) score += 8;
                    if (news.Contains("利空") || news.Contains("下跌")) score -= 12;
                }
            }
            return Math.Max(0, Math.Min(100, score));
        }
    }
}