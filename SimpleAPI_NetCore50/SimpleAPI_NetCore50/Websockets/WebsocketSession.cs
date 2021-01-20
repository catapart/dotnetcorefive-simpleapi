using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using SimpleAPI_NetCore50.Utilities;

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
        private ConcurrentDictionary<string, WebsocketSessionPeer> Peers = new ConcurrentDictionary<string, WebsocketSessionPeer>();
        private ConcurrentDictionary<string, IProcessArtifact> Attributes = new ConcurrentDictionary<string, IProcessArtifact>();

        public static WebsocketSessionType GetSessionType(string typeName)
        {
            if (String.Equals(typeName, "progress", StringComparison.CurrentCultureIgnoreCase))
            {
                return WebsocketSessionType.Progress;
            }
            return WebsocketSessionType.Messaging;
        }

        public WebsocketSessionPeer GetPeerById(string id)
        {
            return Peers.FirstOrDefault(pair => pair.Key == id).Value;
        }

        public ConcurrentDictionary<string, WebsocketSessionPeer> GetPeers()
        {
            return Peers;
        }

        public string GetId(WebsocketSessionPeer socket)
        {
            return Peers.FirstOrDefault(pair => pair.Value == socket).Key;
        }

        public WebsocketSessionPeer AddPeer(WebSocket socket)
        {
            string id = CreatePeerId();
            WebsocketSessionPeer peer = new WebsocketSessionPeer(id, socket);
            Peers.TryAdd(id, peer);
            return peer;
        }

        public void AddPeer(WebsocketSessionPeer peer)
        {
            Peers.TryAdd(CreatePeerId(), peer);
        }
        public async Task RemovePeer(string socketId)
        {
            WebsocketSessionPeer socket;
            Peers.TryRemove(socketId, out socket);

            if (socket != null && socket.Socket.State != WebSocketState.Closed)
            {
                await socket.Socket.CloseAsync(closeStatus: WebSocketCloseStatus.NormalClosure, statusDescription: "Closed by the SocketSession", cancellationToken: CancellationToken.None);
            }
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            IEnumerable<Task> tasks = Peers.Select(pair => RemovePeer(pair.Value.Token.PeerId));
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

        private string CreatePeerId()
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
