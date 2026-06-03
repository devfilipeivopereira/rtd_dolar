using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            };

            rtdClient.StatusChanged += (status, error) =>
            {
                var message = new Dictionary<string, object>
                {
                    ["type"] = "status",
                    ["status"] = status,
                    ["asset"] = config.Rtd.Asset,
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
                () => BuildHealth(config, rtdClient),
                () => rtdClient.CurrentSnapshot.ToLiveMessage(),
                hub,
                log))
            {
                Console.CancelKeyPress += (sender, args) =>
                {
                    args.Cancel = true;
                    Quit.Set();
                };

                try
                {
                    server.Start();
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

        private static Dictionary<string, object> BuildHealth(AppConfig config, RtdClient rtdClient)
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
                ["lastError"] = lastError == null ? null : lastError.Message
            };
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
