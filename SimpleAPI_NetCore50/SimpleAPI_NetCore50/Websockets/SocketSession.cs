using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

namespace SimpleAPI_NetCore50.Websockets
{
    public enum WebsocketSessionType
    {
        Unknown,
        Progress,
        Message,
        Stream
    }

    public class SocketSession
    {
        public WebsocketSessionType SessionType;
        public string SessionKey;
        private ConcurrentDictionary<string, SessionSocket> Sockets = new ConcurrentDictionary<string, SessionSocket>();

        public static WebsocketSessionType GetSessionType(string typeName)
        {
            if (String.Equals(typeName, "progress", StringComparison.CurrentCultureIgnoreCase))
            {
                return WebsocketSessionType.Progress;
            }
            if (String.Equals(typeName, "message", StringComparison.CurrentCultureIgnoreCase))
            {
                return WebsocketSessionType.Message;
            }
            if (String.Equals(typeName, "stream", StringComparison.CurrentCultureIgnoreCase))
            {
                return WebsocketSessionType.Stream;
            }

            return WebsocketSessionType.Unknown;
        }

        public SessionSocket GetSocketById(string id)
        {
            return Sockets.FirstOrDefault(pair => pair.Key == id).Value;
        }

        public ConcurrentDictionary<string, SessionSocket> GetSockets()
        {
            return Sockets;
        }

        public string GetId(SessionSocket socket)
        {
            return Sockets.FirstOrDefault(pair => pair.Value == socket).Key;
        }

        public SessionSocket AddWebSocket(WebSocket socket)
        {
            string id = CreateSocketId();
            SessionSocket sessionSocket = new SessionSocket(id, socket);
            Sockets.TryAdd(id, sessionSocket);
            return sessionSocket;
        }

        public void AddSessionSocket(SessionSocket sessionSocket)
        {
            Sockets.TryAdd(CreateSocketId(), sessionSocket);
        }
        public async Task RemoveSessionSocket(string key)
        {
            SessionSocket socket;
            Sockets.TryRemove(key, out socket);
            await socket.Socket.CloseAsync(closeStatus: WebSocketCloseStatus.NormalClosure, statusDescription: "Closed by the SocketSession", cancellationToken: CancellationToken.None);
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            await Task.WhenAll(Sockets.Select(pair => pair.Value.Socket.CloseAsync(closeStatus: closeStatus, statusDescription: statusDescription, cancellationToken: cancellationToken)));
        }

        private string CreateSocketId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
