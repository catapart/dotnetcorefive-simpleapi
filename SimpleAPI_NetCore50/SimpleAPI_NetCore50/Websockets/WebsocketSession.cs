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
        Messaging,
        Progress,
    }

    public class WebsocketSession
    {
        public WebsocketSessionType SessionType;
        public string SessionKey;
        private ConcurrentDictionary<string, SessionSocket> Sockets = new ConcurrentDictionary<string, SessionSocket>();
        private ConcurrentDictionary<string, IProcessArtifact> Attributes = new ConcurrentDictionary<string, IProcessArtifact>();

        public static WebsocketSessionType GetSessionType(string typeName)
        {
            if (String.Equals(typeName, "progress", StringComparison.CurrentCultureIgnoreCase))
            {
                return WebsocketSessionType.Progress;
            }
            return WebsocketSessionType.Messaging;
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
            IProcessArtifact attribute = this.Attributes[name];
            object value = attribute.Data;
            return (T) value;
        }
        public object GetAttributeValue(string name)
        {
            IProcessArtifact attribute = this.Attributes[name];
            var value = Convert.ChangeType(attribute.Data, attribute.DataType);

            return value;
        }
        public ConcurrentDictionary<string, IProcessArtifact> GetAttributePairs()
        {
            return this.Attributes;
        }
        public IProcessArtifact GetAttribute(string name)
        {
            return this.Attributes[name];
        }
        public ConcurrentDictionary<string, IProcessArtifact> GetAttributes()
        {
            return this.Attributes;
        }
        public void SetAttribute(string name, object value)
        {
            this.AddOrUpdateAttribute(name, value);
        }
        public void SetAttributes(IEnumerable<IProcessArtifact> attributes)
        {
            foreach (IProcessArtifact attribute in attributes)
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
            IProcessArtifact attribute = null;

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

            this.Attributes.TryAdd(name, ProcessArtifact.Create(name, value));
        }
    }
}
