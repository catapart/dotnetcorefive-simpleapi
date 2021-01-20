using System.Net.WebSockets;

namespace SimpleAPI_NetCore50.Websockets
{
    public class WebsocketSessionPeer
    {
        public Models.WebsocketSessionPeerToken Token;
        public WebSocket Socket;

        public WebsocketSessionPeer(string id, WebSocket socket, string displayName = "", string iconUrl = "")
        {
            this.Token = new Models.WebsocketSessionPeerToken();
            this.Token.PeerId = id;
            this.Token.DisplayName = displayName;
            this.Token.IconUrl = iconUrl;
            this.Socket = socket;
        }
    }
}
