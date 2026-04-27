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

            // === 1. AI角色和核心原则 ===
            sb.AppendLine("# 🎯 顶级量化交易分析师");
            sb.AppendLine("你是一位基于多维度量化模型的顶级A股分析师，融合以下分析框架：");
            sb.AppendLine("- 技术分析: 均线/趋势/背离(MACD+RSI+量价)/布林带/多周期共振");
            sb.AppendLine("- 资金分析: 主力资金分级(超大/大/中/小单)/筹码分布/资金分流");
            sb.AppendLine("- 板块联动: 个股vs板块相对强度/板块内部排名");
            sb.AppendLine("- 情绪分析: 新闻情绪量化评分/波动率分位数");

            sb.AppendLine($"\n> 数据采集时间: {DateTime.Now:yyyy-MM-dd HH:mm}，以下所有行情与指标均为实时数据。");

            sb.AppendLine("\n## 📋 核心分析原则");
            sb.AppendLine("1. **背离信号优先**: MACD/RSI顶背离是最高级别看空信号，底背离是最高级别看多信号");
            sb.AppendLine("2. **多周期验证**: 日线+60分+15分共振时，信号强度翻倍");
            sb.AppendLine("3. **主力vs散户**: 超大单+大单=主力，中单+小单=散户；主力买散户卖=收集，主力卖散户买=出货");
            sb.AppendLine("4. **反身性**: 当大部分散户能看懂的\"买入信号\"出现时，需考虑是否已被price in");
            sb.AppendLine("5. **风险优先**: 任何风险信号都应得到相应扣分，宁缺毋滥");

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

            // === 3. 量化评分结果 ===
            sb.AppendLine("\n## 📊 多维度量化评分表（全部自选股）");
            string header = "| 排名 | 股票 | 综合 | 技术 | 基本面 | 资金 | 趋势 | 估值 | 板块 | 多周期 | 背离 | 风险 | 置信度 | 胜率 | 凯利 | 推荐 |";
            sb.AppendLine(header);
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            var highQualityStocks = scores
                .OrderByDescending(s => s.OverallScore)
                .ToList();

            int rank = 1;
            foreach (var score in highQualityStocks)
            {
                sb.AppendLine($"| {rank++} | {score.StockName} | {score.OverallScore:F0} | {score.TechnicalScore:F0} | " +
                             $"{score.FundamentalScore:F0} | {score.FundFlowScore:F0} | {score.TrendStrengthScore:F0} | " +
                             $"{score.ValueScore:F0} | {score.SectorStrengthScore:F0} | {score.MultiTimeframeScore:F0} | " +
                             $"{score.DivergenceScore:F0} | {score.RiskScore:F0} | {score.ConfidenceLevel:F0}% | " +
                             $"{score.WinProbability:F0}% | {score.KellyFraction:F0}% | {score.RecommendationLevel} |");
            }

            // 维度区分度分析
            string dimAnalysis = AdviceTracker.GetDimensionPerformanceAnalysis();
            if (!string.IsNullOrEmpty(dimAnalysis))
            {
                sb.AppendLine("\n**📉 历史维度区分度（区分度越高该维度越有效）**:");
                sb.AppendLine(dimAnalysis);
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
                    sb.AppendLine($"以下股票数据质量存在问题: {string.Join(", ", lowQualityStocks)}");
                }
            }

            // === 5. 输出格式要求 ===
            sb.AppendLine("\n## 📝 输出格式要求");
            sb.AppendLine("对**全部自选股**按以下格式逐一输出，**不得遗漏**:");
            sb.AppendLine("");
            sb.AppendLine("## 🎯 [股票名] ([代码]) - [推荐级别]");
            sb.AppendLine("");
            sb.AppendLine("### 📊 核心决策");
            sb.AppendLine("- 操作: [买入/谨慎买入/底背离关注/观望/顶背离减仓] | 仓位: [x%] | 综合评分: [x]/100");
            sb.AppendLine("- 买入参考: [价格]元 | 止损: [价格]元 ([止损理由])");
            sb.AppendLine("- 止盈1/2/3: [价格1]/[价格2]/[价格3]");
            sb.AppendLine("- 多空方向: [一句话判断]");
            sb.AppendLine("");
            sb.AppendLine("### ✅ 核心优势 (2-3条)");
            sb.AppendLine("- [优势1: 引用具体量化数据]");
            sb.AppendLine("- [优势2]");
            sb.AppendLine("");
            sb.AppendLine("### ⚠️ 风险警示 (2-3条，不可为空)");
            sb.AppendLine("- [风险1: 引用具体量化数据]");
            sb.AppendLine("- [风险2]");
            sb.AppendLine("");
            sb.AppendLine("### 🔍 深度分析");
            sb.AppendLine("- **技术面**: [形态+关键位+背离情况]");
            sb.AppendLine("- **资金面**: [主力行为+散户行为+筹码结构]");
            sb.AppendLine("- **板块联动**: [个股vs板块相对强度]");
            sb.AppendLine("- **多周期**: [日线/60分钟/15分钟MACD共振情况]");
            sb.AppendLine("- **基本面**: [估值与盈利能力]");
            sb.AppendLine("- **综合结论**: [最明确的建议]");
            sb.AppendLine("");
            sb.AppendLine("---");

            // === 6. 重要提醒 ===
            sb.AppendLine("\n## 🚨 重要提醒");
            sb.AppendLine("1. **背离优先**: 顶背离出现时即使其他指标好也必须建议减仓；底背离可适当增加关注度");
            sb.AppendLine("2. **主力追踪**: 超大单+大单净流向代表真实主力意图，将中单+小单流向与主力方向对比");
            sb.AppendLine("3. **量化约束**: 所有支撑/阻力/止损/目标价必须参考提供的量化数据，**禁止凭空编造**");
            sb.AppendLine("4. **反身性思维**: 当多数指标共振指向同一方向时，反问\"市场是否已经反映了这个信息\"");
            sb.AppendLine("5. **客观诚实**: 评分低的数据差的，坦诚给出负面诊断，不要为了\"看起来积极\"而粉饰");
            sb.AppendLine("6. **行情判定**: 当前环境为" + (marketCondition switch
            {
                MarketCondition.Crash => "暴跌 - 严格空仓",
                MarketCondition.Weak => "弱势 - 减少操作，关注抗跌标的",
                MarketCondition.Neutral => "中性 - 可正常参与",
                MarketCondition.Strong => "强势 - 可积极关注",
                _ => "不明"
            }));

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

                string sectorStr = sectors != null && sectors.TryGetValue(score.StockCode, out var s) ? $" [{s}]" : "";

                if (hasQualityIssue)
                {
                    sb.AppendLine($"### ⚠️ {score.StockName} ({score.StockCode}){sectorStr} - 数据质量问题，请谨慎分析");
                }
                else
                {
                    sb.AppendLine($"### 📊 {score.StockName} ({score.StockCode}){sectorStr}");
                }

                sb.AppendLine("**量化评分概览**:");
                sb.AppendLine($"- 综合:{score.OverallScore:F1} 技:{score.TechnicalScore:F1} 基:{score.FundamentalScore:F1} " +
                             $"资:{score.FundFlowScore:F1} 趋:{score.TrendStrengthScore:F1} 值:{score.ValueScore:F1}");
                sb.AppendLine($"- 板块:{score.SectorStrengthScore:F1} 多周期:{score.MultiTimeframeScore:F1} 背离:{score.DivergenceScore:F1}");
                sb.AppendLine($"- 风险分:{score.RiskScore:F1} 置信度:{score.ConfidenceLevel:F1}% 预期胜率:{score.WinProbability:F1}%");
                if (score.KellyFraction > 0)
                    sb.AppendLine($"- 凯利仓位:{score.KellyFraction:F1}% (满凯利:{score.KellyPosition:F1}%)");
                sb.AppendLine("");

                sb.AppendLine("**📈 实时行情**:");
                sb.AppendLine($"- 现价:{ctx.CurrentPrice:F2}元 (涨跌{ctx.PctChange:+0.00;-0.00}%) " +
                             $"今日量比:{ctx.VolumeRatio:F2} 换手:{ctx.TurnoverRate:F2}%");
                sb.AppendLine($"- 均线:MA5={ctx.MA5:F2} MA10={ctx.MA10:F2} MA20={ctx.MA20:F2} MA60={ctx.MA60:F2} MA120={ctx.MA120:F2}");
                sb.AppendLine($"- 乖离率:MA5={ctx.BiasMA5:+0.00;-0.00}% MA10={ctx.BiasMA10:+0.00;-0.00}% " +
                             $"MA60={ctx.BiasMA60:+0.00;-0.00}% 形态:{ctx.MAAlignment}");
                sb.AppendLine($"- 波动率:20日{ctx.Volatility20Day:F1}% 60日{ctx.Volatility60Day:F1}% 120日{ctx.Volatility120Day:F1}% " +
                             $"分位数{ctx.VolatilityPercentile:F0}%");

                if (ctx.TechScore != null)
                {
                    sb.AppendLine("**🛠️ 高级技术指标**:");
                    sb.AppendLine($"- MACD: 值={ctx.TechScore.MACD:F3} 柱状图={ctx.TechScore.MACDHistogram:F3} " +
                                 $"(分位{ctx.TechScore.MACDHistogramPercentile:F0}%)");
                    sb.AppendLine($"- RSI(6/12/24): {ctx.TechScore.RSI6:F1}/{ctx.TechScore.RSI12:F1}/{ctx.TechScore.RSI24:F1} " +
                                 $"(分位{ctx.TechScore.RSIPercentile:F0}%)");
                    sb.AppendLine($"- KDJ: K={ctx.TechScore.KDJ_K:F1} D={ctx.TechScore.KDJ_D:F1} J={ctx.TechScore.KDJ_J:F1}");
                    sb.AppendLine($"- 支撑阻力: 支撑1={ctx.TechScore.SupportLevel1:F2} 支撑2={ctx.TechScore.SupportLevel2:F2} " +
                                 $"阻力1={ctx.TechScore.ResistanceLevel1:F2} 阻力2={ctx.TechScore.ResistanceLevel2:F2}");
                    // 背离信号
                    if (!string.IsNullOrEmpty(ctx.TechScore.DivergenceDetail))
                        sb.AppendLine($"- 背离: {ctx.TechScore.DivergenceDetail}");
                    // 多周期共振
                    if (!string.IsNullOrEmpty(ctx.TechScore.MultiTimeframeDetail))
                        sb.AppendLine($"- 多周期: {ctx.TechScore.MultiTimeframeDetail}");
                    // 技术信号
                    if (ctx.TechScore.Signals.Count > 0)
                        sb.AppendLine($"- 信号: {string.Join(", ", ctx.TechScore.Signals.Take(5))}");
                }

                if (ctx.SmartStop != null && ctx.SmartStop.DynamicStopLoss > 0)
                {
                    sb.AppendLine("**🎯 量化交易计划 (参考)**:");
                    sb.AppendLine($"- ATR动态止损: {ctx.SmartStop.DynamicStopLoss:F2}元 (强防守位)");
                    sb.AppendLine($"- 移动止盈: {ctx.SmartStop.TrailingStop:F2}元");
                    sb.AppendLine($"- 阶梯止盈位: 第一目标={ctx.SmartStop.TargetPrice1:F2} 第二目标={ctx.SmartStop.TargetPrice2:F2} 第三目标={ctx.SmartStop.TargetPrice3:F2}");
                    if (ctx.SmartStop.ExitSignals.Count > 0)
                        sb.AppendLine($"- 离场信号: {string.Join(", ", ctx.SmartStop.ExitSignals.Take(2))}");
                }

                if (ctx.Timing != null && !string.IsNullOrEmpty(ctx.Timing.BestTimingWindow))
                {
                    sb.AppendLine($"- 择时建议: {ctx.Timing.BestTimingWindow} (评分:{ctx.Timing.OverallTimingScore:F1})");
                }
                sb.AppendLine("");

                // 板块联动数据
                sb.AppendLine("**📊 板块联动**:");
                if (!string.IsNullOrEmpty(ctx.SectorName))
                    sb.AppendLine($"- 所属板块: {ctx.SectorName} | 板块涨跌:{ctx.SectorPctChange:+0.00;-0.00}%");
                sb.AppendLine($"- 相对板块强度: {ctx.RelativeStrengthVsSector:+0.00;-0.00}% | 板块排名:{ctx.SectorRankPercent:F0}%");
                sb.AppendLine("");

                sb.AppendLine("**🏦 基本面**:");
                sb.AppendLine($"- 估值:PE={ctx.PE:F1} PB={ctx.PB:F1} 市值={ctx.TotalMarketValue:F1}亿");
                sb.AppendLine($"- 盈利:ROE={ctx.ROE:F1}% 净利润={ctx.NetProfit:F1}亿 营收={ctx.OperatingRevenue:F1}亿");
                sb.AppendLine($"- 现金流:{ctx.OperatingCashFlowPerShare:F2}元/股");
                sb.AppendLine("");

                sb.AppendLine("**🌊 资金面**:");
                // 主力资金分级展示
                string flowEmoji = ctx.MainForceNetInflow >= 0 ? "🟢" : "🔴";
                double absFlow = Math.Abs(ctx.MainForceNetInflow);
                string flowStr = absFlow >= 100000000
                    ? $"{ctx.MainForceNetInflow / 100000000.0:+0.00;-0.00}亿"
                    : $"{ctx.MainForceNetInflow / 10000.0:+0.00;-0.00}万";
                sb.AppendLine($"- {flowEmoji} 主力净流入:{flowStr} (占成交{ctx.MainForceInflowRatio:F1}%)");
                // 分级资金
                sb.AppendLine($"- 超大单:{FormatFlow(ctx.SuperLargeOrderInflow)} 大单:{FormatFlow(ctx.LargeOrderInflow)} " +
                             $"中单:{FormatFlow(ctx.MediumOrderInflow)} 小单:{FormatFlow(ctx.SmallOrderInflow)}");
                sb.AppendLine($"- 换手:{ctx.TurnoverRate:F1}% 成交额:{ctx.TurnoverAmount / 100000000.0:F2}亿");
                sb.AppendLine($"- 筹码:成本{ctx.ChipAvgCost:F2}元 获利盘{ctx.ProfitRatio:F1}% " +
                             $"集中度{ctx.ChipConcentration90:F1}%");
                sb.AppendLine($"- 筹码峰:压力位{ctx.ChipPeakPressure:F2} 支撑位{ctx.ChipPeakSupport:F2}");
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
                sb.AppendLine($"- 新闻情绪:{ctx.NewsSentimentScore:F0}/100 影响力:{ctx.NewsImpactScore:F0}/100");
                sb.AppendLine("");

                sb.AppendLine("---");
                sb.AppendLine("");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 格式化资金流向显示
        /// </summary>
        private static string FormatFlow(double flow)
        {
            double absFlow = Math.Abs(flow);
            string sign = flow >= 0 ? "+" : "-";
            if (absFlow >= 100000000)
                return $"{sign}{absFlow / 100000000:F2}亿";
            if (absFlow >= 10000)
                return $"{sign}{absFlow / 10000:F0}万";
            return $"{sign}{absFlow:F0}";
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
            List<MarketEnvironmentAnalyzer.MarketIndexData> indices,
            MarketCondition marketCondition = MarketCondition.Neutral)
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

            sb.AppendLine($"\n### 🌡️ 市场环境预判");
            sb.AppendLine($"- **量化评级**: {MarketEnvironmentAnalyzer.GetMarketOperationGuidance(marketCondition)}");
            // 添加近期对比基准：帮助AI判断今日是极端行情还是普通波动
            sb.AppendLine($"- **涨跌比**: {overview.UpCount}:{overview.DownCount} (涨{overview.UpCount}家 vs 跌{overview.DownCount}家)");
            if (overview.UpCount + overview.DownCount > 0)
            {
                double upRatio = (double)overview.UpCount / (overview.UpCount + overview.DownCount + overview.FlatCount) * 100;
                string breadthDesc = upRatio > 70 ? "赚钱效应强" : upRatio > 50 ? "偏暖" : upRatio > 30 ? "结构分化" : "亏钱效应强";
                sb.AppendLine($"- **赚钱效应**: {upRatio:F1}%个股上涨 ({breadthDesc})");
            }
            if (overview.TopSectors.Any() && overview.BottomSectors.Any())
            {
                double spread = overview.TopSectors.First().ChangePct - overview.BottomSectors.First().ChangePct;
                sb.AppendLine($"- **板块分化度**: {spread:F2}% (首位板块涨跌幅差，>5%为高度分化)");
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

            sb.AppendLine("\n## 📝 输出要求 (复盘报告格式)");
            sb.AppendLine("请严格按以下精简格式输出，杜绝废话：");
            sb.AppendLine("");
            sb.AppendLine("## 🏛️ [日期] A 股大盘极简复盘");
            sb.AppendLine("");
            sb.AppendLine("### 1. 核心定调 (Market Summary)");
            sb.AppendLine("（1句话总结今日市场真实情绪与环境：冰点/震荡/主升）");
            sb.AppendLine("");
            sb.AppendLine("### 2. 量价与情绪 (Volume & Emotion)");
            sb.AppendLine("（结合涨跌分布、涨跌停和成交量，判断做多动能和资金承接力，是否有量价背离）");
            sb.AppendLine("");
            sb.AppendLine("### 3. 主线脉络 (Sector Focus)");
            sb.AppendLine("（点出当前核心主线和轮动节奏，警惕退潮板块，不超2句话）");
            sb.AppendLine("");
            sb.AppendLine("### 4. 明日推演与策略 (Outlook & Strategy)");
            sb.AppendLine("- **走势预判**: [1句话推演明日走势剧本]");
            sb.AppendLine("- **仓位指引**: [建议总仓位%及加减仓动作]");
            sb.AppendLine("- **关注重点**: [重点关注的具体行业、连板梯队或防守方向]");
            sb.AppendLine("");
            sb.AppendLine("---");
            sb.AppendLine("*注：以上分析仅基于量化数据及 AI 推导，不构成投资建议。市场有风险，入市需谨慎。*");

            return sb.ToString();
        }
    }
}