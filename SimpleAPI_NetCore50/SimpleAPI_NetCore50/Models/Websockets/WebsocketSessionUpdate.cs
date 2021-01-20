namespace SimpleAPI_NetCore50.Models
{
    public struct WebsocketSessionUpdate
    {
        public string Status { get; set; }
        public WebsocketSessionPeerToken[] Peers { get; set; }
        public WebsocketSessionFileProgress Progress { get; set; }
    }
}