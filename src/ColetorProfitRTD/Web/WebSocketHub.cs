using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColetorProfitRTD.Web
{
    public sealed class WebSocketHub
    {
        private readonly object _lock = new object();
        private readonly List<WebSocket> _clients = new List<WebSocket>();
        private readonly Logger _log;
        private readonly Func<string> _initialMessageFactory;
        private long _broadcasts;
        private long _targetMessages;
        private long _failedSends;
        private DateTimeOffset? _lastBroadcastAt;

        public WebSocketHub(Logger log, Func<string> initialMessageFactory)
        {
            _log = log;
            _initialMessageFactory = initialMessageFactory;
        }

        public async Task AcceptAsync(HttpListenerContext context)
        {
            HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
            WebSocket socket = wsContext.WebSocket;

            lock (_lock)
            {
                _clients.Add(socket);
            }

            _log.Info("Cliente WebSocket conectado.");

            string initial = _initialMessageFactory?.Invoke();

            if (!string.IsNullOrWhiteSpace(initial))
            {
                await SendAsync(socket, initial);
            }

            await ReceiveUntilClosedAsync(socket);
            Remove(socket);
            _log.Info("Cliente WebSocket desconectado.");
        }

        public Task BroadcastAsync(string message)
        {
            List<WebSocket> clients;

            lock (_lock)
            {
                clients = _clients.ToList();
                _broadcasts++;
                _targetMessages += clients.Count;
                _lastBroadcastAt = DateTimeOffset.Now;
            }

            return Task.WhenAll(clients.Select(x => SendOrRemoveAsync(x, message)));
        }

        public Dictionary<string, object> Health()
        {
            lock (_lock)
            {
                return new Dictionary<string, object>
                {
                    ["clients"] = _clients.Count(x => x.State == WebSocketState.Open),
                    ["broadcasts"] = _broadcasts,
                    ["targetMessages"] = _targetMessages,
                    ["failedSends"] = _failedSends,
                    ["lastBroadcastAt"] = _lastBroadcastAt.HasValue ? _lastBroadcastAt.Value.ToString("o") : null
                };
            }
        }

        private async Task SendOrRemoveAsync(WebSocket socket, string message)
        {
            try
            {
                await SendAsync(socket, message);
            }
            catch
            {
                lock (_lock)
                {
                    _failedSends++;
                }

                Remove(socket);
            }
        }

        private static Task SendAsync(WebSocket socket, string message)
        {
            if (socket.State != WebSocketState.Open)
            {
                return Task.CompletedTask;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(bytes);
            return socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ReceiveUntilClosedAsync(WebSocket socket)
        {
            var buffer = new byte[512];

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
                    break;
                }
            }
        }

        private void Remove(WebSocket socket)
        {
            lock (_lock)
            {
                _clients.Remove(socket);
            }

            socket.Dispose();
        }
    }
}
