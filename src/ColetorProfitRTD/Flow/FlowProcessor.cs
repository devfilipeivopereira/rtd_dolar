using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Flow
{
    public sealed class FlowProcessor : IDisposable
    {
        private readonly FlowConfig _config;
        private readonly Logger _log;
        private readonly Dictionary<string, AssetFlowState> _states = new Dictionary<string, AssetFlowState>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<MarketSnapshot> _queue = new Queue<MarketSnapshot>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly object _lock = new object();

        private Thread _thread;
        private bool _running;
        private DateTime _lastBroadcastUtc = DateTime.MinValue;
        private Dictionary<string, object> _lastFlowMessage;

        public FlowProcessor(FlowConfig config, Logger log)
        {
            _config = config ?? new FlowConfig();
            _log = log;
        }

        public event Action<Dictionary<string, object>, FlowMetrics> FlowUpdated;
        public event Action<Dictionary<string, object>, FlowSignal> SignalGenerated;

        public bool Enabled => _config.Enabled;
        public FlowMetrics CurrentMetrics
        {
            get
            {
                lock (_lock)
                {
                    AssetFlowState state = LatestStateUnlocked();
                    return state == null ? new FlowMetrics() : state.Engine.CurrentMetrics;
                }
            }
        }

        public void Start()
        {
            if (!_config.Enabled || _running)
            {
                return;
            }

            _running = true;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "Order Flow Processor"
            };
            _thread.Start();
            _log.Info("FlowProcessor iniciado.");
        }

        public void Stop()
        {
            _running = false;
            _wake.Set();

            if (_thread != null && !_thread.Join(2000))
            {
                _log.Warn("FlowProcessor nao encerrou dentro do timeout.");
            }
        }

        public void Post(MarketSnapshot snapshot)
        {
            if (!_config.Enabled || snapshot == null)
            {
                return;
            }

            lock (_lock)
            {
                _queue.Enqueue(snapshot.Clone());

                while (_queue.Count > Math.Max(_config.MaxQueueSize, 1))
                {
                    _queue.Dequeue();
                }
            }

            _wake.Set();
        }

        public Dictionary<string, object> CurrentFlowMessage()
        {
            lock (_lock)
            {
                if (_lastFlowMessage != null)
                {
                    return _lastFlowMessage;
                }

                AssetFlowState state = LatestStateUnlocked();
                return state == null ? BuildFlowMessage(new AssetFlowState(_config)) : BuildFlowMessage(state);
            }
        }

        public Dictionary<string, object> CurrentSignalsMessage()
        {
            List<Dictionary<string, object>> signals;

            lock (_lock)
            {
                signals = _states.Values
                    .SelectMany(x => x.Engine.ActiveSignals)
                    .OrderByDescending(x => x.Timestamp)
                    .Take(80)
                    .Select(x => x.ToMessage())
                    .ToList();
            }

            return new Dictionary<string, object>
            {
                ["type"] = "signals",
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o"),
                ["signals"] = signals
            };
        }

        public Dictionary<string, object> Health()
        {
            FlowMetrics metrics = CurrentMetrics;

            return new Dictionary<string, object>
            {
                ["enabled"] = _config.Enabled,
                ["dataQuality"] = QualityName(metrics.DataQuality),
                ["queueSize"] = QueueSize(),
                ["assetCount"] = AssetCount(),
                ["asset"] = metrics.Asset,
                ["events"] = metrics.EventsProcessed,
                ["trades"] = metrics.TradesDerived,
                ["signals"] = metrics.SignalsGenerated,
                ["lastFlowUpdate"] = metrics.Timestamp == default(DateTimeOffset) ? null : metrics.Timestamp.ToString("o")
            };
        }

        public void Dispose()
        {
            Stop();
            _wake.Dispose();
        }

        private void Loop()
        {
            while (_running)
            {
                _wake.WaitOne(250);

                if (!_running)
                {
                    break;
                }

                List<MarketSnapshot> snapshots = DrainLatestByAsset();

                if (snapshots.Count == 0)
                {
                    continue;
                }

                Thread.Sleep(Math.Max(_config.CoalescingMs, 0));

                List<MarketSnapshot> newer = DrainLatestByAsset();
                if (newer.Count > 0)
                {
                    snapshots = newer;
                }

                foreach (MarketSnapshot snapshot in snapshots)
                {
                    try
                    {
                        Process(snapshot);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Falha no FlowProcessor.", ex);
                    }
                }
            }
        }

        private void Process(MarketSnapshot snapshot)
        {
            AssetFlowState state = GetOrCreateState(snapshot.Asset);
            NormalizedMarketEvent ev = state.Coalescer.Build(snapshot);
            if (ev == null)
            {
                return;
            }

            List<FlowSignal> signals = state.Engine.Process(ev, QueueSize());
            Dictionary<string, object> message = BuildFlowMessage(state);

            lock (_lock)
            {
                state.LastFlowMessage = message;
                _lastFlowMessage = message;
            }

            foreach (FlowSignal signal in signals)
            {
                var signalMessage = new Dictionary<string, object>
                {
                    ["type"] = "signal",
                    ["signal"] = signal.ToMessage()
                };
                SignalGenerated?.Invoke(signalMessage, signal);
            }

            DateTime now = DateTime.UtcNow;
            if ((now - state.LastBroadcastUtc).TotalMilliseconds >= Math.Max(_config.BroadcastIntervalMs, 50))
            {
                state.LastBroadcastUtc = now;
                _lastBroadcastUtc = now;
                FlowUpdated?.Invoke(message, state.Engine.CurrentMetrics);
            }
        }

        private Dictionary<string, object> BuildFlowMessage(AssetFlowState state)
        {
            FlowMetrics metrics = state.Engine.CurrentMetrics;
            if (metrics == null)
            {
                metrics = new FlowMetrics();
            }

            return new Dictionary<string, object>
            {
                ["type"] = "flow",
                ["asset"] = metrics.Asset,
                ["localTimestamp"] = metrics.Timestamp == default(DateTimeOffset) ? DateTimeOffset.Now.ToString("o") : metrics.Timestamp.ToString("o"),
                ["dataQuality"] = QualityName(metrics.DataQuality),
                ["metrics"] = metrics.ToMessage(),
                ["recentTrades"] = state.Engine.RecentTrades.Take(40).Select(x => x.ToMessage()).ToList(),
                ["activeSignals"] = state.Engine.ActiveSignals.Select(x => x.ToMessage()).ToList()
            };
        }

        private List<MarketSnapshot> DrainLatestByAsset()
        {
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    return new List<MarketSnapshot>();
                }

                var byAsset = new Dictionary<string, MarketSnapshot>(StringComparer.OrdinalIgnoreCase);
                while (_queue.Count > 0)
                {
                    MarketSnapshot snapshot = _queue.Dequeue();
                    string asset = string.IsNullOrWhiteSpace(snapshot.Asset) ? string.Empty : snapshot.Asset;
                    byAsset[asset] = snapshot;
                }

                return byAsset.Values.ToList();
            }
        }

        private int QueueSize()
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }

        private int AssetCount()
        {
            lock (_lock)
            {
                return _states.Count;
            }
        }

        private AssetFlowState GetOrCreateState(string asset)
        {
            string key = string.IsNullOrWhiteSpace(asset) ? "UNKNOWN" : asset.Trim().ToUpperInvariant();

            lock (_lock)
            {
                if (!_states.TryGetValue(key, out AssetFlowState state))
                {
                    state = new AssetFlowState(_config);
                    _states[key] = state;
                }

                return state;
            }
        }

        private AssetFlowState LatestStateUnlocked()
        {
            return _states.Values
                .OrderByDescending(x => x.Engine.CurrentMetrics.Timestamp)
                .FirstOrDefault();
        }

        public static string QualityName(MarketDataQuality quality)
        {
            switch (quality)
            {
                case MarketDataQuality.TopOfBookOnly:
                    return "topOfBookOnly";
                case MarketDataQuality.DerivedTape:
                    return "derivedTape";
                case MarketDataQuality.FullTimesAndTrades:
                    return "fullTimesAndTrades";
                case MarketDataQuality.FullDepth:
                    return "fullDepth";
                default:
                    return "unknown";
            }
        }

        private sealed class AssetFlowState
        {
            public AssetFlowState(FlowConfig config)
            {
                Coalescer = new SnapshotCoalescer();
                Engine = new FlowEngine(config);
            }

            public SnapshotCoalescer Coalescer { get; }
            public FlowEngine Engine { get; }
            public DateTime LastBroadcastUtc { get; set; } = DateTime.MinValue;
            public Dictionary<string, object> LastFlowMessage { get; set; }
        }
    }
}
