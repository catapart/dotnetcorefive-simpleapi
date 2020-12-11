using System.Net.WebSockets;

namespace SimpleAPI_NetCore50.Websockets
{
    public class SessionSocket
    {
        public Models.SocketToken Token;
        public WebSocket Socket;

        public SessionSocket(string id, WebSocket socket, string displayName = "", string iconUrl = "")
        {
            this.Token = new Models.SocketToken();
            this.Token.SocketId = id;
            this.Token.DisplayName = displayName;
            this.Token.IconUrl = iconUrl;
            this.Socket = socket;
        }
    }
}
