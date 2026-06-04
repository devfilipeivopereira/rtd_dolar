using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ColetorProfitRTD.Flow;
using ColetorProfitRTD.MarketData;
using ColetorProfitRTD.Rtd;
using ColetorProfitRTD.Storage;
using ColetorProfitRTD.Web;

namespace ColetorProfitRTD
{
    internal static class Program
    {
        private static readonly ManualResetEventSlim Quit = new ManualResetEventSlim(false);

        [STAThread]
        private static void Main(string[] args)
        {
            AppConfig config = AppConfig.Load(ResolveConfigPath());
            string logPath = ResolveWritablePath(config.Diagnostics.LogPath);
            var log = new Logger(logPath);

            log.Info("Aplicacao iniciada.");
            log.Info("Processo: " + (Environment.Is64BitProcess ? "x64" : "x86") + ".");

            if (args != null && Array.Exists(args, x => string.Equals(x, "--probe", StringComparison.OrdinalIgnoreCase)))
            {
                RunProbe(config, log);
                return;
            }

            string staticRoot = ResolveStaticRoot(config.Web.StaticFilesPath);
            log.Info("Dashboard: " + staticRoot);

            var rtdClient = new RtdClient(config.Rtd, log);
            var flowProcessor = new FlowProcessor(config.Flow, log);
            var store = new SqliteSnapshotStore(config.Storage, log);

            try
            {
                store.Initialize();
            }
            catch (Exception ex)
            {
                log.Error("SQLite indisponivel. O RTD e o WebSocket continuam ativos.", ex);
            }

            string SnapshotJson()
            {
                return JsonHelper.Serialize(rtdClient.CurrentSnapshot.ToLiveMessage());
            }

            var hub = new WebSocketHub(log, SnapshotJson);

            rtdClient.SnapshotReceived += snapshot =>
            {
                store.QueueSave(snapshot);
                string json = JsonHelper.Serialize(snapshot.ToLiveMessage());
                Task.Run(() => hub.BroadcastAsync(json));
                flowProcessor.Post(snapshot);
            };

            flowProcessor.FlowUpdated += (message, metrics) =>
            {
                store.QueueSaveFlowMetrics(metrics);
                string json = JsonHelper.Serialize(message);
                Task.Run(() => hub.BroadcastAsync(json));
            };

            flowProcessor.SignalGenerated += (message, signal) =>
            {
                store.QueueSaveSignal(signal);
                string json = JsonHelper.Serialize(message);
                Task.Run(() => hub.BroadcastAsync(json));
            };

            rtdClient.StatusChanged += (status, error) =>
            {
                var message = new Dictionary<string, object>
                {
                    ["type"] = "status",
                    ["status"] = status,
                    ["asset"] = config.Rtd.Asset,
                    ["assets"] = rtdClient.AssetStates(),
                    ["localTimestamp"] = DateTimeOffset.Now.ToString("o"),
                    ["error"] = error == null ? null : error.Message
                };

                string json = JsonHelper.Serialize(message);
                Task.Run(() => hub.BroadcastAsync(json));
            };

            using (var server = new LocalWebServer(
                config.Web.HttpPort,
                staticRoot,
                config.Web.WebSocketPath,
                () => BuildHealth(config, rtdClient, flowProcessor),
                () => rtdClient.CurrentSnapshot.ToLiveMessage(),
                () => flowProcessor.CurrentFlowMessage(),
                () => flowProcessor.CurrentSignalsMessage(),
                () => BuildAssets(rtdClient),
                body => AddAsset(rtdClient, body),
                body => ToggleAsset(rtdClient, body),
                body => DeleteAsset(rtdClient, flowProcessor, body),
                body => UpdateAssetChannels(rtdClient, body),
                hub,
                log))
            {
                Console.CancelKeyPress += (sender, cancelArgs) =>
                {
                    cancelArgs.Cancel = true;
                    Quit.Set();
                };

                try
                {
                    server.Start();
                    flowProcessor.Start();
                    rtdClient.Start();

                    Console.WriteLine();
                    Console.WriteLine("Dashboard: " + server.Url);
                    Console.WriteLine("WebSocket: ws://localhost:" + config.Web.HttpPort + config.Web.WebSocketPath);
                    Console.WriteLine("Pressione Ctrl+C para encerrar.");
                    Console.WriteLine();

                    Quit.Wait();
                }
                catch (Exception ex)
                {
                    log.Error("Falha fatal.", ex);
                }
                finally
                {
                    rtdClient.Stop();
                    flowProcessor.Stop();
                    server.Stop();
                    log.Info("Aplicacao encerrada.");
                }
            }
        }

        private static void RunProbe(AppConfig config, Logger log)
        {
            log.Info("Modo probe: assinando apenas VOL.");
            config.Rtd.Fields = new List<string> { "VOL" };

            using (var client = new RtdClient(config.Rtd, log))
            {
                int received = 0;

                client.SnapshotReceived += snapshot =>
                {
                    if (snapshot.Volume.HasValue)
                    {
                        received++;
                        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " | " + config.Rtd.Asset + " | VOL | " + snapshot.Volume.Value);
                    }
                };

                client.Start();
                Console.WriteLine("Aguardando VOL por 30 segundos. Pressione Ctrl+C para sair.");

                Console.CancelKeyPress += (sender, args) =>
                {
                    args.Cancel = true;
                    Quit.Set();
                };

                Quit.Wait(TimeSpan.FromSeconds(30));
                client.Stop();
                log.Info("Probe encerrado. Atualizacoes VOL recebidas: " + received + ".");
            }
        }

        private static Dictionary<string, object> BuildHealth(AppConfig config, RtdClient rtdClient, FlowProcessor flowProcessor)
        {
            MarketSnapshot snapshot = rtdClient.CurrentSnapshot;
            Exception lastError = rtdClient.LastError;

            return new Dictionary<string, object>
            {
                ["status"] = "ok",
                ["profitRtd"] = rtdClient.Status,
                ["asset"] = config.Rtd.Asset,
                ["progId"] = config.Rtd.ProgId,
                ["processArchitecture"] = Environment.Is64BitProcess ? "x64" : "x86",
                ["lastUpdate"] = snapshot.LocalTimestamp.ToString("o"),
                ["lastError"] = lastError == null ? null : lastError.Message,
                ["assets"] = rtdClient.AssetStates(),
                ["flow"] = flowProcessor.Health()
            };
        }

        private static Dictionary<string, object> BuildAssets(RtdClient rtdClient)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "assets",
                ["assets"] = rtdClient.AssetStates(),
                ["availableChannels"] = RtdFieldCatalog.DefaultChannels.ToList(),
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o")
            };
        }

        private static Dictionary<string, object> AddAsset(RtdClient rtdClient, Dictionary<string, object> body)
        {
            string asset = GetString(body, "asset");
            bool enabled = GetBool(body, "enabled", true);
            List<string> channels = GetStringList(body, "channels");
            Dictionary<string, object> state = rtdClient.AddAsset(asset, enabled, channels);

            return new Dictionary<string, object>
            {
                ["type"] = "asset",
                ["asset"] = state,
                ["assets"] = rtdClient.AssetStates(),
                ["availableChannels"] = RtdFieldCatalog.DefaultChannels.ToList(),
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o")
            };
        }

        private static Dictionary<string, object> UpdateAssetChannels(RtdClient rtdClient, Dictionary<string, object> body)
        {
            string asset = GetString(body, "asset");
            List<string> channels = GetStringList(body, "channels");
            Dictionary<string, object> state = rtdClient.SetAssetChannels(asset, channels);

            return new Dictionary<string, object>
            {
                ["type"] = "asset",
                ["asset"] = state,
                ["assets"] = rtdClient.AssetStates(),
                ["availableChannels"] = RtdFieldCatalog.DefaultChannels.ToList(),
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o")
            };
        }

        private static Dictionary<string, object> DeleteAsset(RtdClient rtdClient, FlowProcessor flowProcessor, Dictionary<string, object> body)
        {
            string asset = GetString(body, "asset");
            Dictionary<string, object> deleted = rtdClient.DeleteAsset(asset);
            flowProcessor.RemoveAsset(asset);

            return new Dictionary<string, object>
            {
                ["type"] = "assetDeleted",
                ["asset"] = deleted,
                ["deletedAsset"] = deleted["asset"],
                ["assets"] = rtdClient.AssetStates(),
                ["availableChannels"] = RtdFieldCatalog.DefaultChannels.ToList(),
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o")
            };
        }

        private static Dictionary<string, object> ToggleAsset(RtdClient rtdClient, Dictionary<string, object> body)
        {
            string asset = GetString(body, "asset");
            bool enabled = GetBool(body, "enabled", true);
            Dictionary<string, object> state = rtdClient.SetAssetEnabled(asset, enabled);

            return new Dictionary<string, object>
            {
                ["type"] = "asset",
                ["asset"] = state,
                ["assets"] = rtdClient.AssetStates(),
                ["availableChannels"] = RtdFieldCatalog.DefaultChannels.ToList(),
                ["localTimestamp"] = DateTimeOffset.Now.ToString("o")
            };
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

        private static string ResolveConfigPath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string outputPath = Path.Combine(basePath, "appsettings.json");

            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            string sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "ColetorProfitRTD", "appsettings.json");

            if (File.Exists(sourcePath))
            {
                return sourcePath;
            }

            return outputPath;
        }

        private static string ResolveStaticRoot(string configuredPath)
        {
            string path = configuredPath ?? "dashboard";
            string outputPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));

            if (Directory.Exists(outputPath))
            {
                return outputPath;
            }

            string sourcePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "dashboard"));

            if (Directory.Exists(sourcePath))
            {
                return sourcePath;
            }

            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        private static string ResolveWritablePath(string configuredPath)
        {
            string path = configuredPath ?? "logs/coletor.log";

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            string cwdPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
            string cwdDirectory = Path.GetDirectoryName(cwdPath);

            if (!string.IsNullOrWhiteSpace(cwdDirectory) && Directory.Exists(cwdDirectory))
            {
                return cwdPath;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
        }
    }

}
