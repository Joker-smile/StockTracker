using System;
using System.Collections.Generic;
using System.Linq;

namespace StockTracker
{
    /// <summary>
    /// 高级技术指标计算系统
    /// </summary>
    public static class AdvancedTechnicalIndicators
    {
        /// <summary>
        /// 计算RSI指标
        /// </summary>
        public static (double RSI6, double RSI12, double RSI24) CalculateRSI(List<double> prices)
        {
            if (prices.Count < 25) return (50, 50, 50); // 默认中性值

            return (CalculateRSIInternal(prices, 6),
                   CalculateRSIInternal(prices, 12),
                   CalculateRSIInternal(prices, 24));
        }

        private static double CalculateRSIInternal(List<double> prices, int period)
        {
            if (prices.Count < period + 1) return 50;

            var gains = new List<double>();
            var losses = new List<double>();

            for (int i = 1; i < prices.Count; i++)
            {
                double change = prices[i] - prices[i - 1];
                gains.Add(change > 0 ? change : 0);
                losses.Add(change < 0 ? Math.Abs(change) : 0);
            }

            if (gains.Count < period) return 50;

            double avgGain = gains.TakeLast(period).Average();
            double avgLoss = losses.TakeLast(period).Average();

            if (avgLoss == 0) return 100;

            double rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }

        /// <summary>
        /// 计算MACD指标
        /// </summary>
        public static (double MACD, double Signal, double Histogram) CalculateMACD(List<double> prices)
        {
            if (prices.Count < 26) return (0, 0, 0);

            double ema12 = CalculateEMA(prices, 12);
            double ema26 = CalculateEMA(prices, 26);
            double macd = ema12 - ema26;

            // 计算信号线（MACD的9日EMA）
            var macdHistory = new List<double>();
            for (int i = 0; i < prices.Count; i++)
            {
                if (i < 26) continue;
                var slice = prices.Take(i + 1).ToList();
                var e12 = CalculateEMA(slice, 12);
                var e26 = CalculateEMA(slice, 26);
                macdHistory.Add(e12 - e26);
            }

            double signal = macdHistory.Count >= 9 ? CalculateEMA(macdHistory, 9) : 0;
            double histogram = macd - signal;

            return (macd, signal, histogram);
        }

        /// <summary>
        /// 计算EMA指数移动平均
        /// </summary>
        public static double CalculateEMA(List<double> prices, int period)
        {
            if (prices.Count < period) return prices.LastOrDefault();

            double multiplier = 2.0 / (period + 1);
            double ema = prices.Take(period).Average();

            for (int i = period; i < prices.Count; i++)
            {
                ema = (prices[i] - ema) * multiplier + ema;
            }

            return ema;
        }

        /// <summary>
        /// 计算KDJ指标
        /// </summary>
        public static (double K, double D, double J) CalculateKDJ(
            List<double> highs, List<double> lows, List<double> closes)
        {
            if (closes.Count < 9) return (50, 50, 50);

            var period = 9;
            var recentHighs = highs.TakeLast(period).ToList();
            var recentLows = lows.TakeLast(period).ToList();

            double highestHigh = recentHighs.Max();
            double lowestLow = recentLows.Min();

            double rsv = (closes.Last() - lowestLow) / (highestHigh - lowestLow) * 100;

            // 简化版KDJ计算（实际应该有前一日K值）
            double k = (2.0 / 3) * 50 + (1.0 / 3) * rsv;
            double d = (2.0 / 3) * 50 + (1.0 / 3) * k;
            double j = 3 * k - 2 * d;

            return (k, d, j);
        }

        /// <summary>
        /// 计算布林带
        /// </summary>
        public static (double Upper, double Middle, double Lower, double Width) CalculateBollingerBands(
            List<double> prices, int period = 20, double stdDevMultiplier = 2)
        {
            if (prices.Count < period) return (0, 0, 0, 0);

            var recentPrices = prices.TakeLast(period).ToList();
            double middle = recentPrices.Average();
            double stdDev = Math.Sqrt(recentPrices.Sum(p => Math.Pow(p - middle, 2)) / period);

            double upper = middle + stdDevMultiplier * stdDev;
            double lower = middle - stdDevMultiplier * stdDev;
            double width = (upper - lower) / middle * 100; // 百分比宽度

            return (upper, middle, lower, width);
        }

        /// <summary>
        /// 识别支撑阻力位
        /// </summary>
        public static (double Support1, double Support2, double Resistance1, double Resistance2)
            IdentifySupportResistance(List<double> prices, List<double> volumes)
        {
            if (prices.Count < 20) return (0, 0, 0, 0);

            var recentPrices = prices.TakeLast(20).ToList();
            double currentPrice = prices.Last();

            var highs = new List<double>();
            var lows = new List<double>();

            // 识别局部高点和低点
            for (int i = 2; i < recentPrices.Count - 2; i++)
            {
                // 局部高点
                if (recentPrices[i] > recentPrices[i - 1] &&
                    recentPrices[i] > recentPrices[i - 2] &&
                    recentPrices[i] > recentPrices[i + 1] &&
                    recentPrices[i] > recentPrices[i + 2])
                {
                    highs.Add(recentPrices[i]);
                }

                // 局部低点
                if (recentPrices[i] < recentPrices[i - 1] &&
                    recentPrices[i] < recentPrices[i - 2] &&
                    recentPrices[i] < recentPrices[i + 1] &&
                    recentPrices[i] < recentPrices[i + 2])
                {
                    lows.Add(recentPrices[i]);
                }
            }

            // 寻找最近的支撑阻力位
            double resistance1 = highs.OrderByDescending(h => h).FirstOrDefault(h => h > currentPrice);
            double resistance2 = highs.OrderByDescending(h => h).Skip(1).FirstOrDefault(h => h > currentPrice);
            double support1 = lows.OrderBy(l => l).FirstOrDefault(l => l < currentPrice);
            double support2 = lows.OrderBy(l => l).Skip(1).FirstOrDefault(l => l < currentPrice);

            return (support1, support2, resistance1, resistance2);
        }

        /// <summary>
        /// 识别技术形态
        /// </summary>
        public static TechnicalPattern IdentifyTechnicalPattern(List<double> prices)
        {
            if (prices.Count < 10) return TechnicalPattern.Unknown;

            var recentPrices = prices.TakeLast(10).ToList();
            double firstPrice = recentPrices.First();
            double lastPrice = recentPrices.Last();
            double maxPrice = recentPrices.Max();
            double minPrice = recentPrices.Min();

            // 判断趋势方向
            bool isUpTrend = lastPrice > firstPrice * 1.03; // 上涨3%以上
            bool isDownTrend = lastPrice < firstPrice * 0.97; // 下跌3%以上

            // 判断波动性
            double volatility = (maxPrice - minPrice) / firstPrice * 100;
            bool isHighVolatility = volatility > 8;
            bool isLowVolatility = volatility < 3;

            // 形态识别
            if (isUpTrend && !isHighVolatility)
                return TechnicalPattern.SteadyRise;
            else if (isUpTrend && isHighVolatility)
                return TechnicalPattern.VolatileRise;
            else if (isDownTrend && !isHighVolatility)
                return TechnicalPattern.SteadyFall;
            else if (isDownTrend && isHighVolatility)
                return TechnicalPattern.VolatileFall;
            else if (isLowVolatility)
                return TechnicalPattern.Consolidation;
            else
                return TechnicalPattern.Unknown;
        }

        /// <summary>
        /// 计算波动率
        /// </summary>
        public static double CalculateVolatility(List<double> prices, int period = 20)
        {
            if (prices.Count < period + 1) return 0;

            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                if (prices[i - 1] > 0)
                {
                    returns.Add(Math.Log(prices[i] / prices[i - 1]));
                }
            }

            if (returns.Count < period) return 0;

            var recentReturns = returns.TakeLast(period).ToList();
            double mean = recentReturns.Average();
            double variance = recentReturns.Sum(r => Math.Pow(r - mean, 2)) / period;

            return Math.Sqrt(variance) * Math.Sqrt(252) * 100; // 年化波动率
        }

        /// <summary>
        /// 计算动量指标
        /// </summary>
        public static double CalculateMomentum(List<double> prices, int period = 10)
        {
            if (prices.Count < period + 1) return 0;

            double currentPrice = prices.Last();
            double pastPrice = prices[prices.Count - period - 1];

            if (pastPrice == 0) return 0;

            return ((currentPrice - pastPrice) / pastPrice) * 100;
        }

        /// <summary>
        /// 计算成交量变化率
        /// </summary>
        public static double CalculateVolumeChangeRate(List<double> volumes, int period = 5)
        {
            if (volumes.Count < period + 1) return 0;

            double recentAvg = volumes.TakeLast(period).Average();
            double pastAvg = volumes.Skip(Math.Max(0, volumes.Count - period * 2)).Take(period).Average();

            if (pastAvg == 0) return 0;

            return ((recentAvg - pastAvg) / pastAvg) * 100;
        }

        /// <summary>
        /// 综合技术分析评分
        /// </summary>
        public static TechnicalAnalysisScore ComprehensiveTechnicalAnalysis(
            List<double> prices,
            List<double> volumes,
            List<double>? highs = null,
            List<double>? lows = null)
        {
            var score = new TechnicalAnalysisScore();

            if (prices.Count < 20) return score;

            // 基础指标
            var (rsi6, rsi12, rsi24) = CalculateRSI(prices);
            score.RSI6 = rsi6;
            score.RSI12 = rsi12;
            score.RSI24 = rsi24;

            var (macd, signal, histogram) = CalculateMACD(prices);
            score.MACD = macd;
            score.MACDSignal = signal;
            score.MACDHistogram = histogram;

            var (upper, middle, lower, width) = CalculateBollingerBands(prices);
            score.BollingerUpper = upper;
            score.BollingerMiddle = middle;
            score.BollingerLower = lower;
            score.BollingerWidth = width;

            // KDJ需要高低价数据
            if (highs != null && lows != null && highs.Count == lows.Count)
            {
                var (k, d, j) = CalculateKDJ(highs, lows, prices);
                score.KDJ_K = k;
                score.KDJ_D = d;
                score.KDJ_J = j;
            }

            // 支撑阻力位
            var (support1, support2, resistance1, resistance2) = IdentifySupportResistance(prices, volumes);
            score.SupportLevel1 = support1;
            score.SupportLevel2 = support2;
            score.ResistanceLevel1 = resistance1;
            score.ResistanceLevel2 = resistance2;

            // 其他指标
            score.Volatility = CalculateVolatility(prices);
            score.Momentum = CalculateMomentum(prices);
            score.VolumeChangeRate = CalculateVolumeChangeRate(volumes);
            score.Pattern = IdentifyTechnicalPattern(prices);

            // === 新增：背离检测 ===
            DetectDivergences(score, prices, volumes);

            // === 新增：历史分位数计算 ===
            CalculateHistoricalPercentiles(score, prices, volumes);

            // 技术信号识别
            score.Signals = IdentifyTechnicalSignals(score);

            return score;
        }

        /// <summary>
        /// 检测各类背离（MACD背离、RSI背离、量价背离）
        /// </summary>
        public static void DetectDivergences(TechnicalAnalysisScore score, List<double> prices, List<double> volumes)
        {
            if (prices.Count < 40) return;
            var divergences = new List<string>();

            // === MACD背离检测 ===
            // 计算MACD柱状图序列用于找峰值
            var macdHistograms = new List<double>();
            for (int i = 26; i < prices.Count; i++)
            {
                var slice = prices.Take(i + 1).ToList();
                var (m, s, h) = CalculateMACD(slice);
                macdHistograms.Add(h);
            }

            if (macdHistograms.Count >= 10)
            {
                // 找最近两个价格高点和对应的MACD柱高
                int recentPeriod = Math.Min(30, prices.Count - 26);
                var recentPrices = prices.Skip(prices.Count - recentPeriod).ToList();
                var recentHistograms = macdHistograms.Skip(macdHistograms.Count - recentPeriod).ToList();

                // 顶背离：价格创新高但MACD柱不创新高（缩小或走平）
                int halfPeriod = recentPeriod / 2;
                var firstHalfPrices = recentPrices.Take(halfPeriod).ToList();
                var secondHalfPrices = recentPrices.Skip(halfPeriod).ToList();
                var firstHalfMacd = recentHistograms.Take(halfPeriod).ToList();
                var secondHalfMacd = recentHistograms.Skip(halfPeriod).ToList();

                double maxPriceFirst = firstHalfPrices.Max();
                double maxPriceSecond = secondHalfPrices.Max();
                double maxMacdFirst = firstHalfMacd.Max();
                double maxMacdSecond = secondHalfMacd.Max();

                // 价创新高但MACD柱峰降低 → 顶背离
                if (maxPriceSecond > maxPriceFirst * 1.02 && maxMacdSecond < maxMacdFirst * 0.8)
                {
                    score.HasBearishDivergence = true;
                    divergences.Add("🔴 MACD顶背离（价创新高动能减弱）");
                }

                // 底背离：价格创新低但MACD柱不创新低
                double minPriceFirst = firstHalfPrices.Min();
                double minPriceSecond = secondHalfPrices.Min();
                double minMacdFirst = firstHalfMacd.Min();
                double minMacdSecond = secondHalfMacd.Min();

                if (minPriceSecond < minPriceFirst * 0.98 && minMacdSecond > minMacdFirst)
                {
                    score.HasBullishDivergence = true;
                    divergences.Add("🟢 MACD底背离（价创新低动能减弱）");
                }
            }

            // === RSI背离检测 ===
            // 计算RSI序列
            var rsiList = new List<double>();
            for (int i = 14; i < prices.Count; i++)
            {
                var slice = prices.Skip(i - 14).Take(14).ToList();
                rsiList.Add(CalculateRSIInternal(slice, 14));
            }

            if (rsiList.Count >= 10)
            {
                int recentPeriod = Math.Min(20, rsiList.Count);
                var recentPriceSlice = prices.Skip(prices.Count - recentPeriod).ToList();
                var recentRsiSlice = rsiList.Skip(rsiList.Count - recentPeriod).ToList();
                int half = recentPeriod / 2;

                // RSI顶背离
                double priceMax1 = recentPriceSlice.Take(half).Max();
                double priceMax2 = recentPriceSlice.Skip(half).Max();
                double rsiMax1 = recentRsiSlice.Take(half).Max();
                double rsiMax2 = recentRsiSlice.Skip(half).Max();

                if (priceMax2 > priceMax1 && rsiMax2 < rsiMax1 - 3)
                {
                    score.HasRSIBearishDivergence = true;
                    divergences.Add("🔴 RSI顶背离（价格新高RSI未新高）");
                }

                // RSI底背离
                double priceMin1 = recentPriceSlice.Take(half).Min();
                double priceMin2 = recentPriceSlice.Skip(half).Min();
                double rsiMin1 = recentRsiSlice.Take(half).Min();
                double rsiMin2 = recentRsiSlice.Skip(half).Min();

                if (priceMin2 < priceMin1 && rsiMin2 > rsiMin1 + 3)
                {
                    score.HasRSIBullishDivergence = true;
                    divergences.Add("🟢 RSI底背离（价格新低RSI未新低）");
                }
            }

            // === 量价背离检测 ===
            if (volumes.Count >= 5)
            {
                double recentPriceChange = (prices.Last() - prices[prices.Count - 6]) / prices[prices.Count - 6] * 100;
                double recentAvgVol = volumes.TakeLast(5).Average();
                double priorAvgVol = volumes.Skip(volumes.Count - 10).Take(5).Average();

                // 价涨量缩 → 量价背离（看空）
                if (recentPriceChange > 3 && priorAvgVol > 0 && recentAvgVol / priorAvgVol < 0.8)
                {
                    score.HasVolumePriceDivergence = true;
                    divergences.Add("⚠️ 量价背离（价涨量缩，上涨乏力）");
                }
                // 价跌量增 → 恐慌盘（看跌中继）
                if (recentPriceChange < -3 && priorAvgVol > 0 && recentAvgVol / priorAvgVol > 1.3)
                {
                    score.HasVolumePriceDivergence = true;
                    divergences.Add("⚠️ 放量下跌（恐慌抛售或主力出货）");
                }
                // 价跌量缩到极致 → 可能见底
                if (recentPriceChange < -2 && recentAvgVol < priorAvgVol * 0.5 && recentAvgVol > 0)
                {
                    divergences.Add("🔍 缩量下跌（可能接近底部）");
                }
            }

            score.DivergenceDetail = string.Join(" | ", divergences);
        }

        /// <summary>
        /// 计算历史分位数（RSI、成交量、MACD柱）
        /// </summary>
        public static void CalculateHistoricalPercentiles(TechnicalAnalysisScore score, List<double> prices, List<double> volumes)
        {
            // RSI分位数
            var rsiHistory = new List<double>();
            for (int i = 14; i < prices.Count; i++)
            {
                var slice = prices.Skip(i - 14).Take(14).ToList();
                rsiHistory.Add(CalculateRSIInternal(slice, 14));
            }
            if (rsiHistory.Count > 0)
            {
                rsiHistory.Sort();
                int rsiPos = rsiHistory.BinarySearch(score.RSI12);
                if (rsiPos < 0) rsiPos = ~rsiPos;
                score.RSIPercentile = (double)rsiPos / rsiHistory.Count * 100;
            }

            // 成交量分位数
            if (volumes.Count >= 20)
            {
                var volAvgs = new List<double>();
                for (int i = 5; i < volumes.Count; i++)
                {
                    volAvgs.Add(volumes.Skip(i - 5).Take(5).Average());
                }
                if (volAvgs.Count > 0)
                {
                    double currentVol = volumes.TakeLast(1).First();
                    volAvgs.Sort();
                    int volPos = volAvgs.BinarySearch(currentVol);
                    if (volPos < 0) volPos = ~volPos;
                    score.VolumePercentile = (double)volPos / volAvgs.Count * 100;
                }
            }

            // MACD柱分位数
            var macdHistHistory = new List<double>();
            for (int i = 26; i < prices.Count; i++)
            {
                var slice = prices.Take(i + 1).ToList();
                var (_, _, h) = CalculateMACD(slice);
                macdHistHistory.Add(h);
            }
            if (macdHistHistory.Count > 5)
            {
                macdHistHistory.Sort();
                int macdPos = macdHistHistory.BinarySearch(score.MACDHistogram);
                if (macdPos < 0) macdPos = ~macdPos;
                score.MACDHistogramPercentile = (double)macdPos / macdHistHistory.Count * 100;
            }
        }

        /// <summary>
        /// 多周期共振分析
        /// </summary>
        public static void AnalyzeMultiTimeframeResonance(
            TechnicalAnalysisScore dailyScore,
            TechnicalAnalysisScore score60Min,
            TechnicalAnalysisScore score15Min)
        {
            // 多周期看多共振：日线+60分钟+15分钟MACD全部金叉
            bool dailyBullish = dailyScore.MACD > dailyScore.MACDSignal && dailyScore.MACDHistogram > 0;
            bool hourBullish = score60Min.MACD > score60Min.MACDSignal && score60Min.MACDHistogram > 0;
            bool min15Bullish = score15Min.MACD > score15Min.MACDSignal && score15Min.MACDHistogram > 0;

            dailyScore.IsMultiTimeframeBullish = dailyBullish && hourBullish && min15Bullish;
            dailyScore.IsMultiTimeframeBearish = !dailyBullish && !hourBullish && !min15Bullish;

            if (dailyScore.IsMultiTimeframeBullish)
                dailyScore.MultiTimeframeDetail = "🟢 日线+60分钟+15分钟 MACD同步金叉，多周期共振做多";
            else if (dailyScore.IsMultiTimeframeBearish)
                dailyScore.MultiTimeframeDetail = "🔴 日线+60分钟+15分钟 MACD同步死叉，多周期共振做空";
            else if (dailyBullish && !hourBullish && min15Bullish)
                dailyScore.MultiTimeframeDetail = "🟡 日线+15分钟MACD金叉，60分钟未确认，等待60分钟信号";
            else if (dailyBullish)
                dailyScore.MultiTimeframeDetail = "🟢 日线MACD金叉，小周期等待确认";
            else
                dailyScore.MultiTimeframeDetail = "⚪ 多周期信号不一致，趋势不明确";
        }

        /// <summary>
        /// 识别技术信号
        /// </summary>
        private static List<string> IdentifyTechnicalSignals(TechnicalAnalysisScore score)
        {
            var signals = new List<string>();

            // RSI信号
            if (score.RSI12 < 25) signals.Add("RSI严重超卖(买点)");
            else if (score.RSI12 < 35) signals.Add($"RSI超卖({score.RSI12:F0},分位{score.RSIPercentile:F0}%)");
            else if (score.RSI12 > 75) signals.Add($"RSI超买({score.RSI12:F0},分位{score.RSIPercentile:F0}%)");
            else if (score.RSI12 > 85) signals.Add("RSI严重超买(卖点)");
            else if (score.RSI12 > 40 && score.RSI12 < 60) signals.Add("RSI中性");

            // MACD信号
            if (score.MACD > score.MACDSignal && score.MACDHistogram > 0)
                signals.Add("MACD金叉(买入信号)");
            else if (score.MACD < score.MACDSignal && score.MACDHistogram < 0)
                signals.Add("MACD死叉(卖出信号)");
            else if (score.MACD > score.MACDSignal && score.MACDHistogram < 0)
                signals.Add("MACD弱势金叉(谨慎)");

            // KDJ信号
            if (score.KDJ_K > score.KDJ_D && score.KDJ_K < 80)
                signals.Add("KDJ金叉");
            else if (score.KDJ_K < score.KDJ_D && score.KDJ_K > 20)
                signals.Add("KDJ死叉");
            else if (score.KDJ_J > 100) signals.Add("KDJ严重超买");
            else if (score.KDJ_J < 0) signals.Add("KDJ严重超卖");

            // 布林带信号（使用当前价格需要通过参数传入，这里用中轨近似）
            if (score.Volatility < 10 && score.BollingerWidth < 5)
                signals.Add("布林带收窄(即将变盘)");
            else if (score.BollingerWidth > 20)
                signals.Add("布林带极度扩张(波动加大)");

            // 动量信号
            if (score.Momentum > 5) signals.Add("强势动量");
            else if (score.Momentum < -5) signals.Add("弱势动量");

            // === 背离信号（高优先级） ===
            if (score.HasBearishDivergence)
                signals.Add("🔴 MACD顶背离(强烈看空)");
            if (score.HasBullishDivergence)
                signals.Add("🟢 MACD底背离(反转信号)");
            if (score.HasRSIBearishDivergence)
                signals.Add("🔴 RSI顶背离(动能衰竭)");
            if (score.HasRSIBullishDivergence)
                signals.Add("🟢 RSI底背离(动能积聚)");
            if (score.HasVolumePriceDivergence)
                signals.Add("⚠️ 量价背离(趋势不可持续)");

            // === 多周期共振信号 ===
            if (score.IsMultiTimeframeBullish)
                signals.Add("🟢 多周期MACD共振做多");
            else if (score.IsMultiTimeframeBearish)
                signals.Add("🔴 多周期MACD共振做空");

            return signals;
        }
    }

    /// <summary>
    /// 技术形态枚举
    /// </summary>
    public enum TechnicalPattern
    {
        Unknown,
        SteadyRise,      // 稳步上涨
        VolatileRise,    // 震荡上涨
        SteadyFall,      // 稳步下跌
        VolatileFall,    // 震荡下跌
        Consolidation    // 横盘整理
    }

    /// <summary>
    /// 技术分析评分结果
    /// </summary>
    public class TechnicalAnalysisScore
    {
        // RSI指标
        public double RSI6 { get; set; }
        public double RSI12 { get; set; }
        public double RSI24 { get; set; }

        // MACD指标
        public double MACD { get; set; }
        public double MACDSignal { get; set; }
        public double MACDHistogram { get; set; }

        // KDJ指标
        public double KDJ_K { get; set; }
        public double KDJ_D { get; set; }
        public double KDJ_J { get; set; }

        // 布林带
        public double BollingerUpper { get; set; }
        public double BollingerMiddle { get; set; }
        public double BollingerLower { get; set; }
        public double BollingerWidth { get; set; }

        // 支撑阻力位
        public double SupportLevel1 { get; set; }
        public double SupportLevel2 { get; set; }
        public double ResistanceLevel1 { get; set; }
        public double ResistanceLevel2 { get; set; }

        // 其他指标
        public double Volatility { get; set; }       // 波动率
        public double Momentum { get; set; }         // 动量
        public double VolumeChangeRate { get; set; } // 成交量变化率

        // 技术形态
        public TechnicalPattern Pattern { get; set; }

        // 技术信号
        public List<string> Signals { get; set; } = new();

        // === 新增：背离指标 ===
        public bool HasBearishDivergence { get; set; } // MACD顶背离
        public bool HasBullishDivergence { get; set; } // MACD底背离
        public bool HasRSIBearishDivergence { get; set; } // RSI顶背离
        public bool HasRSIBullishDivergence { get; set; } // RSI底背离
        public bool HasVolumePriceDivergence { get; set; } // 量价背离
        public string DivergenceDetail { get; set; } = string.Empty;

        // === 新增：多周期共振 ===
        public bool IsMultiTimeframeBullish { get; set; } // 多周期看多共振
        public bool IsMultiTimeframeBearish { get; set; } // 多周期看空共振
        public string MultiTimeframeDetail { get; set; } = string.Empty;

        // === 新增：历史分位数 ===
        public double RSIPercentile { get; set; }         // RSI在历史中的分位数
        public double VolumePercentile { get; set; }      // 成交量在历史中的分位数
        public double MACDHistogramPercentile { get; set; } // MACD柱在历史中的分位数

        /// <summary>
        /// 获取技术分析摘要
        /// </summary>
        public string GetSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("📈 技术分析摘要:");
            summary.AppendLine($"RSI(12): {RSI12:F1} | MACD: {MACDHistogram:F2}");
            summary.AppendLine($"KDJ: K={KDJ_K:F1} D={KDJ_D:F1} J={KDJ_J:F1}");
            summary.AppendLine($"布林带宽度: {BollingerWidth:F2}% | 波动率: {Volatility:F2}%");
            summary.AppendLine($"技术形态: {GetPatternName(Pattern)}");
            if (!string.IsNullOrEmpty(DivergenceDetail))
                summary.AppendLine($"背离: {DivergenceDetail}");
            summary.AppendLine($"主要信号: {string.Join(", ", Signals.Take(3))}");
            return summary.ToString();
        }

        private string GetPatternName(TechnicalPattern pattern)
        {
            return pattern switch
            {
                TechnicalPattern.SteadyRise => "稳步上涨",
                TechnicalPattern.VolatileRise => "震荡上涨",
                TechnicalPattern.SteadyFall => "稳步下跌",
                TechnicalPattern.VolatileFall => "震荡下跌",
                TechnicalPattern.Consolidation => "横盘整理",
                _ => "形态不明"
            };
        }
    }
}