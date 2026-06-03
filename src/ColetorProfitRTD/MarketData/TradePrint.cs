using System;

namespace ColetorProfitRTD.MarketData
{
    public sealed class TradePrint
    {
        public long Sequence { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Asset { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public string AggressorSide { get; set; }
        public string Classification { get; set; }
        public bool Derived { get; set; }
        public decimal? BidAtTrade { get; set; }
        public decimal? AskAtTrade { get; set; }

        public object ToMessage()
        {
            return new
            {
                sequence = Sequence,
                time = Timestamp.ToString("HH:mm:ss.fff"),
                timestamp = Timestamp.ToString("o"),
                asset = Asset,
                price = Price,
                qty = Quantity,
                side = AggressorSide,
                classification = Classification,
                derived = Derived,
                bid = BidAtTrade,
                ask = AskAtTrade
            };
        }
    }
}
