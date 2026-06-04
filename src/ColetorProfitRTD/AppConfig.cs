using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using ColetorProfitRTD.Rtd;

namespace ColetorProfitRTD
{
    public sealed class AppConfig
    {
        public RtdConfig Rtd { get; set; } = new RtdConfig();
        public WebConfig Web { get; set; } = new WebConfig();
        public StorageConfig Storage { get; set; } = new StorageConfig();
        public DiagnosticsConfig Diagnostics { get; set; } = new DiagnosticsConfig();
        public FlowConfig Flow { get; set; } = new FlowConfig();

        public static AppConfig Load(string path)
        {
            AppConfig config = null;

            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                config = new JavaScriptSerializer().Deserialize<AppConfig>(json);
            }

            config = config ?? new AppConfig();
            config.Rtd = config.Rtd ?? new RtdConfig();
            config.Web = config.Web ?? new WebConfig();
            config.Storage = config.Storage ?? new StorageConfig();
            config.Diagnostics = config.Diagnostics ?? new DiagnosticsConfig();
            config.Flow = config.Flow ?? new FlowConfig();
            config.Rtd.Assets = NormalizeAssets(config.Rtd.Asset, config.Rtd.Assets);
            config.Rtd.ActiveAssets = NormalizeAssets(config.Rtd.Asset, config.Rtd.ActiveAssets);
            config.Rtd.Fields = NormalizeFields(config.Rtd.Fields);
            config.Rtd.ChannelFields = NormalizeChannelFields(config.Rtd.ChannelFields);
            config.Rtd.AssetChannels = NormalizeAssetChannels(config.Rtd.Assets, config.Rtd.AssetChannels);

            return config;
        }

        private static List<string> NormalizeFields(List<string> fields)
        {
            IEnumerable<string> source = fields == null || fields.Count == 0
                ? RtdFieldCatalog.DefaultLiveFields
                : fields;

            return source
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> NormalizeAssets(string defaultAsset, List<string> assets)
        {
            IEnumerable<string> source = assets == null || assets.Count == 0
                ? (IEnumerable<string>)new[] { defaultAsset }
                : assets;

            return source
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, List<string>> NormalizeChannelFields(Dictionary<string, List<string>> channelFields)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, IReadOnlyList<string>> pair in RtdFieldCatalog.DefaultChannelFields)
            {
                result[pair.Key] = NormalizeFields(pair.Value.ToList());
            }

            if (channelFields != null)
            {
                foreach (KeyValuePair<string, List<string>> pair in channelFields)
                {
                    string channel = RtdFieldCatalog.NormalizeChannel(pair.Key);
                    if (string.IsNullOrWhiteSpace(channel))
                    {
                        continue;
                    }

                    result[channel] = NormalizeFields(pair.Value);
                }
            }

            return result;
        }

        private static Dictionary<string, List<string>> NormalizeAssetChannels(List<string> assets, Dictionary<string, List<string>> assetChannels)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (string asset in assets ?? new List<string>())
            {
                result[asset] = RtdFieldCatalog.DefaultChannels.ToList();
            }

            if (assetChannels != null)
            {
                foreach (KeyValuePair<string, List<string>> pair in assetChannels)
                {
                    string asset = string.IsNullOrWhiteSpace(pair.Key) ? null : pair.Key.Trim().ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(asset))
                    {
                        continue;
                    }

                    result[asset] = NormalizeChannels(pair.Value);
                }
            }

            return result;
        }

        public static List<string> NormalizeChannels(List<string> channels)
        {
            IEnumerable<string> source = channels == null || channels.Count == 0
                ? RtdFieldCatalog.DefaultChannels
                : channels;

            List<string> normalized = source
                .Select(RtdFieldCatalog.NormalizeChannel)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return normalized.Count == 0 ? RtdFieldCatalog.DefaultChannels.ToList() : normalized;
        }
    }

    public sealed class RtdConfig
    {
        public string ProgId { get; set; } = "RTDTrading.RTDServer";
        public string Asset { get; set; } = "WDOFUT_F_0";
        public List<string> Assets { get; set; } = new List<string> { "WDOFUT_F_0" };
        public List<string> ActiveAssets { get; set; } = new List<string> { "WDOFUT_F_0" };
        public Dictionary<string, List<string>> AssetChannels { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["WDOFUT_F_0"] = RtdFieldCatalog.DefaultChannels.ToList()
        };
        public Dictionary<string, List<string>> ChannelFields { get; set; } = RtdFieldCatalog.DefaultChannelFields
            .ToDictionary(x => x.Key, x => x.Value.ToList(), StringComparer.OrdinalIgnoreCase);
        public int PollIntervalMs { get; set; } = 250;
        public int ReconnectIntervalMs { get; set; } = 5000;
        public List<string> Fields { get; set; } = RtdFieldCatalog.DefaultLiveFields.ToList();
    }

    public sealed class WebConfig
    {
        public int HttpPort { get; set; } = 5000;
        public string WebSocketPath { get; set; } = "/ws";
        public string StaticFilesPath { get; set; } = "dashboard";
    }

    public sealed class StorageConfig
    {
        public bool Enabled { get; set; } = true;
        public string ConnectionString { get; set; } = "Data Source=data/marketdata.sqlite;Version=3;";
        public int SnapshotIntervalMs { get; set; } = 1000;
    }

    public sealed class DiagnosticsConfig
    {
        public string LogPath { get; set; } = "logs/coletor.log";
    }

    public sealed class FlowConfig
    {
        public bool Enabled { get; set; } = true;
        public decimal TickSize { get; set; } = 0.5m;
        public int CoalescingMs { get; set; } = 75;
        public int BroadcastIntervalMs { get; set; } = 250;
        public int MaxQueueSize { get; set; } = 2048;
        public int MaxTradeBuffer { get; set; } = 5000;
        public int MaxBookBuffer { get; set; } = 5000;
        public int ScoreThreshold { get; set; } = 60;
        public int StrongSignalThreshold { get; set; } = 75;
        public int ExcellentSignalThreshold { get; set; } = 90;
        public int SignalCooldownMs { get; set; } = 8000;
        public int SignalTtlMs { get; set; } = 15000;
        public int DataQualityCapTopOfBook { get; set; } = 78;
        public int DataQualityCapDerivedTape { get; set; } = 85;
        public bool UseProfitMedAsVwapFallback { get; set; } = true;
        public string VolumeFieldMode { get; set; } = "Auto";
    }
}
