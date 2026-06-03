using System;
using System.Collections.Generic;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class RollingWindow
    {
        private readonly TimeSpan _window;
        private readonly LinkedList<TradePrint> _items = new LinkedList<TradePrint>();

        public RollingWindow(TimeSpan window)
        {
            _window = window;
        }

        public void Add(TradePrint trade)
        {
            if (trade == null)
            {
                return;
            }

            _items.AddLast(trade);
            Trim(trade.Timestamp);
        }

        public void Trim(DateTimeOffset now)
        {
            while (_items.First != null && now - _items.First.Value.Timestamp > _window)
            {
                _items.RemoveFirst();
            }
        }

        public decimal Delta(DateTimeOffset now)
        {
            Trim(now);
            decimal total = 0;

            foreach (TradePrint trade in _items)
            {
                if (trade.AggressorSide == "buy")
                {
                    total += trade.Quantity;
                }
                else if (trade.AggressorSide == "sell")
                {
                    total -= trade.Quantity;
                }
            }

            return total;
        }

        public decimal Volume(DateTimeOffset now)
        {
            Trim(now);
            decimal total = 0;

            foreach (TradePrint trade in _items)
            {
                total += trade.Quantity;
            }

            return total;
        }
    }
}
