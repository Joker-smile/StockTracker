using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockTracker
{
    /// <summary>
    /// 数据质量验证系统 - 确保AI分析基于高质量数据
    /// </summary>
    public class DataQualityValidator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Warnings { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public List<string> Info { get; set; } = new();
            public double DataCompletenessScore { get; set; } // 数据完整性评分 0-100
            public List<string> MissingFields { get; set; } = new();
            public List<string> SuspiciousFields { get; set; } = new();

            public string GetSummary()
            {
                if (!IsValid)
                {
                    return $"🔴 数据验证失败: {Errors.Count}个错误, {Warnings.Count}个警告";
                }
                else if (Warnings.Count > 0)
                {
                    return $"🟡 数据可用但有{Warnings.Count}个警告";
                }
                else
                {
                    return $"🟢 数据质量良好 (完整性:{DataCompletenessScore:F1}%)";
                }
            }
        }

        /// <summary>
        /// 全面验证股票数据质量
        /// </summary>
        public static ValidationResult ValidateStockData(StockDeepAnalysisContext ctx)
        {
            var result = new ValidationResult { IsValid = true };
            int totalFields = 0;
            int validFields = 0;

            // === 1. 基础数据验证 ===
            ValidateBasicData(ctx, result, ref totalFields, ref validFields);

            // === 2. 技术数据验证 ===
            ValidateTechnicalData(ctx, result, ref totalFields, ref validFields);

            // === 3. 基本面数据验证 ===
            ValidateFundamentalData(ctx, result, ref totalFields, ref validFields);

            // === 4. 资金数据验证 ===
            ValidateFundFlowData(ctx, result, ref totalFields, ref validFields);

            // === 5. 数据一致性验证 ===
            ValidateDataConsistency(ctx, result);

            // === 6. 数据源可用性验证 ===
            ValidateSourceReliability(ctx, result, ref totalFields, ref validFields);

            // === 6. 计算数据完整性评分 ===
            result.DataCompletenessScore = totalFields > 0 ? (validFields / (double)totalFields) * 100 : 0;

            // === 7. 最终判断 ===
            if (result.Errors.Count > 0)
            {
                result.IsValid = false;
            }
            else if (result.DataCompletenessScore < 40)
            {
                result.IsValid = false;
                result.Errors.Add("数据完整性过低，无法进行可靠分析");
            }
            else if (result.DataCompletenessScore < 60)
            {
                result.Warnings.Add("数据完整性偏低，分析结果可能不够准确");
            }

            return result;
        }

        private static void ValidateSourceReliability(
            StockDeepAnalysisContext ctx,
            ValidationResult result,
            ref int totalFields,
            ref int validFields)
        {
            if (ctx.DataPoints.Count == 0)
            {
                result.Warnings.Add("数据源元信息缺失，无法区分真实0值与接口缺失");
                return;
            }

            var coreFields = new[] { "CurrentPrice", "RecentPrices", "TechScore", "MainForceNetInflow", "Prices60Min", "Prices15Min" };
            foreach (var field in coreFields)
            {
                totalFields++;
                var points = ctx.DataPoints.Where(p => p.FieldName == field).ToList();
                if (points.Any(p => !p.IsMissing))
                {
                    validFields++;
                }
                else
                {
                    result.MissingFields.Add(field);
                    result.Warnings.Add($"核心量化字段缺失: {field}");
                }
            }

            foreach (var missing in ctx.DataPoints.Where(p => p.IsMissing && !string.IsNullOrWhiteSpace(p.Note)).Take(5))
            {
                result.Info.Add($"{missing.Source}/{missing.FieldName}: {missing.Note}");
            }

            if (ctx.DataReliabilityScore > 0 && ctx.DataReliabilityScore < 55)
            {
                result.Warnings.Add($"数据源可靠性偏低: {ctx.DataReliabilityScore:F0}/100");
            }
        }

        /// <summary>
        /// 验证基础数据
        /// </summary>
        private static void ValidateBasicData(
            StockDeepAnalysisContext ctx,
            ValidationResult result,
            ref int totalFields,
            ref int validFields)
        {
            totalFields += 4;

            // 股票代码
            if (!string.IsNullOrEmpty(ctx.Code))
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("股票代码");
                result.Errors.Add("股票代码缺失");
            }

            // 股票名称
            if (!string.IsNullOrEmpty(ctx.Name))
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("股票名称");
                result.Warnings.Add("股票名称缺失");
            }

            // 当前价格
            if (ctx.CurrentPrice > 0)
            {
                validFields++;
                // 价格合理性检查
                if (ctx.CurrentPrice < 0.1 || ctx.CurrentPrice > 10000)
                {
                    result.SuspiciousFields.Add($"当前价格异常: {ctx.CurrentPrice}");
                    result.Warnings.Add("当前价格可能存在异常");
                }
            }
            else
            {
                result.MissingFields.Add("当前价格");
                result.Errors.Add("当前价格缺失或无效");
            }

            // 涨跌幅
            if (ctx.PctChange != 0 || ctx.CurrentPrice > 0)
            {
                validFields++;
                // 涨跌幅合理性检查
                if (Math.Abs(ctx.PctChange) > 20) // 考虑ST股限制
                {
                    result.SuspiciousFields.Add($"涨跌幅异常: {ctx.PctChange}%");
                    result.Warnings.Add("涨跌幅可能存在异常");
                }
            }
            else
            {
                result.MissingFields.Add("涨跌幅");
            }
        }

        /// <summary>
        /// 验证技术数据
        /// </summary>
        private static void ValidateTechnicalData(
            StockDeepAnalysisContext ctx,
            ValidationResult result,
            ref int totalFields,
            ref int validFields)
        {
            totalFields += 11; // 8原有 + 3新增(日线MACD+60分钟+15分钟)

            // 均线数据
            ValidateIfPositive(ctx.MA5, "MA5", result, ref validFields);
            ValidateIfPositive(ctx.MA10, "MA10", result, ref validFields);
            ValidateIfPositive(ctx.MA20, "MA20", result, ref validFields);

            // 乖离率
            if (ctx.BiasMA5 != 0 || ctx.MA5 > 0)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("MA5乖离率");
            }

            // 量比
            if (ctx.VolumeRatio > 0)
            {
                validFields++;
                if (ctx.VolumeRatio > 100)
                {
                    result.SuspiciousFields.Add($"量比异常: {ctx.VolumeRatio}");
                    result.Warnings.Add("量比数据可能异常");
                }
            }
            else
            {
                result.MissingFields.Add("量比");
            }

            // 换手率
            if (ctx.TurnoverRate >= 0)
            {
                validFields++;
                if (ctx.TurnoverRate > 100)
                {
                    result.SuspiciousFields.Add($"换手率异常: {ctx.TurnoverRate}%");
                    result.Warnings.Add("换手率数据可能异常");
                }
            }
            else
            {
                result.MissingFields.Add("换手率");
            }

            // 均线排列
            if (!string.IsNullOrEmpty(ctx.MAAlignment))
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("均线排列状态");
            }

            // === 新增：MACD/多周期数据有效性验证 ===
            // 日线技术指标
            if (ctx.TechScore != null && ctx.TechScore.IsComputed)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("日线MACD/RSI/KDJ");
                result.Warnings.Add("日线技术指标未成功计算，K线数据可能获取失败");
            }

            // 60分钟技术指标
            if (ctx.TechScore60Min != null && ctx.TechScore60Min.IsComputed)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("60分钟MACD");
                result.Warnings.Add("60分钟技术指标缺失，多周期共振分析不完整");
            }

            // 15分钟技术指标
            if (ctx.TechScore15Min != null && ctx.TechScore15Min.IsComputed)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("15分钟MACD");
                result.Warnings.Add("15分钟技术指标缺失，多周期共振分析不完整");
            }
        }

        /// <summary>
        /// 验证基本面数据
        /// </summary>
        private static void ValidateFundamentalData(
            StockDeepAnalysisContext ctx,
            ValidationResult result,
            ref int totalFields,
            ref int validFields)
        {
            totalFields += 7;

            // PE
            if (ctx.PE != 0)
            {
                validFields++;
                if (ctx.PE < -100 || ctx.PE > 1000)
                {
                    result.SuspiciousFields.Add($"PE异常: {ctx.PE}");
                    result.Warnings.Add("PE数据可能异常");
                }
            }
            else
            {
                result.MissingFields.Add("PE");
            }

            // PB
            if (ctx.PB > 0)
            {
                validFields++;
                if (ctx.PB > 100)
                {
                    result.SuspiciousFields.Add($"PB异常: {ctx.PB}");
                    result.Warnings.Add("PB数据可能异常");
                }
            }
            else
            {
                result.MissingFields.Add("PB");
            }

            // ROE
            if (ctx.ROE != 0)
            {
                validFields++;
                if (ctx.ROE < -100 || ctx.ROE > 200)
                {
                    result.SuspiciousFields.Add($"ROE异常: {ctx.ROE}%");
                    result.Warnings.Add("ROE数据可能异常");
                }
            }
            else
            {
                result.MissingFields.Add("ROE");
            }

            // 净利润
            if (ctx.NetProfit != 0 || ctx.OperatingRevenue > 0)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("净利润");
            }

            // 营业收入
            if (ctx.OperatingRevenue > 0)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("营业收入");
            }

            // 现金流
            if (ctx.OperatingCashFlowPerShare != 0)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("每股现金流");
            }

            // 市值
            if (ctx.TotalMarketValue > 0)
            {
                validFields++;
                if (ctx.TotalMarketValue < 0.1 || ctx.TotalMarketValue > 100000)
                {
                    result.SuspiciousFields.Add($"市值异常: {ctx.TotalMarketValue}亿");
                    result.Warnings.Add("市值数据可能异常");
                }
            }
            else
            {
                result.MissingFields.Add("总市值");
            }
        }

        /// <summary>
        /// 验证资金数据
        /// </summary>
        private static void ValidateFundFlowData(
            StockDeepAnalysisContext ctx,
            ValidationResult result,
            ref int totalFields,
            ref int validFields)
        {
            totalFields += 5; // 4原有 + 1新增(北向资金)

            // 主力净流入
            if (ctx.MainForceNetInflow != 0)
            {
                validFields++;
                // 合理性检查
                double absInflow = Math.Abs(ctx.MainForceNetInflow);
                if (absInflow > ctx.TotalMarketValue * 10000) // 超过市值
                {
                    result.SuspiciousFields.Add($"主力流入异常: {ctx.MainForceNetInflow}");
                    result.Warnings.Add("主力流入数据可能异常");
                }
            }
            else
            {
                result.MissingFields.Add("主力净流入");
                // 检查是否全部资金分级都为0：区分"真的为0"和"未获取到数据"
                if (ctx.SuperLargeOrderInflow == 0 && ctx.LargeOrderInflow == 0 &&
                    ctx.MediumOrderInflow == 0 && ctx.SmallOrderInflow == 0)
                {
                    result.Warnings.Add("主力资金分级数据全部为0，可能是API未返回资金流向数据（非交易时段或接口限流）");
                }
            }

            // 北向资金
            if (ctx.NorthBoundNetInflow != 0 || ctx.NorthBoundTotalPosition > 0)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("北向资金");
                result.Warnings.Add("北向资金数据缺失，可能该股无北向持仓或接口未返回数据");
            }

            // 筹码数据
            if (ctx.ProfitRatio >= 0 && ctx.ProfitRatio <= 100)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("获利盘比例");
            }

            if (ctx.ChipAvgCost > 0)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("平均成本");
            }

            if (ctx.ChipConcentration90 >= 0 && ctx.ChipConcentration90 <= 100)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add("筹码集中度");
            }
        }

        /// <summary>
        /// 验证数据一致性
        /// </summary>
        private static void ValidateDataConsistency(StockDeepAnalysisContext ctx, ValidationResult result)
        {
            // 价格与均线关系
            if (ctx.CurrentPrice > 0 && ctx.MA5 > 0)
            {
                double deviation = Math.Abs(ctx.CurrentPrice - ctx.MA5) / ctx.MA5 * 100;
                if (deviation > 50 && ctx.BiasMA5 < 10)
                {
                    result.Warnings.Add($"价格与MA5偏差过大({deviation:F1}%)，但乖离率显示正常，数据可能不一致");
                }
            }

            // 量比与换手率关系
            if (ctx.VolumeRatio > 10 && ctx.TurnoverRate < 1)
            {
                result.Warnings.Add("量比极高但换手率低，数据可能不一致");
            }

            // PE与ROE关系
            if (ctx.PE > 0 && ctx.ROE > 0)
            {
                double expectedPE = 100 / ctx.ROE; // 粗略估算
                if (ctx.PE > expectedPE * 3 && ctx.PE > 50)
                {
                    result.Info.Add($"PE({ctx.PE})相对于ROE({ctx.ROE}%)偏高，可能存在溢价");
                }
            }

            // 市值与价格关系
            if (ctx.TotalMarketValue > 0 && ctx.CurrentPrice > 0)
            {
                // 这里没有股本数据，无法精确验证，但可以记录信息
                result.Info.Add($"市值{ctx.TotalMarketValue}亿，价格{ctx.CurrentPrice}元");
            }
        }

        /// <summary>
        /// 验证字段是否为正值
        /// </summary>
        private static void ValidateIfPositive(
            double value,
            string fieldName,
            ValidationResult result,
            ref int validFields)
        {
            if (value > 0)
            {
                validFields++;
            }
            else
            {
                result.MissingFields.Add(fieldName);
            }
        }

        /// <summary>
        /// 批量验证多只股票数据
        /// </summary>
        public static Dictionary<string, ValidationResult> ValidateBatchData(
            List<StockDeepAnalysisContext> contexts)
        {
            var results = new Dictionary<string, ValidationResult>();

            foreach (var ctx in contexts)
            {
                if (!string.IsNullOrEmpty(ctx.Code))
                {
                    results[ctx.Code] = ValidateStockData(ctx);
                }
            }

            return results;
        }

        /// <summary>
        /// 获取数据质量报告
        /// </summary>
        public static string GetQualityReport(ValidationResult result, string stockCode = "", string stockName = "")
        {
            var sb = new System.Text.StringBuilder();

            string title = !string.IsNullOrEmpty(stockName) ? $"{stockName}({stockCode})" : stockCode;
            sb.AppendLine($"📊 数据质量验证报告 - {title}");
            sb.AppendLine($"状态: {result.GetSummary()}");
            sb.AppendLine($"完整性评分: {result.DataCompletenessScore:F1}/100");
            sb.AppendLine();

            if (result.Errors.Count > 0)
            {
                sb.AppendLine("🔴 错误:");
                foreach (var error in result.Errors)
                {
                    sb.AppendLine($"  • {error}");
                }
                sb.AppendLine();
            }

            if (result.Warnings.Count > 0)
            {
                sb.AppendLine("🟡 警告:");
                foreach (var warning in result.Warnings)
                {
                    sb.AppendLine($"  • {warning}");
                }
                sb.AppendLine();
            }

            if (result.MissingFields.Count > 0)
            {
                sb.AppendLine("📋 缺失字段:");
                sb.AppendLine($"  • {string.Join(", ", result.MissingFields)}");
                sb.AppendLine();
            }

            if (result.SuspiciousFields.Count > 0)
            {
                sb.AppendLine("🔍 可疑数据:");
                foreach (var suspicious in result.SuspiciousFields)
                {
                    sb.AppendLine($"  • {suspicious}");
                }
                sb.AppendLine();
            }

            if (result.Info.Count > 0)
            {
                sb.AppendLine("ℹ️ 补充信息:");
                foreach (var info in result.Info)
                {
                    sb.AppendLine($"  • {info}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 判断数据是否适合AI分析
        /// </summary>
        public static bool IsSuitableForAIAnalysis(ValidationResult result)
        {
            // 必须数据有效性
            if (!result.IsValid) return false;

            // 完整性要求
            if (result.DataCompletenessScore < 50) return false;

            // 关键字段要求
            string[] criticalFields = { "当前价格", "MA5", "换手率" };
            var missingCritical = result.MissingFields.Intersect(criticalFields);
            if (missingCritical.Any()) return false;

            // 严重错误排除
            if (result.Errors.Any(e => e.Contains("异常") || e.Contains("失败"))) return false;

            return true;
        }

        /// <summary>
        /// 获取数据质量等级
        /// </summary>
        public static string GetQualityGrade(ValidationResult result)
        {
            if (!result.IsValid || result.DataCompletenessScore < 40)
                return "D - 数据质量差，不建议使用";
            else if (result.DataCompletenessScore < 60)
                return "C - 数据质量一般，谨慎使用";
            else if (result.DataCompletenessScore < 80)
                return "B - 数据质量良好，可以使用";
            else if (result.Warnings.Count == 0)
                return "A - 数据质量优秀，推荐使用";
            else
                return "A - 数据质量优秀，有少量警告";
        }

        /// <summary>
        /// 智能数据修复（尝试修复可疑数据）
        /// </summary>
        public static StockDeepAnalysisContext TryRepairData(StockDeepAnalysisContext ctx, ValidationResult result)
        {
            var repairedCtx = ctx; // 实际应该深度克隆

            // 尝试修复乖离率计算
            if (result.SuspiciousFields.Any(f => f.Contains("偏差过大")) &&
                ctx.CurrentPrice > 0 && ctx.MA5 > 0)
            {
                double correctBias = (ctx.CurrentPrice - ctx.MA5) / ctx.MA5 * 100;
                if (Math.Abs(correctBias - ctx.BiasMA5) > 10)
                {
                    repairedCtx.BiasMA5 = correctBias;
                    result.Info.Add($"已修复MA5乖离率: {ctx.BiasMA5:F2}% -> {correctBias:F2}%");
                }
            }

            // 尝试修复异常量比
            if (result.SuspiciousFields.Any(f => f.Contains("量比异常")) &&
                ctx.VolumeRatio > 100)
            {
                repairedCtx.VolumeRatio = ctx.VolumeRatio / 100; // 尝试除以100
                result.Info.Add($"已修复量比: {ctx.VolumeRatio:F2} -> {repairedCtx.VolumeRatio:F2}");
            }

            return repairedCtx;
        }
    }
}
