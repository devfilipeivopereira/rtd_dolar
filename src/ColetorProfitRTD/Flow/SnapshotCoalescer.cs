using System;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class SnapshotCoalescer
    {
        private long _sequence;
        private MarketSnapshot _lastEmitted;

        public NormalizedMarketEvent Build(MarketSnapshot current)
        {
            if (current == null)
            {
                return null;
            }

            MarketSnapshot previous = _lastEmitted;
            _lastEmitted = current.Clone();

            decimal? volumeDelta = PositiveDelta(current.Volume, previous == null ? null : previous.Volume);
            decimal? quantityDelta = PositiveDelta(current.Quantidade, previous == null ? null : previous.Quantidade);
            decimal? tradesDelta = PositiveDelta(current.Negocios, previous == null ? null : previous.Negocios);
            decimal? priceDelta = Delta(current.Ultimo, previous == null ? null : previous.Ultimo);
            decimal? bidSizeDelta = Delta(current.VolumeOfertaCompra, previous == null ? null : previous.VolumeOfertaCompra);
            decimal? askSizeDelta = Delta(current.VolumeOfertaVenda, previous == null ? null : previous.VolumeOfertaVenda);

            bool priceChanged = priceDelta.HasValue && priceDelta.Value != 0;
            bool volumeChanged = volumeDelta.HasValue && volumeDelta.Value > 0;
            bool tradesChanged = tradesDelta.HasValue && tradesDelta.Value > 0;
            bool lastQtyChanged = current.Quantidade.HasValue && current.Quantidade.Value > 0;
            bool bookChanged = HasChanged(current.OfertaCompra, previous == null ? null : previous.OfertaCompra)
                || HasChanged(current.OfertaVenda, previous == null ? null : previous.OfertaVenda)
                || HasChanged(current.VolumeOfertaCompra, previous == null ? null : previous.VolumeOfertaCompra)
                || HasChanged(current.VolumeOfertaVenda, previous == null ? null : previous.VolumeOfertaVenda);

            var ev = new NormalizedMarketEvent
            {
                Sequence = ++_sequence,
                LocalTimestamp = current.LocalTimestamp,
                ProfitDate = current.DataProfit,
                ProfitTime = current.HoraProfit,
                Asset = current.Asset,
                LastPrice = current.Ultimo,
                Open = current.Abertura,
                High = current.Maxima,
                Low = current.Minima,
                ProfitMed = current.Media,
                TotalVolume = current.Volume,
                LastQuantity = current.GetFieldDecimal("QUL"),
                TotalQuantity = current.Quantidade,
                TotalTrades = current.Negocios,
                BidPrice = current.OfertaCompra,
                AskPrice = current.OfertaVenda,
                BidSize = current.VolumeOfertaCompra,
                AskSize = current.VolumeOfertaVenda,
                VolumeDeltaFromPrevious = volumeDelta,
                QuantityDeltaFromPrevious = quantityDelta,
                TradesDeltaFromPrevious = tradesDelta,
                PriceDeltaFromPrevious = priceDelta,
                BidSizeDeltaFromPrevious = bidSizeDelta,
                AskSizeDeltaFromPrevious = askSizeDelta,
                IsTradeCandidate = priceChanged || volumeChanged || tradesChanged || lastQtyChanged,
                IsBookCandidate = bookChanged
            };

            ev.Quality = ResolveQuality(ev);
            return ev;
        }

        private static MarketDataQuality ResolveQuality(NormalizedMarketEvent ev)
        {
            bool hasTrade = ev.LastPrice.HasValue && (ev.LastQuantity.HasValue || ev.VolumeDeltaFromPrevious.HasValue || ev.QuantityDeltaFromPrevious.HasValue);
            bool hasBook = ev.BidPrice.HasValue || ev.AskPrice.HasValue || ev.BidSize.HasValue || ev.AskSize.HasValue;

            if (hasTrade)
            {
                return MarketDataQuality.DerivedTape;
            }

            if (hasBook)
            {
                return MarketDataQuality.TopOfBookOnly;
            }

            return MarketDataQuality.Unknown;
        }

        private static decimal? PositiveDelta(decimal? current, decimal? previous)
        {
            decimal? delta = Delta(current, previous);

            if (!delta.HasValue || delta.Value < 0)
            {
                return null;
            }

            return delta;
        }

        private static decimal? Delta(decimal? current, decimal? previous)
        {
            if (!current.HasValue || !previous.HasValue)
            {
                return null;
            }

            return current.Value - previous.Value;
        }

        private static bool HasChanged(decimal? current, decimal? previous)
        {
            if (!current.HasValue && !previous.HasValue)
            {
                return false;
            }

            if (current.HasValue != previous.HasValue)
            {
                return true;
            }

            return current.Value != previous.Value;
        }
    }
}
