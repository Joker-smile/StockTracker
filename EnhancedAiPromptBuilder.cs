using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StockTracker
{
    /// <summary>
    /// 增强的AI提示词构建器 - 更精准的股票分析指导
    /// </summary>
    public class EnhancedAiPromptBuilder
    {
        public static string BuildEnhancedAnalysisPrompt(
            List<ImprovedWinRateScoring.EnhancedStockScore> scores,
            MarketCondition marketCondition,
            List<MarketEnvironmentAnalyzer.MarketIndexData> marketIndices,
            Dictionary<string, DataQualityValidator.ValidationResult> dataQualityResults)
        {
            var sb = new StringBuilder();

            // === 1. AI角色和核心原则（精简版） ===
            sb.AppendLine("# 🎯 顶级量化交易分析师");
            sb.AppendLine("你是一位基于多维度量化模型的顶级A股分析师，专注于提供高胜率、低风险的交易建议。");

            sb.AppendLine("\n## 📋 核心分析原则");
            sb.AppendLine("1. **量化优先**: 严格基于量化评分，只推荐综合评分≥70分的股票");
            sb.AppendLine("2. **风险控制**: 任何风险信号都应谨慎对待，宁缺毋滥");
            sb.AppendLine("3. **市场环境**: 根据大盘环境调整策略，恶劣环境强制观望");
            sb.AppendLine("4. **置信度**: 对预测置信度低于50%的建议要特别谨慎");

            // === 2. 市场环境分析 ===
            sb.AppendLine($"\n## 🌍 当前市场环境");
            sb.AppendLine($"**状态**: {MarketEnvironmentAnalyzer.GetMarketOperationGuidance(marketCondition)}");

            if (marketIndices != null && marketIndices.Count > 0)
            {
                sb.AppendLine("\n**大盘指数表现**:");
                foreach (var idx in marketIndices)
                {
                    string emoji = idx.PctChange switch
                    {
                        > 1 => "🟢",
                        > 0 => "🔵",
                        > -1 => "🟡",
                        _ => "🔴"
                    };
                    sb.AppendLine($"- {emoji} {idx.Name}: {idx.Price:F2} ({idx.PctChange:+0.00;-0.00}%)");
                }
            }

            // === 3. 量化评分结果（按优先级排序） ===
            var highQualityStocks = scores
                .Where(s => s.OverallScore >= 60 && s.ConfidenceLevel >= 40)
                .OrderByDescending(s => s.OverallScore)
                .ThenByDescending(s => s.ConfidenceLevel)
                .ToList();

            sb.AppendLine("\n## 📊 量化评分结果（仅显示评分≥60且置信度≥40的股票）");
            sb.AppendLine("| 排名 | 股票 | 代码 | 综合评分 | 置信度 | 胜率预测 | 技术面 | 基本面 | 资金面 | 风险分 | 推荐 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

            int rank = 1;
            foreach (var score in highQualityStocks.Take(10)) // 最多显示前10名
            {
                string recommendationEmoji = score.OverallScore switch
                {
                    >= 75 => "⭐⭐⭐⭐⭐",
                    >= 70 => "⭐⭐⭐⭐",
                    >= 60 => "⭐⭐⭐",
                    _ => "⭐⭐"
                };

                sb.AppendLine($"| {rank++} | {score.StockName} | {score.StockCode} | " +
                             $"{score.OverallScore:F1} | {score.ConfidenceLevel:F1}% | " +
                             $"{score.WinProbability:F1}% | {score.TechnicalScore:F1} | " +
                             $"{score.FundamentalScore:F1} | {score.FundFlowScore:F1} | " +
                             $"{score.RiskScore:F1} | {recommendationEmoji} |");
            }

            // === 4. 数据质量提醒 ===
            if (dataQualityResults != null && dataQualityResults.Count > 0)
            {
                var lowQualityStocks = dataQualityResults
                    .Where(kvp => !kvp.Value.IsValid || kvp.Value.DataCompletenessScore < 60)
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (lowQualityStocks.Count > 0)
                {
                    sb.AppendLine($"\n## ⚠️ 数据质量警告");
                    sb.AppendLine($"以下股票数据质量存在问题，建议谨慎对待或跳过:");
                    sb.AppendLine($"- {string.Join(", ", lowQualityStocks)}");
                }
            }

            // === 5. 分析输出要求 ===
            sb.AppendLine("\n## 📝 输出要求");

            sb.AppendLine("\n### 📊 每只股票的分析格式");
            sb.AppendLine("对于每个**评分≥70分且置信度≥50%**的股票，按以下格式输出:");
            sb.AppendLine("");
            sb.AppendLine("```");
            sb.AppendLine("## 🎯 [股票名称] ([代码]) - [推荐级别]");
            sb.AppendLine("");
            sb.AppendLine("### 📈 核心数据");
            sb.AppendLine("- **综合评分**: [评分]/100 | **置信度**: [置信度%]");
            sb.AppendLine("- **预期胜率**: [胜率%] | **操作建议**: [买入/观望]");
            sb.AppendLine("- **当前价格**: [价格]元 | **建议仓位**: [仓位%]");
            sb.AppendLine("");
            sb.AppendLine("### 🎯 操作策略");
            sb.AppendLine("- **建议买入价**: [价格]元 ([当前价的X%])");
            sb.AppendLine("- **止损价格**: [价格]元 ([下跌X%])");
            sb.AppendLine("- **目标价格**: [价格]元 ([上涨X%])");
            sb.AppendLine("- **持有周期**: [短期/中期/长期]");
            sb.AppendLine("");
            sb.AppendLine("### ✅ 核心优势");
            sb.AppendLine("- [列出2-3个最关键的积极信号，每个信号要具体且量化]");
            sb.AppendLine("");
            sb.AppendLine("### ⚠️ 风险提示");
            sb.AppendLine("- [列出2-3个最重要的风险信号，每个风险要具体且可量化]");
            sb.AppendLine("");
            sb.AppendLine("### 📊 分析依据");
            sb.AppendLine("**技术面**: [1-2句话总结技术分析，重点说明趋势和支撑阻力]");
            sb.AppendLine("");
            sb.AppendLine("**基本面**: [1-2句话总结基本面分析，重点说明估值和盈利能力]");
            sb.AppendLine("");
            sb.AppendLine("**资金面**: [1-2句话总结资金流向分析，重点说明主力动向和筹码结构]");
            sb.AppendLine("");
            sb.AppendLine("**综合判断**: [1句话给出明确的操作建议和理由]");
            sb.AppendLine("```");
            sb.AppendLine("");
            sb.AppendLine("---");

            // === 6. 特别提醒 ===
            sb.AppendLine("\n## 🚨 重要提醒");
            sb.AppendLine("1. **严格遵守评分**: 只分析评分≥70分的股票，其他股票一律不建议操作");
            sb.AppendLine("2. **风险优先**: 风险分<50或置信度<50%的股票，即使评分高也要谨慎");
            sb.AppendLine("3. **市场环境**: 当前市场环境为" + (marketCondition switch
            {
                MarketCondition.Crash => "暴跌状态，强烈建议空仓观望",
                MarketCondition.Weak => "弱势状态，只参与极高评分(≥75)的股票",
                MarketCondition.Neutral => "中性状态，正常参与高评分股票",
                MarketCondition.Strong => "强势状态，可积极参与高评分股票",
                _ => "不稳定状态"
            }));
            sb.AppendLine("4. **量化客观**: 基于客观数据分析，不要被市场情绪影响");
            sb.AppendLine("5. **宁缺毋滥**: 没有符合条件的股票就建议观望，不要强行推荐");

            return sb.ToString();
        }

        /// <summary>
        /// 构建详细股票数据提示词
        /// </summary>
        public static string BuildDetailedStockDataPrompt(
            List<ImprovedWinRateScoring.EnhancedStockScore> scores,
            Dictionary<string, StockDeepAnalysisContext> stockContexts,
            Dictionary<string, DataQualityValidator.ValidationResult> dataQualityResults)
        {
            var sb = new StringBuilder();

            sb.AppendLine("\n## 📋 详细股票数据");
            sb.AppendLine("以下是各股票的详细数据，请结合量化评分进行深度分析:");
            sb.AppendLine("");

            // 只分析高质量股票
            var highQualityStocks = scores
                .Where(s => s.OverallScore >= 60 && s.ConfidenceLevel >= 40)
                .OrderByDescending(s => s.OverallScore)
                .Take(10)
                .ToList();

            foreach (var score in highQualityStocks)
            {
                if (!stockContexts.ContainsKey(score.StockCode)) continue;

                var ctx = stockContexts[score.StockCode];
                bool hasQualityIssue = dataQualityResults.ContainsKey(score.StockCode) &&
                                     (!dataQualityResults[score.StockCode].IsValid ||
                                      dataQualityResults[score.StockCode].DataCompletenessScore < 60);

                if (hasQualityIssue)
                {
                    sb.AppendLine($"### ⚠️ {score.StockName} ({score.StockCode}) - 数据质量问题，请谨慎分析");
                }
                else
                {
                    sb.AppendLine($"### 📊 {score.StockName} ({score.StockCode})");
                }

                sb.AppendLine("**量化评分概览**:");
                sb.AppendLine($"- 综合:{score.OverallScore:F1} 技:{score.TechnicalScore:F1} 基:{score.FundamentalScore:F1} " +
                             $"资:{score.FundFlowScore:F1} 趋:{score.TrendStrengthScore:F1} 值:{score.ValueScore:F1}");
                sb.AppendLine($"- 风险分:{score.RiskScore:F1} 置信度:{score.ConfidenceLevel:F1}% 预期胜率:{score.WinProbability:F1}%");
                sb.AppendLine("");

                sb.AppendLine("**📈 实时行情**:");
                sb.AppendLine($"- 现价:{ctx.CurrentPrice:F2}元 (涨跌{ctx.PctChange:+0.00;-0.00}%) " +
                             $"量比:{ctx.VolumeRatio:F2} 换手:{ctx.TurnoverRate:F2}%");
                sb.AppendLine($"- 均线:MA5={ctx.MA5:F2} MA10={ctx.MA10:F2} MA20={ctx.MA20:F2}");
                sb.AppendLine($"- 乖离率:MA5={ctx.BiasMA5:+0.00;-0.00}% MA10={ctx.BiasMA10:+0.00;-0.00}% " +
                             $"形态:{ctx.MAAlignment}");
                sb.AppendLine("");

                sb.AppendLine("**🏦 基本面**:");
                sb.AppendLine($"- 估值:PE={ctx.PE:F1} PB={ctx.PB:F1} 市值={ctx.TotalMarketValue:F1}亿");
                sb.AppendLine($"- 盈利:ROE={ctx.ROE:F1}% 净利润={ctx.NetProfit:F1}亿 营收={ctx.OperatingRevenue:F1}亿");
                sb.AppendLine($"- 现金流:{ctx.OperatingCashFlowPerShare:F2}元/股");
                sb.AppendLine("");

                sb.AppendLine("**🌊 资金面**:");
                string flowEmoji = ctx.MainForceNetInflow >= 0 ? "🟢" : "🔴";
                sb.AppendLine($"- {flowEmoji} 主力:{ctx.MainForceNetInflow/10000:+0.00;-0.00}万 " +
                             $"换手:{ctx.TurnoverRate:F1}%");
                sb.AppendLine($"- 筹码:成本{ctx.ChipAvgCost:F2}元 获利盘{ctx.ProfitRatio:F1}% " +
                             $"集中度{ctx.ChipConcentration90:F1}%");
                sb.AppendLine("");

                sb.AppendLine("**📰 舆情面**:");
                if (ctx.LatestNews.Count > 0)
                {
                    foreach (var news in ctx.LatestNews.Take(3))
                    {
                        sb.AppendLine($"- {news}");
                    }
                }
                else
                {
                    sb.AppendLine("- 无重大新闻");
                }

                sb.AppendLine("");
                sb.AppendLine("---");
                sb.AppendLine("");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 构建回测优化建议提示词
        /// </summary>
        public static string BuildBacktestOptimizationPrompt(BackTestResult backtestResult)
        {
            var sb = new StringBuilder();

            sb.AppendLine("\n## 📈 历史表现数据");
            sb.AppendLine($"**{backtestResult.GetSummary()}**");
            sb.AppendLine("");

            if (backtestResult.ScoreRangeAnalysis != null && backtestResult.ScoreRangeAnalysis.Count > 0)
            {
                sb.AppendLine("**评分区间表现**:");
                foreach (var range in backtestResult.ScoreRangeAnalysis.OrderByDescending(r => r.WinRate))
                {
                    string emoji = range.WinRate switch
                    {
                        > 60 => "🟢",
                        > 40 => "🟡",
                        _ => "🔴"
                    };
                    sb.AppendLine($"- {emoji} {range.ScoreRange}: 胜率{range.WinRate:F1}% " +
                                 $"平均盈亏{range.AverageProfit:+0.00;-0.00}%");
                }
                sb.AppendLine("");
            }

            if (backtestResult.MarketConditionAnalysis != null && backtestResult.MarketConditionAnalysis.Count > 0)
            {
                sb.AppendLine("**市场环境影响**:");
                foreach (var market in backtestResult.MarketConditionAnalysis.OrderByDescending(m => m.WinRate))
                {
                    string emoji = market.WinRate switch
                    {
                        > 60 => "🟢",
                        > 40 => "🟡",
                        _ => "🔴"
                    };
                    sb.AppendLine($"- {emoji} {market.MarketCondition}: 胜率{market.WinRate:F1}% " +
                                 $"平均盈亏{market.AverageProfit:+0.00;-0.00}%");
                }
                sb.AppendLine("");
            }

            var suggestions = AdviceTracker.GetOptimizationSuggestions();
            if (suggestions.Count > 0)
            {
                sb.AppendLine("**🎯 优化建议**:");
                foreach (var suggestion in suggestions)
                {
                    sb.AppendLine($"- {suggestion}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 构建完整分析提示词
        /// </summary>
        public static string BuildCompleteAnalysisPrompt(
            List<ImprovedWinRateScoring.EnhancedStockScore> scores,
            MarketCondition marketCondition,
            List<MarketEnvironmentAnalyzer.MarketIndexData> marketIndices,
            Dictionary<string, StockDeepAnalysisContext> stockContexts,
            Dictionary<string, DataQualityValidator.ValidationResult> dataQualityResults,
            BackTestResult? backtestResult = null)
        {
            var prompt = new StringBuilder();

            // 1. 基础分析提示词
            prompt.Append(BuildEnhancedAnalysisPrompt(scores, marketCondition, marketIndices, dataQualityResults));

            // 2. 详细股票数据
            prompt.Append(BuildDetailedStockDataPrompt(scores, stockContexts, dataQualityResults));

            // 3. 历史表现数据（如果有）
            if (backtestResult != null && backtestResult.TotalTrades > 0)
            {
                prompt.Append(BuildBacktestOptimizationPrompt(backtestResult));
            }

            // 4. 最终要求
            prompt.AppendLine("\n## 📤 输出说明");
            prompt.AppendLine("请严格按照上述格式输出，不要添加额外的解释性文字。");
            prompt.AppendLine("重点突出量化数据和操作建议，确保建议的可执行性。");
            prompt.AppendLine("如果没有任何股票符合条件，请明确说明\"当前没有符合条件的股票，建议观望\"。");

            return prompt.ToString();
        }
    }
}