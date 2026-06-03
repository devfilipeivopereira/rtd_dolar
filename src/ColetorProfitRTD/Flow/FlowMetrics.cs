using System;
using System.Collections.Generic;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class FlowMetrics
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Asset { get; set; }
        public MarketDataQuality DataQuality { get; set; }

        public decimal? LastPrice { get; set; }
        public decimal? BidPrice { get; set; }
        public decimal? AskPrice { get; set; }
        public decimal? BidSize { get; set; }
        public decimal? AskSize { get; set; }
        public decimal? Spread { get; set; }
        public decimal? MidPrice { get; set; }
        public decimal? MicroPrice { get; set; }
        public decimal? MicroBiasTicks { get; set; }

        public decimal BuyAggressorVolume { get; set; }
        public decimal SellAggressorVolume { get; set; }
        public decimal Delta { get; set; }
        public decimal CumulativeDelta { get; set; }
        public decimal Delta1s { get; set; }
        public decimal Delta5s { get; set; }
        public decimal Delta15s { get; set; }
        public decimal Delta60s { get; set; }
        public decimal Volume1s { get; set; }
        public decimal Volume5s { get; set; }
        public decimal Volume15s { get; set; }
        public decimal Volume60s { get; set; }

        public decimal? TopBookImbalance { get; set; }
        public decimal? TopBookRatio { get; set; }
        public decimal? OrderFlowImbalance { get; set; }
        public decimal? BidStackPull { get; set; }
        public decimal? AskStackPull { get; set; }

        public decimal? Vwap { get; set; }
        public decimal? VwapStd1 { get; set; }
        public decimal? VwapStd2 { get; set; }
        public decimal? VwapDistanceTicks { get; set; }

        public decimal? Poc { get; set; }
        public decimal? Vah { get; set; }
        public decimal? Val { get; set; }

        public int EventsProcessed { get; set; }
        public int TradesDerived { get; set; }
        public int SignalsGenerated { get; set; }
        public int QueueSize { get; set; }

        public List<FlowSignal> ActiveSignals { get; set; } = new List<FlowSignal>();

        public Dictionary<string, object> ToMessage()
        {
            return new Dictionary<string, object>
            {
                ["lastPrice"] = LastPrice,
                ["bidPrice"] = BidPrice,
                ["askPrice"] = AskPrice,
                ["bidSize"] = BidSize,
                ["askSize"] = AskSize,
                ["spread"] = Spread,
                ["midPrice"] = MidPrice,
                ["microPrice"] = MicroPrice,
                ["microBiasTicks"] = MicroBiasTicks,
                ["buyAggressorVolume"] = BuyAggressorVolume,
                ["sellAggressorVolume"] = SellAggressorVolume,
                ["delta"] = Delta,
                ["cumulativeDelta"] = CumulativeDelta,
                ["delta1s"] = Delta1s,
                ["delta5s"] = Delta5s,
                ["delta15s"] = Delta15s,
                ["delta60s"] = Delta60s,
                ["volume1s"] = Volume1s,
                ["volume5s"] = Volume5s,
                ["volume15s"] = Volume15s,
                ["volume60s"] = Volume60s,
                ["topBookImbalance"] = TopBookImbalance,
                ["topBookRatio"] = TopBookRatio,
                ["orderFlowImbalance"] = OrderFlowImbalance,
                ["bidStackPull"] = BidStackPull,
                ["askStackPull"] = AskStackPull,
                ["vwap"] = Vwap,
                ["vwapStd1"] = VwapStd1,
                ["vwapStd2"] = VwapStd2,
                ["vwapDistanceTicks"] = VwapDistanceTicks,
                ["poc"] = Poc,
                ["vah"] = Vah,
                ["val"] = Val,
                ["eventsProcessed"] = EventsProcessed,
                ["tradesDerived"] = TradesDerived,
                ["signalsGenerated"] = SignalsGenerated,
                ["queueSize"] = QueueSize
            };
        }
    }
}
