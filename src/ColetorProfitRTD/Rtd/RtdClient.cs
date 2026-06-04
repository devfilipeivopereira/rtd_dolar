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
        private const int BookDepthBroadcastIntervalMs = 100;
        private const int TimesTradesBroadcastIntervalMs = 150;
        private readonly RtdConfig _config;
        private readonly AssetRegistry _assetRegistry;
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
        private readonly Dictionary<string, Dictionary<int, Dictionary<string, object>>> _bookLevels = new Dictionary<string, Dictionary<int, Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, object>> _bookInfo = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<int, Dictionary<string, object>>> _timesRows = new Dictionary<string, Dictionary<int, Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, object>> _timesInfo = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastBookBroadcastUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastTimesBroadcastUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<Action<IRtdServer>> _controlCommands = new Queue<Action<IRtdServer>>();
        private Thread _thread;
        private RtdUpdateEvent _callback;
        private volatile bool _stopRequested;
        private string _status = "starting";
        private Exception _lastError;
        private int _nextTopicId = 1;

        public RtdClient(RtdConfig config, Logger log)
            : this(config, null, log)
        {
        }

        public RtdClient(RtdConfig config, AssetRegistry assetRegistry, Logger log)
        {
            _config = config;
            _assetRegistry = assetRegistry;
            _log = log;
            _state = new MarketState(config.Asset);

            if (_assetRegistry != null)
            {
                return;
            }

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
        public event Action<Dictionary<string, object>> BookDepthReceived;
        public event Action<Dictionary<string, object>> TimesTradesReceived;
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
                if (_assetRegistry != null)
                {
                    return _assetRegistry.List()
                        .Where(asset => asset.Enabled)
                        .Select(asset => asset.Asset)
                        .OrderBy(x => x)
                        .ToList();
                }

                lock (_assetLock)
                {
                    return _activeAssets.OrderBy(x => x).ToList();
                }
            }
        }

        public List<Dictionary<string, object>> AssetStates()
        {
            if (_assetRegistry != null)
            {
                return AssetStatesFromRegistry();
            }

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
            if (_assetRegistry != null)
            {
                var body = new Dictionary<string, object>
                {
                    ["asset"] = asset,
                    ["enabled"] = enabled,
                    ["channels"] = channels
                };

                return SaveAsset(body);
            }

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
            if (_assetRegistry != null)
            {
                var body = new Dictionary<string, object>
                {
                    ["asset"] = asset,
                    ["channels"] = channels
                };

                return SaveAsset(body);
            }

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
            if (_assetRegistry != null)
            {
                Dictionary<string, object> deleted = _assetRegistry.Delete(new Dictionary<string, object>
                {
                    ["asset"] = asset
                });
                string normalizedDeleted = deleted["asset"].ToString();
                EnqueueControl(server => DisconnectAsset(server, normalizedDeleted));
                _state.Remove(normalizedDeleted);
                ClearBookAndTimes(normalizedDeleted);
                return deleted;
            }

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
            if (_assetRegistry != null)
            {
                Dictionary<string, object> state = ToggleAsset(new Dictionary<string, object>
                {
                    ["asset"] = asset,
                    ["enabled"] = enabled
                });

                return state;
            }

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

        public Dictionary<string, object> SaveAsset(Dictionary<string, object> body)
        {
            if (_assetRegistry == null)
            {
                string asset = GetBodyString(body, "asset");
                bool enabled = GetBodyBool(body, "enabled", true);
                List<string> channels = GetBodyStringList(body, "channels");
                return AddAsset(asset, enabled, channels);
            }

            AssetConfig assetConfig = _assetRegistry.Upsert(body);
            EnqueueControl(server =>
            {
                DisconnectAsset(server, assetConfig.Asset);
                if (assetConfig.Enabled)
                {
                    SubscribeAsset(server, assetConfig.Asset);
                }
            });

            return AssetStates().First(x => string.Equals((string)x["asset"], assetConfig.Asset, StringComparison.OrdinalIgnoreCase));
        }

        public Dictionary<string, object> ToggleAsset(Dictionary<string, object> body)
        {
            if (_assetRegistry == null)
            {
                string asset = GetBodyString(body, "asset");
                bool enabled = GetBodyBool(body, "enabled", true);
                return SetAssetEnabled(asset, enabled);
            }

            AssetConfig assetConfig = _assetRegistry.Toggle(body);

            EnqueueControl(server =>
            {
                DisconnectAsset(server, assetConfig.Asset);
                if (assetConfig.Enabled)
                {
                    SubscribeAsset(server, assetConfig.Asset);
                }
            });

            return AssetStates().First(x => string.Equals((string)x["asset"], assetConfig.Asset, StringComparison.OrdinalIgnoreCase));
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
            if (_assetRegistry != null)
            {
                foreach (AssetConfig asset in _assetRegistry.List().Where(x => x.Enabled).OrderBy(x => x.Asset))
                {
                    SubscribeAsset(server, asset.Asset);
                }

                return;
            }

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

            if (_assetRegistry != null)
            {
                AssetConfig config = _assetRegistry.Get(normalizedAsset);
                if (config == null || !config.Enabled)
                {
                    return;
                }

                foreach (RtdTopic topic in BuildTopics(config))
                {
                    SubscribePreparedTopic(server, topic);
                }

                PublishCurrentDepthMessages(config);
                return;
            }

            foreach (string field in ResolveFieldsForAsset(normalizedAsset))
            {
                var topic = new RtdTopic
                {
                    Asset = normalizedAsset,
                    Channel = RtdChannel.Price,
                    Topic = normalizedAsset,
                    Field = field,
                    Args = new object[] { normalizedAsset, field }
                };
                string key = topic.Key;

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

                topic.TopicId = topicId;
                object initialValue = ConnectDataWithFallback(server, topic);
                topic.LastValue = initialValue;

                lock (_topicLock)
                {
                    _topics[topicId] = topic;
                    _topicByKey[key] = topicId;
                }

                _log.Info("Assinado RTD " + topic.Key + ".");
                Publish(topic, initialValue);
            }
        }

        private void SubscribePreparedTopic(IRtdServer server, RtdTopic topic)
        {
            lock (_topicLock)
            {
                if (_topicByKey.ContainsKey(topic.Key))
                {
                    return;
                }

                topic.TopicId = _nextTopicId++;
            }

            object initialValue = ConnectDataWithFallback(server, topic);
            topic.LastValue = initialValue;

            lock (_topicLock)
            {
                _topics[topic.TopicId] = topic;
                _topicByKey[topic.Key] = topic.TopicId;
            }

            _log.Info("Assinado RTD " + topic.Key + ".");
            Publish(topic, initialValue);
        }

        private List<RtdTopic> BuildTopics(AssetConfig asset)
        {
            var topics = new List<RtdTopic>();
            string assetCode = NormalizeAsset(asset.Asset);

            if (asset.PriceRtd != null && asset.PriceRtd.Enabled)
            {
                foreach (string field in asset.PriceRtd.Fields)
                {
                    topics.Add(new RtdTopic
                    {
                        Asset = assetCode,
                        Channel = RtdChannel.Price,
                        Topic = asset.PriceRtd.Topic,
                        Field = field,
                        Args = new object[] { asset.PriceRtd.Topic, field }
                    });
                }
            }

            if (asset.BookRtd != null && asset.BookRtd.Enabled)
            {
                topics.Add(new RtdTopic
                {
                    Asset = assetCode,
                    Channel = RtdChannel.Book,
                    Topic = asset.BookRtd.Topic,
                    Field = "INFO",
                    Extra = "ATV",
                    Args = new object[] { asset.BookRtd.Topic, "INFO", "ATV" }
                });
                topics.Add(new RtdTopic
                {
                    Asset = assetCode,
                    Channel = RtdChannel.Book,
                    Topic = asset.BookRtd.Topic,
                    Field = "INFO",
                    Extra = "TAB",
                    Args = new object[] { asset.BookRtd.Topic, "INFO", "TAB" }
                });

                for (int level = 0; level < asset.BookRtd.Depth; level++)
                {
                    foreach (string field in asset.BookRtd.Fields)
                    {
                        topics.Add(new RtdTopic
                        {
                            Asset = assetCode,
                            Channel = RtdChannel.Book,
                            Topic = asset.BookRtd.Topic,
                            Field = field,
                            Index = level,
                            Args = new object[] { asset.BookRtd.Topic, field, level }
                        });
                    }
                }
            }

            if (asset.TimesRtd != null && asset.TimesRtd.Enabled)
            {
                topics.Add(new RtdTopic
                {
                    Asset = assetCode,
                    Channel = RtdChannel.TimesTrades,
                    Topic = asset.TimesRtd.Topic,
                    Field = "INFO",
                    Extra = "ATV",
                    Args = new object[] { asset.TimesRtd.Topic, "INFO", "ATV" }
                });
                topics.Add(new RtdTopic
                {
                    Asset = assetCode,
                    Channel = RtdChannel.TimesTrades,
                    Topic = asset.TimesRtd.Topic,
                    Field = "INFO",
                    Extra = "TAB",
                    Args = new object[] { asset.TimesRtd.Topic, "INFO", "TAB" }
                });

                for (int row = 0; row < asset.TimesRtd.Rows; row++)
                {
                    foreach (string field in asset.TimesRtd.Fields)
                    {
                        topics.Add(new RtdTopic
                        {
                            Asset = assetCode,
                            Channel = RtdChannel.TimesTrades,
                            Topic = asset.TimesRtd.Topic,
                            Field = field,
                            Index = row,
                            Args = new object[] { asset.TimesRtd.Topic, field, row }
                        });
                    }
                }
            }

            return topics;
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

        private object ConnectDataWithFallback(IRtdServer server, RtdTopic topic)
        {
            object[] args = topic.Args ?? new object[] { topic.Topic ?? topic.Asset, topic.Field };
            var attempts = new List<Tuple<string, Func<object>>>
            {
                Tuple.Create<string, Func<object>>("object[] zero-based", () =>
                {
                    bool getNewValues = true;
                    object[] topicArgs = args.ToArray();
                    return server.ConnectData(topic.TopicId, ref topicArgs, ref getNewValues);
                }),
                Tuple.Create<string, Func<object>>("object[] with empty server slot", () =>
                {
                    bool getNewValues = true;
                    object[] topicArgs = new object[] { string.Empty }.Concat(args).ToArray();
                    return server.ConnectData(topic.TopicId, ref topicArgs, ref getNewValues);
                })
            };

            Exception lastError = null;

            foreach (Tuple<string, Func<object>> attempt in attempts)
            {
                try
                {
                    object value = attempt.Item2();
                    _log.Info("ConnectData OK " + topic.Key + " usando " + attempt.Item1 + ".");
                    return value;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _log.Warn("ConnectData falhou " + topic.Key + " usando " + attempt.Item1 + " | " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            throw new InvalidOperationException("Falha ao assinar RTD " + topic.Key + ". Ultima tentativa tambem falhou.", lastError);
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
            if (topic.Channel == RtdChannel.Book)
            {
                Dictionary<string, object> message = UpdateBookDepth(topic, value);
                if (message != null)
                {
                    BookDepthReceived?.Invoke(message);
                }

                return;
            }

            if (topic.Channel == RtdChannel.TimesTrades)
            {
                Dictionary<string, object> message = UpdateTimesTrades(topic, value);
                if (message != null)
                {
                    TimesTradesReceived?.Invoke(message);
                }

                return;
            }

            MarketSnapshot snapshot = _state.Update(topic.Asset, topic.Field, value, Status);
            SnapshotReceived?.Invoke(snapshot);
        }

        private Dictionary<string, object> UpdateBookDepth(RtdTopic topic, object value)
        {
            lock (_assetLock)
            {
                string asset = NormalizeAsset(topic.Asset);
                if (!_bookLevels.TryGetValue(asset, out Dictionary<int, Dictionary<string, object>> levels))
                {
                    levels = new Dictionary<int, Dictionary<string, object>>();
                    _bookLevels[asset] = levels;
                }

                if (!_bookInfo.TryGetValue(asset, out Dictionary<string, object> info))
                {
                    info = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    _bookInfo[asset] = info;
                }

                if (string.Equals(topic.Field, "INFO", StringComparison.OrdinalIgnoreCase))
                {
                    info[topic.Extra ?? "INFO"] = ValueParser.ToJsonValue(value);
                }
                else if (topic.Index.HasValue)
                {
                    if (!levels.TryGetValue(topic.Index.Value, out Dictionary<string, object> row))
                    {
                        row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        levels[topic.Index.Value] = row;
                    }

                    row[topic.Field] = ValueParser.ToJsonValue(value);
                }

                if (!ShouldBroadcastAuxMessage(asset, _lastBookBroadcastUtc, BookDepthBroadcastIntervalMs))
                {
                    return null;
                }

                return BuildBookDepthMessage(asset, topic.Topic, info, levels, BookDepthBroadcastIntervalMs);
            }
        }

        private Dictionary<string, object> UpdateTimesTrades(RtdTopic topic, object value)
        {
            lock (_assetLock)
            {
                string asset = NormalizeAsset(topic.Asset);
                if (!_timesRows.TryGetValue(asset, out Dictionary<int, Dictionary<string, object>> rows))
                {
                    rows = new Dictionary<int, Dictionary<string, object>>();
                    _timesRows[asset] = rows;
                }

                if (!_timesInfo.TryGetValue(asset, out Dictionary<string, object> info))
                {
                    info = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    _timesInfo[asset] = info;
                }

                if (string.Equals(topic.Field, "INFO", StringComparison.OrdinalIgnoreCase))
                {
                    info[topic.Extra ?? "INFO"] = ValueParser.ToJsonValue(value);
                }
                else if (topic.Index.HasValue)
                {
                    if (!rows.TryGetValue(topic.Index.Value, out Dictionary<string, object> row))
                    {
                        row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        rows[topic.Index.Value] = row;
                    }

                    row[topic.Field] = ValueParser.ToJsonValue(value);
                }

                if (!ShouldBroadcastAuxMessage(asset, _lastTimesBroadcastUtc, TimesTradesBroadcastIntervalMs))
                {
                    return null;
                }

                return BuildTimesTradesMessage(asset, topic.Topic, info, rows, TimesTradesBroadcastIntervalMs);
            }
        }

        private void PublishCurrentDepthMessages(AssetConfig config)
        {
            Dictionary<string, object> bookMessage = null;
            Dictionary<string, object> timesMessage = null;
            string asset = NormalizeAsset(config.Asset);

            lock (_assetLock)
            {
                if (config.BookRtd != null && config.BookRtd.Enabled &&
                    _bookInfo.TryGetValue(asset, out Dictionary<string, object> bookInfo) &&
                    _bookLevels.TryGetValue(asset, out Dictionary<int, Dictionary<string, object>> bookLevels))
                {
                    _lastBookBroadcastUtc[asset] = DateTime.UtcNow;
                    bookMessage = BuildBookDepthMessage(asset, config.BookRtd.Topic, bookInfo, bookLevels, BookDepthBroadcastIntervalMs);
                }

                if (config.TimesRtd != null && config.TimesRtd.Enabled &&
                    _timesInfo.TryGetValue(asset, out Dictionary<string, object> timesInfo) &&
                    _timesRows.TryGetValue(asset, out Dictionary<int, Dictionary<string, object>> timesRows))
                {
                    _lastTimesBroadcastUtc[asset] = DateTime.UtcNow;
                    timesMessage = BuildTimesTradesMessage(asset, config.TimesRtd.Topic, timesInfo, timesRows, TimesTradesBroadcastIntervalMs);
                }
            }

            if (bookMessage != null)
            {
                BookDepthReceived?.Invoke(bookMessage);
            }

            if (timesMessage != null)
            {
                TimesTradesReceived?.Invoke(timesMessage);
            }
        }

        private bool ShouldBroadcastAuxMessage(string asset, Dictionary<string, DateTime> lastBroadcastUtc, int intervalMs)
        {
            DateTime now = DateTime.UtcNow;

            if (lastBroadcastUtc.TryGetValue(asset, out DateTime last) &&
                (now - last).TotalMilliseconds < intervalMs)
            {
                return false;
            }

            lastBroadcastUtc[asset] = now;
            return true;
        }

        private Dictionary<string, object> BuildBookDepthMessage(string asset, string topic, Dictionary<string, object> info, Dictionary<int, Dictionary<string, object>> levels, int coalescingMs)
        {
            var bids = new List<Dictionary<string, object>>();
            var asks = new List<Dictionary<string, object>>();

            foreach (KeyValuePair<int, Dictionary<string, object>> pair in levels.OrderBy(x => x.Key))
            {
                Dictionary<string, object> row = pair.Value;

                if (HasAny(row, "HORC", "ACP", "VOC", "OCP"))
                {
                    bids.Add(new Dictionary<string, object>
                    {
                        ["level"] = pair.Key,
                        ["time"] = GetValue(row, "HORC"),
                        ["agent"] = GetValue(row, "ACP"),
                        ["qty"] = GetValue(row, "VOC"),
                        ["price"] = GetValue(row, "OCP")
                    });
                }

                if (HasAny(row, "OVD", "VOV", "AVD", "HORV"))
                {
                    asks.Add(new Dictionary<string, object>
                    {
                        ["level"] = pair.Key,
                        ["price"] = GetValue(row, "OVD"),
                        ["qty"] = GetValue(row, "VOV"),
                        ["agent"] = GetValue(row, "AVD"),
                        ["time"] = GetValue(row, "HORV")
                    });
                }
            }

            return new Dictionary<string, object>
            {
                ["type"] = "bookDepth",
                ["asset"] = asset,
                ["topic"] = topic,
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o"),
                ["coalescingMs"] = coalescingMs,
                ["info"] = new Dictionary<string, object>(info, StringComparer.OrdinalIgnoreCase),
                ["bids"] = bids,
                ["asks"] = asks
            };
        }

        private Dictionary<string, object> BuildTimesTradesMessage(string asset, string topic, Dictionary<string, object> info, Dictionary<int, Dictionary<string, object>> rows, int coalescingMs)
        {
            var trades = new List<Dictionary<string, object>>();

            foreach (KeyValuePair<int, Dictionary<string, object>> pair in rows.OrderBy(x => x.Key))
            {
                Dictionary<string, object> row = pair.Value;
                if (!HasAny(row, "DAT", "ACP", "PRE", "QUL", "AVD", "AGR"))
                {
                    continue;
                }

                trades.Add(new Dictionary<string, object>
                {
                    ["row"] = pair.Key,
                    ["time"] = GetValue(row, "DAT"),
                    ["buyer"] = GetValue(row, "ACP"),
                    ["price"] = GetValue(row, "PRE"),
                    ["qty"] = GetValue(row, "QUL"),
                    ["seller"] = GetValue(row, "AVD"),
                    ["aggressor"] = GetValue(row, "AGR")
                });
            }

            return new Dictionary<string, object>
            {
                ["type"] = "timesTrades",
                ["asset"] = asset,
                ["topic"] = topic,
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o"),
                ["coalescingMs"] = coalescingMs,
                ["info"] = new Dictionary<string, object>(info, StringComparer.OrdinalIgnoreCase),
                ["trades"] = trades
            };
        }

        private static bool HasAny(Dictionary<string, object> row, params string[] fields)
        {
            return fields.Any(field => row.TryGetValue(field, out object value) && value != null && !string.IsNullOrWhiteSpace(value.ToString()));
        }

        private static object GetValue(Dictionary<string, object> row, string field)
        {
            return row.TryGetValue(field, out object value) ? value : null;
        }

        private List<Dictionary<string, object>> AssetStatesFromRegistry()
        {
            List<AssetConfig> assets = _assetRegistry.List();
            Dictionary<string, List<RtdTopic>> topicsByAsset;

            lock (_topicLock)
            {
                topicsByAsset = _topics.Values
                    .GroupBy(topic => topic.Asset, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            }

            return assets.Select(asset =>
            {
                List<RtdTopic> topics = topicsByAsset.TryGetValue(asset.Asset, out List<RtdTopic> found)
                    ? found
                    : new List<RtdTopic>();

                var channels = new List<string>();
                if (asset.PriceRtd?.Enabled == true)
                {
                    channels.Add(RtdChannel.Price);
                }

                if (asset.BookRtd?.Enabled == true)
                {
                    channels.Add(RtdChannel.Book);
                }

                if (asset.TimesRtd?.Enabled == true)
                {
                    channels.Add(RtdChannel.TimesTrades);
                }

                return new Dictionary<string, object>
                {
                    ["id"] = asset.Id,
                    ["asset"] = asset.Asset,
                    ["label"] = asset.Label,
                    ["enabled"] = asset.Enabled,
                    ["subscribed"] = topics.Count > 0,
                    ["isDefault"] = string.Equals(asset.Asset, _config.Asset, StringComparison.OrdinalIgnoreCase),
                    ["channels"] = channels,
                    ["fields"] = asset.PriceRtd?.Fields ?? new List<string>(),
                    ["priceRtd"] = SourceState(asset.PriceRtd, topics),
                    ["bookRtd"] = SourceState(asset.BookRtd, topics),
                    ["timesRtd"] = SourceState(asset.TimesRtd, topics),
                    ["history"] = asset.History ?? new AssetHistoryInfo()
                };
            }).ToList();
        }

        private Dictionary<string, object> SourceState(RtdSourceConfig source, List<RtdTopic> topics)
        {
            if (source == null)
            {
                return new Dictionary<string, object>
                {
                    ["enabled"] = false,
                    ["subscribed"] = false
                };
            }

            int subscribed = topics.Count(topic => string.Equals(topic.Channel, source.Channel, StringComparison.OrdinalIgnoreCase));

            return new Dictionary<string, object>
            {
                ["channel"] = source.Channel,
                ["enabled"] = source.Enabled,
                ["subscribed"] = subscribed > 0,
                ["topic"] = source.Topic,
                ["depth"] = source.Depth,
                ["rows"] = source.Rows,
                ["fields"] = source.Fields ?? new List<string>(),
                ["subscribedTopics"] = subscribed,
                ["status"] = source.Enabled ? (subscribed > 0 ? "ok" : "pending") : "off"
            };
        }

        private void ClearBookAndTimes(string asset)
        {
            string normalized = NormalizeAsset(asset);

            lock (_assetLock)
            {
                _bookLevels.Remove(normalized);
                _bookInfo.Remove(normalized);
                _timesRows.Remove(normalized);
                _timesInfo.Remove(normalized);
                _lastBookBroadcastUtc.Remove(normalized);
                _lastTimesBroadcastUtc.Remove(normalized);
            }
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

        private static string GetBodyString(Dictionary<string, object> body, string key)
        {
            if (body == null || !body.TryGetValue(key, out object value) || value == null)
            {
                return null;
            }

            return value.ToString();
        }

        private static bool GetBodyBool(Dictionary<string, object> body, string key, bool fallback)
        {
            if (body == null || !body.TryGetValue(key, out object value) || value == null)
            {
                return fallback;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            return bool.TryParse(value.ToString(), out bool parsed) ? parsed : fallback;
        }

        private static List<string> GetBodyStringList(Dictionary<string, object> body, string key)
        {
            var result = new List<string>();

            if (body == null || !body.TryGetValue(key, out object value) || value == null)
            {
                return result;
            }

            if (value is string text)
            {
                result.Add(text);
                return result;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item != null)
                    {
                        result.Add(item.ToString());
                    }
                }
            }

            return result;
        }
    }
}
