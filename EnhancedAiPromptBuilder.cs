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
            IDictionary<string, DataQualityValidator.ValidationResult> dataQualityResults)
        {
            var sb = new StringBuilder();

            // === 1. AI角色和核心原则（精简版） ===
            sb.AppendLine("# 🎯 顶级量化交易分析师");
            sb.AppendLine("你是一位基于多维度量化模型的顶级A股分析师，专注于提供高胜率、低风险的交易建议。");

            sb.AppendLine("\n## 📋 核心分析原则");
            sb.AppendLine("1. **全面分析**: 对每一只提供的自选股进行深度诊断，评分仅作为参考逻辑。");
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
            sb.AppendLine("\n## 📊 量化评分结果（全部自选股）");
            sb.AppendLine("| 排名 | 股票 | 代码 | 综合评分 | 置信度 | 胜率预测 | 技术面 | 基本面 | 资金面 | 风险分 | 推荐 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

            var highQualityStocks = scores
                .OrderByDescending(s => s.OverallScore)
                .ToList();

            int rank = 1;
            foreach (var score in highQualityStocks) // 分析所有自选股
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
            sb.AppendLine("请对以下所有自选股按统一格式输出深度诊断:");
            sb.AppendLine("");
            sb.AppendLine("```");
            sb.AppendLine("## 🎯 [股票名称] ([代码]) - [评分建议]");
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
            sb.AppendLine("1. **深度诊断**: 你必须根据提供的数据对每只股票进行深度剖析，不要泛泛而谈，要结合具体的技术图形特征和资金流向进行逻辑推演。");
            sb.AppendLine("2. **全量诊断**: 你必须对用户列表中的每一只股票进行分析。评分反映了量化胜率，但你的分析应体现更多深度推演。");
            sb.AppendLine("3. **评分参考**: 评分仅代表量化模型建议，不要因为评分低而强制过滤或拒绝分析。");
            sb.AppendLine("4. **市场环境**: 当前市场环境为" + (marketCondition switch
            {
                MarketCondition.Crash => "暴跌状态，强烈建议空仓观望",
                MarketCondition.Weak => "弱势状态，建议寻找具备底部分型或逆势抗跌的个股",
                MarketCondition.Neutral => "中性状态，可关注中长期趋势向好的股票",
                MarketCondition.Strong => "强势状态，可积极关注顺势上攻的个股",
                _ => "不稳定状态"
            }));
            sb.AppendLine("5. **量化客观**: 基于客观数据分析，不要被市场情绪影响");
            sb.AppendLine("6. **知无不言**: 即使数据有缺失或评分较低，也要基于现有信息给出最专业的诊断建议。");

            return sb.ToString();
        }

        /// <summary>
        /// 构建详细股票数据提示词
        /// </summary>
        public static string BuildDetailedStockDataPrompt(
            List<ImprovedWinRateScoring.EnhancedStockScore> scores,
            IDictionary<string, StockDeepAnalysisContext> stockContexts,
            IDictionary<string, DataQualityValidator.ValidationResult> dataQualityResults,
            IDictionary<string, string>? sectors = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("\n## 📋 详细股票数据");
            sb.AppendLine("以下是各股票的详细数据，请结合量化评分进行深度分析:");
            sb.AppendLine("");

            // 分析所有股票
            var highQualityStocks = scores
                .OrderByDescending(s => s.OverallScore)
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
                    string sectorStr = sectors != null && sectors.TryGetValue(score.StockCode, out var s) ? $" [{s}]" : "";
                    sb.AppendLine($"### ⚠️ {score.StockName} ({score.StockCode}){sectorStr} - 数据质量问题，请谨慎分析");
                }
                else
                {
                    string sectorStr = sectors != null && sectors.TryGetValue(score.StockCode, out var s) ? $" [{s}]" : "";
                    sb.AppendLine($"### 📊 {score.StockName} ({score.StockCode}){sectorStr}");
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
                    foreach (var news in ctx.LatestNews.Take(5))
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
            IDictionary<string, StockDeepAnalysisContext> stockContexts,
            IDictionary<string, DataQualityValidator.ValidationResult> dataQualityResults,
            BackTestResult? backtestResult = null,
            IDictionary<string, string>? sectors = null)
        {
            var prompt = new StringBuilder();

            // 1. 基础分析提示词
            prompt.Append(BuildEnhancedAnalysisPrompt(scores, marketCondition, marketIndices, dataQualityResults));

            // 2. 详细股票数据
            prompt.Append(BuildDetailedStockDataPrompt(scores, stockContexts, dataQualityResults, sectors));

            // 3. 历史表现数据（如果有）
            if (backtestResult != null && backtestResult.TotalTrades > 0)
            {
                prompt.Append(BuildBacktestOptimizationPrompt(backtestResult));
            }

            // 4. 最终要求
            prompt.AppendLine("\n## 📤 输出说明");
            prompt.AppendLine("请严格按照上述格式输出，不要添加额外的解释性文字。");
            prompt.AppendLine("重点突出量化数据和操作建议，确保建议的可执行性。");
            prompt.AppendLine("请严格对上述数据中的每一只股票进行专业诊断，体现分析的专业性。");

            return prompt.ToString();
        }

        /// <summary>
        /// 构建大盘复盘分析提示词
        /// </summary>
        public static string BuildMarketReviewPrompt(
            MarketOverviewData overview,
            List<MarketEnvironmentAnalyzer.MarketIndexData> indices)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# 🏛️ 全量大盘复盘分析专家");
            sb.AppendLine("你是一位资深的宏观策略分析师，擅长从指数走势、市场广度、板块轮动及宏观新闻中洞察市场本质并制定应对策略。");

            sb.AppendLine($"\n## 📅 市场数据 ({overview.Date})");
            
            if (indices != null && indices.Count > 0)
            {
                sb.AppendLine("### 📈 主要指数表现");
                foreach (var idx in indices)
                {
                    string emoji = idx.PctChange switch
                    {
                        > 1 => "🟢",
                        > 0 => "🔵",
                        > -1 => "🟡",
                        _ => "🔴"
                    };
                    sb.AppendLine($"- {emoji} **{idx.Name}**: {idx.Price:F2} ({idx.PctChange:+0.00;-0.00}%)");
                }
            }

            sb.AppendLine("\n### 📊 市场广度与流动性");
            sb.AppendLine($"- **涨跌分布**: 🟢 上涨 **{overview.UpCount}** | 🔴 下跌 **{overview.DownCount}** | ⚪ 平盘 **{overview.FlatCount}**");
            sb.AppendLine($"- **高度活跃**: 🔥 涨停 **{overview.LimitUpCount}** | ❄️ 跌停 **{overview.LimitDownCount}**");
            sb.AppendLine($"- **成交规模**: 💰 两市成交额 **{overview.TotalAmount:F1}** 亿");

            sb.AppendLine("\n### 🎭 板块表现");
            if (overview.TopSectors.Any())
                sb.AppendLine($"- **🔥 领涨板块**: {string.Join(" | ", overview.TopSectors.Select(s => $"{s.Name}({s.ChangePct:+0.00;-0.00}%)"))}");
            if (overview.BottomSectors.Any())
                sb.AppendLine($"- **💧 领跌板块**: {string.Join(" | ", overview.BottomSectors.Select(s => $"{s.Name}({s.ChangePct:+0.00;-0.00}%)"))}");

            sb.AppendLine("\n### 📰 宏观/市场要闻");
            if (overview.MarketNews.Any())
            {
                foreach (var news in overview.MarketNews.Take(6))
                {
                    sb.AppendLine(news);
                }
            }
            else
            {
                sb.AppendLine("- 暂无显著宏观变动。");
            }

            sb.AppendLine("\n---");

            sb.AppendLine("\n## 📝 输出要求 (极致精炼复盘报告)");
            sb.AppendLine("你必须扮演一位冷静、极简的基金经理。禁止废话，禁止通用开场白，直接输出核心干货。");
            sb.AppendLine("要求使用 Github Markdown 格式，利用表格或精炼列表。");
            sb.AppendLine("");
            sb.AppendLine("### 🏛️ [日期] A 股大盘 AI 极简复盘");
            sb.AppendLine("");
            sb.AppendLine("#### 1️⃣ 市场体温 (Snapshot)");
            sb.AppendLine("- **多空比**: {上涨}/{下跌} ({上涨/总数}%)");
            sb.AppendLine("- **高度板**: 涨停 {数量} / 跌停 {数量} (评估短线情绪风险)");
            sb.AppendLine("- **量能**: {成交额} 亿 ({放量/缩量} 预警)");
            sb.AppendLine("");
            sb.AppendLine("#### 2️⃣ 指数共振 (Pulse)");
            sb.AppendLine("- **核心特征**: [用10字以内概括指数状态，例如：沪指破位/创业板缩量十字星]");
            sb.AppendLine("- **风险点**: [核心预警点，无则忽略]");
            sb.AppendLine("");
            sb.AppendLine("#### 3️⃣ 题材轮动 (Rotation)");
            sb.AppendLine("- **最强音**: {领涨板块} (1句话核心逻辑)");
            sb.AppendLine("- **避雷点**: {领跌板块} (1句话核心风险)");
            sb.AppendLine("");
            sb.AppendLine("#### 4️⃣ 操盘决策 (Action Signal)");
            sb.AppendLine("- **定性**: [进攻/防御/观望]");
            sb.AppendLine("- **仓位**: [具体百分比]");
            sb.AppendLine("- **防守**: [核心支撑位或风控逻辑]");
            sb.AppendLine("");
            sb.AppendLine("---");
            sb.AppendLine("*注：极简推演，不构成建议。*");

            return sb.ToString();
        }
    }
}