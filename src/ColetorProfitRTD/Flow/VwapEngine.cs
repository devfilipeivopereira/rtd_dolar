using System;
using System.Collections.Generic;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class VwapEngine
    {
        private decimal _priceVolume;
        private decimal _volume;
        private decimal _weightedSquareDistance;
        private decimal? _vwap;

        public decimal? Vwap => _vwap;

        public void Add(TradePrint trade)
        {
            if (trade == null || trade.Quantity <= 0)
            {
                return;
            }

            _priceVolume += trade.Price * trade.Quantity;
            _volume += trade.Quantity;
            _vwap = _volume > 0 ? _priceVolume / _volume : (decimal?)null;

            if (_vwap.HasValue)
            {
                decimal diff = trade.Price - _vwap.Value;
                _weightedSquareDistance += diff * diff * trade.Quantity;
            }
        }

        public decimal? Std()
        {
            if (_volume <= 0)
            {
                return null;
            }

            double variance = (double)(_weightedSquareDistance / _volume);
            return (decimal)Math.Sqrt(Math.Max(variance, 0));
        }

        public decimal? ResolveVwap(decimal? profitMed, bool useProfitMedFallback)
        {
            if (_vwap.HasValue)
            {
                return _vwap;
            }

            return useProfitMedFallback ? profitMed : null;
        }
    }
}
