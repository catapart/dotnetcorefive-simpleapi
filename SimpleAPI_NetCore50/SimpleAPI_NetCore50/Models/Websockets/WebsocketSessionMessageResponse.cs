namespace SimpleAPI_NetCore50.Models
{
    public class WebsocketSessionMessageResponse
    {
        public int MessageId { get; set; }
        public WebsocketSessionMessageType MessageType { get; set; }
        public string SenderId { get; set; }
        public string Message { get; set; }
        public string[] Recipients { get; set; }
    }
}