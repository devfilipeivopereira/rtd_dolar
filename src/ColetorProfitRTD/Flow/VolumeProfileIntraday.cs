using System;
using System.Collections.Generic;
using System.Linq;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class VolumeProfileIntraday
    {
        private readonly decimal _tickSize;
        private readonly Dictionary<decimal, decimal> _volumeByPrice = new Dictionary<decimal, decimal>();

        public VolumeProfileIntraday(decimal tickSize)
        {
            _tickSize = tickSize <= 0 ? 0.5m : tickSize;
        }

        public decimal? Poc { get; private set; }
        public decimal? Vah { get; private set; }
        public decimal? Val { get; private set; }

        public void Add(TradePrint trade)
        {
            if (trade == null || trade.Quantity <= 0)
            {
                return;
            }

            decimal price = RoundToTick(trade.Price);
            decimal current;
            _volumeByPrice.TryGetValue(price, out current);
            _volumeByPrice[price] = current + trade.Quantity;
            RecalculateValueArea();
        }

        private void RecalculateValueArea()
        {
            if (_volumeByPrice.Count == 0)
            {
                Poc = null;
                Vah = null;
                Val = null;
                return;
            }

            var ordered = _volumeByPrice.OrderByDescending(x => x.Value).ToList();
            Poc = ordered[0].Key;

            decimal total = _volumeByPrice.Values.Sum();
            decimal target = total * 0.70m;
            decimal accumulated = 0;
            var selected = new List<decimal>();

            foreach (var item in ordered)
            {
                selected.Add(item.Key);
                accumulated += item.Value;

                if (accumulated >= target)
                {
                    break;
                }
            }

            Vah = selected.Max();
            Val = selected.Min();
        }

        private decimal RoundToTick(decimal price)
        {
            return Math.Round(price / _tickSize, 0, MidpointRounding.AwayFromZero) * _tickSize;
        }
    }
}
