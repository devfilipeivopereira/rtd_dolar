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
        private readonly MarketState _state;
        private readonly object _statusLock = new object();
        private readonly object _assetLock = new object();
        private readonly object _commandLock = new object();
        private readonly object _topicLock = new object();
        private readonly Dictionary<int, RtdTopic> _topics = new Dictionary<int, RtdTopic>();
        private readonly Dictionary<string, int> _topicByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _knownAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _assetChannels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<Action<IRtdServer>> _controlCommands = new Queue<Action<IRtdServer>>();
        private Thread _thread;
        private RtdUpdateEvent _callback;
        private volatile bool _stopRequested;
        private string _status = "starting";
        private Exception _lastError;
        private int _nextTopicId = 1;

        public RtdClient(RtdConfig config, Logger log)
        {
            _config = config;
            _log = log;
            _state = new MarketState(config.Asset);

            IEnumerable<string> configuredAssets = config.Assets == null || config.Assets.Count == 0
                ? (IEnumerable<string>)new[] { config.Asset }
                : config.Assets;

            foreach (string asset in configuredAssets)
            {
                string normalized = NormalizeAsset(asset);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _knownAssets.Add(normalized);
                    _assetChannels[normalized] = ChannelsFor(normalized);
                }
            }

            IEnumerable<string> activeAssets = config.ActiveAssets == null || config.ActiveAssets.Count == 0
                ? (IEnumerable<string>)_knownAssets
                : config.ActiveAssets;

            foreach (string asset in activeAssets)
            {
                string normalized = NormalizeAsset(asset);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _knownAssets.Add(normalized);
                    _activeAssets.Add(normalized);
                    _assetChannels[normalized] = ChannelsFor(normalized);
                }
            }
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

        public MarketSnapshot CurrentSnapshot => _state.Current(_config.Asset);

        public List<MarketSnapshot> CurrentSnapshots => _state.All();

        public IReadOnlyList<string> ActiveAssets
        {
            get
            {
                lock (_assetLock)
                {
                    return _activeAssets.OrderBy(x => x).ToList();
                }
            }
        }

        public List<Dictionary<string, object>> AssetStates()
        {
            List<string> knownAssets;
            List<string> activeAssets;
            List<string> subscribedAssets;
            Dictionary<string, List<string>> channelsByAsset;

            lock (_assetLock)
            {
                knownAssets = _knownAssets.OrderBy(x => x).ToList();
                activeAssets = _activeAssets.ToList();
                channelsByAsset = _assetChannels.ToDictionary(x => x.Key, x => x.Value.ToList(), StringComparer.OrdinalIgnoreCase);
            }

            lock (_topicLock)
            {
                subscribedAssets = _topics.Values
                    .Select(topic => topic.Asset)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return knownAssets
                .Select(asset => new Dictionary<string, object>
                {
                    ["asset"] = asset,
                    ["enabled"] = activeAssets.Contains(asset, StringComparer.OrdinalIgnoreCase),
                    ["subscribed"] = subscribedAssets.Contains(asset, StringComparer.OrdinalIgnoreCase),
                    ["isDefault"] = string.Equals(asset, _config.Asset, StringComparison.OrdinalIgnoreCase),
                    ["channels"] = channelsByAsset.TryGetValue(asset, out List<string> channels) ? channels : RtdFieldCatalog.DefaultChannels.ToList(),
                    ["fields"] = ResolveFieldsForAsset(asset).ToList()
                })
                .ToList();
        }

        public Dictionary<string, object> AddAsset(string asset, bool enabled, List<string> channels)
        {
            string normalized = NormalizeAsset(asset);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Ativo invalido.", nameof(asset));
            }

            List<string> normalizedChannels = AppConfig.NormalizeChannels(channels);

            lock (_assetLock)
            {
                _knownAssets.Add(normalized);
                _assetChannels[normalized] = normalizedChannels;
                if (enabled)
                {
                    _activeAssets.Add(normalized);
                }
            }

            if (enabled)
            {
                EnqueueControl(server => SubscribeAsset(server, normalized));
            }

            return AssetStates().First(x => string.Equals((string)x["asset"], normalized, StringComparison.OrdinalIgnoreCase));
        }

        public Dictionary<string, object> SetAssetChannels(string asset, List<string> channels)
        {
            string normalized = NormalizeAsset(asset);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Ativo invalido.", nameof(asset));
            }

            bool active;

            lock (_assetLock)
            {
                _knownAssets.Add(normalized);
                _assetChannels[normalized] = AppConfig.NormalizeChannels(channels);
                active = _activeAssets.Contains(normalized);
            }

            if (active)
            {
                EnqueueControl(server =>
                {
                    DisconnectAsset(server, normalized);
                    SubscribeAsset(server, normalized);
                });
            }

            return AssetStates().First(x => string.Equals((string)x["asset"], normalized, StringComparison.OrdinalIgnoreCase));
        }

        public Dictionary<string, object> DeleteAsset(string asset)
        {
            string normalized = NormalizeAsset(asset);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Ativo invalido.", nameof(asset));
            }

            lock (_assetLock)
            {
                _knownAssets.Remove(normalized);
                _activeAssets.Remove(normalized);
                _assetChannels.Remove(normalized);
            }

            EnqueueControl(server => DisconnectAsset(server, normalized));
            _state.Remove(normalized);

            return new Dictionary<string, object>
            {
                ["asset"] = normalized,
                ["deleted"] = true
            };
        }

        public Dictionary<string, object> SetAssetEnabled(string asset, bool enabled)
        {
            string normalized = NormalizeAsset(asset);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Ativo invalido.", nameof(asset));
            }

            bool changed;

            lock (_assetLock)
            {
                _knownAssets.Add(normalized);
                if (!_assetChannels.ContainsKey(normalized))
                {
                    _assetChannels[normalized] = ChannelsFor(normalized);
                }
                changed = enabled ? _activeAssets.Add(normalized) : _activeAssets.Remove(normalized);
            }

            if (changed)
            {
                if (enabled)
                {
                    EnqueueControl(server => SubscribeAsset(server, normalized));
                }
                else
                {
                    EnqueueControl(server => DisconnectAsset(server, normalized));
                }
            }

            return AssetStates().First(x => string.Equals((string)x["asset"], normalized, StringComparison.OrdinalIgnoreCase));
        }

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
                lock (_topicLock)
                {
                    _topics.Clear();
                    _topicByKey.Clear();
                    _nextTopicId = 1;
                }

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

                SubscribeAllActive(server);
                SetStatus("connected", null);

                DateTime nextHeartbeat = DateTime.UtcNow.AddSeconds(5);

                while (!_stopRequested)
                {
                    PumpRefreshData(server);
                    DrainControlCommands(server);

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

        private void SubscribeAllActive(IRtdServer server)
        {
            List<string> assets;
            lock (_assetLock)
            {
                assets = _activeAssets.OrderBy(x => x).ToList();
            }

            foreach (string asset in assets)
            {
                SubscribeAsset(server, asset);
            }
        }

        private void SubscribeAsset(IRtdServer server, string asset)
        {
            if (server == null)
            {
                return;
            }

            string normalizedAsset = NormalizeAsset(asset);

            foreach (string field in ResolveFieldsForAsset(normalizedAsset))
            {
                string key = normalizedAsset + ":" + field;
                lock (_topicLock)
                {
                    if (_topicByKey.ContainsKey(key))
                    {
                        continue;
                    }
                }

                int topicId;

                lock (_topicLock)
                {
                    topicId = _nextTopicId++;
                }

                object initialValue = ConnectDataWithFallback(server, topicId, normalizedAsset, field);

                var topic = new RtdTopic
                {
                    TopicId = topicId,
                    Asset = normalizedAsset,
                    Field = field,
                    LastValue = initialValue
                };

                lock (_topicLock)
                {
                    _topics[topicId] = topic;
                    _topicByKey[key] = topicId;
                }

                _log.Info("Assinado RTD " + topic.Key + ".");
                Publish(topic, initialValue);
            }
        }

        private void DisconnectAsset(IRtdServer server, string asset)
        {
            string normalizedAsset = NormalizeAsset(asset);
            List<RtdTopic> topics;

            lock (_topicLock)
            {
                topics = _topics.Values
                    .Where(topic => string.Equals(topic.Asset, normalizedAsset, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (RtdTopic topic in topics)
            {
                try
                {
                    server.DisconnectData(topic.TopicId);
                    _log.Info("RTD desligado " + topic.Key + ".");
                }
                catch (Exception ex)
                {
                    _log.Warn("Falha ao desconectar topico " + topic.Key + ": " + ex.Message);
                }

                lock (_topicLock)
                {
                    _topics.Remove(topic.TopicId);
                    _topicByKey.Remove(topic.Key);
                }
            }
        }

        private object ConnectDataWithFallback(IRtdServer server, int topicId, string asset, string field)
        {
            var attempts = new List<Tuple<string, Func<object>>>
            {
                Tuple.Create<string, Func<object>>("object[] zero-based", () =>
                {
                    bool getNewValues = true;
                    object[] topicArgs = new object[] { asset, field };
                    return server.ConnectData(topicId, ref topicArgs, ref getNewValues);
                }),
                Tuple.Create<string, Func<object>>("object[] with empty server slot", () =>
                {
                    bool getNewValues = true;
                    object[] topicArgs = new object[] { string.Empty, asset, field };
                    return server.ConnectData(topicId, ref topicArgs, ref getNewValues);
                })
            };

            Exception lastError = null;

            foreach (Tuple<string, Func<object>> attempt in attempts)
            {
                try
                {
                    object value = attempt.Item2();
                    _log.Info("ConnectData OK " + asset + ":" + field + " usando " + attempt.Item1 + ".");
                    return value;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _log.Warn("ConnectData falhou " + asset + ":" + field + " usando " + attempt.Item1 + " | " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            throw new InvalidOperationException("Falha ao assinar RTD " + asset + ":" + field + ". Ultima tentativa tambem falhou.", lastError);
        }

        private void PumpRefreshData(IRtdServer server)
        {
            int topicCount = 0;
            object[,] data = server.RefreshData(ref topicCount);

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

                RtdTopic topic;

                lock (_topicLock)
                {
                    if (!_topics.TryGetValue(topicId, out topic))
                    {
                        continue;
                    }
                }

                lock (_topicLock)
                {
                    topic.LastValue = value;
                }

                Publish(topic, value);
            }
        }

        private void EnqueueControl(Action<IRtdServer> command)
        {
            lock (_commandLock)
            {
                _controlCommands.Enqueue(command);
            }
        }

        private void DrainControlCommands(IRtdServer server)
        {
            while (true)
            {
                Action<IRtdServer> command;

                lock (_commandLock)
                {
                    if (_controlCommands.Count == 0)
                    {
                        return;
                    }

                    command = _controlCommands.Dequeue();
                }

                command(server);
            }
        }

        private void Publish(RtdTopic topic, object value)
        {
            MarketSnapshot snapshot = _state.Update(topic.Asset, topic.Field, value, Status);
            SnapshotReceived?.Invoke(snapshot);
        }

        private List<string> ChannelsFor(string asset)
        {
            string normalized = NormalizeAsset(asset);

            if (_config.AssetChannels != null &&
                _config.AssetChannels.TryGetValue(normalized, out List<string> channels))
            {
                return AppConfig.NormalizeChannels(channels);
            }

            return RtdFieldCatalog.DefaultChannels.ToList();
        }

        private List<string> ResolveFieldsForAsset(string asset)
        {
            List<string> channels;

            lock (_assetLock)
            {
                if (!_assetChannels.TryGetValue(asset, out channels))
                {
                    channels = ChannelsFor(asset);
                }

                channels = AppConfig.NormalizeChannels(channels);
            }

            var fields = new List<string>();

            foreach (string channel in channels)
            {
                if (_config.ChannelFields != null &&
                    _config.ChannelFields.TryGetValue(channel, out List<string> channelFields))
                {
                    fields.AddRange(channelFields);
                }
            }

            if (fields.Count == 0)
            {
                fields.AddRange(_config.Fields);
            }

            return fields
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void Disconnect(IRtdServer server)
        {
            if (server == null)
            {
                return;
            }

            List<RtdTopic> topics;

            lock (_topicLock)
            {
                topics = _topics.Values.ToList();
            }

            foreach (RtdTopic topic in topics)
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

        private string NormalizeAsset(string asset)
        {
            return string.IsNullOrWhiteSpace(asset) ? null : asset.Trim().ToUpperInvariant();
        }
    }
}
