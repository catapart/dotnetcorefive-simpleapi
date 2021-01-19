using System.ComponentModel.DataAnnotations;

namespace SimpleAPI_NetCore50.Schemas
{
    public class SocketSessionMessageResponse
    {
        public int MessageId { get; set; }
        public SocketSessionMessageType MessageType { get; set; }
        public string SenderId { get; set; }
        public string Message { get; set; }
        public string[] Recipients { get; set; }
    }
}