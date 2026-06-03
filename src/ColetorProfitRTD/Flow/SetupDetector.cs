using System;
using System.Collections.Generic;
using System.Globalization;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class SetupDetector
    {
        private readonly FlowConfig _config;
        private readonly Dictionary<string, DateTimeOffset> _cooldowns = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        public SetupDetector(FlowConfig config)
        {
            _config = config;
        }

        public List<FlowSignal> Detect(NormalizedMarketEvent ev, FlowMetrics metrics, TradePrint trade)
        {
            var signals = new List<FlowSignal>();

            if (ev == null || metrics == null || !metrics.LastPrice.HasValue)
            {
                return signals;
            }

            TryAdd(signals, BuildAbsorption(ev, metrics, trade, true));
            TryAdd(signals, BuildAbsorption(ev, metrics, trade, false));
            TryAdd(signals, BuildExhaustion(ev, metrics, true));
            TryAdd(signals, BuildExhaustion(ev, metrics, false));
            TryAdd(signals, BuildBreakout(ev, metrics, true));
            TryAdd(signals, BuildBreakout(ev, metrics, false));
            TryAdd(signals, BuildVwapSignal(ev, metrics, true));
            TryAdd(signals, BuildVwapSignal(ev, metrics, false));

            return signals;
        }

        private void TryAdd(List<FlowSignal> signals, FlowSignal signal)
        {
            if (signal == null)
            {
                return;
            }

            signal.Score = ApplyQualityCap(signal.Score, signal.DataQuality);

            if (signal.Score < _config.ScoreThreshold)
            {
                return;
            }

            string key = signal.Setup + ":" + signal.Direction + ":" + RoundKey(signal.Price);
            DateTimeOffset now = signal.Timestamp;
            DateTimeOffset last;

            if (_cooldowns.TryGetValue(key, out last) && (now - last).TotalMilliseconds < _config.SignalCooldownMs)
            {
                return;
            }

            _cooldowns[key] = now;
            signal.Id = now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + "-" + signal.Setup + "-" + signal.Direction + "-" + RoundKey(signal.Price);
            signal.Confidence = Confidence(signal.Score);
            signal.TtlMs = _config.SignalTtlMs;
            signals.Add(signal);
        }

        private FlowSignal BuildAbsorption(NormalizedMarketEvent ev, FlowMetrics m, TradePrint trade, bool buy)
        {
            decimal flow = buy ? -m.Delta5s : m.Delta5s;
            decimal book = buy ? (m.TopBookImbalance ?? 0) : -(m.TopBookImbalance ?? 0);
            decimal stack = buy ? (m.BidStackPull ?? 0) : -(m.AskStackPull ?? 0);
            bool sideOk = trade == null || trade.AggressorSide == (buy ? "sell" : "buy") || trade.AggressorSide == "unknown";

            if (flow < 20 || book < -0.15m || !sideOk)
            {
                return null;
            }

            decimal score = 45 + Math.Min(22, flow / 10) + Math.Min(12, Math.Max(0, book) * 35) + Math.Min(10, Math.Max(0, stack) / 15);

            return Signal(ev, buy ? "absorption" : "absorption", buy ? "buy" : "sell", score,
                buy ? "Absorcao compradora derivada" : "Absorcao vendedora derivada",
                buy
                    ? new[] { "venda agressora derivada contra nivel", "preco segurou", "bid/top book sustentou" }
                    : new[] { "compra agressora derivada contra nivel", "preco segurou", "ask/top book sustentou" });
        }

        private FlowSignal BuildExhaustion(NormalizedMarketEvent ev, FlowMetrics m, bool buy)
        {
            decimal delta = buy ? m.Delta5s : -m.Delta5s;
            decimal shortDelta = buy ? m.Delta1s : -m.Delta1s;
            decimal priceDelta = ev.PriceDeltaFromPrevious ?? 0;
            bool stalled = buy ? priceDelta <= 0 : priceDelta >= 0;

            if (delta < 35 || shortDelta < -5 || !stalled)
            {
                return null;
            }

            decimal score = 42 + Math.Min(24, delta / 12) + Math.Min(10, m.Volume5s / 40);
            return Signal(ev, "exhaustion", buy ? "sell" : "buy", score,
                buy ? "Exaustao compradora derivada" : "Exaustao vendedora derivada",
                buy
                    ? new[] { "delta comprador perdeu resposta", "preco parou de subir" }
                    : new[] { "delta vendedor perdeu resposta", "preco parou de cair" });
        }

        private FlowSignal BuildBreakout(NormalizedMarketEvent ev, FlowMetrics m, bool buy)
        {
            decimal priceDelta = ev.PriceDeltaFromPrevious ?? 0;
            decimal delta = buy ? m.Delta5s : -m.Delta5s;
            decimal bias = buy ? (m.MicroBiasTicks ?? 0) : -(m.MicroBiasTicks ?? 0);

            if ((buy && priceDelta <= 0) || (!buy && priceDelta >= 0) || delta < 25 || bias < -0.2m)
            {
                return null;
            }

            decimal score = 44 + Math.Min(22, delta / 12) + Math.Min(12, Math.Max(0, bias) * 18);
            return Signal(ev, "breakout", buy ? "buy" : "sell", score,
                buy ? "Rompimento comprador de fluxo" : "Rompimento vendedor de fluxo",
                buy
                    ? new[] { "preco rompeu para cima", "delta comprador positivo", "microbias favoravel" }
                    : new[] { "preco rompeu para baixo", "delta vendedor positivo", "microbias favoravel" });
        }

        private FlowSignal BuildVwapSignal(NormalizedMarketEvent ev, FlowMetrics m, bool continuation)
        {
            if (!m.VwapDistanceTicks.HasValue)
            {
                return null;
            }

            decimal dist = m.VwapDistanceTicks.Value;

            if (continuation)
            {
                bool buy = dist > 1 && m.Delta5s > 20;
                bool sell = dist < -1 && m.Delta5s < -20;

                if (!buy && !sell)
                {
                    return null;
                }

                decimal score = 44 + Math.Min(18, Math.Abs(m.Delta5s) / 15) + Math.Min(12, Math.Abs(dist) * 2);
                return Signal(ev, "vwapContinuation", buy ? "buy" : "sell", score, "Continuacao pela VWAP derivada",
                    new[] { "preco afastado da VWAP", "delta acompanha direcao" });
            }

            bool reversionBuy = dist < -4 && m.Delta1s > 0;
            bool reversionSell = dist > 4 && m.Delta1s < 0;

            if (!reversionBuy && !reversionSell)
            {
                return null;
            }

            decimal reversionScore = 42 + Math.Min(18, Math.Abs(dist) * 2) + Math.Min(10, Math.Abs(m.Delta1s) / 10);
            return Signal(ev, "vwapReversion", reversionBuy ? "buy" : "sell", reversionScore, "Reversao para VWAP derivada",
                new[] { "preco esticado da VWAP", "delta curto virou contra o esticamento" });
        }

        private FlowSignal Signal(NormalizedMarketEvent ev, string setup, string direction, decimal score, string label, IEnumerable<string> reasons)
        {
            return new FlowSignal
            {
                Timestamp = ev.LocalTimestamp,
                Asset = ev.Asset,
                Setup = setup,
                Direction = direction,
                Price = ev.LastPrice,
                Score = score,
                DataQuality = ev.Quality,
                Derived = ev.Quality == MarketDataQuality.DerivedTape || ev.Quality == MarketDataQuality.TopOfBookOnly,
                LevelLabel = label,
                LevelPrice = ev.LastPrice,
                Reasons = new List<string>(reasons)
            };
        }

        private decimal ApplyQualityCap(decimal score, MarketDataQuality quality)
        {
            if (quality == MarketDataQuality.TopOfBookOnly)
            {
                return Math.Min(score, _config.DataQualityCapTopOfBook);
            }

            if (quality == MarketDataQuality.DerivedTape)
            {
                return Math.Min(score, _config.DataQualityCapDerivedTape);
            }

            if (quality == MarketDataQuality.Unknown)
            {
                return Math.Min(score, 45);
            }

            return Math.Min(score, 100);
        }

        private string Confidence(decimal score)
        {
            if (score >= _config.ExcellentSignalThreshold)
            {
                return "excelente";
            }

            if (score >= _config.StrongSignalThreshold)
            {
                return "muito_forte";
            }

            return "moderado";
        }

        private string RoundKey(decimal? price)
        {
            if (!price.HasValue)
            {
                return "na";
            }

            decimal tick = _config.TickSize <= 0 ? 0.5m : _config.TickSize;
            return (Math.Round(price.Value / tick, 0, MidpointRounding.AwayFromZero) * tick).ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
