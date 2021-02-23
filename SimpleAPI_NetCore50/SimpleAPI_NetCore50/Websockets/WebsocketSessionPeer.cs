using System.Net.WebSockets;

namespace SimpleAPI_NetCore50.Websockets
{
    public class WebsocketSessionPeer
    {
        public Models.WebsocketSessionPeerToken Token;
        public WebSocket Socket;
        public System.Guid IdAsGUID;
        public byte[] IdAsByteArray;

        public WebsocketSessionPeer(WebSocket socket, System.Guid? id = null, string displayName = "", string iconUrl = "")
        {
            if(id == null || id == System.Guid.Empty)
            {
                IdAsGUID = System.Guid.NewGuid();
            }
            else
            {
                IdAsGUID = (System.Guid)id;
            }


            this.Token = new Models.WebsocketSessionPeerToken();
            this.Token.PeerId = IdAsGUID.ToString();
            this.Token.DisplayName = displayName;
            this.Token.IconUrl = iconUrl;
            this.Socket = socket;

            System.ArraySegment<byte> segment = new System.ArraySegment<byte>(array: System.Text.Encoding.ASCII.GetBytes(this.Token.PeerId), offset: 0, count: this.Token.PeerId.Length);
            IdAsByteArray = segment.ToArray();
        }
    }
}
