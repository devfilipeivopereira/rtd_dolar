using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ColetorProfitRTD.MarketData;

namespace ColetorProfitRTD.Rtd
{
    public sealed class RtdClient : IDisposable
    {
        private readonly RtdConfig _config;
        private readonly Logger _log;
        private readonly MarketState _state = new MarketState();
        private readonly object _statusLock = new object();
        private readonly Dictionary<int, RtdTopic> _topics = new Dictionary<int, RtdTopic>();
        private Thread _thread;
        private RtdUpdateEvent _callback;
        private volatile bool _stopRequested;
        private string _status = "starting";
        private Exception _lastError;

        public RtdClient(RtdConfig config, Logger log)
        {
            _config = config;
            _log = log;
        }

        public event Action<MarketSnapshot> SnapshotReceived;
        public event Action<string, Exception> StatusChanged;

        public string Status
        {
            get
            {
                lock (_statusLock)
                {
                    return _status;
                }
            }
        }

        public Exception LastError
        {
            get
            {
                lock (_statusLock)
                {
                    return _lastError;
                }
            }
        }

        public MarketSnapshot CurrentSnapshot => _state.Current();

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "Profit RTD STA"
            };

            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public void Stop()
        {
            _stopRequested = true;

            if (_thread != null && _thread.IsAlive)
            {
                _thread.Join(TimeSpan.FromSeconds(5));
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void Run()
        {
            while (!_stopRequested)
            {
                try
                {
                    ConnectAndPump();
                }
                catch (Exception ex)
                {
                    SetStatus("disconnected", ex);
                    _log.Error("Falha no loop RTD. Tentando reconectar.", ex);
                    SleepInterruptible(Math.Max(_config.ReconnectIntervalMs, 1000));
                }
            }
        }

        private void ConnectAndPump()
        {
            object rtdObject = null;
            IRtdServer server = null;

            try
            {
                SetStatus("connecting", null);
                _topics.Clear();

                Type rtdType = Type.GetTypeFromProgID(_config.ProgId);

                if (rtdType == null)
                {
                    throw new InvalidOperationException("Servidor RTD nao encontrado: " + _config.ProgId);
                }

                rtdObject = Activator.CreateInstance(rtdType);
                server = (IRtdServer)rtdObject;
                _callback = new RtdUpdateEvent();

                int startResult = server.ServerStart(_callback);
                _log.Info("ServerStart retornou " + startResult + ".");

                if (startResult <= 0)
                {
                    throw new InvalidOperationException("ServerStart falhou. Codigo: " + startResult);
                }

                SubscribeAll(server);
                SetStatus("connected", null);

                DateTime nextHeartbeat = DateTime.UtcNow.AddSeconds(5);

                while (!_stopRequested)
                {
                    PumpRefreshData(server);

                    if (DateTime.UtcNow >= nextHeartbeat)
                    {
                        int heartbeat = server.Heartbeat();

                        if (heartbeat <= 0)
                        {
                            throw new InvalidOperationException("Heartbeat RTD falhou. Codigo: " + heartbeat);
                        }

                        nextHeartbeat = DateTime.UtcNow.AddSeconds(5);
                    }

                    SleepInterruptible(Math.Max(_config.PollIntervalMs, 50));
                }
            }
            finally
            {
                Disconnect(server);
                _callback = null;

                if (rtdObject != null && Marshal.IsComObject(rtdObject))
                {
                    Marshal.ReleaseComObject(rtdObject);
                }
            }
        }

        private void SubscribeAll(IRtdServer server)
        {
            int topicId = 1;

            foreach (string field in _config.Fields.Select(x => x.Trim().ToUpperInvariant()).Distinct())
            {
                bool getNewValues = true;
                Array topicArgs = new[] { _config.Asset, field };
                object initialValue = server.ConnectData(topicId, ref topicArgs, ref getNewValues);

                var topic = new RtdTopic
                {
                    TopicId = topicId,
                    Asset = _config.Asset,
                    Field = field,
                    LastValue = initialValue
                };

                _topics[topicId] = topic;
                _log.Info("Assinado RTD " + topic.Key + ".");
                Publish(topic, initialValue);

                topicId++;
            }
        }

        private void PumpRefreshData(IRtdServer server)
        {
            int topicCount = 0;
            Array data = server.RefreshData(ref topicCount);

            if (data == null || topicCount <= 0)
            {
                return;
            }

            int rowLower = data.GetLowerBound(0);
            int colLower = data.GetLowerBound(1);

            for (int i = 0; i < topicCount; i++)
            {
                object idObject = data.GetValue(rowLower, colLower + i);
                object value = data.GetValue(rowLower + 1, colLower + i);

                if (idObject == null)
                {
                    continue;
                }

                int topicId = Convert.ToInt32(idObject);

                if (!_topics.TryGetValue(topicId, out RtdTopic topic))
                {
                    continue;
                }

                topic.LastValue = value;
                Publish(topic, value);
            }
        }

        private void Publish(RtdTopic topic, object value)
        {
            MarketSnapshot snapshot = _state.Update(topic.Asset, topic.Field, value, Status);
            SnapshotReceived?.Invoke(snapshot);
        }

        private void Disconnect(IRtdServer server)
        {
            if (server == null)
            {
                return;
            }

            foreach (RtdTopic topic in _topics.Values.ToList())
            {
                try
                {
                    server.DisconnectData(topic.TopicId);
                }
                catch (Exception ex)
                {
                    _log.Warn("Falha ao desconectar topico " + topic.Key + ": " + ex.Message);
                }
            }

            try
            {
                server.ServerTerminate();
            }
            catch (Exception ex)
            {
                _log.Warn("Falha ao finalizar servidor RTD: " + ex.Message);
            }
        }

        private void SetStatus(string status, Exception error)
        {
            bool changed;

            lock (_statusLock)
            {
                changed = !string.Equals(_status, status, StringComparison.OrdinalIgnoreCase) || error != _lastError;
                _status = status;
                _lastError = error;
            }

            MarketSnapshot snapshot = _state.MarkStatus(status);
            SnapshotReceived?.Invoke(snapshot);

            if (changed)
            {
                _log.Info("Status RTD: " + status + ".");
                StatusChanged?.Invoke(status, error);
            }
        }

        private void SleepInterruptible(int milliseconds)
        {
            int remaining = milliseconds;

            while (!_stopRequested && remaining > 0)
            {
                int chunk = Math.Min(remaining, 100);
                Thread.Sleep(chunk);
                remaining -= chunk;
            }
        }
    }
}
