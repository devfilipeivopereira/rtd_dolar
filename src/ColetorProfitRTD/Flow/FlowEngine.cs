using System;
using System.Collections.Generic;
using System.Linq;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class FlowEngine
    {
        private readonly FlowConfig _config;
        private readonly RollingWindow _w1 = new RollingWindow(TimeSpan.FromSeconds(1));
        private readonly RollingWindow _w5 = new RollingWindow(TimeSpan.FromSeconds(5));
        private readonly RollingWindow _w15 = new RollingWindow(TimeSpan.FromSeconds(15));
        private readonly RollingWindow _w60 = new RollingWindow(TimeSpan.FromSeconds(60));
        private readonly VwapEngine _vwap = new VwapEngine();
        private readonly VolumeProfileIntraday _profile;
        private readonly SetupDetector _detector;
        private readonly List<TradePrint> _recentTrades = new List<TradePrint>();
        private readonly List<FlowSignal> _activeSignals = new List<FlowSignal>();

        private decimal? _previousLastPrice;
        private decimal? _previousBid;
        private decimal? _previousAsk;
        private decimal _cumulativeDelta;
        private int _eventsProcessed;
        private int _tradesDerived;
        private int _signalsGenerated;

        public FlowEngine(FlowConfig config)
        {
            _config = config;
            _profile = new VolumeProfileIntraday(config.TickSize);
            _detector = new SetupDetector(config);
        }

        public FlowMetrics CurrentMetrics { get; private set; } = new FlowMetrics();

        public IReadOnlyList<TradePrint> RecentTrades => _recentTrades;
        public IReadOnlyList<FlowSignal> ActiveSignals => _activeSignals;

        public List<FlowSignal> Process(NormalizedMarketEvent ev, int queueSize)
        {
            if (ev == null)
            {
                return new List<FlowSignal>();
            }

            _eventsProcessed++;
            TradePrint trade = TryBuildTrade(ev);

            if (trade != null)
            {
                _tradesDerived++;
                AddTrade(trade);
            }

            FlowMetrics metrics = BuildMetrics(ev, queueSize);
            List<FlowSignal> generated = _detector.Detect(ev, metrics, trade);

            if (generated.Count > 0)
            {
                _signalsGenerated += generated.Count;
                _activeSignals.InsertRange(0, generated);
            }

            PruneSignals(ev.LocalTimestamp);
            metrics.ActiveSignals = _activeSignals.ToList();
            CurrentMetrics = metrics;

            _previousLastPrice = ev.LastPrice;
            _previousBid = ev.BidPrice;
            _previousAsk = ev.AskPrice;

            return generated;
        }

        private TradePrint TryBuildTrade(NormalizedMarketEvent ev)
        {
            if (!ev.IsTradeCandidate || !ev.LastPrice.HasValue)
            {
                return null;
            }

            decimal qty = ResolveTradeQuantity(ev);

            if (qty <= 0)
            {
                return null;
            }

            string classification;
            string side = ResolveAggressorSide(ev, out classification);

            return new TradePrint
            {
                Sequence = ev.Sequence,
                Timestamp = ev.LocalTimestamp,
                Asset = ev.Asset,
                Price = ev.LastPrice.Value,
                Quantity = qty,
                AggressorSide = side,
                Classification = classification,
                Derived = true,
                BidAtTrade = ev.BidPrice,
                AskAtTrade = ev.AskPrice
            };
        }

        private decimal ResolveTradeQuantity(NormalizedMarketEvent ev)
        {
            string mode = (_config.VolumeFieldMode ?? "Auto").Trim();

            if (mode.Equals("LastQuantityOnly", StringComparison.OrdinalIgnoreCase))
            {
                return Positive(ev.LastQuantity);
            }

            if (mode.Equals("VolumeDelta", StringComparison.OrdinalIgnoreCase))
            {
                return Positive(ev.VolumeDeltaFromPrevious);
            }

            if (mode.Equals("QuantityDelta", StringComparison.OrdinalIgnoreCase))
            {
                return Positive(ev.QuantityDeltaFromPrevious);
            }

            decimal value = Positive(ev.LastQuantity);
            if (value > 0) return value;

            value = Positive(ev.VolumeDeltaFromPrevious);
            if (value > 0) return value;

            return Positive(ev.QuantityDeltaFromPrevious);
        }

        private string ResolveAggressorSide(NormalizedMarketEvent ev, out string classification)
        {
            decimal price = ev.LastPrice.Value;

            if (ev.AskPrice.HasValue && price >= ev.AskPrice.Value)
            {
                classification = "quote";
                return "buy";
            }

            if (ev.BidPrice.HasValue && price <= ev.BidPrice.Value)
            {
                classification = "quote";
                return "sell";
            }

            if (_previousAsk.HasValue && price >= _previousAsk.Value)
            {
                classification = "quotePrevious";
                return "buy";
            }

            if (_previousBid.HasValue && price <= _previousBid.Value)
            {
                classification = "quotePrevious";
                return "sell";
            }

            if (_previousLastPrice.HasValue && price > _previousLastPrice.Value)
            {
                classification = "tick";
                return "buy";
            }

            if (_previousLastPrice.HasValue && price < _previousLastPrice.Value)
            {
                classification = "tick";
                return "sell";
            }

            classification = "unknown";
            return "unknown";
        }

        private void AddTrade(TradePrint trade)
        {
            _recentTrades.Insert(0, trade);

            if (_recentTrades.Count > Math.Max(_config.MaxTradeBuffer, 100))
            {
                _recentTrades.RemoveRange(Math.Max(_config.MaxTradeBuffer, 100), _recentTrades.Count - Math.Max(_config.MaxTradeBuffer, 100));
            }

            if (trade.AggressorSide == "buy")
            {
                _cumulativeDelta += trade.Quantity;
            }
            else if (trade.AggressorSide == "sell")
            {
                _cumulativeDelta -= trade.Quantity;
            }

            _w1.Add(trade);
            _w5.Add(trade);
            _w15.Add(trade);
            _w60.Add(trade);
            _vwap.Add(trade);
            _profile.Add(trade);
        }

        private FlowMetrics BuildMetrics(NormalizedMarketEvent ev, int queueSize)
        {
            DateTimeOffset now = ev.LocalTimestamp;
            decimal? spread = ev.AskPrice.HasValue && ev.BidPrice.HasValue ? ev.AskPrice - ev.BidPrice : null;
            decimal? mid = ev.AskPrice.HasValue && ev.BidPrice.HasValue ? (ev.AskPrice + ev.BidPrice) / 2 : null;
            decimal? imbalance = TopBookImbalance(ev.BidSize, ev.AskSize);
            decimal? micro = MicroPrice(ev.BidPrice, ev.AskPrice, ev.BidSize, ev.AskSize);
            decimal tick = _config.TickSize <= 0 ? 0.5m : _config.TickSize;
            decimal? microBias = micro.HasValue && mid.HasValue ? (micro - mid) / tick : null;
            decimal? vwap = _vwap.ResolveVwap(ev.ProfitMed, _config.UseProfitMedAsVwapFallback);
            decimal? std = _vwap.Std();

            decimal buy = _recentTrades.Where(t => t.AggressorSide == "buy").Take(200).Sum(t => t.Quantity);
            decimal sell = _recentTrades.Where(t => t.AggressorSide == "sell").Take(200).Sum(t => t.Quantity);

            return new FlowMetrics
            {
                Timestamp = now,
                Asset = ev.Asset,
                DataQuality = ev.Quality,
                LastPrice = ev.LastPrice,
                BidPrice = ev.BidPrice,
                AskPrice = ev.AskPrice,
                BidSize = ev.BidSize,
                AskSize = ev.AskSize,
                Spread = spread,
                MidPrice = mid,
                MicroPrice = micro,
                MicroBiasTicks = microBias,
                BuyAggressorVolume = buy,
                SellAggressorVolume = sell,
                Delta = _w1.Delta(now),
                CumulativeDelta = _cumulativeDelta,
                Delta1s = _w1.Delta(now),
                Delta5s = _w5.Delta(now),
                Delta15s = _w15.Delta(now),
                Delta60s = _w60.Delta(now),
                Volume1s = _w1.Volume(now),
                Volume5s = _w5.Volume(now),
                Volume15s = _w15.Volume(now),
                Volume60s = _w60.Volume(now),
                TopBookImbalance = imbalance,
                TopBookRatio = TopBookRatio(ev.BidSize, ev.AskSize),
                OrderFlowImbalance = (ev.BidSizeDeltaFromPrevious ?? 0) - (ev.AskSizeDeltaFromPrevious ?? 0),
                BidStackPull = ev.BidSizeDeltaFromPrevious,
                AskStackPull = ev.AskSizeDeltaFromPrevious,
                Vwap = vwap,
                VwapStd1 = std,
                VwapStd2 = std.HasValue ? std * 2 : null,
                VwapDistanceTicks = vwap.HasValue && ev.LastPrice.HasValue ? (ev.LastPrice - vwap) / tick : null,
                Poc = _profile.Poc,
                Vah = _profile.Vah,
                Val = _profile.Val,
                EventsProcessed = _eventsProcessed,
                TradesDerived = _tradesDerived,
                SignalsGenerated = _signalsGenerated,
                QueueSize = queueSize
            };
        }

        private void PruneSignals(DateTimeOffset now)
        {
            _activeSignals.RemoveAll(signal => (now - signal.Timestamp).TotalMilliseconds > signal.TtlMs);

            if (_activeSignals.Count > 80)
            {
                _activeSignals.RemoveRange(80, _activeSignals.Count - 80);
            }
        }

        private static decimal Positive(decimal? value)
        {
            return value.HasValue && value.Value > 0 ? value.Value : 0;
        }

        private static decimal? TopBookImbalance(decimal? bidSize, decimal? askSize)
        {
            if (!bidSize.HasValue || !askSize.HasValue || bidSize.Value + askSize.Value == 0)
            {
                return null;
            }

            return (bidSize.Value - askSize.Value) / (bidSize.Value + askSize.Value);
        }

        private static decimal? TopBookRatio(decimal? bidSize, decimal? askSize)
        {
            if (!bidSize.HasValue || !askSize.HasValue || askSize.Value == 0)
            {
                return null;
            }

            return bidSize.Value / askSize.Value;
        }

        private static decimal? MicroPrice(decimal? bid, decimal? ask, decimal? bidSize, decimal? askSize)
        {
            if (!bid.HasValue || !ask.HasValue || !bidSize.HasValue || !askSize.HasValue || bidSize.Value + askSize.Value == 0)
            {
                return null;
            }

            return (ask.Value * bidSize.Value + bid.Value * askSize.Value) / (bidSize.Value + askSize.Value);
        }
    }
}
