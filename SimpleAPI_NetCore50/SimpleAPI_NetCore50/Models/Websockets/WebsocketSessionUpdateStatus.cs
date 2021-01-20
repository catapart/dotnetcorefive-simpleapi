namespace SimpleAPI_NetCore50.Models
{
    public enum WebsocketSessionUpdateStatus
    {
        [System.ComponentModel.Description("unknown (Server use only)")]
        Unknown, // error checking
        Connect, // peer connected
        Disconnect, // peer disconnected
        AccessGranted, // peer can access requested session
        AccessDenied, // peer can not access requested session
        Progress, // progress has been made
    }
}
