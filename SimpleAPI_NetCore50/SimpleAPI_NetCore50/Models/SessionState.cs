namespace SimpleAPI_NetCore50.Models
{
    public class SessionState
    {
        public SocketToken Token  { get; set; }
        public SocketToken HostToken { get; set; }
        public SocketToken[] Peers { get; set; }
    }
}
