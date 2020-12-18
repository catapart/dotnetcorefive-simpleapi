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
        private ConcurrentBag<ISessionAttribute> Attributes = new ConcurrentBag<ISessionAttribute>();

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
        public async Task RemoveSessionSocket(string socketId)
        {
            SessionSocket socket;
            Sockets.TryRemove(socketId, out socket);

            if (socket != null && socket.Socket.State != WebSocketState.Closed)
            {
                await socket.Socket.CloseAsync(closeStatus: WebSocketCloseStatus.NormalClosure, statusDescription: "Closed by the SocketSession", cancellationToken: CancellationToken.None);
            }
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            IEnumerable<Task> tasks = Sockets.Select(pair => RemoveSessionSocket(pair.Value.Token.SocketId));
            await Task.WhenAll(tasks);
        }
       

        public T GetAttributeValue<T>(string name)
        {
            ISessionAttribute attribute = this.Attributes.FirstOrDefault(attribute => attribute.Name == name);
            object value = attribute.Data;
            return (T) value;
        }
        public object GetAttributeValue(string name)
        {
            ISessionAttribute attribute = this.Attributes.FirstOrDefault(attribute => attribute.Name == name);
            var value = Convert.ChangeType(attribute.Data, attribute.DataType);

            return value;
        }
        public ISessionAttribute GetAttribute(string name)
        {
            return this.Attributes.FirstOrDefault(attribute => attribute.Name == name);
        }
        public ConcurrentBag<ISessionAttribute> GetAttributes()
        {
            return this.Attributes;
        }
        public void SetAttribute(string name, object value)
        {
            this.Attributes.Add(SessionAttribute.Create(name, value));
        }
        public void SetAttributes(IEnumerable<ISessionAttribute> attributes)
        {
            foreach (ISessionAttribute attribute in attributes)
            {
                this.Attributes.Add(attribute);
            }
        }

        private string CreateSocketId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
