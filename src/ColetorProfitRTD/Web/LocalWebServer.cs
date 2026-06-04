using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColetorProfitRTD.Web
{
    public sealed class LocalWebServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly Logger _log;
        private readonly string _staticRoot;
        private readonly string _webSocketPath;
        private readonly Func<object> _healthFactory;
        private readonly Func<object> _snapshotFactory;
        private readonly Func<object> _flowFactory;
        private readonly Func<object> _signalsFactory;
        private readonly Func<object> _assetsFactory;
        private readonly Func<object> _bootstrapFactory;
        private readonly Func<Dictionary<string, object>, object> _assetAddHandler;
        private readonly Func<Dictionary<string, object>, object> _assetToggleHandler;
        private readonly Func<Dictionary<string, object>, object> _assetDeleteHandler;
        private readonly Func<Dictionary<string, object>, object> _assetChannelsHandler;
        private readonly Func<Dictionary<string, object>, object> _assetHistorySaveHandler;
        private readonly Func<string, object> _assetHistoryLoadHandler;
        private readonly WebSocketHub _hub;
        private CancellationTokenSource _cts;

        public LocalWebServer(
            int port,
            string staticRoot,
            string webSocketPath,
            Func<object> healthFactory,
            Func<object> snapshotFactory,
            Func<object> flowFactory,
            Func<object> signalsFactory,
            Func<object> assetsFactory,
            Func<object> bootstrapFactory,
            Func<Dictionary<string, object>, object> assetAddHandler,
            Func<Dictionary<string, object>, object> assetToggleHandler,
            Func<Dictionary<string, object>, object> assetDeleteHandler,
            Func<Dictionary<string, object>, object> assetChannelsHandler,
            Func<Dictionary<string, object>, object> assetHistorySaveHandler,
            Func<string, object> assetHistoryLoadHandler,
            WebSocketHub hub,
            Logger log)
        {
            Port = port;
            _staticRoot = staticRoot;
            _webSocketPath = string.IsNullOrWhiteSpace(webSocketPath) ? "/ws" : webSocketPath;
            _healthFactory = healthFactory;
            _snapshotFactory = snapshotFactory;
            _flowFactory = flowFactory;
            _signalsFactory = signalsFactory;
            _assetsFactory = assetsFactory;
            _bootstrapFactory = bootstrapFactory;
            _assetAddHandler = assetAddHandler;
            _assetToggleHandler = assetToggleHandler;
            _assetDeleteHandler = assetDeleteHandler;
            _assetChannelsHandler = assetChannelsHandler;
            _assetHistorySaveHandler = assetHistorySaveHandler;
            _assetHistoryLoadHandler = assetHistoryLoadHandler;
            _hub = hub;
            _log = log;
            _listener.Prefixes.Add("http://localhost:" + Port + "/");
        }

        public int Port { get; }
        public string Url => "http://localhost:" + Port + "/";

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            Task.Run(() => ListenLoopAsync(_cts.Token));
            _log.Info("Servidor local iniciado em " + Url);
        }

        public void Stop()
        {
            _cts?.Cancel();

            if (_listener.IsListening)
            {
                _listener.Stop();
            }
        }

        public void Dispose()
        {
            Stop();
            _listener.Close();
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleAsync(context));
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error("Falha no servidor HTTP.", ex);
                }
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;

            try
            {
                if (context.Request.IsWebSocketRequest && IsPath(path, _webSocketPath))
                {
                    await _hub.AcceptAsync(context);
                    return;
                }

                if (IsPath(path, "/health"))
                {
                    await WriteJsonAsync(context, _healthFactory());
                    return;
                }

                if (IsPath(path, "/snapshot"))
                {
                    await WriteJsonAsync(context, _snapshotFactory());
                    return;
                }

                if (IsPath(path, "/flow"))
                {
                    await WriteJsonAsync(context, _flowFactory());
                    return;
                }

                if (IsPath(path, "/signals"))
                {
                    await WriteJsonAsync(context, _signalsFactory());
                    return;
                }

                if (IsPath(path, "/bootstrap"))
                {
                    await WriteJsonAsync(context, _bootstrapFactory());
                    return;
                }

                if (IsPath(path, "/assets"))
                {
                    if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteJsonAsync(context, _assetsFactory());
                        return;
                    }

                    if (string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        Dictionary<string, object> body = await ReadJsonBodyAsync(context);
                        await WriteJsonAsync(context, _assetAddHandler(body));
                        return;
                    }

                    if (string.Equals(context.Request.HttpMethod, "DELETE", StringComparison.OrdinalIgnoreCase))
                    {
                        Dictionary<string, object> body = await ReadJsonBodyAsync(context);
                        await WriteJsonAsync(context, _assetDeleteHandler(body));
                        return;
                    }
                }

                if (IsPath(path, "/assets/toggle") && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, object> body = await ReadJsonBodyAsync(context);
                    await WriteJsonAsync(context, _assetToggleHandler(body));
                    return;
                }

                if (IsPath(path, "/assets/delete") && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, object> body = await ReadJsonBodyAsync(context);
                    await WriteJsonAsync(context, _assetDeleteHandler(body));
                    return;
                }

                if (IsPath(path, "/assets/channels") && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, object> body = await ReadJsonBodyAsync(context);
                    await WriteJsonAsync(context, _assetChannelsHandler(body));
                    return;
                }

                if (IsPath(path, "/assets/history"))
                {
                    if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        string asset = context.Request.QueryString["asset"];
                        await WriteJsonAsync(context, _assetHistoryLoadHandler(asset));
                        return;
                    }

                    if (string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        Dictionary<string, object> body = await ReadJsonBodyAsync(context);
                        await WriteJsonAsync(context, _assetHistorySaveHandler(body));
                        return;
                    }
                }

                await WriteStaticAsync(context, path);
            }
            catch (Exception ex)
            {
                _log.Error("Falha ao responder " + path + ".", ex);
                await WriteJsonAsync(context, new Dictionary<string, object>
                {
                    ["type"] = "error",
                    ["message"] = ex.Message
                }, 500);
            }
        }

        private async Task WriteStaticAsync(HttpListenerContext context, string path)
        {
            string relative = path == "/" ? "index.html" : path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(_staticRoot, relative));
            string root = Path.GetFullPath(_staticRoot);

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                await WriteTextAsync(context, "404", "text/plain; charset=utf-8", 404);
                return;
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            context.Response.StatusCode = 200;
            context.Response.ContentType = ContentTypeFor(fullPath);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        private static Task WriteJsonAsync(HttpListenerContext context, object value, int statusCode = 200)
        {
            return WriteTextAsync(context, JsonHelper.Serialize(value), "application/json; charset=utf-8", statusCode);
        }

        private static async Task<Dictionary<string, object>> ReadJsonBodyAsync(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
            {
                string json = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }

                return JsonHelper.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static async Task WriteTextAsync(HttpListenerContext context, string text, string contentType, int statusCode)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        private static bool IsPath(string actual, string expected)
        {
            return string.Equals(actual.TrimEnd('/'), expected.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        private static string ContentTypeFor(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            switch (ext)
            {
                case ".html":
                    return "text/html; charset=utf-8";
                case ".css":
                    return "text/css; charset=utf-8";
                case ".js":
                    return "application/javascript; charset=utf-8";
                case ".json":
                    return "application/json; charset=utf-8";
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".svg":
                    return "image/svg+xml";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
