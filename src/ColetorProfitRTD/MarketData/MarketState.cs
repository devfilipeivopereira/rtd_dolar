using System;
using System.Collections.Generic;
using System.Linq;

namespace ColetorProfitRTD.MarketData
{
    public sealed class MarketState
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, MarketSnapshot> _snapshots = new Dictionary<string, MarketSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly string _defaultAsset;

        public MarketState(string defaultAsset)
        {
            _defaultAsset = string.IsNullOrWhiteSpace(defaultAsset) ? "WDOFUT_F_0" : defaultAsset.Trim().ToUpperInvariant();
        }

        public MarketSnapshot Update(string asset, string field, object value, string status)
        {
            lock (_lock)
            {
                MarketSnapshot snapshot = GetOrCreate(asset);
                string normalizedField = field == null ? string.Empty : field.Trim().ToUpperInvariant();
                snapshot.LocalTimestamp = DateTimeOffset.Now;
                snapshot.Asset = NormalizeAsset(asset);
                snapshot.Status = status;
                snapshot.Rtd[normalizedField] = ValueParser.ToJsonValue(value);
                snapshot.Raw[normalizedField] = ValueParser.ToText(value);

                return snapshot.Clone();
            }
        }

        public MarketSnapshot MarkStatus(string status)
        {
            lock (_lock)
            {
                if (_snapshots.Count == 0)
                {
                    GetOrCreate(_defaultAsset);
                }

                foreach (MarketSnapshot snapshot in _snapshots.Values)
                {
                    snapshot.LocalTimestamp = DateTimeOffset.Now;
                    snapshot.Status = status;
                }

                return CurrentUnlocked(_defaultAsset);
            }
        }

        public MarketSnapshot Current(string asset = null)
        {
            lock (_lock)
            {
                return CurrentUnlocked(asset);
            }
        }

        public List<MarketSnapshot> All()
        {
            lock (_lock)
            {
                return _snapshots.Values.Select(x => x.Clone()).ToList();
            }
        }

        public void Remove(string asset)
        {
            lock (_lock)
            {
                string key = NormalizeAsset(asset);
                _snapshots.Remove(key);
            }
        }

        private MarketSnapshot CurrentUnlocked(string asset)
        {
            string key = NormalizeAsset(asset);

            if (_snapshots.TryGetValue(key, out MarketSnapshot snapshot))
            {
                return snapshot.Clone();
            }

            if (_snapshots.TryGetValue(_defaultAsset, out snapshot))
            {
                return snapshot.Clone();
            }

            return GetOrCreate(_defaultAsset).Clone();
        }

        private MarketSnapshot GetOrCreate(string asset)
        {
            string key = NormalizeAsset(asset);

            if (!_snapshots.TryGetValue(key, out MarketSnapshot snapshot))
            {
                snapshot = new MarketSnapshot
                {
                    Asset = key,
                    Status = "starting"
                };
                _snapshots[key] = snapshot;
            }

            return snapshot;
        }

        private string NormalizeAsset(string asset)
        {
            return string.IsNullOrWhiteSpace(asset) ? _defaultAsset : asset.Trim().ToUpperInvariant();
        }
    }
}
