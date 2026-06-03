using System;

namespace ColetorProfitRTD.MarketData
{
    public sealed class BookSnapshot
    {
        public long Sequence { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Asset { get; set; }
        public decimal? BidPrice { get; set; }
        public decimal? AskPrice { get; set; }
        public decimal? BidSize { get; set; }
        public decimal? AskSize { get; set; }
        public decimal? Spread { get; set; }
        public decimal? MidPrice { get; set; }
        public decimal? MicroPrice { get; set; }
        public decimal? TopBookImbalance { get; set; }
    }
}
