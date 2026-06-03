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
        private readonly SnapshotCoalescer _coalescer = new SnapshotCoalescer();
        private readonly FlowEngine _engine;
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
            _engine = new FlowEngine(_config);
        }

        public event Action<Dictionary<string, object>, FlowMetrics> FlowUpdated;
        public event Action<Dictionary<string, object>, FlowSignal> SignalGenerated;

        public bool Enabled => _config.Enabled;
        public FlowMetrics CurrentMetrics => _engine.CurrentMetrics;

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
                return _lastFlowMessage ?? BuildFlowMessage(_engine.CurrentMetrics);
            }
        }

        public Dictionary<string, object> CurrentSignalsMessage()
        {
            return new Dictionary<string, object>
            {
                ["type"] = "signals",
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o"),
                ["signals"] = _engine.ActiveSignals.Select(x => x.ToMessage()).ToList()
            };
        }

        public Dictionary<string, object> Health()
        {
            FlowMetrics metrics = _engine.CurrentMetrics;

            return new Dictionary<string, object>
            {
                ["enabled"] = _config.Enabled,
                ["dataQuality"] = QualityName(metrics.DataQuality),
                ["queueSize"] = QueueSize(),
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

                MarketSnapshot latest = DrainLatest();

                if (latest == null)
                {
                    continue;
                }

                Thread.Sleep(Math.Max(_config.CoalescingMs, 0));

                MarketSnapshot newer = DrainLatest();
                if (newer != null)
                {
                    latest = newer;
                }

                try
                {
                    Process(latest);
                }
                catch (Exception ex)
                {
                    _log.Error("Falha no FlowProcessor.", ex);
                }
            }
        }

        private void Process(MarketSnapshot snapshot)
        {
            NormalizedMarketEvent ev = _coalescer.Build(snapshot);
            if (ev == null)
            {
                return;
            }

            List<FlowSignal> signals = _engine.Process(ev, QueueSize());
            Dictionary<string, object> message = BuildFlowMessage(_engine.CurrentMetrics);

            lock (_lock)
            {
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
            if ((now - _lastBroadcastUtc).TotalMilliseconds >= Math.Max(_config.BroadcastIntervalMs, 50))
            {
                _lastBroadcastUtc = now;
                FlowUpdated?.Invoke(message, _engine.CurrentMetrics);
            }
        }

        private Dictionary<string, object> BuildFlowMessage(FlowMetrics metrics)
        {
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
                ["recentTrades"] = _engine.RecentTrades.Take(40).Select(x => x.ToMessage()).ToList(),
                ["activeSignals"] = _engine.ActiveSignals.Select(x => x.ToMessage()).ToList()
            };
        }

        private MarketSnapshot DrainLatest()
        {
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    return null;
                }

                MarketSnapshot latest = null;
                while (_queue.Count > 0)
                {
                    latest = _queue.Dequeue();
                }

                return latest;
            }
        }

        private int QueueSize()
        {
            lock (_lock)
            {
                return _queue.Count;
            }
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
    }
}
