using System.ComponentModel.DataAnnotations;

namespace SimpleAPI_NetCore50.Models
{
    public class WebsocketSessionMessageRequest
    {
        public WebsocketSessionMessageType Type { get; set; }
        [Required(ErrorMessage = "Message is Required")]
        public string Message { get; set; }
        public string TargetMessageId { get; set; }
        public string[] Recipients { get; set; }
    }
}