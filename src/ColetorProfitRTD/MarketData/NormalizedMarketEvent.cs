using System;

namespace ColetorProfitRTD.MarketData
{
    public sealed class NormalizedMarketEvent
    {
        public long Sequence { get; set; }
        public DateTimeOffset LocalTimestamp { get; set; }
        public string ProfitDate { get; set; }
        public string ProfitTime { get; set; }
        public string Asset { get; set; }

        public decimal? LastPrice { get; set; }
        public decimal? Open { get; set; }
        public decimal? High { get; set; }
        public decimal? Low { get; set; }
        public decimal? ProfitMed { get; set; }

        public decimal? TotalVolume { get; set; }
        public decimal? LastQuantity { get; set; }
        public decimal? TotalQuantity { get; set; }
        public decimal? TotalTrades { get; set; }

        public decimal? BidPrice { get; set; }
        public decimal? AskPrice { get; set; }
        public decimal? BidSize { get; set; }
        public decimal? AskSize { get; set; }

        public decimal? VolumeDeltaFromPrevious { get; set; }
        public decimal? QuantityDeltaFromPrevious { get; set; }
        public decimal? TradesDeltaFromPrevious { get; set; }
        public decimal? PriceDeltaFromPrevious { get; set; }
        public decimal? BidSizeDeltaFromPrevious { get; set; }
        public decimal? AskSizeDeltaFromPrevious { get; set; }

        public MarketDataQuality Quality { get; set; }
        public bool IsTradeCandidate { get; set; }
        public bool IsBookCandidate { get; set; }
    }
}
