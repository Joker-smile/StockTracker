using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StockTracker
{
    /// <summary>
    /// 量化评分系统 - 多维度胜率预测模型
    /// </summary>
    public class WinRatePredictionModel
    {
        public class StockScore
        {
            public string StockCode { get; set; } = string.Empty;
            public string StockName { get; set; } = string.Empty;

            // 维度评分
            public double TechnicalScore { get; set; }      // 技术面评分 (0-100)
            public double FundamentalScore { get; set; }    // 基本面评分 (0-100)
            public double FundFlowScore { get; set; }       // 资金面评分 (0-100)
            public double SentimentScore { get; set; }      // 情绪面评分 (0-100)
            public double RiskScore { get; set; }           // 风险评分 (0-100, 越高越安全)

            // 综合评分
            public double OverallScore { get; set; }        // 综合评分 (0-100)

            // 胜率预测
            public double WinProbability { get; set; }      // 预期胜率 (0-100%)
            public string RecommendationLevel { get; set; } = "观望"; // 推荐级别

            // 风险评估
            public List<string> RiskSignals { get; set; } = new();
            public List<string> PositiveSignals { get; set; } = new();

            // 操作建议
            public string ActionAdvice { get; set; } = "观望";        // 买入/观望/卖出
            public decimal SuggestedBuyPrice { get; set; }  // 建议买入价
            public decimal StopLossPrice { get; set; }      // 止损价
            public decimal TargetPrice { get; set; }        // 目标价
            public double PositionSize { get; set; }        // 建议仓位 (0-100%)
        }

        public static StockScore CalculateWinRateScore(StockDeepAnalysisContext ctx, MarketCondition marketCondition)
        {
            var score = new StockScore
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
                score.RiskSignals.Add("⚠️ 数据获取不完整，建议暂缓操作");
                return score;
            }

            // === 1. 技术面评分 (权重30%) ===
            score.TechnicalScore = CalculateTechnicalScore(ctx);

            // === 2. 基本面评分 (权重30%) ===
            score.FundamentalScore = CalculateFundamentalScore(ctx);

            // === 3. 资金面评分 (权重20%) ===
            score.FundFlowScore = CalculateFundFlowScore(ctx);

            // === 4. 情绪面评分 (权重20%) ===
            score.SentimentScore = CalculateSentimentScore(ctx);

            // === 5. 风险评分 ===
            score.RiskScore = CalculateRiskScore(ctx);

            // === 综合评分计算 ===
            score.OverallScore =
                score.TechnicalScore * 0.3 +
                score.FundamentalScore * 0.3 +
                score.FundFlowScore * 0.2 +
                score.SentimentScore * 0.2;

            // === 胜率预测 ===
            score.WinProbability = CalculateWinProbability(score.OverallScore, score.RiskScore, marketCondition);

            // === 推荐级别 ===
            score.RecommendationLevel = GetRecommendationLevel(score.OverallScore, score.RiskScore);

            // === 操作建议 ===
            GenerateActionAdvice(score, ctx, marketCondition);

            // === 信号收集 ===
            CollectSignals(score, ctx);

            return score;
        }

        private static bool IsDataValid(StockDeepAnalysisContext ctx)
        {
            // 检查关键字段是否有效
            if (ctx.CurrentPrice <= 0) return false;
            if (ctx.MA5 <= 0 && ctx.MA10 <= 0 && ctx.MA20 <= 0) return false;
            return true;
        }

        private static double CalculateTechnicalScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;
            int signalCount = 0;

            // 1. 趋势分析 (25分)
            if (ctx.MAAlignment.Contains("多头") || ctx.MA5 > ctx.MA20) { score += 15; signalCount++; }
            if (ctx.MA5 > ctx.MA10 && ctx.MA10 > ctx.MA20) { score += 10; signalCount++; }

            // 2. 均线系统 (20分)
            if (ctx.BiasMA5 > 0 && ctx.BiasMA5 < 3) { score += 10; signalCount++; } // 适度乖离
            else if (ctx.BiasMA5 > 5) { score -= 10; signalCount++; } // 过度乖离扣分
            else if (ctx.BiasMA5 < 0 && ctx.BiasMA5 > -2) { score += 5; } // 轻度负乖离

            // 3. 量价关系 (20分)
            if (ctx.VolumeRatio > 1.2 && ctx.VolumeRatio < 2.5) { score += 15; signalCount++; }
            if (ctx.VolumeChangeRatio > 1.0 && ctx.PriceChangeRatio > 0) { score += 5; signalCount++; } // 量价齐升

            // 4. 技术形态 (25分)
            if (ctx.MA5 > ctx.MA10 && ctx.BiasMA5 > 0 && ctx.BiasMA5 < 2) { score += 15; signalCount++; } // 均线多头排列
            if (ctx.TurnoverRate > 3 && ctx.TurnoverRate < 15) { score += 10; signalCount++; } // 换手率适中

            // 5. 价格位置 (10分)
            if (ctx.CurrentPrice > ctx.MA10 && ctx.CurrentPrice < ctx.MA10 * 1.05) { score += 10; signalCount++; } // 回踩MA10

            return Math.Max(0, Math.Min(100, score));
        }

        private static double CalculateFundamentalScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 估值合理 (30分)
            if (ctx.PE > 0 && ctx.PE < 30) { score += 20; }
            else if (ctx.PE >= 30 && ctx.PE < 50) { score += 10; }
            else if (ctx.PE >= 50) { score -= 10; } // 高估值扣分

            if (ctx.PB > 0 && ctx.PB < 3) { score += 10; }

            // 2. 盈利能力 (30分)
            if (ctx.ROE > 15) { score += 20; }
            else if (ctx.ROE > 10) { score += 15; }
            else if (ctx.ROE > 5) { score += 10; }

            if (ctx.NetProfit > 0 && ctx.OperatingRevenue > 0) { score += 10; }

            // 3. 现金流 (20分)
            if (ctx.OperatingCashFlowPerShare > 0) { score += 20; }

            // 4. 市值适中 (20分)
            if (ctx.TotalMarketValue > 50 && ctx.TotalMarketValue < 1000) { score += 20; }
            else if (ctx.TotalMarketValue >= 20 && ctx.TotalMarketValue <= 50) { score += 15; }

            return Math.Max(0, Math.Min(100, score));
        }

        private static double CalculateFundFlowScore(StockDeepAnalysisContext ctx)
        {
            double score = 0;

            // 1. 主力资金 (40分)
            if (ctx.MainForceNetInflow > 1000) { score += 40; }
            else if (ctx.MainForceNetInflow > 500) { score += 30; }
            else if (ctx.MainForceNetInflow > 0) { score += 20; }
            else { score -= 20; } // 主力流出大幅扣分

            // 2. 筹码结构 (30分)
            if (ctx.ProfitRatio > 40 && ctx.ProfitRatio < 80) { score += 20; }
            else if (ctx.ProfitRatio > 80) { score -= 10; } // 获利盘过多风险
            else if (ctx.ProfitRatio < 20) { score -= 10; } // 深套

            if (ctx.ChipConcentration90 < 20) { score += 10; } // 筹码集中

            // 3. 换手率 (30分)
            if (ctx.TurnoverRate > 3 && ctx.TurnoverRate < 15) { score += 30; }
            else if (ctx.TurnoverRate >= 15) { score -= 10; } // 换手率过高

            return Math.Max(0, Math.Min(100, score));
        }

        private static double CalculateSentimentScore(StockDeepAnalysisContext ctx)
        {
            double score = 50; // 基础分

            // 根据新闻情绪调整分数
            if (ctx.LatestNews.Count > 0)
            {
                foreach (var news in ctx.LatestNews)
                {
                    if (news.Contains("利好") || news.Contains("上涨") || news.Contains("突破")) { score += 10; }
                    if (news.Contains("利空") || news.Contains("下跌") || news.Contains("风险")) { score -= 15; }
                }
            }

            return Math.Max(0, Math.Min(100, score));
        }

        private static double CalculateRiskScore(StockDeepAnalysisContext ctx)
        {
            double score = 100; // 从满分开始扣分

            // 风险信号扣分
            if (ctx.BiasMA5 > 5) { score -= 20; } // 乖离率过大
            if (ctx.VolumeRatio > 3.0 && ctx.PriceChangeRatio < 2.0) { score -= 30; } // 放量滞涨
            if (ctx.ProfitRatio > 85) { score -= 20; } // 获利盘过多
            if (ctx.MainForceNetInflow < -500) { score -= 25; } // 主力大幅流出
            if (ctx.TurnoverRate > 20) { score -= 15; } // 换手率过高

            return Math.Max(0, score);
        }

        private static double CalculateWinProbability(double overallScore, double riskScore, MarketCondition marketCondition)
        {
            double baseProbability = overallScore * 0.8; // 基础胜率

            // 风险调整
            baseProbability = baseProbability * (riskScore / 100.0);

            // 市场环境调整
            baseProbability = marketCondition switch
            {
                MarketCondition.Strong => baseProbability * 1.2,
                MarketCondition.Neutral => baseProbability * 1.0,
                MarketCondition.Weak => baseProbability * 0.7,
                MarketCondition.Crash => baseProbability * 0.3,
                _ => baseProbability
            };

            return Math.Max(0, Math.Min(95, baseProbability)); // 最高95%胜率
        }

        private static string GetRecommendationLevel(double overallScore, double riskScore)
        {
            if (overallScore >= 80 && riskScore >= 70) return "⭐⭐⭐⭐⭐ 强烈推荐";
            if (overallScore >= 70 && riskScore >= 60) return "⭐⭐⭐⭐ 推荐";
            if (overallScore >= 60 && riskScore >= 50) return "⭐⭐⭐ 谨慎推荐";
            if (overallScore >= 50 && riskScore >= 40) return "⭐⭐ 观望";
            return "⭐ 不建议操作";
        }

        private static void GenerateActionAdvice(StockScore score, StockDeepAnalysisContext ctx, MarketCondition marketCondition)
        {
            // 根据市场环境和评分决定操作建议
            if (marketCondition == MarketCondition.Crash)
            {
                score.ActionAdvice = "空仓观望";
                score.PositionSize = 0;
                return;
            }

            if (score.OverallScore >= 75 && score.RiskScore >= 70)
            {
                score.ActionAdvice = "买入";
                score.PositionSize = marketCondition == MarketCondition.Strong ? 30 : 20;
                score.SuggestedBuyPrice = (decimal)(ctx.CurrentPrice * 0.98); // 略低于现价
                score.StopLossPrice = (decimal)(ctx.CurrentPrice * 0.92); // 8%止损
                score.TargetPrice = (decimal)(ctx.CurrentPrice * 1.15); // 15%目标
            }
            else if (score.OverallScore >= 60 && score.RiskScore >= 50)
            {
                score.ActionAdvice = "谨慎买入";
                score.PositionSize = 10;
                score.SuggestedBuyPrice = (decimal)(ctx.CurrentPrice * 0.95); // 等待回踩
                score.StopLossPrice = (decimal)(ctx.CurrentPrice * 0.90);
                score.TargetPrice = (decimal)(ctx.CurrentPrice * 1.10);
            }
            else
            {
                score.ActionAdvice = "观望";
                score.PositionSize = 0;
            }
        }

        private static void CollectSignals(StockScore score, StockDeepAnalysisContext ctx)
        {
            // 积极信号
            if (ctx.MA5 > ctx.MA20) score.PositiveSignals.Add("✅ 均线多头排列");
            if (ctx.MainForceNetInflow > 500) score.PositiveSignals.Add($"✅ 主力净流入{(ctx.MainForceNetInflow / 10000.0):F2}万");
            if (ctx.ROE > 15) score.PositiveSignals.Add($"✅ ROE优秀({ctx.ROE:F2}%)");
            if (ctx.TurnoverRate > 3 && ctx.TurnoverRate < 15) score.PositiveSignals.Add("✅ 换手率活跃");
            if (ctx.ProfitRatio > 40 && ctx.ProfitRatio < 70) score.PositiveSignals.Add("✅ 筹码结构良好");

            // 风险信号
            if (ctx.BiasMA5 > 5) score.RiskSignals.Add($"⚠️ 乖离率过大({ctx.BiasMA5:F2}%)");
            if (ctx.VolumeRatio > 3.0 && ctx.PriceChangeRatio < 2.0) score.RiskSignals.Add("⚠️ 放量滞涨");
            if (ctx.ProfitRatio > 85) score.RiskSignals.Add("⚠️ 获利盘过多");
            if (ctx.MainForceNetInflow < -500) score.RiskSignals.Add($"⚠️ 主力流出{(Math.Abs(ctx.MainForceNetInflow) / 10000.0):F2}万");
            if (ctx.TurnoverRate > 20) score.RiskSignals.Add("⚠️ 换手率过高");
        }
    }

    /// <summary>
    /// 市场环境枚举
    /// </summary>
    public enum MarketCondition
    {
        Crash,   // 暴跌：建议空仓
        Weak,    // 弱势：减少操作
        Neutral, // 中性：正常操作
        Strong   // 强势：可积极参与
    }

    /// <summary>
    /// 市场环境分析器
    /// </summary>
    public class MarketEnvironmentAnalyzer
    {
        public class MarketIndexData
        {
            public string Name { get; set; } = string.Empty;
            public double Price { get; set; }
            public double PctChange { get; set; }
        }

        public static MarketCondition AnalyzeMarketCondition(List<MarketIndexData> indices)
        {
            if (indices == null || indices.Count == 0)
                return MarketCondition.Neutral;

            double avgChange = indices.Average(x => x.PctChange);
            double fallCount = indices.Count(x => x.PctChange < -1.0);
            double riseCount = indices.Count(x => x.PctChange > 1.0);

            // 暴跌判断：平均跌幅超过2%或有2个以上指数暴跌
            if (avgChange < -2.0 || fallCount >= 2)
                return MarketCondition.Crash;

            // 弱势判断：平均跌幅超过0.5%
            if (avgChange < -0.5)
                return MarketCondition.Weak;

            // 强势判断：平均涨幅超过1%且至少2个指数上涨
            if (avgChange > 1.0 && riseCount >= 2)
                return MarketCondition.Strong;

            return MarketCondition.Neutral;
        }

        public static string GetMarketOperationGuidance(MarketCondition condition)
        {
            return condition switch
            {
                MarketCondition.Crash => "🔴 大盘环境恶劣，建议空仓观望，严禁操作",
                MarketCondition.Weak => "🟡 大盘弱势，建议减少操作，只参与高胜率机会",
                MarketCondition.Neutral => "🟢 大盘中性，可正常参与市场",
                MarketCondition.Strong => "🟢 大盘强势，可积极参与，适当扩大仓位",
                _ => "⚪ 市场环境不明，建议谨慎操作"
            };
        }
    }

    /// <summary>
    /// 优化的AI提示词构建器
    /// </summary>
    public class OptimizedAiPromptBuilder
    {
        public static string BuildAnalysisPrompt(List<WinRatePredictionModel.StockScore> scores, MarketCondition marketCondition, List<MarketEnvironmentAnalyzer.MarketIndexData> marketIndices)
        {
            var sb = new StringBuilder();

            // === 系统角色 ===
            sb.AppendLine("你是顶级A股量化分析师，基于多维度技术分析和量化评分模型，提供高胜率买入建议。");

            // === 核心原则 ===
            sb.AppendLine("\n【核心胜率原则】");
            sb.AppendLine("1. 只有多重技术指标共振且综合评分≥70分时才建议买入");
            sb.AppendLine("2. 任何单一风险信号都应降低仓位或建议观望");
            sb.AppendLine("3. 必须考虑市场环境，暴跌时强制建议空仓");
            sb.AppendLine("4. 买入建议必须包含明确的买入价、止损价、目标价和仓位建议");

            // === 市场环境 ===
            sb.AppendLine($"\n【当前市场环境】{MarketEnvironmentAnalyzer.GetMarketOperationGuidance(marketCondition)}");
            if (marketIndices != null && marketIndices.Count > 0)
            {
                sb.AppendLine("大盘指数：");
                foreach (var idx in marketIndices)
                {
                    string sign = idx.PctChange >= 0 ? "+" : "";
                    sb.AppendLine($"- {idx.Name}: {idx.Price:F2} ({sign}{idx.PctChange:F2}%)");
                }
            }

            // === 分析结果 ===
            var highScoreStocks = scores.OrderByDescending(s => s.OverallScore).ToList();

            sb.AppendLine("\n【量化评分结果】");
            sb.AppendLine("| 股票代码 | 股票名称 | 综合评分 | 技术面 | 基本面 | 资金面 | 情绪面 | 风险分 | 预期胜率 | 推荐级别 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");

            foreach (var score in highScoreStocks)
            {
                sb.AppendLine($"| {score.StockCode} | {score.StockName} | {score.OverallScore:F1} | {score.TechnicalScore:F1} | {score.FundamentalScore:F1} | {score.FundFlowScore:F1} | {score.SentimentScore:F1} | {score.RiskScore:F1} | {score.WinProbability:F1}% | {score.RecommendationLevel} |");
            }

            sb.AppendLine("\n【输出格式要求】");
            sb.AppendLine("对于上表中的每一只股票，请务必全部按以下格式逐一输出详细分析，不得遗漏：");
            sb.AppendLine("");
            sb.AppendLine("## 📊 [股票名称] ([代码]) - [推荐级别]");
            sb.AppendLine("**预期胜率**: [胜率%] | **综合评分**: [评分]/100");
            sb.AppendLine("");
            sb.AppendLine("### 🎯 操作建议");
            sb.AppendLine("- **操作**: [买入/观望/卖出]");
            sb.AppendLine("- **建议买入价**: [价格]元 (当前价的X%)");
            sb.AppendLine("- **止损价**: [价格]元 (下跌X%)");
            sb.AppendLine("- **目标价**: [价格]元 (上涨X%)");
            sb.AppendLine("- **建议仓位**: [仓位%]");
            sb.AppendLine("");
            sb.AppendLine("### ✅ 积极信号");
            sb.AppendLine("- [列出所有积极信号]");
            sb.AppendLine("");
            sb.AppendLine("### ⚠️ 风险提示");
            sb.AppendLine("- [列出所有风险信号]");
            sb.AppendLine("");
            sb.AppendLine("### 📈 分析依据");
            sb.AppendLine("- **技术面**: [简要说明技术分析]");
            sb.AppendLine("- **基本面**: [简要说明基本面分析]");
            sb.AppendLine("- **资金面**: [简要说明资金流向分析]");
            sb.AppendLine("");
            sb.AppendLine("---");

            return sb.ToString();
        }
    }
}