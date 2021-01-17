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
        private ConcurrentDictionary<string, ISessionAttribute> Attributes = new ConcurrentDictionary<string, ISessionAttribute>();

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
            ISessionAttribute attribute = this.Attributes[name];
            object value = attribute.Data;
            return (T) value;
        }
        public object GetAttributeValue(string name)
        {
            ISessionAttribute attribute = this.Attributes[name];
            var value = Convert.ChangeType(attribute.Data, attribute.DataType);

            return value;
        }
        public ConcurrentDictionary<string, ISessionAttribute> GetAttributePairs()
        {
            return this.Attributes;
        }
        public ISessionAttribute GetAttribute(string name)
        {
            return this.Attributes[name];
        }
        public ConcurrentDictionary<string, ISessionAttribute> GetAttributes()
        {
            return this.Attributes;
        }
        public void SetAttribute(string name, object value)
        {
            this.AddOrUpdateAttribute(name, value);
        }
        public void SetAttributes(IEnumerable<ISessionAttribute> attributes)
        {
            foreach (ISessionAttribute attribute in attributes)
            {
                this.AddOrUpdateAttribute(attribute.Name, attribute.Data);
            }
        }

        private string CreateSocketId()
        {
            return Guid.NewGuid().ToString();
        }

        private void AddOrUpdateAttribute(string name, object value)
        {
            ISessionAttribute attribute = null;

            try
            {
                attribute = this.GetAttribute(name);
            }
            catch (Exception exception) { }
            finally { }// [NOTE] - no need to handle exception; this is a guard;
            if (attribute != null)
            {
                this.Attributes.TryRemove(name, out attribute); // delete existing attribute to remake it; simpler than updating generic types;
            }

            this.Attributes.TryAdd(name, SessionAttribute.Create(name, value));
        }
    }
}
