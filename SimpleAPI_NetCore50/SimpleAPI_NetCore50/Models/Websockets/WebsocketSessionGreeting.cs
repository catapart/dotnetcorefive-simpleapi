namespace SimpleAPI_NetCore50.Models
{
    public class WebsocketSessionGreeting
    {
        public string SessionKey { get; set; }
        public WebsocketSessionPeerToken Token  { get; set; }
        public WebsocketSessionPeerToken HostToken { get; set; }
        public WebsocketSessionPeerToken[] Peers { get; set; }
    }
}
