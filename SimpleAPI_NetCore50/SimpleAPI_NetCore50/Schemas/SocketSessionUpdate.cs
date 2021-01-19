using System.ComponentModel.DataAnnotations;

namespace SimpleAPI_NetCore50.Schemas
{
    public class SocketSessionUpdate
    {
        public string Status { get; set; }
        public Models.SocketToken[] Peers { get; set; }
        public ProgressResponse Progress { get; set; }
    }
}