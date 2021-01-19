using System.ComponentModel.DataAnnotations;

namespace SimpleAPI_NetCore50.Schemas
{
    public class SocketSessionMessageRequest
    {
        public SocketSessionMessageType Type { get; set; }
        [Required(ErrorMessage = "Message is Required")]
        public string Message { get; set; }
        public string TargetMessageId { get; set; }
        public string[] Recipients { get; set; }
    }
}