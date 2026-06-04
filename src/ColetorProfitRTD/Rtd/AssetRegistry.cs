using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace ColetorProfitRTD.Rtd
{
    public sealed class AssetRegistry
    {
        private readonly object _lock = new object();
        private readonly string _rootPath;
        private readonly string _configPath;
        private readonly RtdConfig _rtdConfig;
        private readonly Logger _log;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer
        {
            MaxJsonLength = 32 * 1024 * 1024
        };
        private readonly Dictionary<string, AssetConfig> _assets = new Dictionary<string, AssetConfig>(StringComparer.OrdinalIgnoreCase);

        public AssetRegistry(string rootPath, RtdConfig rtdConfig, Logger log)
        {
            _rootPath = rootPath;
            _configPath = Path.Combine(rootPath, "assets.json");
            _rtdConfig = rtdConfig;
            _log = log;
        }

        public void Initialize()
        {
            lock (_lock)
            {
                Directory.CreateDirectory(_rootPath);

                if (File.Exists(_configPath))
                {
                    try
                    {
                        string json = File.ReadAllText(_configPath, Encoding.UTF8);
                        AssetRegistryFile file = _serializer.Deserialize<AssetRegistryFile>(json);

                        foreach (AssetConfig asset in file?.Assets ?? new List<AssetConfig>())
                        {
                            asset.Normalize(_rtdConfig.ProgId);
                            _assets[asset.Asset] = asset;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Warn("Falha ao carregar data/assets/assets.json. Usando configuracao padrao: " + ex.Message);
                    }
                }

                if (_assets.Count == 0)
                {
                    SeedFromLegacyConfig();
                    SaveUnlocked();
                }
            }
        }

        public List<AssetConfig> List()
        {
            lock (_lock)
            {
                return _assets.Values
                    .OrderBy(x => x.Asset)
                    .Select(x => x.Clone())
                    .ToList();
            }
        }

        public AssetConfig Get(string asset)
        {
            string normalized = AssetConfig.NormalizeAsset(asset);

            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(normalized) && _assets.TryGetValue(normalized, out AssetConfig config))
                {
                    return config.Clone();
                }

                return null;
            }
        }

        public AssetConfig Upsert(Dictionary<string, object> body)
        {
            string assetCode = AssetConfig.NormalizeAsset(GetString(body, "asset") ?? GetString(body, "code") ?? GetString(body, "id"));
            if (string.IsNullOrWhiteSpace(assetCode))
            {
                throw new ArgumentException("Codigo do ativo/preco e obrigatorio.");
            }

            lock (_lock)
            {
                if (!_assets.TryGetValue(assetCode, out AssetConfig asset))
                {
                    asset = AssetConfig.CreateDefault(assetCode, _rtdConfig.ProgId);
                }

                asset.Asset = assetCode;
                asset.Id = assetCode;
                asset.Label = GetString(body, "label") ?? asset.Label;
                asset.Enabled = GetBool(body, "enabled", asset.Enabled);

                ConfigureSource(asset.PriceRtd, GetDictionary(body, "priceRtd"), body, "price", assetCode);
                ConfigureSource(asset.BookRtd, GetDictionary(body, "bookRtd"), body, "book", "BOOK0");
                ConfigureSource(asset.TimesRtd, GetDictionary(body, "timesRtd"), body, "times", "T&T0");

                List<string> channels = GetStringList(body, "channels");
                if (channels.Count > 0)
                {
                    HashSet<string> enabledChannels = new HashSet<string>(
                        channels.Select(RtdChannel.Normalize).Where(x => !string.IsNullOrWhiteSpace(x)),
                        StringComparer.OrdinalIgnoreCase);

                    asset.PriceRtd.Enabled = enabledChannels.Contains(RtdChannel.Price);
                    asset.BookRtd.Enabled = enabledChannels.Contains(RtdChannel.Book);
                    asset.TimesRtd.Enabled = enabledChannels.Contains(RtdChannel.TimesTrades);
                }

                asset.Normalize(_rtdConfig.ProgId);
                _assets[asset.Asset] = asset;
                SaveUnlocked();
                return asset.Clone();
            }
        }

        public AssetConfig Toggle(Dictionary<string, object> body)
        {
            string assetCode = AssetConfig.NormalizeAsset(GetString(body, "asset"));
            if (string.IsNullOrWhiteSpace(assetCode))
            {
                throw new ArgumentException("Ativo invalido.");
            }

            string channel = RtdChannel.Normalize(GetString(body, "channel"));
            bool enabled = GetBool(body, "enabled", true);

            lock (_lock)
            {
                if (!_assets.TryGetValue(assetCode, out AssetConfig asset))
                {
                    throw new ArgumentException("Ativo nao cadastrado: " + assetCode);
                }

                if (string.IsNullOrWhiteSpace(channel))
                {
                    asset.Enabled = enabled;
                }
                else if (channel == RtdChannel.Price)
                {
                    asset.PriceRtd.Enabled = enabled;
                }
                else if (channel == RtdChannel.Book)
                {
                    asset.BookRtd.Enabled = enabled;
                }
                else if (channel == RtdChannel.TimesTrades)
                {
                    asset.TimesRtd.Enabled = enabled;
                }

                asset.Normalize(_rtdConfig.ProgId);
                SaveUnlocked();
                return asset.Clone();
            }
        }

        public Dictionary<string, object> Delete(Dictionary<string, object> body)
        {
            string assetCode = AssetConfig.NormalizeAsset(GetString(body, "asset"));
            if (string.IsNullOrWhiteSpace(assetCode))
            {
                throw new ArgumentException("Ativo invalido.");
            }

            lock (_lock)
            {
                bool removed = _assets.Remove(assetCode);
                SaveUnlocked();

                return new Dictionary<string, object>
                {
                    ["asset"] = assetCode,
                    ["deleted"] = removed
                };
            }
        }

        public Dictionary<string, object> SaveHistory(Dictionary<string, object> body)
        {
            string assetCode = AssetConfig.NormalizeAsset(GetString(body, "asset"));
            string csvText = GetString(body, "csvText") ?? GetString(body, "csv");
            string fileName = GetString(body, "fileName") ?? "history.csv";

            if (string.IsNullOrWhiteSpace(assetCode))
            {
                throw new ArgumentException("Ativo invalido.");
            }

            if (csvText == null)
            {
                throw new ArgumentException("CSV vazio.");
            }

            lock (_lock)
            {
                if (!_assets.TryGetValue(assetCode, out AssetConfig asset))
                {
                    asset = AssetConfig.CreateDefault(assetCode, _rtdConfig.ProgId);
                    _assets[asset.Asset] = asset;
                }

                string directory = AssetDirectory(assetCode);
                Directory.CreateDirectory(directory);
                string csvPath = Path.Combine(directory, "history.csv");
                File.WriteAllText(csvPath, csvText, Encoding.UTF8);

                asset.History = new AssetHistoryInfo
                {
                    HasCsv = true,
                    FileName = Path.GetFileName(fileName),
                    SavedAt = DateTimeOffset.Now.ToString("o"),
                    Rows = CountCsvRows(csvText),
                    Bytes = Encoding.UTF8.GetByteCount(csvText)
                };

                SaveUnlocked();

                return new Dictionary<string, object>
                {
                    ["asset"] = asset.Asset,
                    ["history"] = asset.History.Clone()
                };
            }
        }

        public Dictionary<string, object> LoadHistory(string asset)
        {
            string assetCode = AssetConfig.NormalizeAsset(asset);
            if (string.IsNullOrWhiteSpace(assetCode))
            {
                throw new ArgumentException("Ativo invalido.");
            }

            lock (_lock)
            {
                AssetConfig config = _assets.TryGetValue(assetCode, out AssetConfig found) ? found : null;
                string csvPath = Path.Combine(AssetDirectory(assetCode), "history.csv");
                bool hasCsv = File.Exists(csvPath);

                return new Dictionary<string, object>
                {
                    ["asset"] = assetCode,
                    ["history"] = config?.History ?? new AssetHistoryInfo { HasCsv = hasCsv },
                    ["csvText"] = hasCsv ? File.ReadAllText(csvPath, Encoding.UTF8) : null
                };
            }
        }

        private void SeedFromLegacyConfig()
        {
            IEnumerable<string> assets = _rtdConfig.Assets == null || _rtdConfig.Assets.Count == 0
                ? (IEnumerable<string>)new[] { _rtdConfig.Asset }
                : _rtdConfig.Assets;

            HashSet<string> active = new HashSet<string>(
                (_rtdConfig.ActiveAssets == null || _rtdConfig.ActiveAssets.Count == 0 ? assets : _rtdConfig.ActiveAssets)
                    .Select(AssetConfig.NormalizeAsset)
                    .Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);

            foreach (string rawAsset in assets)
            {
                string assetCode = AssetConfig.NormalizeAsset(rawAsset);
                if (string.IsNullOrWhiteSpace(assetCode))
                {
                    continue;
                }

                AssetConfig asset = AssetConfig.CreateDefault(assetCode, _rtdConfig.ProgId);
                asset.Enabled = active.Contains(assetCode);

                List<string> channels = null;
                if (_rtdConfig.AssetChannels != null && _rtdConfig.AssetChannels.TryGetValue(assetCode, out List<string> configured))
                {
                    channels = configured;
                }

                if (channels != null && channels.Count > 0)
                {
                    HashSet<string> normalized = new HashSet<string>(
                        channels.Select(RtdChannel.Normalize).Where(x => !string.IsNullOrWhiteSpace(x)),
                        StringComparer.OrdinalIgnoreCase);
                    asset.PriceRtd.Enabled = normalized.Contains(RtdChannel.Price);
                    asset.BookRtd.Enabled = normalized.Contains(RtdChannel.Book);
                    asset.TimesRtd.Enabled = normalized.Contains(RtdChannel.TimesTrades);
                }

                asset.Normalize(_rtdConfig.ProgId);
                _assets[asset.Asset] = asset;
            }
        }

        private void ConfigureSource(RtdSourceConfig source, Dictionary<string, object> nested, Dictionary<string, object> flat, string prefix, string fallbackTopic)
        {
            if (source == null)
            {
                return;
            }

            Dictionary<string, object> body = nested ?? flat;
            string topic = GetString(body, "topic") ?? GetString(flat, prefix + "Topic");
            string channel = RtdChannel.Normalize(GetString(body, "channel")) ?? source.Channel;

            source.Channel = channel;
            source.Topic = string.IsNullOrWhiteSpace(topic) ? source.Topic ?? fallbackTopic : topic;
            source.Enabled = GetBool(body, "enabled", GetBool(flat, prefix + "Enabled", source.Enabled));
            source.Depth = GetInt(body, "depth", GetInt(flat, prefix + "Depth", source.Depth));
            source.Rows = GetInt(body, "rows", GetInt(flat, prefix + "Rows", source.Rows));

            List<string> fields = GetStringList(body, "fields");
            if (fields.Count > 0)
            {
                source.Fields = fields;
            }
        }

        private void SaveUnlocked()
        {
            Directory.CreateDirectory(_rootPath);
            var file = new AssetRegistryFile
            {
                Version = 1,
                UpdatedAt = DateTimeOffset.Now.ToString("o"),
                Assets = _assets.Values.OrderBy(x => x.Asset).Select(x => x.Clone()).ToList()
            };

            File.WriteAllText(_configPath, _serializer.Serialize(file), Encoding.UTF8);
        }

        private string AssetDirectory(string asset)
        {
            string safe = new string((asset ?? "asset")
                .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
                .ToArray());

            return Path.Combine(_rootPath, safe);
        }

        private static int CountCsvRows(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Count(line => !string.IsNullOrWhiteSpace(line));
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> body, string key)
        {
            if (body == null || !body.TryGetValue(key, out object value) || value == null)
            {
                return null;
            }

            if (value is Dictionary<string, object> typed)
            {
                return typed;
            }

            if (value is IDictionary dictionary)
            {
                var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in dictionary)
                {
                    result[entry.Key.ToString()] = entry.Value;
                }

                return result;
            }

            return null;
        }

        private static string GetString(Dictionary<string, object> body, string key)
        {
            if (body == null || !body.TryGetValue(key, out object value) || value == null)
            {
                return null;
            }

            return value.ToString();
        }

        private static bool GetBool(Dictionary<string, object> body, string key, bool fallback)
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

        private static int GetInt(Dictionary<string, object> body, string key, int fallback)
        {
            if (body == null || !body.TryGetValue(key, out object value) || value == null)
            {
                return fallback;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            return int.TryParse(value.ToString(), out int parsed) ? parsed : fallback;
        }

        private static List<string> GetStringList(Dictionary<string, object> body, string key)
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

            if (value is IEnumerable enumerable)
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

    public sealed class AssetRegistryFile
    {
        public int Version { get; set; }
        public string UpdatedAt { get; set; }
        public List<AssetConfig> Assets { get; set; } = new List<AssetConfig>();
    }
}
