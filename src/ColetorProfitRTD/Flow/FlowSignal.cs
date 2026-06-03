using System;
using System.Collections.Generic;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class FlowSignal
    {
        public string Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Asset { get; set; }
        public string Setup { get; set; }
        public string Direction { get; set; }
        public decimal? Price { get; set; }
        public decimal Score { get; set; }
        public string Confidence { get; set; }
        public MarketDataQuality DataQuality { get; set; }
        public bool Derived { get; set; }
        public string LevelLabel { get; set; }
        public decimal? LevelPrice { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public int TtlMs { get; set; }

        public Dictionary<string, object> ToMessage()
        {
            return new Dictionary<string, object>
            {
                ["id"] = Id,
                ["timestamp"] = Timestamp.ToString("o"),
                ["asset"] = Asset,
                ["setup"] = Setup,
                ["direction"] = Direction,
                ["price"] = Price,
                ["score"] = Score,
                ["confidence"] = Confidence,
                ["dataQuality"] = DataQuality.ToString(),
                ["derived"] = Derived,
                ["levelLabel"] = LevelLabel,
                ["levelPrice"] = LevelPrice,
                ["reasons"] = Reasons,
                ["ttlMs"] = TtlMs
            };
        }
    }
}
